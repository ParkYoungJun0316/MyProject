/// <summary>
/// 씬 전환 간 공유가 필요한 세션 데이터 보관소.
/// DontDestroyOnLoad 불필요 — 정적이므로 프로세스 생존 기간 동안 유지됨.
///
/// clientId → 색 매핑은 PlayerSpawnCoordinator(NetworkList)가 SSOT.
/// 런타임 조회는 PlayerSpawnCoordinator.TryGetColor / GetAllEntries를 사용할 것.
/// </summary>
public static class NetworkSessionData
{
    /// <summary>
    /// 세션 랜덤 시드.
    /// LobbyNetworkManager.StartGameServerRpc()에서 생성하고
    /// 모든 클라이언트에 브로드캐스트됨.
    /// 사망 리로드 시 StageNetworkState가 새 시드를 배포.
    /// </summary>
    public static int Seed { get; set; } = 0;

    /// <summary>데이터를 비움. 타이틀 복귀·새 게임 시 호출.</summary>
    public static void Clear()
    {
        Seed = 0;
    }
}
