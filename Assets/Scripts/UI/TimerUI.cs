using Unity.Netcode;
using UnityEngine;
using TMPro;

/// <summary>
/// Timer_Panel에 붙이는 스크립트.
/// TimerMode.PlayTime  : 씬 시작부터 경과 시간 표시 (항상 동작)
/// TimerMode.Survival  : SurviveTimeObjective 남은 시간 표시 (Objective 연결 필요)
/// </summary>
public class TimerUI : MonoBehaviour
{
    public enum TimerMode
    {
        PlayTime,   // 경과 시간 (플레이 타임)
        Survival,   // 남은 시간 (생존 목표)
    }

    [Header("모드 선택")]
    [SerializeField] TimerMode mode = TimerMode.PlayTime;

    [Header("연결 (Survival 모드에서만 필요)")]
    [SerializeField] SurviveTimeObjective objective;

    [Header("UI 참조")]
    [SerializeField] TextMeshProUGUI timerText;

    [Header("경고 색상 (Survival 모드에서만 적용)")]
    [SerializeField] Color normalColor       = Color.white;
    [SerializeField] Color warningColor      = Color.red;
    [Tooltip("이 초 이하로 남으면 경고 색으로 변경")]
    [SerializeField] float warningThreshold  = 30f;

    // 씬 재로드(사망 리로드)에도 초기화되지 않도록 static 보관
    static float s_accumulatedTime = 0f;

    float _playTime;
    // 이번 씬 시작 시점의 누적 시간 (이전 스테이지 합산).
    // ServerTime 기반 경과 시간을 더하는 기준값.
    float _accumulatedOffset;

    void Start()
    {
        if (mode == TimerMode.Survival)
        {
            if (objective == null)
            {
                gameObject.SetActive(false);
                return;
            }
            objective.OnTimeChanged.AddListener(OnSurvivalTimeChanged);
            UpdateDisplay(objective.Remaining, isSurvival: true);
        }
        else
        {
            // PlayTime: 이전 누적 시간에서 이어서 시작
            _accumulatedOffset = s_accumulatedTime;
            _playTime          = _accumulatedOffset;
            if (timerText != null)
                timerText.color = normalColor;
            UpdateDisplay(_playTime, isSurvival: false);
        }
    }

    void Update()
    {
        if (mode != TimerMode.PlayTime) return;

        var nm  = NetworkManager.Singleton;
        var sns = StageNetworkState.Instance;

        if (nm != null && nm.IsListening && sns != null && sns.StageStartServerTime > 0)
        {
            // 네트워크 모드: Host ServerTime 기준으로 Host/Client 모두 동일한 시간을 표시.
            // _accumulatedOffset은 이번 씬 Start() 시점의 s_accumulatedTime이므로
            // 이전 스테이지 시간이 그대로 유지된다.
            float stageElapsed = (float)(nm.ServerTime.Time - sns.StageStartServerTime);
            _playTime = _accumulatedOffset + Mathf.Max(0f, stageElapsed);
        }
        else
        {
            // 오프라인 또는 스테이지 시작 전 대기 구간: 로컬 시간으로 카운트
            _playTime += Time.deltaTime;
        }

        s_accumulatedTime = _playTime;
        UpdateDisplay(_playTime, isSurvival: false);
    }

    /// <summary>새 게임 시작 시 누적 타이머 리셋. GameSession.ResetSession() 등에서 호출.</summary>
    public static void ResetTimer() => s_accumulatedTime = 0f;

    void OnSurvivalTimeChanged(float remaining)
    {
        UpdateDisplay(remaining, isSurvival: true);
    }

    void UpdateDisplay(float seconds, bool isSurvival)
    {
        if (timerText == null) return;

        int min = (int)(seconds / 60f);
        int sec = (int)(seconds % 60f);
        timerText.text = $"{min:00}:{sec:00}";

        if (isSurvival)
            timerText.color = seconds <= warningThreshold ? warningColor : normalColor;
    }

    void OnDestroy()
    {
        if (objective != null)
            objective.OnTimeChanged.RemoveListener(OnSurvivalTimeChanged);
    }
}
