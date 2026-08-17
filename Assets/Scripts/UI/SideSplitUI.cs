using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using TMPro;

/// <summary>
/// 좌/우 분기 미니게임 전용 UI. 진행도(3/5)는 ObjectiveUI Count 모드가 담당한다.
///
/// [Inspector 연결]
///   challenge    : SideSplitChallenge
///   mainText     : 안내 문구 / 성공·실패 / Clear!를 교체해 표시하는 TMP
///   timerText    : 라운드 진행 중에만 표시되는 타이머 TMP
///
/// [문구 템플릿 — String Table 참조, OXQuizManager.OXQuestion과 동일 원칙(Inspector에서 문자열
/// 직접 입력이 아니라 Table+Entry 연결)]
///   promptNoColor    : {0}=왼쪽 인원, {1}=오른쪽 인원
///   promptColorLeft  : {0}=왼쪽 인원, {1}=오른쪽 인원, {2}=필수 색상명 (왼쪽에 색 조건)
///   promptColorRight : {0}=왼쪽 인원, {1}=오른쪽 인원, {2}=필수 색상명 (오른쪽에 색 조건)
///   colorName*       : PlayerColorType.Blue/Purple/Green/Yellow 각각의 로컬라이즈된 색상명
///
/// [표시 흐름]
///   안내 문구 표시
///   → 타이머 카운트다운 (roundTimeLimit 동안)
///   → 타이머 숨김 + 성공/실패 텍스트 (resultDisplayDuration 초)
///   → 다음 라운드 안내 or Clear! → 패널 숨김
///
/// [주의] 씬 로드 시 이 오브젝트는 활성 상태여야 Awake에서 리스너를 등록할 수 있다.
///        Awake 끝에서 SetActive(false)로 숨긴다.
/// </summary>
public class SideSplitUI : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] SideSplitChallenge challenge;

    [Header("메인 텍스트 (안내 문구 / 성공·실패 / Clear! 공용)")]
    [SerializeField] TextMeshProUGUI mainText;

    [Header("타이머")]
    [SerializeField] TextMeshProUGUI timerText;

    [Header("안내 문구 템플릿 (String Table 엔트리 연결)")]
    [SerializeField] LocalizedString promptNoColor;
    [SerializeField] LocalizedString promptColorLeft;
    [SerializeField] LocalizedString promptColorRight;

    [Header("결과 텍스트 (String Table 엔트리 연결)")]
    [SerializeField] LocalizedString successText;
    [SerializeField] LocalizedString failText;
    [SerializeField] LocalizedString clearText;

    [Header("색상명 (String Table 엔트리 연결 — Blue/Purple/Green/Yellow만 사용)")]
    [SerializeField] LocalizedString colorNameBlue;
    [SerializeField] LocalizedString colorNamePurple;
    [SerializeField] LocalizedString colorNameGreen;
    [SerializeField] LocalizedString colorNameYellow;

    [Header("표시 시간(초)")]
    [SerializeField] float resultDisplayDuration = 1.5f;
    [SerializeField] float clearDisplayDuration   = 2f;

    [Header("텍스트 색상")]
    [SerializeField] Color promptColor  = Color.white;
    [SerializeField] Color successColor = new Color(0.2f, 0.9f, 0.3f, 1f);
    [SerializeField] Color failColor    = new Color(0.95f, 0.2f, 0.2f, 1f);
    [SerializeField] Color clearColor   = Color.white;

    Coroutine _sequence;

    // ── Unity 라이프사이클 ─────────────────────────────────────────

    void Awake()
    {
        // 리스너를 먼저 등록한 뒤 숨김.
        // Start()에 두면 오브젝트 비활성 → Start 지연 → 첫 OnRoundReady 수신 실패.
        RegisterListeners();
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
        challenge.OnAllCleared.AddListener(ShowClear);
    }

    void UnregisterListeners()
    {
        if (challenge == null) return;

        challenge.OnRoundReady.RemoveListener(ShowRound);
        challenge.OnTimerTick.RemoveListener(SetTimer);
        challenge.OnRoundSuccess.RemoveListener(ShowSuccess);
        challenge.OnRoundFailed.RemoveListener(ShowFail);
        challenge.OnAllCleared.RemoveListener(ShowClear);
    }

    // ── 수신 메서드 ───────────────────────────────────────────────

    /// <summary>SideSplitChallenge.OnRoundReady</summary>
    void ShowRound(SideSplitRoundInfo info)
    {
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

    /// <summary>SideSplitChallenge.OnAllCleared</summary>
    void ShowClear()
    {
        StopSequence();
        _sequence = StartCoroutine(ClearSequence());
    }

    // ── 내부 시퀀스 ───────────────────────────────────────────────

    IEnumerator ResultSequence(bool success)
    {
        SetTimerVisible(false);

        LocalizedString text = success ? successText : failText;
        SetMain(text.GetLocalizedString(), success ? successColor : failColor);
        yield return new WaitForSeconds(resultDisplayDuration);

        _sequence = null;
        // 매니저의 resolveDelay가 끝나면 OnRoundReady → ShowRound 자동 호출 (없으면 OnAllCleared)
    }

    IEnumerator ClearSequence()
    {
        SetTimerVisible(false);
        SetMain(clearText.GetLocalizedString(), clearColor);
        yield return new WaitForSeconds(clearDisplayDuration);

        gameObject.SetActive(false);
        _sequence = null;
    }

    // ── 내부 유틸 ─────────────────────────────────────────────────

    string BuildPromptText(SideSplitRoundInfo info)
    {
        if (!info.hasColorRequirement)
        {
            promptNoColor.Arguments = new object[] { info.leftCount, info.rightCount };
            return promptNoColor.GetLocalizedString();
        }

        LocalizedString template = info.colorOnLeft ? promptColorLeft : promptColorRight;
        template.Arguments = new object[] { info.leftCount, info.rightCount, GetColorName(info.requiredColor) };
        return template.GetLocalizedString();
    }

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
