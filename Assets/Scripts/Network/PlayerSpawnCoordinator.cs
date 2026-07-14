using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 플레이어 스폰 완료 신호를 씬 내 모든 구독자에게 1회 전달하는 코디네이터이자,
/// clientId → 색 매핑의 <b>단일 진실 공급원(Single Source of Truth)</b>.
/// M.Stage1 · T.Stage1 씬 내 빈 GameObject에 NetworkObject + 이 컴포넌트를 추가.
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
/// NetworkSessionData.ClientColors(static Dictionary)는 로비→스테이지 전환 시
/// 딱 1번 쓰이는 "브릿지 입력값"일 뿐이다(LobbyNetworkManager.StartGameServerRpc가
/// 씬 로드 직전 서버에서만 채움). 실제 런타임 조회는 전부 이 클래스의 NetworkList를
/// 거쳐야 한다 — NetworkList는 이 오브젝트가 스폰될 때 초기값이 함께 복제되므로
/// OnNetworkSpawn 이후 어디서 읽어도(서버든 클라이언트든) 레이스가 없다.
/// 서버 코드(CheerService, PlayerSpawnManager 등)도 예외 없이 TryGetColor/GetAllEntries를
/// 통해서만 색을 조회한다 — "어느 게 진짜 소스인지" 코드마다 다르게 보이는 문제를 없앤다.
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

    void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        // 서버(Host): NetworkSessionData.ClientColors(항상 신뢰 가능한 서버 측 원본)를
        // NetworkList에 옮겨 담아 전 클라이언트에 스폰과 동시에 복제한다.
        if (IsServer) PopulateClientColorsFromSession();
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

    void PopulateClientColorsFromSession()
    {
        _clientColors.Clear();
        foreach (var kv in NetworkSessionData.ClientColors)
            _clientColors.Add(new ClientColorEntry { ClientId = kv.Key, Color = kv.Value });
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
    /// 씬 오브젝트들이 OnPlayersReady 구독 후 IsReady를 확인했을 때 오래된 값을 읽지 않도록 초기화.
    /// Instance 없이도 동작하도록 static.
    /// </summary>
    public static void ResetReady() => IsReady = false;

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
