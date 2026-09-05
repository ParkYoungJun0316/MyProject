using System.Collections;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Tutorial CheerName 입력·확정 UI — NetworkDesign.md §6B.7 P6 / CheerAndTutorialDesign.md §3.4·§8.3.
/// 네트워크 코어(PlayerCheerNameSync)는 이미 있음 — 이 스크립트는 그걸 붙잡는 로컬 입력 UI만 담당.
///
/// [배치] Tutorial 상시 HUD의 Canvas 자식(씬에 1개, TutorialRoomCodeDisplay와 형제)에 부착.
/// Player 프리팹에 붙이지 않는다 — 각 클라이언트는 자기 화면의 UI 하나만 보면 되므로 인원수만큼
/// 중복 생성할 필요가 없다(§6B.2 동적 합류와도 무관하게 항상 씬에 1개만 존재).
///
/// [TeamCheerWord, CheerSystemDesign.md D1]
/// 같은 패널에 Host 전용 입력 섹션을 둔다. Host는 TrySetTeamCheerWord를 직접 호출(RPC 없음).
/// 비-Host는 현재 값만 읽기 전용. 팀워드 확정은 패널을 닫지 않는다(CheerName 확정만 닫음).
/// 미연결이면 팀워드 UI만 없음 — CheerName 입력은 그대로 동작.
/// teamWordInputField/teamWordConfirmButton은 hostTeamWordSection의 자식으로 배치 — 부모
/// SetActive 1번으로 같이 꺼짐/켜짐(개별 SetActive 중복 방지).
///
/// [상시 표시 → 상호작용 표지판 개폐로 변경, 2026-08-19]
/// 이전엔 항상 화면에 떠 있었으나, 화면을 계속 가리고 "그 순간 지나면 다시 못 여는" DialogueUI식
/// 1회성 노출의 단점을 피하고자 Tutorial 씬의 상호작용 표지판(TutorialCheerNameSignboard)이
/// Open()/Close()를 호출해 여닫는 방식으로 변경. 이 GameObject 자체(패널 루트)가 활성/비활성으로
/// 토글된다 — 씬에는 기본 비활성 상태로 배치할 것(사용자 에디터 작업).
///
/// [입력 우선권, 2026-08-22]
/// 열려있는 동안 키보드 입력의 최우선권을 가진다 — Enter는 확정 제출(InGameChatUI는 무시/자동 닫힘),
/// Esc는 이 패널을 닫음(EscMenuController는 무시). 같은 프레임에 Esc가 눌렸을 때 "패널이 닫히자마자
/// Esc 메뉴가 같이 뜨는" 이중 소비를 막기 위해, 실행 순서에 의존하지 않고 <see cref="ConsumedEscThisFrame"/>
/// 명시적 플래그로 "이번 프레임에 Esc를 이미 이 패널이 소비했음"을 알린다(스크립트 실행 순서 가정 대신
/// 명시적 상태로 — Bug Hunter 리뷰 3항목 중 3번 수정).
///
/// [커서 공유, 2026-08-22]
/// 커서 lock/visible을 직접 건드리지 않고 <see cref="CursorUnlockRequestUtil"/>에 요청만 한다 —
/// EscMenu·이모트 메뉴가 동시에 열려 있을 때 "마지막에 닫은 UI가 무조건 잠금"으로 서로 덮어쓰지
/// 않도록(Bug Hunter 리뷰 3항목 중 2번 수정). 요청/해제는 Open()/Close()가 아니라 OnEnable/OnDisable에
/// 걸어, 씬 리로드로 패널이 열린 채 파괴돼도(Close() 호출 없이) Unity가 파괴 직전 자동 호출하는
/// OnDisable에서 요청이 반드시 정리된다. 다만 그 파괴가 씬 통째 언로드(TitleReturnFlow 등)로 인한
/// 것이면 실제 Cursor는 건드리지 않고 목록에서만 빠진다(<see cref="CursorUnlockRequestUtil.Forget"/>) —
/// 그렇지 않으면 타이틀 복귀 시 이미 풀어둔 커서를 도로 잠가버리는 회귀가 생긴다(2026-08-22 재수정).
/// </summary>
public class TutorialCheerNameUI : MonoBehaviour
{
    [Header("입력")]
    [SerializeField] TMP_InputField nameInputField;
    [SerializeField] Button confirmButton;
    [SerializeField] int maxLength = 12;

    [Header("닫기")]
    [Tooltip("비워도 됨 — 상호작용 표지판에서 다시 상호작용해도 닫힘(토글).")]
    [SerializeField] Button closeButton;

