using System.Collections.Generic;

/// <summary>
/// 로비 → 스테이지 씬 전환 시 clientId·색 정보를 넘기는 정적 컨텍스트.
/// DontDestroyOnLoad 불필요 — 정적이므로 프로세스 생존 기간 동안 유지됨.
///
/// [쓰는 곳] LobbyNetworkManager.StartGameServerRpc() — 씬 전환 직전 저장.
/// [읽는 곳] PlayerSpawnManager.SpawnAllPlayers() — M.Stage1 진입 시 스폰에 사용.
/// </summary>
public static class NetworkSessionData
{
    /// <summary>clientId → 선택된 PlayerColorType 매핑.</summary>
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
