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
/// New Input System (activeInputHandler=1) 전용.
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
/// [키 흐름]
/// Enter (입력창 닫힘) → 입력창 열기
/// Enter (입력창 열림, 텍스트 있음) → 전송 + 재포커스 (창 유지)
/// Enter (입력창 열림, 텍스트 없음) → 입력창 닫기
/// Escape → 입력창 닫기
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
            // Enter → 전송 or 닫기
            if (enterPressed && !_skipNextSubmit)
            {
                string text = inputField != null ? inputField.text.Trim() : string.Empty;
                if (string.IsNullOrEmpty(text))
                    CloseInput();
                else
                    SendAndStay(text);
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
        StartCoroutine(ActivateInputNextFrame());
    }

    IEnumerator ActivateInputNextFrame()
    {
        _skipNextSubmit = true;
        yield return null;
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

        RestartAutoHide();
    }

    // ── 메시지 전송 ───────────────────────────────────────────────

    void SendAndStay(string trimmed)
    {
        TrySubmitCheer(trimmed);

        SendMessageServerRpc(trimmed);

        if (inputField != null) inputField.text = string.Empty;
        StartCoroutine(RefocusNextFrame());
    }

    IEnumerator RefocusNextFrame()
    {
        yield return null;
        if (_inputOpen && inputField != null)
        {
            inputField.ActivateInputField();
            EventSystem.current?.SetSelectedGameObject(inputField.gameObject);
        }
    }

    // ── /cheer 파싱 ───────────────────────────────────────────────

    void TrySubmitCheer(string text)
    {
        if (!text.StartsWith("/cheer ", System.StringComparison.OrdinalIgnoreCase)) return;
        string[] parts = text.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return;

        int targetIdx = CheerService.GetColorIndex(parts[1]);
        if (targetIdx < 0) return;

        CheerService.Instance?.SubmitCheerServerRpc(targetIdx, false);
    }

    // ── 네트워크 ──────────────────────────────────────────────────

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void SendMessageServerRpc(string message, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        int colorIdx   = -1;
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

    static int GetMyColorIndex()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            ulong myId = NetworkManager.Singleton.LocalClientId;
            // PlayerSpawnCoordinator(NetworkList) — 클라이언트에서도 레이스 없이 항상 최신값
            if (PlayerSpawnCoordinator.TryGetColor(myId, out var color))
                return System.Array.IndexOf(PlayerColorUtil.ColorOrder, color);
        }
        foreach (var p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            var net = p.GetComponent<NetworkObject>();
            if ((net != null && net.IsOwner) || p.isOwnerControlled)
                return System.Array.IndexOf(PlayerColorUtil.ColorOrder, p.playerColorType);
        }
        return -1;
    }
}