    [Header("표시")]
    [SerializeField] TMP_Text currentNameText;
    [SerializeField] TMP_Text feedbackText;
    [SerializeField] float feedbackDisplaySeconds = 2.5f;

    [Header("TeamCheerWord")]
    [Tooltip("Host 전용 입력 섹션 루트 — teamWordInputField/teamWordConfirmButton을 이 GameObject의 " +
             "자식으로 배치할 것(SetActive 1회로 같이 꺼짐/켜짐). 비-Host에선 숨김.")]
    [SerializeField] GameObject hostTeamWordSection;
    [Tooltip("비-Host 읽기 전용 섹션 루트. Host에선 숨김. 비워도 됨.")]
    [SerializeField] GameObject clientTeamWordSection;
    [Tooltip("hostTeamWordSection의 자식으로 배치.")]
    [SerializeField] TMP_InputField teamWordInputField;
    [Tooltip("hostTeamWordSection의 자식으로 배치.")]
    [SerializeField] Button teamWordConfirmButton;
    [SerializeField] TMP_Text currentTeamWordText;

    [Header("커서")]
    [Tooltip("패널 닫을 때 커서를 다시 잠글지 여부. ThirdPersonCamera.lockCursor 설정과 일치시키세요 " +
             "(EscMenuController/PlayerEmoteMenuUI와 동일 패턴).")]
    [SerializeField] bool lockCursorOnClose = true;

    /// <summary>패널이 열려있는 동안 true — Player.cs가 이동 입력을 잠그는 데 사용
    /// (InGameChatUI.IsChatOpen과 동일 패턴, §7.3 타이핑 중 WASD 새는 문제 방지).</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>이번 프레임에 Esc로 이 패널이 막 닫혔는지 — EscMenuController가 같은 프레임에
    /// 자기 메뉴를 열지 않도록 확인하는 명시적 플래그(실행 순서 비의존).</summary>
    public static bool ConsumedEscThisFrame => s_escClosedFrame == Time.frameCount;
    static int s_escClosedFrame = -1;

    /// <summary>이번 프레임에 Enter로 CheerName 또는 TeamCheerWord 확정을 시도했는지 — InGameChatUI가 같은 프레임에
    /// 채팅을 열지 않도록 확인하는 명시적 플래그. Host 자체 테스트 등에서 ServerRpc 왕복이
    /// 같은 프레임 안에 끝나 확정 성공과 동시에 IsOpen이 false로 바뀌어버리면, InGameChatUI가
    /// "입력창 닫힌 상태에서 Enter"로 오인해 같은 물리 Enter로 채팅을 열어버리는 문제를 막는다
    /// (ConsumedEscThisFrame과 동일 패턴, 2026-08-22 수정).</summary>
    public static bool ConsumedEnterThisFrame => s_enterConfirmFrame == Time.frameCount;
    static int s_enterConfirmFrame = -1;

    /// <summary>제출 응답이 이 시간 안에 안 오면 입력칸을 다시 열어준다 (아래 SubmitTimeoutRoutine).</summary>
    const float SubmitTimeoutSec = 3f;

    PlayerCheerNameSync _mySync;
    string _lastShownName;
    string _lastShownTeamWord;
    bool? _teamWordHostVisible;
    float _feedbackHideAt = -1f;
    bool _awaitingSubmitResult;
    Coroutine _submitTimeoutRoutine;

    void Awake()
    {
        if (nameInputField != null)
        {
            nameInputField.characterLimit = maxLength;
            nameInputField.onValidateInput = ValidateCharacter;
            nameInputField.onSubmit.AddListener(_ => OnConfirmClicked());
        }
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (teamWordInputField != null)
        {
            teamWordInputField.characterLimit = maxLength;
            teamWordInputField.onValidateInput = ValidateCharacter;
            teamWordInputField.onSubmit.AddListener(_ => OnTeamWordConfirmClicked());
        }
        if (teamWordConfirmButton != null)
            teamWordConfirmButton.onClick.AddListener(OnTeamWordConfirmClicked);

        SetInteractable(false); // 내 캐릭터를 찾기 전까지 비활성
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        IsOpen = true;
        // Esc를 눌러야만 커서가 풀리던 문제 — 패널이 열리면 즉시 커서를 풀어 마우스로 바로
        // 입력창/확정 버튼을 클릭할 수 있게 한다. OnEnable/OnDisable 짝으로 걸어 씬 파괴 시에도
        // Release가 보장된다(클래스 doc [커서 공유] 참고).
        CursorUnlockRequestUtil.Request(this);
        _teamWordHostVisible = null;
        _lastShownTeamWord = null;

        // 닫혀 있는 동안 제출 응답을 놓쳤을 수 있다(닫히면 코루틴·타임아웃이 같이 멈춘다).
        // 열릴 때마다 입력 상태를 다시 확정해 "회색 입력칸" 고착이 어떤 경로로도 남지 않게 한다.
        _awaitingSubmitResult = false;
        _submitTimeoutRoutine = null;
        HideFeedback();
        SetInteractable(_mySync != null);

        ApplyTeamWordRole();
    }

