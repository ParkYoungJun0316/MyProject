using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// ColorTileChallenge 라운드 진행 중 남은 시간 + 성공/실패 결과를 표시하는 전용 UI.
///
/// SideSplitUI와 동일한 방식으로 challenge.OnTimerTick(ChallengeStepStartServerTime을
/// ServerTime 기준으로 역산 — Host/Client 공통값)을 코드로 직접 구독한다(Inspector UnityEvent
/// 연결이 아님). StageStartGate 전용 StartCountdownUI/StartCountdown_Panel과는 완전히 별개 —
/// 스테이지 시작 대기 카운트다운(_countdownStartServerTime)과 인게임 챌린지 타이머
/// (ChallengeStepStartServerTime)는 서로 다른 NV라 같은 오브젝트/슬롯을 공유하지 않는다.
///
/// [Inspector 연결]
///   challenge   : 감시할 ColorTileChallenge (T.Boss처럼 인스턴스가 여러 개면 UI도 인스턴스별로 배치)
///   timerText   : 남은 초 / 결과 문구를 함께 표시할 TMP (SideSplitUI.timerText와 달리 하나로 공용)
///   successText : Success 문구 (String Table 엔트리 연결 — SideSplit/Result.Success 재사용 권장)
///   failText    : Fail 문구 (String Table 엔트리 연결 — SideSplit/Result.Fail 재사용 권장)
///
/// [표시 흐름 — SideSplitUI.ResultSequence와 동일 원칙]
///   OnChallengeStarted → 표시 시작, 이전 결과 시퀀스 취소
///   OnTimerTick(remaining) → 올림 정수로 갱신 (0 이하면 빈 문자열 — 패널 자체는 숨기지 않음.
///     결과 문구가 도착하기 전에 패널이 먼저 꺼지는 것을 방지)
///   OnSuccess / OnFail → 결과 문구 + 색상으로 교체 → resultDisplayDuration 대기 → 패널 숨김
/// </summary>
public class ColorTileTimerUI : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] ColorTileChallenge challenge;

    [Header("UI 참조")]
    [SerializeField] TextMeshProUGUI timerText;

    [Header("결과 텍스트 (String Table 엔트리 연결 — SideSplit/Result.Success, Result.Fail 재사용 권장)")]
    [SerializeField] LocalizedString successText;
    [SerializeField] LocalizedString failText;

    [Header("결과 표시 시간(초)")]
    [SerializeField] float resultDisplayDuration = 1.5f;

    [Header("텍스트 색상")]
    [SerializeField] Color normalColor  = Color.white;
    [SerializeField] Color successColor = new Color(0.2f, 0.9f, 0.3f, 1f);
    [SerializeField] Color failColor    = new Color(0.95f, 0.2f, 0.2f, 1f);

    Coroutine _sequence;

    void Awake()
    {
        // 리스너를 먼저 등록한 뒤 숨김 — SideSplitUI.Awake와 동일 원칙.
        // Start()에 두면 오브젝트가 비활성 상태라 Start 자체가 지연되어 첫 이벤트를 놓칠 수 있다.
        RegisterListeners();
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        UnregisterListeners();
    }

    void RegisterListeners()
    {
        if (challenge == null) return;

        challenge.OnChallengeStarted.AddListener(Show);
        challenge.OnTimerTick.AddListener(SetRemaining);
        challenge.OnSuccess.AddListener(ShowSuccess);
        challenge.OnFail.AddListener(ShowFail);
        challenge.OnQuotaChanged.AddListener(SetQuota);
    }

    void UnregisterListeners()
    {
        if (challenge == null) return;

        challenge.OnChallengeStarted.RemoveListener(Show);
        challenge.OnTimerTick.RemoveListener(SetRemaining);
        challenge.OnSuccess.RemoveListener(ShowSuccess);
        challenge.OnFail.RemoveListener(ShowFail);
        challenge.OnQuotaChanged.RemoveListener(SetQuota);
    }

    /// <summary>ColorTileChallenge.OnChallengeStarted</summary>
    void Show()
    {
        StopSequence();
        gameObject.SetActive(true);
        if (timerText != null) timerText.color = normalColor;
        SetQuota();
    }

    /// <summary>
    /// ColorTileChallenge.OnTimerTick(float)에 연결. 남은 시간을 올림 정수로 표시.
    /// 0 이하가 돼도 패널을 숨기지 않는다(SideSplitUI.SetTimer와 동일) — 여기서 숨겨버리면
    /// 타임아웃 시 뒤이어 오는 OnFail 결과 문구를 보여줄 틈이 없어진다.
    /// </summary>
    void SetRemaining(float remaining)
    {
        if (timerText == null) return;
        int display = Mathf.CeilToInt(remaining);
        timerText.text = display > 0 ? display.ToString() : "";
    }

    void SetQuota()
    {
        if (timerText == null || challenge == null || !challenge.UsesQuotaScoring) return;
        timerText.text = $"{challenge.QuotaProgress}/{challenge.QuotaRequired}";
        timerText.color = normalColor;
    }

    /// <summary>ColorTileChallenge.OnSuccess</summary>
    void ShowSuccess()
    {
        StopSequence();
        _sequence = StartCoroutine(ResultSequence(true));
    }

    /// <summary>ColorTileChallenge.OnFail</summary>
    void ShowFail()
    {
        StopSequence();
        _sequence = StartCoroutine(ResultSequence(false));
    }

    IEnumerator ResultSequence(bool success)
    {
        if (timerText != null)
        {
            LocalizedString text = success ? successText : failText;
            timerText.text  = text.GetLocalizedString();
            timerText.color = success ? successColor : failColor;
        }

        yield return new WaitForSeconds(resultDisplayDuration);

        gameObject.SetActive(false);
        _sequence = null;
    }

    void StopSequence()
    {
        if (_sequence == null) return;
        StopCoroutine(_sequence);
        _sequence = null;
    }
}
