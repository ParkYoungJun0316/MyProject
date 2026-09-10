/// <summary>
/// 팀 응원 함정 Idle 간격 전역 SSOT.
/// 씬 인스펙터의 randomIntervalMin/Max보다 우선한다. 스테이지별 튜닝을 다시 열면
/// 각 함정 PickSeededInterval이 인스펙터 값을 읽도록 되돌린다.
/// </summary>
public static class TeamCheerSchedule
{
    public const float IdleMinSeconds = 55f;
    public const float IdleMaxSeconds = 80f;
}