    void OnDisable()
    {
        IsOpen = false;
        _awaitingSubmitResult = false;
        _submitTimeoutRoutine = null;

        // 씬이 통째로 언로드되는 중(예: TitleReturnFlow의 SceneManager.LoadScene)이면 자동으로
        // OnDisable이 불려도 목록 제거만 하고 실제 Cursor는 건드리지 않는다 — 그 시점엔 이미
        // TitleReturnFlow 등이 최종 커서 상태를 정해뒀으므로 여기서 다시 잠그면 그걸 덮어써버려
        // "타이틀 씬에서 마우스가 사라지는" 회귀가 생긴다(2026-08-22 수정). 사용자가 직접 닫은
        // 경우(씬은 그대로 로드된 채 SetActive(false)만 됨)만 실제로 Release해서 잠근다.
        if (!gameObject.scene.isLoaded)
        {
            CursorUnlockRequestUtil.Forget(this);
            return;
        }
        CursorUnlockRequestUtil.Release(this, lockCursorOnClose);
    }

    // ── 상호작용 표지판에서 호출 (TutorialCheerNameSignboard) ────────

    /// <summary>패널 열기. 이미 열려있으면 아무 것도 안 함(중복 호출 안전).</summary>
    public void Open()
    {
        if (gameObject.activeSelf) return;
        gameObject.SetActive(true);
        StartCoroutine(FocusInputNextFrame());
    }

    /// <summary>InGameChatUI.ActivateInputNextFrame과 동일 패턴 — SetActive 직후 바로 활성화하면
    /// interactable=false 상태(첫 오픈 시 _mySync 미발견)일 수 있어 1프레임 대기 후 포커스한다.</summary>
    IEnumerator FocusInputNextFrame()
    {
        yield return null;
        if (nameInputField == null || !gameObject.activeSelf) yield break;
        nameInputField.ActivateInputField();
        EventSystem.current?.SetSelectedGameObject(nameInputField.gameObject);
    }

    IEnumerator FocusTeamWordNextFrame()
    {
        yield return null;
        if (teamWordInputField == null || !gameObject.activeSelf) yield break;
        teamWordInputField.ActivateInputField();
        EventSystem.current?.SetSelectedGameObject(teamWordInputField.gameObject);
    }

    /// <summary>패널 닫기 — 확정 여부와 무관, 타이핑 중이던 미확정 글자는 버려짐(§3.4상 문제 없음, 확정 전엔 로컬일 뿐).</summary>
    public void Close()
    {
        if (!gameObject.activeSelf) return;
        gameObject.SetActive(false); // 커서 Release는 OnDisable에서 처리
    }

    /// <summary>열려있으면 닫고, 닫혀있으면 연다 — 표지판 상호작용 1개 입력으로 개폐 겸용.</summary>
    public void Toggle()
    {
        if (gameObject.activeSelf) Close();
        else Open();
    }

    void OnDestroy()
    {
        if (_mySync != null) _mySync.OnSubmitResult -= HandleSubmitResult;
    }

    void Update()
    {
        // Esc는 닫기 버튼 대신 이 패널을 최우선으로 닫는다(§요청 3) — EscMenuController는
        // ConsumedEscThisFrame 플래그를 확인해 같은 프레임엔 자기 메뉴를 열지 않는다(실행 순서 비의존).
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            s_escClosedFrame = Time.frameCount;
            Close();
            return;
        }

        if (_mySync == null)
            TryFindLocalSync();

        if (_mySync != null)
            RefreshCurrentNameDisplay();

        ApplyTeamWordRole();
        RefreshCurrentTeamWordDisplay();

