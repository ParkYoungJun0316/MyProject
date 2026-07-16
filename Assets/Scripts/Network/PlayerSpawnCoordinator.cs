using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 플레이어 스폰 완료 신호를 씬 내 모든 구독자에게 1회 전달하는 코디네이터이자,
/// clientId → 색 매핑의 <b>단일 진실 공급원(Single Source of Truth)</b>.
///
/// [배치] 씬에 직접 두지 않는다.
/// LobbyNetworkManager.StartGameServerRpc()에서 coordinatorPrefab을 destroyWithScene:false로 스폰.
/// 이후 씬 리로드(사망)에도 같은 오브젝트가 유지되어 _clientColors NetworkList를 재사용한다.
///
/// [흐름 — 온라인]
/// LoadEventCompleted → PlayerSpawnManager.SpawnAllPlayers()
///   → NotifyPlayersReady() → Host OnPlayersReady 발행
///   → BroadcastPlayersReadyClientRpc() → Client OnPlayersReady 발행
///
///
/// [구독 방법 (모든 구독자 공통)]
/// void Start() {
///     PlayerSpawnCoordinator.OnPlayersReady += MyInit;
///     if (PlayerSpawnCoordinator.IsReady) MyInit(); // 늦은 구독 대비
/// }
/// void OnDestroy() { PlayerSpawnCoordinator.OnPlayersReady -= MyInit; }
///
/// [씬 재로드]
/// NetworkDespawn / OnDestroy 시 IsReady = false 로 자동 초기화됨.
/// 씬이 리로드되면 새 Coordinator 인스턴스가 Awake에서 Instance를 덮어씀.
///
/// [clientId → 색 데이터 흐름 — 단일 소스 원칙]
/// LobbyNetworkManager.StartGameServerRpc()가 PrepareColors(dict)로 초기 색 매핑을 예약하면
/// OnNetworkSpawn에서 NetworkList에 기록되어 스폰 메시지와 함께 전 클라이언트에 복제된다.
/// 이후 런타임 조회는 전부 이 클래스의 NetworkList를 거쳐야 한다 — OnNetworkSpawn 이후
/// 어디서 읽어도(서버든 클라이언트든) 레이스가 없다.
/// 서버 코드(CheerService, PlayerSpawnManager 등)도 예외 없이 TryGetColor/GetAllEntries를
/// 통해서만 색을 조회한다.
/// </summary>
public class PlayerSpawnCoordinator : NetworkBehaviour
{
    public static PlayerSpawnCoordinator Instance { get; private set; }

    /// <summary>
    /// Host/Client 모두 수신. 이 이벤트 발행 시점에는
    /// FindObjectsByType&lt;Player&gt;() 로 전원 네트워크 플레이어 조회 보장.
    /// 색 인덱스(playerColorType)는 NV 전달 타이밍에 따라 아직 기본값(Blue)일 수 있음.
    /// 구독자는 TryGetColor / IsColorInSession으로 색을 확정할 것(레이스 없음).
    /// static event — OnDestroy()에서 반드시 -= 로 구독 해제할 것.
    /// </summary>
    public static event System.Action OnPlayersReady;

    /// <summary>현재 씬에서 NotifyPlayersReady()가 이미 발행됐으면 true.</summary>
    public static bool IsReady { get; private set; }

