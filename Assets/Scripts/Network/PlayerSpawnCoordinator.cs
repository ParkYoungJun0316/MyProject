using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 플레이어 스폰 완료 신호를 씬 내 모든 구독자에게 1회 전달하는 코디네이터.
/// M.Stage1 · T.Stage1 씬 내 빈 GameObject에 NetworkObject + 이 컴포넌트를 추가.
///
/// [흐름 — 온라인]
/// LoadEventCompleted → PlayerSpawnManager.SpawnAllPlayers()
///   → NotifyPlayersReady() → Host OnPlayersReady 발행
///   → BroadcastPlayersReadyClientRpc() → Client OnPlayersReady 발행
///
/// [흐름 — 오프라인]
/// PlayerSpawnManager.SpawnOfflinePlayers()
///   → NotifyPlayersReady() → OnPlayersReady 로컬 발행
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
/// </summary>
public class PlayerSpawnCoordinator : NetworkBehaviour
{
    public static PlayerSpawnCoordinator Instance { get; private set; }

    /// <summary>
    /// Host/Client 모두 수신. 이 이벤트 발행 시점에는
    /// FindObjectsByType&lt;Player&gt;() 로 전원 네트워크 플레이어 조회 보장.
    /// static event — OnDestroy()에서 반드시 -= 로 구독 해제할 것.
    /// </summary>
    public static event System.Action OnPlayersReady;

    /// <summary>현재 씬에서 NotifyPlayersReady()가 이미 발행됐으면 true.</summary>
    public static bool IsReady { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkDespawn()
    {
        IsReady = false;
        if (Instance == this) Instance = null;
    }

    void OnDestroy()
    {
        IsReady = false;
        if (Instance == this) Instance = null;
    }

    // ── 발행 ─────────────────────────────────────────────────────

    /// <summary>
    /// Host: SpawnAllPlayers() 완료 후 PlayerSpawnManager가 호출.
    /// 오프라인: SpawnOfflinePlayers() 완료 후 호출.
    /// </summary>
    public void NotifyPlayersReady()
    {
        // 오프라인: NGO 없이 로컬에서만 발행
        if (LobbyContext.IsOffline)
        {
            IsReady = true;
            OnPlayersReady?.Invoke();
            return;
        }

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
