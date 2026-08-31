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
/// LoadEventCompleted → PlayerSpawnManager.SpawnNetworkPlayers()
///   → NotifyPlayersReady() → Host OnPlayersReady 발행
///   → BroadcastPlayersReadyClientRpc() → Client OnPlayersReady 발행
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
    ///
    /// Tutorial 순차 합류(§6B.2)에서는 이 신호가 Host 1인 시점에 1회만 나간다.
    /// 이후 명단이 늘거나 줄어드는 건 <see cref="OnRosterChanged"/>를 구독할 것.
    /// OnPlayersReady를 재발행하지 않는다(§11.4).
    /// </summary>
    public static event System.Action OnPlayersReady;

    /// <summary>
    /// 이 머신에서 플레이어 NetworkObject가 스폰/Despawn된 직후.
    /// RPC 없음 — 스폰 메시지가 도착한 로컬 시점이 신호다.
    /// TeamStatusUI 등 "전원 목록" Consumer 전용. 카메라/HP 자기 슬롯은 OnPlayersReady만으로 충분.
    /// </summary>
    public static event System.Action OnRosterChanged;

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

    /// <summary>
    /// Host: 이미 스폰된 코디네이터에 접속자 1명의 색을 개별 추가.
    /// Tutorial 사전 게이트 구간(NetworkDesign.md §6B.2)처럼 인원이 한 명씩 순차 합류하는
    /// 상황 전용 — PrepareColors(전체 dict, Spawn 직전 1회)와 달리 이미 Spawn된 뒤에도 호출 가능.
    /// 같은 clientId가 이미 있으면 갱신(중복 접속 콜백 방어).
    /// </summary>
    public void AddColorEntry(ulong clientId, PlayerColorType color)
    {
        if (!IsServer) return;
        for (int i = 0; i < _clientColors.Count; i++)
        {
            if (_clientColors[i].ClientId != clientId) continue;
            _clientColors[i] = new ClientColorEntry { ClientId = clientId, Color = color };
            return;
        }
        _clientColors.Add(new ClientColorEntry { ClientId = clientId, Color = color });
    }

    /// <summary>
    /// Host: 접속자 1명의 색 엔트리 제거. Tutorial 사전 게이트 구간 이탈(§6B.4) — 슬롯만 비움.
    /// </summary>
    public void RemoveColorEntry(ulong clientId)
    {
        if (!IsServer) return;
        for (int i = _clientColors.Count - 1; i >= 0; i--)
        {
            if (_clientColors[i].ClientId != clientId) continue;
            _clientColors.RemoveAt(i);
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

    /// <summary>
    /// Host: 이미 전체 Ready(NotifyPlayersReady)가 확정된 뒤 새로 합류한 Client 1명에게
    /// Ready 신호를 개별 재전송한다. Tutorial 사전 게이트 구간(§6B.2)처럼 인원이 한 명씩
    /// 순차 합류하면 최초 접속자(Host) 스폰 시점에 이미 NotifyPlayersReady()가 발행돼버려,
    /// 그 뒤 합류하는 Client는 당시의 BroadcastPlayersReadyClientRpc를 놓친다
    /// (NGO ClientRpc는 호출 시점에 접속 중인 대상에게만 가고 이후 합류자에게 재전달되지 않음).
    /// 이 메서드가 그 개별 보충만 한다. 아직 아무도 Ready 전이면(IsReady=false) 호출해도
    /// 아무 일 없음 — 그 경우는 이후 NotifyPlayersReady()의 전체 브로드캐스트가 정상 전달한다.
    /// </summary>
    public void CatchUpReadyFor(ulong clientId)
    {
        if (!IsServer || !IsReady) return;

        CatchUpReadyClientRpc(new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        });
    }

    [ClientRpc]
    void CatchUpReadyClientRpc(ClientRpcParams rpcParams = default)
    {
        if (IsServer || IsReady) return; // Host 자신 제외 + 중복 Invoke 방지
        IsReady = true;
        OnPlayersReady?.Invoke();
    }

    /// <summary>
    /// 각 머신 로컬에서 플레이어 스폰/Despawn 직후 호출.
    /// Tutorial 순차 합류 때 이미 Ready인 Host/기존 Client의 명단 UI를 다시 그리게 한다.
    /// </summary>
    public static void RaiseRosterChanged()
    {
        OnRosterChanged?.Invoke();
    }
}
