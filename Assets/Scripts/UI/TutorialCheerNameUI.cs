using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.UI;

/// <summary>
/// Tutorial TeamCheerWord 입력·확정 UI — NetworkDesign.md §6B.7 P6 / CheerAndTutorialDesign.md §3.4·§8.3.
///
/// [2026-09-14] 개인 CheerName 커스텀화 완전 삭제 — 이름은 이제 PlayerColorUtil.DefaultCheerNames
/// (berry/guma/sook/dan) 고정값이다. 이 패널이 다루는 건 TeamCheerWord 하나뿐: Host는 입력해서
/// 정하고, 비-Host는 현재 값만 읽기 전용으로 본다. 클래스 이름(TutorialCheerNameUI)은 하위 호환을
/// 위해 유지 — 실질 역할은 "TeamCheerWord 패널"이다.
///
/// [배치] Tutorial 상시 HUD의 Canvas 자식(씬에 1개, TutorialRoomCodeDisplay와 형제)에 부착.
/// Player 프리팹에 붙이지 않는다 — 각 클라이언트는 자기 화면의 UI 하나만 보면 되므로 인원수만큼
/// 중복 생성할 필요가 없다(§6B.2 동적 합류와도 무관하게 항상 씬에 1개만 존재).
///
/// [TeamCheerWord, CheerSystemDesign.md D1]
/// Host는 TrySetTeamCheerWord를 직접 호출(RPC 없음, 동기 처리라 "제출 중" 대기 상태 없음).
/// 비-Host는 현재 값만 읽기 전용. teamWordInputField/teamWordConfirmButton은 hostTeamWordSection의
/// 자식으로 배치 — 부모 SetActive 1번으로 같이 꺼짐/켜짐(개별 SetActive 중복 방지).
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
/// 명시적 플래그로 "이번 프레임에 Esc를 이미 이 패널이 소비했음"을 알린다.
///
/// [커서 공유, 2026-08-22]
/// 커서 lock/visible을 직접 건드리지 않고 <see cref="CursorUnlockRequestUtil"/>에 요청만 한다 —
/// EscMenu·이모트 메뉴가 동시에 열려 있을 때 "마지막에 닫은 UI가 무조건 잠금"으로 서로 덮어쓰지
/// 않도록. 요청/해제는 Open()/Close()가 아니라 OnEnable/OnDisable에 걸어, 씬 리로드로 패널이 열린
/// 채 파괴돼도(Close() 호출 없이) Unity가 파괴 직전 자동 호출하는 OnDisable에서 요청이 반드시
/// 정리된다. 다만 그 파괴가 씬 통째 언로드(TitleReturnFlow 등)로 인한 것이면 실제 Cursor는 건드리지
/// 않고 목록에서만 빠진다(<see cref="CursorUnlockRequestUtil.Forget"/>).
/// </summary>
public class TutorialCheerNameUI : MonoBehaviour
{
    [Header("닫기")]
    [Tooltip("비워도 됨 — 상호작용 표지판에서 다시 상호작용해도 닫힘(토글).")]
    [SerializeField] Button closeButton;

    [Header("표시")]
    [SerializeField] TMP_Text feedbackText;
    [SerializeField] float feedbackDisplaySeconds = 2.5f;

    [Header("TeamCheerWord")]
    [Tooltip("입력 문자 제한(2~12자 형식 검증과는 별개, TMP_InputField.characterLimit).")]
    [SerializeField] int maxLength = 12;
    [Tooltip("Host 전용 입력 섹션 루트 — teamWordInputField/teamWordConfirmButton을 이 GameObject의 " +
             "자식으로 배치할 것(SetActive 1회로 같이 꺼짐/켜짐). 비-Host에선 숨김. " +
             "currentTeamWordText는 이 섹션 밖(패널 직계)에 있어 Host/Client 공통으로 항상 보인다.")]
    [SerializeField] GameObject hostTeamWordSection;
    [Tooltip("hostTeamWordSection의 자식으로 배치.")]
    [SerializeField] TMP_InputField teamWordInputField;
    [Tooltip("hostTeamWordSection의 자식으로 배치.")]
    [SerializeField] Button teamWordConfirmButton;
    [SerializeField] TMP_Text currentTeamWordText;

    [Header("커서")]
    [Tooltip("패널 닫을 때 커서를 다시 잠글지 여부. ThirdPersonCamera.lockCursor 설정과 일치시키세요 " +
             "(EscMenuController와 동일 패턴).")]
    [SerializeField] bool lockCursorOnClose = true;

