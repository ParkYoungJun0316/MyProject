using Unity.Netcode;
using UnityEngine;
using TMPro;

/// <summary>
/// Timer_Panel에 붙이는 스크립트.
/// TimerMode.PlayTime  : 게임 시작부터 실제 경과 시간 표시.
///                       NetworkSessionData.SessionStartServerTime(Host가 게임 시작 시 기록,
///                       전 클라이언트에 배포) 기준으로 ServerTime 경과를 계산 — Host/Client가
///                       항상 같은 값을 보여준다. 로컬 Time.deltaTime 누적이 아니므로
///                       씬 재로드·전환·프레임레이트 차이에 영향받지 않음.
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
            if (timerText != null) timerText.color = normalColor;
            UpdateDisplay(GetPlayTime(), isSurvival: false);
        }
    }

    void Update()
    {
        if (mode != TimerMode.PlayTime) return;
        UpdateDisplay(GetPlayTime(), isSurvival: false);
    }

    /// <summary>
    /// ServerTime - SessionStartServerTime 기준 경과 시간. 세션이 아직 시작 전(-1)이거나
    /// NetworkManager가 없으면(온라인 시작 전 화면 등) 0 반환.
    /// </summary>
    float GetPlayTime()
    {
        var nm = NetworkManager.Singleton;
        double start = NetworkSessionData.SessionStartServerTime;
        if (nm == null || !nm.IsListening || start < 0) return 0f;
        return Mathf.Max(0f, (float)(nm.ServerTime.Time - start));
    }

    /// <summary>타이틀 복귀 등 새 게임 시작 시 세션 시작 시각 초기화.</summary>
    public static void ResetTimer() => NetworkSessionData.SessionStartServerTime = -1.0;

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
