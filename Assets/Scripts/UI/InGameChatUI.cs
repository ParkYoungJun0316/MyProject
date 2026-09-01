using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 인게임 채팅 시스템 (NetworkBehaviour).
/// New Input System(Keyboard.current) 직접 폴링 + activeInputHandler=Both(legacy IME 병행, §IME 참고).
///
/// [히스토리 유지]
/// 씬 재로드·전환이 되어도 static s_history로 메시지가 유지됨.
/// 타이틀 복귀 시 ClearHistory()로 초기화.
///
/// [UI 계층 구조 — 씬에서 직접 생성]
/// HUD Canvas
///   ├── ChatHistoryPanel          ← chatHistoryPanel 연결 (스크롤 뷰 루트)
///   │     └── Scroll View
///   │           └── Viewport → Content   ← chatContent 연결
///   └── ChatInputPanel            ← chatInputPanel 연결
///         └── InputField          ← inputField 연결
///
/// [키 흐름 — 2026-08-22 변경]
/// Enter (입력창 닫힘) → 입력창 열기
/// Enter (입력창 열림) → 텍스트 있으면 전송 후 입력창 닫기, 없으면 그냥 닫기
///   (Enter 한 번의 열기~전송 사이클이 끝나면 항상 정상 플레이로 복귀 — 이전엔 전송 후에도
///   입력창이 계속 열려있어 이동키가 계속 막혀있었음)
/// Escape → 입력창 닫기
///
/// [IME — 2026-08-22 수정]
/// 한글 등 조합형 입력 중 Enter를 누르면 TMP_InputField.text가 조합 문자를 아직 커밋하기 전이라
/// 마지막 음절이 잘려서 전송되는 Unity 버그(Windows IME, Issue Tracker UUM-100241, Won't Fix) —
/// Enter 처리 직전에 Input.compositionString을 강제로 텍스트에 붙여 우회.
///
/// [커서 — 2026-08-22 수정]
/// 입력창 열림/닫힘에 맞춰 CursorUnlockRequestUtil로 커서 해제/재요청(EscMenu 등과 동일 패턴).
/// ThirdPersonCamera는 이 유틸의 IsRequested(커서 해제 SSOT)를 보고 시점 회전을 멈추므로
/// Esc메뉴/이모트메뉴/치어네임패널과 동일하게 여기서도 자동으로 카메라 회전이 멎는다.
///
/// [자동 숨김]
/// 마지막 메시지 후 autoHideSeconds 동안 새 메시지 없으면 히스토리 패널 숨김.
/// </summary>
public class InGameChatUI : NetworkBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────

    [Header("UI 연결 (필수)")]
    [Tooltip("메시지 기록 영역 루트 (ScrollRect가 있는 패널). 자동 표시/숨김됨.")]
    [SerializeField] GameObject     chatHistoryPanel;
    [Tooltip("메시지 ScrollRect")]
    [SerializeField] ScrollRect     chatScrollRect;
    [Tooltip("메시지 TMP_Text들이 생성될 Content Transform (VerticalLayoutGroup + ContentSizeFitter 필요)")]
    [SerializeField] Transform      chatContent;

    [Tooltip("하단 입력창 패널. Enter로 열리고 빈 Enter로 닫힘.")]
    [SerializeField] GameObject     chatInputPanel;
    [Tooltip("TMP_InputField")]
    [SerializeField] TMP_InputField inputField;

    [Header("자동 숨김")]
    [Tooltip("마지막 메시지 이후 이 시간(초) 동안 새 메시지 없으면 히스토리 패널 자동 숨김.")]
    [SerializeField] float autoHideSeconds = 10f;

    [Header("스크롤 휠 (채팅 열림 시)")]
    [Tooltip("마우스 휠 1단위당 스크롤 속도 배율. 너무 빠르면 줄이고 느리면 늘림.")]
    [SerializeField] float scrollWheelSensitivity = 100f;

    [Header("메시지 설정")]
    [SerializeField] int   maxMessages     = 50;
    [Tooltip("GameSettingsManager가 아직 준비 안 됐을 때(비정상 상황)만 쓰이는 폴백값. " +
             "실제 크기는 옵션 메뉴 '채팅 글자 크기' 슬라이더 → GameSettingsManager.ChatFontSize 기준.")]
    [SerializeField] float messageFontSize = 14f;
    [Tooltip("채팅 기록 글자 색상 (이름 색상은 아래 플레이어 색상 기준)")]
    [SerializeField] Color messageTextColor = Color.black;

    [Header("플레이어 이름 색상")]
    [SerializeField] Color colorBerry = new Color(0.35f, 0.64f, 0.82f);  // Blue
    [SerializeField] Color colorGuma  = new Color(0.61f, 0.35f, 0.71f);  // Purple
    [SerializeField] Color colorSsuk  = new Color(0.18f, 0.80f, 0.44f);  // Green
    [SerializeField] Color colorDanho = new Color(0.95f, 0.61f, 0.07f);  // Yellow

    // ── 정적 히스토리 (씬 재로드·전환에도 유지됨) ───────────────

    // (message, senderColorIndex)
    static readonly List<(string message, int colorIndex)> s_history = new();

    /// <summary>
    /// 타이틀 복귀 시 채팅 상태 전체 초기화. TitleReturnFlow에서 호출.
    /// 히스토리 삭제 + 입력창 열림 플래그 리셋.
    /// </summary>
    public static void ResetForTitleReturn()
    {
        s_history.Clear();
        IsChatOpen = false;
    }

    /// <summary>타이틀 복귀 시 채팅 히스토리 초기화. GameSession.ResetSession()에서 호출.</summary>
    public static void ClearHistory() => s_history.Clear();

    // ── 런타임 ────────────────────────────────────────────────────

    bool      _inputOpen;
    bool      _skipNextSubmit;
    Coroutine _autoHideRoutine;
    Coroutine _subscribeFontSizeRoutine;
    bool      _subscribedToFontSize;

    /// <summary>채팅 입력창이 열려 있는지 여부. Player.cs가 이동 차단에 사용.</summary>
    public static bool IsChatOpen { get; private set; }

    readonly List<TMP_Text> _messages = new();

    // ── 초기화 ────────────────────────────────────────────────────

    void OnEnable()
    {
        _subscribeFontSizeRoutine = StartCoroutine(SubscribeToFontSizeWhenReady());
    }

    void OnDisable()
    {
        if (_subscribeFontSizeRoutine != null)
        {
            StopCoroutine(_subscribeFontSizeRoutine);
            _subscribeFontSizeRoutine = null;
        }

        if (_subscribedToFontSize && GameSettingsManager.Instance != null)
            GameSettingsManager.Instance.ChatFontSizeChanged -= OnChatFontSizeChanged;
        _subscribedToFontSize = false;
    }

    /// <summary>씬 언로드 등으로 입력창이 열린 채로 파괴돼도 커서 요청 목록에 잔여 참조가 새지
    /// 않도록 하는 안전장치(EscMenuController/TutorialCheerNameUI와 동일 패턴). 정상적으로 닫힐 때는
    /// CloseInput()의 Release가 처리하므로, 여긴 그 경로를 타지 못한 비정상 파괴에서만 의미 있다.</summary>
    void OnDestroy()
    {
        if (_inputOpen) CursorUnlockRequestUtil.Forget(this);
    }

    /// <summary>
    /// GameSettingsManager는 0.Title에서 DontDestroyOnLoad로 먼저 생성되므로 정상 플로우에서는
    /// OnEnable 시점에 이미 준비돼 있지만, 만약을 대비해 GameSettingsManager.Awake의
    /// ApplySavedMicSettingsWhenReady와 동일한 폴링 패턴으로 구독을 보장함 —
    /// 구독이 조용히 누락되면 이미 떠 있는 채팅 메시지가 옵션 변경에 영원히 반응하지 않게 됨.
    /// </summary>
    IEnumerator SubscribeToFontSizeWhenReady()
    {
        while (GameSettingsManager.Instance == null)
            yield return null;

        GameSettingsManager.Instance.ChatFontSizeChanged += OnChatFontSizeChanged;
        _subscribedToFontSize = true;
    }

    void Start()
    {
        if (chatHistoryPanel != null) chatHistoryPanel.SetActive(false);
        if (chatInputPanel   != null) chatInputPanel.SetActive(false);

        // 이전 씬의 채팅 히스토리 복원
        if (s_history.Count > 0)
            StartCoroutine(RebuildHistoryRoutine());
    }

    /// <summary>현재 적용해야 할 채팅 글자 크기. GameSettingsManager 미준비 시에만 Inspector 폴백값 사용.</summary>
    float CurrentFontSize =>
        GameSettingsManager.Instance != null ? GameSettingsManager.Instance.ChatFontSize : messageFontSize;

    /// <summary>옵션 메뉴에서 채팅 글자 크기를 바꾸면 이미 떠 있는 메시지들에도 즉시 반영.</summary>
    void OnChatFontSizeChanged(float size)
    {
        foreach (TMP_Text msg in _messages)
            if (msg != null) msg.fontSize = size;
    }

    IEnumerator RebuildHistoryRoutine()
    {
        // chatContent 레이아웃이 준비될 때까지 1프레임 대기
        yield return null;

        if (chatContent == null) yield break;

        _messages.Clear();
        foreach (Transform t in chatContent)
            Destroy(t.gameObject);

        foreach (var (msg, colorIdx) in s_history)
            CreateMessageObject(msg, colorIdx);

        if (chatHistoryPanel != null) chatHistoryPanel.SetActive(true);
        RestartAutoHide();

        yield return null;                    // TMP 메시 갱신 대기
        yield return new WaitForEndOfFrame(); // Canvas 레이아웃 확정 대기
        if (chatScrollRect != null)
        {
            chatScrollRect.StopMovement();
            chatScrollRect.verticalNormalizedPosition = 0f; // 0 = 하단(최신)
        }
    }

    // ── 입력 감지 (New Input System 직접 읽기) ────────────────────

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // CheerName 설정 패널이 열려 있는 동안엔 채팅이 Enter를 가져가면 안 된다(우선순위: cheername > chat).
        // 이미 채팅 입력창이 열려 있던 상태에서 패널이 열렸다면 강제로 닫아 포커스 충돌을 없앤다.
        // ConsumedEnterThisFrame도 같이 확인 — Host 자체 테스트처럼 확정 ServerRpc 왕복이 같은
        // 프레임에 끝나 IsOpen이 false로 바뀌어도, 같은 물리 Enter로 채팅이 열려버리는 걸 막는다
        // (ConsumedEscThisFrame·EscMenuController와 동일 이유의 명시적 플래그, 2026-08-22 수정).
        if (TutorialCheerNameUI.IsOpen || TutorialCheerNameUI.ConsumedEnterThisFrame)
        {
            if (_inputOpen) CloseInput();
            return;
        }

        bool enterPressed  = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
        bool escapePressed = kb.escapeKey.wasPressedThisFrame;

        // 입력창 닫힌 상태에서 Enter → 열기
        if (!_inputOpen && enterPressed)
        {
            OpenInput();
            return;
        }

        if (_inputOpen)
        {
            // Enter → 텍스트 있으면 전송, 어느 쪽이든 입력창은 닫고 정상 플레이로 복귀
            if (enterPressed && !_skipNextSubmit)
            {
                string text = string.Empty;
                if (inputField != null)
                {
                    // 한글 IME 조합 중 Enter를 누르면 TMP_InputField.text가 아직 조합 문자를
                    // 반영하기 전이라 마지막 음절이 통째로 잘려서 전송되는 Unity 버그
                    // (Windows IME + TMP_InputField, Unity Issue Tracker UUM-100241, Won't Fix).
                    // 여기서 강제로 조합 문자열을 텍스트에 커밋한 뒤 읽어서 우회한다.
                    if (!string.IsNullOrEmpty(Input.compositionString))
                        inputField.text += Input.compositionString;
                    text = inputField.text.Trim();
                }
                if (!string.IsNullOrEmpty(text))
                    SendChat(text);
                CloseInput();
                return;
            }

            // Escape → 닫기
            if (escapePressed)
            {
                CloseInput();
                return;
            }

            // skipNextSubmit 해제 (OpenInput 직후 한 프레임만 보호)
            if (_skipNextSubmit) _skipNextSubmit = false;

            // 마우스 휠 → 히스토리 스크롤
            // 커서가 Locked 상태일 때 EventSystem이 ScrollRect에 휠 이벤트를 전달하지 못하므로 직접 처리
            if (Mouse.current != null && chatScrollRect != null)
            {
                float wheel = Mouse.current.scroll.ReadValue().y;
                if (wheel != 0f)
                    HandleScrollWheel(wheel);
            }
        }
    }

    // ── 입력창 열기/닫기 ──────────────────────────────────────────

    void OpenInput()
    {
        _inputOpen = true;
        IsChatOpen = true;
        if (chatInputPanel   != null) chatInputPanel.SetActive(true);
        if (chatHistoryPanel != null) chatHistoryPanel.SetActive(true);
        CursorUnlockRequestUtil.Request(this);
        StartCoroutine(ActivateInputNextFrame());
    }

    IEnumerator ActivateInputNextFrame()
    {
        _skipNextSubmit = true;
        yield return null;
        // 1프레임 유예 사이에 CheerName 패널 등이 끼어들어 이미 CloseInput()으로 닫혔다면
        // 여기서 도로 포커스를 뺏어오면 안 된다(RefocusNextFrame이 예전에 하던 것과 동일 가드).
        if (!_inputOpen) yield break;
        if (inputField != null)
        {
            inputField.text = string.Empty;
            inputField.ActivateInputField();
            EventSystem.current?.SetSelectedGameObject(inputField.gameObject);
        }
    }

    void CloseInput()
    {
        _inputOpen = false;
        IsChatOpen = false;
        if (inputField     != null) { inputField.DeactivateInputField(); inputField.text = string.Empty; }
        if (chatInputPanel != null) chatInputPanel.SetActive(false);
        CursorUnlockRequestUtil.Release(this);

        RestartAutoHide();
    }

    // ── 메시지 전송 ───────────────────────────────────────────────

    void SendChat(string trimmed)
    {
        SendMessageServerRpc(trimmed);
    }

    // ── 네트워크 ──────────────────────────────────────────────────

    // [버그 수정 2026-09-01] SequenceRing 이중 판정과 동일 원인(재호스팅 시 같은 프레임 RPC 중복
    // 수신) — 채팅은 시간 쿨다운·Add류 자연 가드가 없어 그대로 두면 같은 메시지가 두 번 찍힌다.
    // 클라이언트는 SendChat()이 프레임당 한 번만 이 RPC를 보내므로 같은 sender의 같은 프레임
    // 2번째 수신은 항상 중복이다 (StageNetworkState.IsDuplicateChallengeSubmit과 동일 패턴).
    private readonly Dictionary<ulong, int> _lastMessageFrame = new();

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void SendMessageServerRpc(string message, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        int frame = Time.frameCount;
        if (_lastMessageFrame.TryGetValue(senderId, out int lastFrame) && lastFrame == frame)
            return;
        _lastMessageFrame[senderId] = frame;

        int colorIdx = -1;
        // PlayerSpawnCoordinator(NetworkList) — 서버·클라이언트 공통 단일 소스
        if (PlayerSpawnCoordinator.TryGetColor(senderId, out var color))
            colorIdx = System.Array.IndexOf(PlayerColorUtil.ColorOrder, color);

        ReceiveMessageClientRpc(message, colorIdx);
    }

    [ClientRpc]
    void ReceiveMessageClientRpc(string message, int senderColorIndex)
        => AddMessageLocal(message, senderColorIndex);

    // ── 메시지 표시 ───────────────────────────────────────────────

    void AddMessageLocal(string message, int senderColorIndex)
    {
        if (chatContent == null) return;

        // 정적 히스토리에 추가
        s_history.Add((message, senderColorIndex));

        // 히스토리 최대치 초과 시 가장 오래된 항목 제거 (정적·로컬 동시)
        if (s_history.Count > maxMessages)
            s_history.RemoveAt(0);

        if (chatHistoryPanel != null) chatHistoryPanel.SetActive(true);
        RestartAutoHide();

        CreateMessageObject(message, senderColorIndex);

        // 최대 메시지 수 초과 시 UI에서도 오래된 것 제거
        if (_messages.Count > maxMessages)
        {
            Destroy(_messages[0].gameObject);
            _messages.RemoveAt(0);
        }

        StartCoroutine(ScrollToBottomRoutine());
    }

    /// <summary>메시지 UI 오브젝트를 생성하고 _messages에 추가.</summary>
    void CreateMessageObject(string message, int senderColorIndex)
    {
        string cheerName = CheerService.GetCheerName(senderColorIndex);
        cheerName = string.IsNullOrEmpty(cheerName) ? "???" : cheerName.ToUpper();
        Color  nameColor = GetPlayerColor(senderColorIndex);
        string hex       = ColorUtility.ToHtmlStringRGB(nameColor);

        var msgObj = new GameObject("Msg");
        msgObj.transform.SetParent(chatContent, false);

        var tmp           = msgObj.AddComponent<TextMeshProUGUI>();
        tmp.text          = $"<color=#{hex}><b>{cheerName}</b></color>: {message}";
        tmp.fontSize      = CurrentFontSize;
        tmp.color         = messageTextColor;
        tmp.raycastTarget = false;

        var rt       = tmp.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(0f, 0f);

        var csf         = msgObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _messages.Add(tmp);
    }

    IEnumerator ScrollToBottomRoutine()
    {
        yield return null;                    // TMP 메시 갱신 대기
        yield return new WaitForEndOfFrame(); // Canvas 레이아웃 확정 대기
        if (chatScrollRect == null) yield break;
        chatScrollRect.StopMovement();
        chatScrollRect.verticalNormalizedPosition = 0f; // 0 = 하단(최신)
    }

    /// <summary>커서 잠금 환경에서 마우스 휠을 직접 읽어 히스토리 스크롤.</summary>
    void HandleScrollWheel(float delta)
    {
        if (chatScrollRect == null) return;
        // ScrollRect velocity.y: 양수 = 콘텐츠 위로(→ 최신), 음수 = 콘텐츠 아래(→ 오래된)
        // 휠 위(delta>0) → 오래된 메시지 → velocity 음수
        // 휠 아래(delta<0) → 최신 메시지 → velocity 양수
        chatScrollRect.velocity = new Vector2(0f, -delta * scrollWheelSensitivity);
    }

    // ── 자동 숨김 ─────────────────────────────────────────────────

    void RestartAutoHide()
    {
        if (_autoHideRoutine != null) StopCoroutine(_autoHideRoutine);
        _autoHideRoutine = StartCoroutine(AutoHideRoutine());
    }

    IEnumerator AutoHideRoutine()
    {
        yield return new WaitForSeconds(autoHideSeconds);
        if (!_inputOpen && chatHistoryPanel != null)
            chatHistoryPanel.SetActive(false);
    }

    // ── 유틸 ──────────────────────────────────────────────────────

    Color GetPlayerColor(int colorIndex) => colorIndex switch
    {
        0 => colorBerry,
        1 => colorGuma,
        2 => colorSsuk,
        3 => colorDanho,
        _ => Color.white,
    };
}