    // ── Localization (Tutorial 테이블, TutorialTranslations.md §CheerNamePanel) ──────────
    // 비어 있거나(IsEmpty) 테이블 로드가 아직 안 끝났으면 한국어 폴백 — OptionsMenuController/
    // DeathOverlayUI와 동일 패턴(LocalizedOrFallback 참고).

    [Header("Localization — 피드백")]
    [SerializeField] LocalizedString feedbackFormat;
    [SerializeField] LocalizedString feedbackBlocked;
    [SerializeField] LocalizedString feedbackReservedTeam;
    [SerializeField] LocalizedString feedbackGenericTeam;
    [SerializeField] LocalizedString feedbackNotServer;

    [Header("Localization — 표시")]
    [Tooltip("{0} 포맷 — GetLocalizedString(팀 키워드 대문자)로 호출.")]
    [SerializeField] LocalizedString teamKeywordPrefix;

    /// <summary>패널이 열려있는 동안 true — Player.cs가 이동 입력을 잠그는 데 사용
    /// (InGameChatUI.IsChatOpen과 동일 패턴, §7.3 타이핑 중 WASD 새는 문제 방지).</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>이번 프레임에 Esc로 이 패널이 막 닫혔는지 — EscMenuController가 같은 프레임에
    /// 자기 메뉴를 열지 않도록 확인하는 명시적 플래그(실행 순서 비의존).</summary>
    public static bool ConsumedEscThisFrame => s_escClosedFrame == Time.frameCount;
    static int s_escClosedFrame = -1;

    /// <summary>이번 프레임에 Enter로 TeamCheerWord 확정을 시도했는지 — InGameChatUI가 같은 프레임에
    /// 채팅을 열지 않도록 확인하는 명시적 플래그.</summary>
    public static bool ConsumedEnterThisFrame => s_enterConfirmFrame == Time.frameCount;
    static int s_enterConfirmFrame = -1;

    string _lastShownTeamWord;
    bool? _teamWordHostVisible;
    float _feedbackHideAt = -1f;

    void Awake()
    {
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

        HideFeedback();
        ApplyTeamWordRole();
    }

    void OnDisable()
    {
        IsOpen = false;

        // 씬이 통째로 언로드되는 중(예: TitleReturnFlow의 SceneManager.LoadScene)이면 자동으로
        // OnDisable이 불려도 목록 제거만 하고 실제 Cursor는 건드리지 않는다 — 그 시점엔 이미
        // TitleReturnFlow 등이 최종 커서 상태를 정해뒀으므로 여기서 다시 잠그면 그걸 덮어써버려
        // "타이틀 씬에서 마우스가 사라지는" 회귀가 생긴다. 사용자가 직접 닫은 경우(씬은 그대로
        // 로드된 채 SetActive(false)만 됨)만 실제로 Release해서 잠근다.
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
        StartCoroutine(FocusTeamWordNextFrame());
    }

    /// <summary>InGameChatUI.ActivateInputNextFrame과 동일 패턴 — SetActive 직후 바로 활성화하면
    /// hostTeamWordSection이 아직 안 켜진 상태(비-Host)일 수 있어 1프레임 대기 후 포커스한다.
    /// 비-Host는 애초에 포커스할 입력창이 없으므로 건너뛴다.</summary>
    IEnumerator FocusTeamWordNextFrame()
    {
        yield return null;
        if (teamWordInputField == null || !gameObject.activeSelf || !IsLocalServer()) yield break;
        teamWordInputField.ActivateInputField();
        EventSystem.current?.SetSelectedGameObject(teamWordInputField.gameObject);
    }

    /// <summary>패널 닫기 — 확정 여부와 무관, 타이핑 중이던 미확정 글자는 버려짐.</summary>
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

    void Update()
    {
        // Esc는 닫기 버튼 대신 이 패널을 최우선으로 닫는다 — EscMenuController는
        // ConsumedEscThisFrame 플래그를 확인해 같은 프레임엔 자기 메뉴를 열지 않는다(실행 순서 비의존).
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            s_escClosedFrame = Time.frameCount;
            Close();
            return;
        }

        ApplyTeamWordRole();
        RefreshCurrentTeamWordDisplay();

