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

    /// <summary>데이터를 비움. 타이틀 복귀·새 게임 시 호출.</summary>
    public static void Clear() => ClientColors.Clear();
}
