/// <summary>
/// 팀 응원 성공 시 「입이 한 일」을 되돌리는 대상.
/// CheerService는 씬에 하나 등록하고, 투표 성공 시 Revert()만 호출한다 (새 RPC 없음).
/// M1·M3·M.Boss = MouthController. M2 = SalivaHazard. M4 = TongueController.
/// Idle에서는 IsAvailable=false. Warning 시작부터 Revert 성공까지 true.
/// </summary>
public interface ITeamCheerRevert
{
    bool IsAvailable { get; }
    void Revert();
}
