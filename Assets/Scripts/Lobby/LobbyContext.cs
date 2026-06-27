/// <summary>
/// 타이틀에서 로비로 넘어올 때 사용하는 모드 enum.
/// </summary>
public enum LobbyMode
{
    Offline,      // 솔로 플레이 — NGO 없음
    OnlineHost,   // 멀티 방 만들기 (Host)
    OnlineClient, // 멀티 방 참여 (Client)
}

/// <summary>
/// 씬 전환 시 로비 모드를 전달하는 경량 정적 컨텍스트.
/// DontDestroyOnLoad 불필요 — 씬 로드 직전에 쓰고 로비 Start()에서 읽음.
///
/// [사용법]
///   설정: LobbyContext.Mode = LobbyMode.Offline;
///   읽기: if (LobbyContext.IsOffline) { ... }
/// </summary>
public static class LobbyContext
{
    public static LobbyMode Mode { get; set; } = LobbyMode.Offline;

    public static bool IsOffline      => Mode == LobbyMode.Offline;
    public static bool IsOnlineHost   => Mode == LobbyMode.OnlineHost;
    public static bool IsOnlineClient => Mode == LobbyMode.OnlineClient;
    public static bool IsOnline       => Mode != LobbyMode.Offline;
}
