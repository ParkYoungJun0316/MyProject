using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using TMPro;

/// <summary>
/// 좌/우(+선택적 앞/뒤) 분기 미니게임 전용 UI. 진행도(3/5)는 ObjectiveUI Count 모드가 담당한다.
///
/// [Inspector 연결]
///   challenge    : SideSplitChallenge
///   mainText     : 안내 문구 / 성공·실패를 교체해 표시하는 TMP
///   timerText    : 라운드 진행 중에만 표시되는 타이머 TMP
///
/// [문구 템플릿 — String Table 참조, OXQuizManager.OXQuestion과 동일 원칙(Inspector에서 문자열
/// 직접 입력이 아니라 Table+Entry 연결)]
///   promptNoColor    : {0}=왼쪽 인원, {1}=오른쪽 인원 (2방향 모드 — M.Stage2)
///   promptColorLeft  : {0}=왼쪽 인원, {1}=오른쪽 인원, {2}=필수 색상명 (왼쪽에 색 조건)
///   promptColorRight : {0}=왼쪽 인원, {1}=오른쪽 인원, {2}=필수 색상명 (오른쪽에 색 조건)
///   colorName*       : PlayerColorType.Blue/Purple/Green/Yellow 각각의 로컬라이즈된 색상명
///   인원 숫자 인자는 TMP 노란 강조, 색상명 인자는 PlayerColorUtil.GetHudTextColor로 칠한다.
///
/// [4방향 확장 템플릿 — challenge.IsFourDirection == true일 때만 사용, T.Stage4]
///   promptNoColor4      : {0}=앞, {1}=뒤, {2}=왼쪽, {3}=오른쪽 인원
///   promptColorFront4/Back4/Left4/Right4 : {0}=앞, {1}=뒤, {2}=왼쪽, {3}=오른쪽 인원, {4}=필수 색상명
///                                          (색 조건이 걸린 방향에 대응하는 템플릿 하나만 선택돼 사용됨)
///
/// [표시 흐름]
///   안내 문구 표시
///   → 타이머 카운트다운 (roundTimeLimit 동안)
///   → 타이머 숨김 + 성공/실패 텍스트 (resultDisplayDuration 초)
///   → 다음 라운드 안내. 전 라운드가 끝나면 성공 연출 후 패널만 숨김
///     (Clear 문구는 ObjectiveUI 씬 클리어 SSOT)
///
/// [주의] 씬 로드 시 이 오브젝트는 활성 상태여야 Awake에서 리스너를 등록할 수 있다.
///        Awake 끝에서 SetActive(false)로 숨긴다.
/// </summary>
public class SideSplitUI : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] SideSplitChallenge challenge;

    [Header("메인 텍스트 (안내 문구 / 성공·실패 공용)")]
    [SerializeField] TextMeshProUGUI mainText;

    [Header("타이머")]
    [SerializeField] TextMeshProUGUI timerText;

    [Header("안내 문구 템플릿 — 2방향 모드 (String Table 엔트리 연결)")]
    [SerializeField] LocalizedString promptNoColor;
    [SerializeField] LocalizedString promptColorLeft;
    [SerializeField] LocalizedString promptColorRight;

    [Header("안내 문구 템플릿 — 4방향 모드 (challenge.IsFourDirection일 때만 사용, T.Stage4)")]
    [SerializeField] LocalizedString promptNoColor4;
    [SerializeField] LocalizedString promptColorFront4;
    [SerializeField] LocalizedString promptColorBack4;
    [SerializeField] LocalizedString promptColorLeft4;
    [SerializeField] LocalizedString promptColorRight4;

    [Header("결과 텍스트 (String Table 엔트리 연결)")]
    [SerializeField] LocalizedString successText;
    [SerializeField] LocalizedString failText;

    [Header("색상명 (String Table 엔트리 연결 — Blue/Purple/Green/Yellow만 사용)")]
    [SerializeField] LocalizedString colorNameBlue;
    [SerializeField] LocalizedString colorNamePurple;
    [SerializeField] LocalizedString colorNameGreen;
    [SerializeField] LocalizedString colorNameYellow;

    [Header("표시 시간(초)")]
    [SerializeField] float resultDisplayDuration = 1.5f;

    [Header("텍스트 색상")]
    [SerializeField] Color promptColor  = Color.white;
    [SerializeField] Color successColor = new Color(0.2f, 0.9f, 0.3f, 1f);
    [SerializeField] Color failColor    = new Color(0.95f, 0.2f, 0.2f, 1f);

    [Header("인원 수 강조 (TMP Rich Text)")]
    [Tooltip("안내 문구의 인원 숫자. 알파 0이면 기본 노랑(기존 프리팹 미직렬화 대비).")]
    [SerializeField] Color countHighlightColor = new Color(1f, 0.85f, 0.2f, 1f);

    static readonly Color FallbackCountHighlight = new Color(1f, 0.85f, 0.2f, 1f);

    Coroutine _sequence;
    bool _allCleared;

    // ── Unity 라이프사이클 ─────────────────────────────────────────

    void Awake()
    {
        // 리스너를 먼저 등록한 뒤 숨김.
        // Start()에 두면 오브젝트 비활성 → Start 지연 → 첫 OnRoundReady 수신 실패.
        RegisterListeners();
        if (mainText != null) mainText.richText = true;
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        UnregisterListeners();
    }

    // ── 이벤트 구독 ───────────────────────────────────────────────

    void RegisterListeners()
    {
        if (challenge == null) return;

        challenge.OnRoundReady.AddListener(ShowRound);
        challenge.OnTimerTick.AddListener(SetTimer);
        challenge.OnRoundSuccess.AddListener(ShowSuccess);
        challenge.OnRoundFailed.AddListener(ShowFail);
        challenge.OnAllCleared.AddListener(HideAfterAllCleared);
    }

    void UnregisterListeners()
    {
        if (challenge == null) return;

        challenge.OnRoundReady.RemoveListener(ShowRound);
        challenge.OnTimerTick.RemoveListener(SetTimer);
        challenge.OnRoundSuccess.RemoveListener(ShowSuccess);
        challenge.OnRoundFailed.RemoveListener(ShowFail);
        challenge.OnAllCleared.RemoveListener(HideAfterAllCleared);
    }

    // ── 수신 메서드 ───────────────────────────────────────────────

    /// <summary>SideSplitChallenge.OnRoundReady</summary>
    void ShowRound(SideSplitRoundInfo info)
    {
        _allCleared = false;
        StopSequence();
        gameObject.SetActive(true);
        SetTimerVisible(true);
        SetMain(BuildPromptText(info), promptColor);
    }

    /// <summary>SideSplitChallenge.OnTimerTick</summary>
    void SetTimer(float remaining)
    {
        if (timerText == null) return;
        int display = Mathf.CeilToInt(remaining);
        timerText.text = display > 0 ? display.ToString() : "";
    }

    /// <summary>SideSplitChallenge.OnRoundSuccess</summary>
    void ShowSuccess()
    {
        StopSequence();
        _sequence = StartCoroutine(ResultSequence(true));
    }

    /// <summary>SideSplitChallenge.OnRoundFailed</summary>
    void ShowFail()
    {
        StopSequence();
        _sequence = StartCoroutine(ResultSequence(false));
    }

    /// <summary>SideSplitChallenge.OnAllCleared — Clear 문구는 ObjectiveUI SSOT. 마지막 성공 연출이 끝나면 패널만 숨김.</summary>
    void HideAfterAllCleared()
    {
        _allCleared = true;
        if (_sequence == null)
            gameObject.SetActive(false);
    }

    // ── 내부 시퀀스 ───────────────────────────────────────────────

    IEnumerator ResultSequence(bool success)
    {
        SetTimerVisible(false);

        LocalizedString text = success ? successText : failText;
        SetMain(text.GetLocalizedString(), success ? successColor : failColor);
        yield return new WaitForSeconds(resultDisplayDuration);

        _sequence = null;
        if (_allCleared)
            gameObject.SetActive(false);
        // 아니면 매니저의 resolveDelay가 끝나면 OnRoundReady → ShowRound
    }

    // ── 내부 유틸 ─────────────────────────────────────────────────

    string BuildPromptText(SideSplitRoundInfo info)
    {
        if (challenge != null && challenge.IsFourDirection)
            return BuildPromptText4(info);

        if (!info.hasColorRequirement)
            return FormatPrompt(promptNoColor, HighlightCount(info.leftCount), HighlightCount(info.rightCount));

        LocalizedString template = info.colorDirection == SideSplitDirection.Left ? promptColorLeft : promptColorRight;
        return FormatPrompt(template,
            HighlightCount(info.leftCount),
            HighlightCount(info.rightCount),
            ColorizeName(info.requiredColor));
    }

    /// <summary>4방향 모드(T.Stage4) 전용 안내 문구 — 인자 순서는 항상 앞/뒤/좌/우 고정.</summary>
    string BuildPromptText4(SideSplitRoundInfo info)
    {
        if (!info.hasColorRequirement)
        {
            return FormatPrompt(promptNoColor4,
                HighlightCount(info.frontCount),
                HighlightCount(info.backCount),
                HighlightCount(info.leftCount),
                HighlightCount(info.rightCount));
        }

        LocalizedString template = info.colorDirection switch
        {
            SideSplitDirection.Front => promptColorFront4,
            SideSplitDirection.Back  => promptColorBack4,
            SideSplitDirection.Left  => promptColorLeft4,
            _                        => promptColorRight4,
        };
        return FormatPrompt(template,
            HighlightCount(info.frontCount),
            HighlightCount(info.backCount),
            HighlightCount(info.leftCount),
            HighlightCount(info.rightCount),
            ColorizeName(info.requiredColor));
    }

    /// <summary>DeathOverlayUI와 동일 — 인자를 GetLocalizedString에 직접 넘겨 {0} 치환을 보장.</summary>
    static string FormatPrompt(LocalizedString template, params object[] args)
    {
        if (template == null || template.IsEmpty) return string.Empty;
        return template.GetLocalizedString(args);
    }

    Color CountAccent => countHighlightColor.a > 0.01f ? countHighlightColor : FallbackCountHighlight;

    string HighlightCount(int count) => WrapTmp(count.ToString(), CountAccent);

    string ColorizeName(PlayerColorType color)
        => WrapTmp(GetColorName(color), PlayerColorUtil.GetHudTextColor(color));

    static string WrapTmp(string inner, Color color)
        => $"<color=#{ColorUtility.ToHtmlStringRGB(color)}><b>{inner}</b></color>";

    string GetColorName(PlayerColorType color) => color switch
    {
        PlayerColorType.Blue   => colorNameBlue.GetLocalizedString(),
        PlayerColorType.Purple => colorNamePurple.GetLocalizedString(),
        PlayerColorType.Green  => colorNameGreen.GetLocalizedString(),
        PlayerColorType.Yellow => colorNameYellow.GetLocalizedString(),
        _                      => color.ToString(),
    };

    void SetMain(string text, Color color)
    {
        if (mainText == null) return;
        mainText.text  = text;
        mainText.color = color;
    }

    void SetTimerVisible(bool visible)
    {
        if (timerText == null) return;
        timerText.gameObject.SetActive(visible);
    }

    void StopSequence()
    {
        if (_sequence == null) return;
        StopCoroutine(_sequence);
        _sequence = null;
    }
}
