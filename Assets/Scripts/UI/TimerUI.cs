using UnityEngine;
using TMPro;

/// <summary>
/// Timer_Panel에 붙이는 스크립트.
/// TimerMode.PlayTime  : 게임 시작부터 실제 경과 시간 표시.
///                       씬 재로드·전환에 무관하게 누적. Disconnect Pause(timeScale=0)에만 멈춤.
/// TimerMode.Survival  : SurviveTimeObjective 남은 시간 표시 (Objective 연결 필요)
///
/// [리셋 타이밍]
/// GameSession.ResetSession() 호출 시 (타이틀 복귀 등) ResetTimer()로 초기화.
/// </summary>
public class TimerUI : MonoBehaviour
{
    public enum TimerMode
    {
        PlayTime,
        Survival,
    }

    [Header("모드 선택")]
    [SerializeField] TimerMode mode = TimerMode.PlayTime;

    [Header("연결 (Survival 모드에서만 필요)")]
    [SerializeField] SurviveTimeObjective objective;

    [Header("UI 참조")]
    [SerializeField] TextMeshProUGUI timerText;

    [Header("경고 색상 (Survival 모드에서만 적용)")]
    [SerializeField] Color normalColor      = Color.white;
    [SerializeField] Color warningColor     = Color.red;
    [Tooltip("이 초 이하로 남으면 경고 색으로 변경")]
    [SerializeField] float warningThreshold = 30f;

    // 씬 재로드·전환에도 유지되는 누적 플레이 시간
    static float s_accumulatedTime = 0f;

    float _playTime;

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
            // 이전 씬에서 누적된 시간 이어받기
            _playTime = s_accumulatedTime;
            if (timerText != null) timerText.color = normalColor;
            UpdateDisplay(_playTime, isSurvival: false);
        }
    }

    void Update()
    {
        if (mode != TimerMode.PlayTime) return;

        // Time.deltaTime: timeScale=0(Disconnect Pause)이면 자동으로 0 → 멈춤
        // 씬 전환·Phase 전환·컷씬 등 다른 요소의 영향 없음
        _playTime += Time.deltaTime;

        s_accumulatedTime = _playTime;
        UpdateDisplay(_playTime, isSurvival: false);
    }

    /// <summary>타이틀 복귀 등 새 게임 시작 시 누적 타이머 초기화.</summary>
    public static void ResetTimer() => s_accumulatedTime = 0f;

    void OnSurvivalTimeChanged(float remaining) => UpdateDisplay(remaining, isSurvival: true);

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
