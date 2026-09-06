/// <summary>
/// 팀 응원 성공 시 「입이 한 일」을 되돌리는 대상.
/// CheerService는 씬에 하나 등록하고, 투표 성공 시 되돌림 명령만 브로드캐스트한다 (새 RPC 없음).
/// M1·M3·M.Boss = MouthController. M2 = SalivaHazard. M4 = TongueController.
/// T1·T3 = EsophagusSqueeze. T2·T4 = EsophagusFog — 이 둘도 SalivaHazard와 동일한
/// Idle→Warning→Attack→Hold→Recover 창 구조(랜덤 주기)를 쓴다. CoopStageAudit.T.md §3·§5 참고.
/// Idle에서는 IsAvailable=false. Warning 시작부터 Revert 성공까지 true.
///
/// [되돌림 명령이 Host 권한인 이유 — 2026-09-05]
/// 함정 머신은 머신마다 로컬로 도는데, 예전 Revert()는 인자 없이 "지금 로컬 창을 닫아라"였다.
/// 그래서 창이 아직 안 열린 머신(씬 로드 시각 차이 등)에서는 명령이 조용히 버려졌고,
/// 그 사람만 암전/침이 유지된 채 위상이 한 사이클 어긋났다. 이제 Host가 세대 번호와 다음 창
/// 재개 ServerTime을 함께 실어 보내고, 창 밖이던 머신도 그 예약을 그대로 받아 위상을 맞춘다.
/// </summary>
public interface ITeamCheerRevert
{
    bool IsAvailable { get; }

    /// <summary>
    /// Host 전용 — 이번 되돌림의 세대 번호와 다음 창 재개 ServerTime을 계산한다.
    /// 자기 상태는 바꾸지 않는다(실제 적용은 브로드캐스트를 받은 Revert에서 전 머신이 동시에).
    /// </summary>
    void BuildRevertOrder(out int generation, out double resumeAtServerTime);

    /// <summary>
    /// 전 머신 공통 — Host가 정한 명령대로 되돌린다.
    /// 이미 처리한 세대(generation ≤ 현재)는 무시하고, 아직 창을 열지 않은 머신은
    /// 그 창을 열지 않고 건너뛰어 위상을 맞춘다.
    /// </summary>
    void Revert(int generation, double resumeAtServerTime);
}