        if (_feedbackHideAt >= 0f && Time.time >= _feedbackHideAt && feedbackText != null)
        {
            feedbackText.gameObject.SetActive(false);
            _feedbackHideAt = -1f;
        }
    }

    // ── 입력 확정 ────────────────────────────────────────────────

    /// <summary>서버 규칙(CheerNameValidator)과 동일한 문자만 입력창에 타이핑 가능(편의용, 최종 검증은 Host).
    /// 숫자/밑줄(_) 제외 — Vosk 음성 인식이 발음 불가능한 문자라 실제 응원 매칭이 안 됨.</summary>
    static char ValidateCharacter(string text, int charIndex, char addedChar)
    {
        char c = char.ToLowerInvariant(addedChar);
        bool allowed = c >= 'a' && c <= 'z';
        return allowed ? c : '\0';
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

    string ResolveTeamWordError(string key) => key switch
    {
        "format"     => LocalizedOrFallback(feedbackFormat, "2~12자, 영문 소문자만 사용할 수 있어요."),
        "reserved"   => LocalizedOrFallback(feedbackReservedTeam, "시스템 예약어라 사용할 수 없는 단어예요."),
        "blocked"    => LocalizedOrFallback(feedbackBlocked, "사용할 수 없는 단어가 포함되어 있어요."),
        "not_server" => LocalizedOrFallback(feedbackNotServer, "호스트만 팀 키워드를 정할 수 있어요."),
        _            => LocalizedOrFallback(feedbackGenericTeam, "팀 키워드를 확정할 수 없어요."),
    };

    /// <summary>String Table 엔트리가 아직 연결 안 됐거나(IsEmpty) 로드 레이스로 빈 문자열이면
    /// 한국어 기본값으로 폴백 (OptionsMenuController.LocalizedOrFallback과 동일 패턴).</summary>
    static string LocalizedOrFallback(LocalizedString localized, string fallback)
    {
        if (localized == null || localized.IsEmpty) return fallback;
        string value = localized.GetLocalizedString();
        return string.IsNullOrEmpty(value) ? fallback : value;
    }

    // ── 표시 ────────────────────────────────────────────────────

    void ApplyTeamWordRole()
    {
        bool isServer = IsLocalServer();
        bool canEdit = isServer && CheerServiceReady();

        if (_teamWordHostVisible != isServer)
        {
            _teamWordHostVisible = isServer;
            // teamWordInputField/teamWordConfirmButton은 hostTeamWordSection의 자식이라
            // 부모 SetActive 1번으로 같이 꺼짐/켜짐 — 개별 SetActive 중복 호출 없음.
            // 비-Host는 이 섹션이 통째로 꺼지고, currentTeamWordText(패널 직계, 항상 표시)로만
            // 현재 팀 키워드를 읽기 전용으로 본다.
            if (hostTeamWordSection != null)
                hostTeamWordSection.SetActive(isServer);
        }

        if (teamWordInputField != null)
            teamWordInputField.interactable = canEdit;
        if (teamWordConfirmButton != null)
            teamWordConfirmButton.interactable = canEdit;
    }

    /// <summary>표시 전용 — 저장/매칭용 값은 그대로 소문자 유지, 화면에 보일 때만 대문자로 바꾼다.</summary>
    void RefreshCurrentTeamWordDisplay()
    {
        if (currentTeamWordText == null) return;

        string word = CheerService.ResolveTeamCheerWord();
        if (word == _lastShownTeamWord) return;
        _lastShownTeamWord = word;

        currentTeamWordText.text = FormatTeamKeywordPrefix(word.ToUpperInvariant());
    }

    const string FallbackTeamKeywordPrefix = "팀 키워드: {0}";

    /// <summary>Tutorial/CheerNamePanel.TeamKeywordPrefix — "{0}" 포맷 문자열, 팀 키워드(대문자)를 인자로 채운다.</summary>
    string FormatTeamKeywordPrefix(string upperWord)
    {
        if (teamKeywordPrefix != null && !teamKeywordPrefix.IsEmpty)
        {
            string localized = teamKeywordPrefix.GetLocalizedString(upperWord);
            if (!string.IsNullOrEmpty(localized)) return localized;
        }
        return string.Format(FallbackTeamKeywordPrefix, upperWord);
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

    void ShowFeedback(string message)
    {
        if (feedbackText == null) return;
        feedbackText.gameObject.SetActive(true);
        feedbackText.text = message;
        _feedbackHideAt = Time.time + feedbackDisplaySeconds;
    }

    void HideFeedback()
    {
        _feedbackHideAt = -1f;
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
    }
}