        if (_feedbackHideAt >= 0f && Time.time >= _feedbackHideAt && feedbackText != null)
        {
            feedbackText.gameObject.SetActive(false);
            _feedbackHideAt = -1f;
        }
    }

    // ── 내 캐릭터 탐색 (1회, 찾으면 폴링 중단) ──────────────────────

    void TryFindLocalSync()
    {
        var all = FindObjectsByType<PlayerCheerNameSync>(FindObjectsSortMode.None);
        foreach (var sync in all)
        {
            var netObj = sync.GetComponent<NetworkObject>();
            if (netObj == null || !netObj.IsOwner) continue;

            _mySync = sync;
            _mySync.OnSubmitResult += HandleSubmitResult;
            SetInteractable(true);
            break;
        }
    }

    // ── 입력 확정 ────────────────────────────────────────────────

    /// <summary>서버 규칙(CheerNameValidator)과 동일한 문자만 입력창에 타이핑 가능(편의용, 최종 검증은 Host).</summary>
    static char ValidateCharacter(string text, int charIndex, char addedChar)
    {
        char c = char.ToLowerInvariant(addedChar);
        bool allowed = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
        return allowed ? c : '\0';
    }

    void OnConfirmClicked()
    {
        if (_mySync == null || nameInputField == null) return;

        s_enterConfirmFrame = Time.frameCount;

        // 비활성화는 RPC 호출 "전에" — Host가 자기 자신에게 보내는 ServerRpc는 네트워크를 안 타고
        // 이 호출 안에서 즉시(동기) 처리되어 HandleSubmitResult까지 여기서 다 끝나버린다. 순서가
        // 바뀌면(호출 뒤에 비활성화) 방금 HandleSubmitResult가 켜둔 interactable=true를 이 줄이
        // 덮어써 false로 고정시켜버린다 — Host에서 최초 1회 확정 후 입력칸이 영구 비활성화되는
        // 버그였다(2026-09-01, Steam 4인 테스트에서 발견). Client는 실제 네트워크 왕복이라 이
        // 호출이 즉시 리턴되므로 순서와 무관하게 항상 정상 동작했다.
        SetInteractable(false); // 응답 오기 전까지 중복 제출 방지
        ShowFeedback("확인 중...", persistent: true);
        _awaitingSubmitResult = true;
        _mySync.SubmitCheerNameServerRpc(new FixedString32Bytes(nameInputField.text));

        // Host는 위 호출 안에서 결과 처리까지 끝나 이미 false가 됐을 수 있다 — 그때는 타이머 불필요.
        if (_awaitingSubmitResult)
            _submitTimeoutRoutine = StartCoroutine(SubmitTimeoutRoutine());
    }

    /// <summary>
    /// 제출 응답이 영원히 안 오는 경우의 복구. SubmitCheerNameServerRpc는 sender 검증
    /// (본인 캐릭터만 제출 가능)에 걸리거나 그 사이 Player가 Despawn되면 아무 응답도 보내지 않는데,
    /// 그러면 입력칸이 interactable=false로 영구 고착된다 — Host 순서 버그(위 주석)와 증상이
    /// 똑같아서 원인을 헷갈리게 만든다. 응답 없음도 실패로 취급해 입력칸을 되돌린다.
    /// </summary>
    IEnumerator SubmitTimeoutRoutine()
    {
        yield return new WaitForSeconds(SubmitTimeoutSec);
        _submitTimeoutRoutine = null;
        if (!_awaitingSubmitResult) yield break;

        _awaitingSubmitResult = false;
        SetInteractable(true);
        ShowFeedback("응답이 없어요. 다시 시도해 주세요.");
        StartCoroutine(FocusInputNextFrame());
    }

    void HandleSubmitResult(bool success, string errorKey)
    {
        _awaitingSubmitResult = false;
        if (_submitTimeoutRoutine != null)
        {
            StopCoroutine(_submitTimeoutRoutine);
            _submitTimeoutRoutine = null;
        }

        SetInteractable(true);

        if (success)
        {
            // 확정되면 닫기 버튼 대신 바로 닫는다(§요청 3) — 확정 결과는 패널 밖 닉네임 표시(예:
            // PlayerHPUI selfNameLabel)로 바로 반영되므로 패널 안에서 문구를 보여줄 필요가 없다.
            // "확인 중..."은 persistent라 여기서 지우지 않으면 자동 숨김도 안 되고, 다음에 패널을
            // 열 때 그대로 남아 있다(2026-09-05 수정).
            HideFeedback();
            if (nameInputField != null) nameInputField.text = "";
            Close();
        }
        else
        {
            ShowFeedback(ResolveErrorMessage(errorKey));
            StartCoroutine(FocusInputNextFrame()); // 실패 시 바로 다시 고쳐 쓸 수 있게 재포커스
        }
    }

    void OnTeamWordConfirmClicked()
    {
        s_enterConfirmFrame = Time.frameCount;

        if (!IsLocalServer())
        {
            ShowFeedback(ResolveTeamWordError("not_server"));
            return;
        }

        if (teamWordInputField == null) return;

        var svc = CheerService.Instance;
        if (svc == null || !svc.IsSpawned)
        {
            ShowFeedback(ResolveTeamWordError(""));
            return;
        }

        if (!svc.TrySetTeamCheerWord(teamWordInputField.text, out string reason))
        {
            ShowFeedback(ResolveTeamWordError(reason));
            StartCoroutine(FocusTeamWordNextFrame());
            return;
        }

        teamWordInputField.text = "";
        _lastShownTeamWord = null;
        RefreshCurrentTeamWordDisplay();
    }

    static string ResolveErrorMessage(string key) => key switch
    {
        "format"   => "2~12자, 영문 소문자/숫자/밑줄(_)만 사용할 수 있어요.",
        "reserved" => "시스템 예약어라 사용할 수 없는 이름이에요.",
        "blocked"  => "사용할 수 없는 단어가 포함되어 있어요.",
        "taken"    => "이미 다른 팀원이 사용 중인 이름이에요.",
        _          => "이름을 확정할 수 없어요.",
    };

    static string ResolveTeamWordError(string key) => key switch
    {
        "format"     => "2~12자, 영문 소문자/숫자/밑줄(_)만 사용할 수 있어요.",
        "reserved"   => "시스템 예약어라 사용할 수 없는 단어예요.",
        "blocked"    => "사용할 수 없는 단어가 포함되어 있어요.",
        "taken"      => "이미 팀원이 응원 이름으로 쓰고 있어요.",
        "not_server" => "호스트만 팀 키워드를 정할 수 있어요.",
        _            => "팀 키워드를 확정할 수 없어요.",
    };

    // ── 표시 ────────────────────────────────────────────────────

    void RefreshCurrentNameDisplay()
    {
        // 내 이름만 표시하는 라벨이라 전체 플레이어를 훑을 필요가 없다 — 이미 캐싱된 _mySync에서
        // 바로 읽는다(과거엔 GetAllEffectiveNames()로 전원 스캔 후 내 clientId만 필터링했음, 불필요).
        if (currentNameText == null || _mySync == null) return;

        string effective = _mySync.EffectiveCheerName;
        if (effective == _lastShownName) return;
        _lastShownName = effective;
        currentNameText.text = string.IsNullOrEmpty(effective) ? "현재 이름: (없음)" : $"현재 이름: {effective}";
    }

    void ApplyTeamWordRole()
    {
        bool isServer = IsLocalServer();
        bool canEdit = isServer && CheerServiceReady();

        if (_teamWordHostVisible != isServer)
        {
            _teamWordHostVisible = isServer;
            // teamWordInputField/teamWordConfirmButton은 hostTeamWordSection의 자식이라
            // 부모 SetActive 1번으로 같이 꺼짐/켜짐 — 개별 SetActive 중복 호출 없음.
            if (hostTeamWordSection != null)
                hostTeamWordSection.SetActive(isServer);
            if (clientTeamWordSection != null)
                clientTeamWordSection.SetActive(!isServer);
        }

        if (teamWordInputField != null)
            teamWordInputField.interactable = canEdit;
        if (teamWordConfirmButton != null)
            teamWordConfirmButton.interactable = canEdit;
    }

    void RefreshCurrentTeamWordDisplay()
    {
        if (currentTeamWordText == null) return;

        string word = ResolveCurrentTeamWord();
        if (word == _lastShownTeamWord) return;
        _lastShownTeamWord = word;
        currentTeamWordText.text = string.IsNullOrEmpty(word)
            ? $"팀 키워드: {GameSession.DefaultTeamCheerWord}"
            : $"팀 키워드: {word}";
    }

    static string ResolveCurrentTeamWord()
    {
        if (CheerService.Instance != null)
            return CheerService.Instance.TeamCheerWord;
        if (GameSession.Instance != null)
            return GameSession.Instance.GetSessionTeamCheerWord();
        return GameSession.DefaultTeamCheerWord;
    }

    static bool IsLocalServer()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && nm.IsServer;
    }

    static bool CheerServiceReady()
    {
        var svc = CheerService.Instance;
        return svc != null && svc.IsSpawned;
    }

    void ShowFeedback(string message, bool persistent = false)
    {
        if (feedbackText == null) return;
        feedbackText.gameObject.SetActive(true);
        feedbackText.text = message;
        _feedbackHideAt = persistent ? -1f : Time.time + feedbackDisplaySeconds;
    }

    void HideFeedback()
    {
        _feedbackHideAt = -1f;
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
    }

    void SetInteractable(bool value)
    {
        if (nameInputField != null) nameInputField.interactable = value;
        if (confirmButton != null)  confirmButton.interactable = value;
    }
}
