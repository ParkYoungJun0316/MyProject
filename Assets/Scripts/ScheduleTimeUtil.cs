using Unity.Netcode;

/// <summary>
/// 씬 리로드 후 스케줄 이벤트가 과거 시간에 즉시 발동하는 것을 방지하는 타이밍 헬퍼.
///
/// [문제]
/// 씬 리로드 시 ServerTime이 계속 흐르는 상태에서 StageStartServerTime 기준으로
/// targetTime = start + t 를 계산하면, 로드 시점이 t초 이상 지났을 경우
/// targetTime이 이미 과거가 되어 이벤트가 즉시 발동함.
///
/// [사용법]
/// foreach (float t in schedule)
/// {
///     float targetTime = startTime + t;
///     if (ScheduleTimeUtil.IsPastEvent(targetTime, nm)) continue;
///     // ... wait + trigger
/// }
/// </summary>
public static class ScheduleTimeUtil
{
    /// <summary>
    /// 온라인 환경에서 targetTime이 현재 ServerTime보다 과거이거나 같으면 true.
    /// 오프라인(nm null / not listening)이면 항상 false — 오프라인은 Time.time 기반이므로
    /// 씬 리로드 시 ServerTime race가 발생하지 않음.
    /// </summary>
    public static bool IsPastEvent(float targetTime, NetworkManager nm)
    {
        if (nm == null || !nm.IsListening) return false;
        return (float)nm.ServerTime.Time >= targetTime;
    }
}
