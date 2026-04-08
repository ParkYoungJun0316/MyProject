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

    float _playTime;

    void Start()
    {
        _playTime = 0f;

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
            if (timerText != null)
                timerText.color = normalColor;
            UpdateDisplay(0f, isSurvival: false);
        }
    }

    void Update()
    {
        if (mode != TimerMode.PlayTime) return;

        _playTime += Time.deltaTime;
        UpdateDisplay(_playTime, isSurvival: false);
    }

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
