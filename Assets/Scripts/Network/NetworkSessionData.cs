using System.Collections.Generic;

/// <summary>
/// 로비 → 스테이지 씬 전환 시 clientId·색 정보를 넘기는 <b>1회성 브릿지</b>.
/// DontDestroyOnLoad 불필요 — 정적이므로 프로세스 생존 기간 동안 유지됨.
///
/// ⚠️ 여기 있는 <see cref="ClientColors"/>는 "최초 입력값"일 뿐, 런타임 조회용 소스가 아니다.
/// clientId → 색을 조회해야 하는 코드(서버·클라이언트 불문)는 전부
/// <see cref="PlayerSpawnCoordinator"/>(NetworkList, 레이스 없음)를 거쳐야 한다.
/// 이 Dictionary를 직접 참조하는 새 코드를 추가하지 말 것.
///
/// [쓰는 곳] LobbyNetworkManager.StartGameServerRpc() — 씬 전환 직전, 서버에서만 1회 저장.
/// [읽는 곳] PlayerSpawnCoordinator.OnNetworkSpawn() — M.Stage1 진입 시 서버에서만 1회 읽어
///           NetworkList로 복제. 그 이후로는 이 Dictionary를 다시 읽지 않는다.
/// </summary>
public static class NetworkSessionData
{
    /// <summary>clientId → 선택된 PlayerColorType 매핑 (로비→스테이지 1회성 입력값).</summary>
    public static readonly Dictionary<ulong, PlayerColorType> ClientColors = new();

    /// <summary>
    /// 세션 랜덤 시드. 0 = 미설정(오프라인).
    /// LobbyNetworkManager.StartGameServerRpc()에서 생성하고
    /// 모든 클라이언트에 브로드캐스트됨.
    /// 사망 리로드 시 StageNetworkState가 새 시드를 배포.
    /// </summary>
    public static int Seed { get; set; } = 0;

    /// <summary>데이터를 비움. 타이틀 복귀·새 게임 시 호출.</summary>
    public static void Clear()
    {
        ClientColors.Clear();
        Seed = 0;
    }
}