    /// <summary>clientId → 색 매핑 NetworkList element. 서버만 쓰고 전원이 읽음.</summary>
    struct ClientColorEntry : INetworkSerializable, IEquatable<ClientColorEntry>
    {
        public ulong ClientId;
        public PlayerColorType Color;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Color);
        }

        public bool Equals(ClientColorEntry other) =>
            ClientId == other.ClientId && Color == other.Color;
    }

    // NetworkList는 Awake 전에 초기화해야 함 (필드 초기화 or Awake)
    readonly NetworkList<ClientColorEntry> _clientColors = new();

    // Spawn() 전에 PrepareColors()로 설정 → OnNetworkSpawn에서 NetworkList에 기록
    System.Collections.Generic.Dictionary<ulong, PlayerColorType> _initColors;

    void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Host: Spawn() 직전에 호출해 초기 색 매핑을 예약.
    /// OnNetworkSpawn에서 NetworkList에 기록되어 전 클라이언트에 스폰 메시지와 함께 복제된다.
    /// </summary>
    public void PrepareColors(System.Collections.Generic.Dictionary<ulong, PlayerColorType> dict)
    {
        _initColors = dict;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer && _initColors != null)
        {
            _clientColors.Clear();
            foreach (var kv in _initColors)
                _clientColors.Add(new ClientColorEntry { ClientId = kv.Key, Color = kv.Value });
            _initColors = null;
        }
    }

    public override void OnNetworkDespawn()
    {
        IsReady = false;
        if (Instance == this) Instance = null;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        IsReady = false;
        if (Instance == this) Instance = null;
    }

    // ── 공개 조회 API (레이스 없음 — OnNetworkSpawn 이후 항상 최신값) ──

    /// <summary>clientId에 매칭된 색을 조회. 온라인 모드 전용.</summary>
    public static bool TryGetColor(ulong clientId, out PlayerColorType color)
    {
        color = default;
        if (Instance == null) return false;

        foreach (var entry in Instance._clientColors)
        {
            if (entry.ClientId != clientId) continue;
            color = entry.Color;
            return true;
        }
        return false;
    }

    /// <summary>이번 세션에 해당 색을 쓰는 클라이언트가 있는지 확인. 온라인 모드 전용.</summary>
    public static bool IsColorInSession(PlayerColorType color)
    {
        if (Instance == null) return false;

        foreach (var entry in Instance._clientColors)
            if (entry.Color == color) return true;
        return false;
    }

    /// <summary>
    /// clientId → 색 전체 목록. PlayerSpawnManager처럼 전원을 순회해야 하는 서버 로직용.
    /// 서버·클라이언트 모두 안전(레이스 없음).
    /// </summary>
    public static IEnumerable<(ulong ClientId, PlayerColorType Color)> GetAllEntries()
    {
        if (Instance == null) yield break;
        foreach (var entry in Instance._clientColors)
            yield return (entry.ClientId, entry.Color);
    }

    /// <summary>이번 세션에서 실제로 쓰이는 색만 중복 없이 반환 (GameSession 활성색 파생용).</summary>
    public static PlayerColorType[] GetActiveColors()
    {
        if (Instance == null) return Array.Empty<PlayerColorType>();

        var set = new HashSet<PlayerColorType>();
        foreach (var entry in Instance._clientColors)
            set.Add(entry.Color);

        var result = new PlayerColorType[set.Count];
        set.CopyTo(result);
        return result;
    }

    /// <summary>현재 등록된 clientId → 색 매핑 개수. 0이면 아직 채워지지 않았거나 빈 세션.</summary>
    public static int EntryCount => Instance?._clientColors.Count ?? 0;

    // ── 발행 ─────────────────────────────────────────────────────

    /// <summary>
    /// PlayerSpawnManager가 새 씬 스폰 직전에 호출.
    /// Host/Client 모두 IsReady = false 로 초기화.
    /// </summary>
    public static void ResetReady()
    {
        IsReady = false;
        Instance?.BroadcastResetReadyClientRpc();
    }

    [ClientRpc]
    void BroadcastResetReadyClientRpc()
    {
        if (IsServer) return;
        IsReady = false;
    }

    /// <summary>
    /// Host: SpawnAllPlayers() 완료 후 PlayerSpawnManager가 호출.
    /// </summary>
    public void NotifyPlayersReady()
    {
        if (!IsServer) return;

        IsReady = true;
        OnPlayersReady?.Invoke();           // Host 로컬
        BroadcastPlayersReadyClientRpc();   // Client 전파
    }

    [ClientRpc]
    void BroadcastPlayersReadyClientRpc()
    {
        if (IsServer) return;   // Host는 이미 위에서 발행
        IsReady = true;
        OnPlayersReady?.Invoke();
    }
}
