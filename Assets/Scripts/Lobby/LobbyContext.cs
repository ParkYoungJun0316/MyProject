/// <summary>
/// 타이틀에서 로비로 넘어올 때 사용하는 모드 enum.
/// </summary>
public enum LobbyMode
{
    OnlineHost,   // 방 만들기 (Host). 솔로 1인도 이 모드.
    OnlineClient, // 방 참여 (Client)
}

/// <summary>
/// 씬 전환 시 로비 모드를 전달하는 경량 정적 컨텍스트.
/// DontDestroyOnLoad 불필요 — 씬 로드 직전에 쓰고 로비 Start()에서 읽음.
///
/// [사용법]
///   설정: LobbyContext.Mode = LobbyMode.OnlineHost;
///   읽기: if (LobbyContext.IsOnlineHost) { ... }
/// </summary>
public static class LobbyContext
{
    public static LobbyMode Mode { get; set; } = LobbyMode.OnlineHost;

    public static bool IsOnlineHost   => Mode == LobbyMode.OnlineHost;
    public static bool IsOnlineClient => Mode == LobbyMode.OnlineClient;
    public static bool IsOnline       => true;
}
