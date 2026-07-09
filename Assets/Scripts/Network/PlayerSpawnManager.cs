using Unity.Netcode;
using UnityEngine;

/// <summary>
/// M.Stage1 (및 이후 스테이지) 씬에 배치해 플레이어를 동적으로 스폰.
/// NetworkBehaviour — Host에서만 실행.
///
/// [동작]
/// OnNetworkSpawn (Host) :
///   1. 씬에 미리 배치된 비네트워크 Player 인스턴스 제거
///   2. PlayerSpawnCoordinator(단일 소스, NetworkList) 기반으로 각 clientId에 맞는 색의
///      ColoredStartZone.spawnPoint 위에 Network Player Prefab 스폰
///   3. NetworkPlayerSetup.SetColorIndex() 호출해 색 동기화
///
/// [배치]
/// M.Stage1 씬 빈 GameObject → NetworkObject + PlayerSpawnManager 추가.
///
/// [Inspector]
/// - playerPrefab : Network Player Prefab (NetworkObject 포함)
/// </summary>
public class PlayerSpawnManager : NetworkBehaviour
{
    [Header("Network Player Prefab")]
    [Tooltip("NetworkObject + Player + NetworkPlayerSetup + ClientNetworkTransform(서버권한 NetworkTransform) 포함 Prefab.")]
    [SerializeField] private GameObject playerPrefab;

    // ── 초기화 ────────────────────────────────────────────────────

    void Start()
    {
        // 오프라인: NGO 없이 일반 Instantiate로 스폰
        if (LobbyContext.IsOffline)
            SpawnOfflinePlayers();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsHost) return;
        if (LobbyContext.IsOffline) return;

        RemoveScenePlayers();

        // SpawnAllPlayers()를 OnSessionOwnerLoadedScene 시점에 바로 호출하면
        // 클라이언트가 씬 로드 완료 전에 CreateObjectMessage를 받아 10초 후 타임아웃.
        // LoadEventCompleted: ALL 클라이언트의 씬 로드가 완료된 시점 → 여기서 스폰.
        NetworkManager.SceneManager.OnSceneEvent += OnSceneEventForSpawn;
    }

    void OnSceneEventForSpawn(SceneEvent sceneEvent)
    {
        if (sceneEvent.SceneEventType != SceneEventType.LoadEventCompleted) return;

        NetworkManager.SceneManager.OnSceneEvent -= OnSceneEventForSpawn;
        SpawnAllPlayers();
        PlayerSpawnCoordinator.Instance?.NotifyPlayersReady();
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager?.SceneManager != null)
            NetworkManager.SceneManager.OnSceneEvent -= OnSceneEventForSpawn;
    }

    // ── 오프라인 스폰 ────────────────────────────────────────────

    void SpawnOfflinePlayers()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[PlayerSpawnManager] playerPrefab이 연결되지 않았습니다.");
            return;
        }

        if (GameSession.Instance == null) return;

        var activeColors = GameSession.Instance.GetActiveColors();
        if (activeColors.Count == 0)
        {
            Debug.LogWarning("[PlayerSpawnManager] 오프라인: 활성 색 없음. " +
                             "Lobby에서 색 선택 후 Start를 눌렀는지 확인하세요.");
            return;
        }

        var zones     = FindObjectsByType<ColoredStartZone>(FindObjectsSortMode.None);
        var topDownCam = FindAnyObjectByType<TopDownCamera>();
        bool firstPlayer = true;

        foreach (var color in activeColors)
        {
            ColoredStartZone zone = FindZone(zones, color);
            Vector3    pos = zone != null ? zone.SpawnPosition : new Vector3(0f, 0.5f, 0f);
            Quaternion rot = zone != null ? zone.SpawnRotation : Quaternion.identity;

            var go     = Instantiate(playerPrefab, pos, rot);
            var player = go.GetComponent<Player>();

            if (player != null)
            {
                PlayerColorUtil.ApplyToPlayer(player, color);
                player.isOwnerControlled = true;
                player.isUniqueColor = true;
                player.ForceSetSpawnPoint(pos, rot);

                if (firstPlayer)
                {
                    firstPlayer = false;

                    // 솔로 첫 플레이어에게 카메라 연결
                    if (topDownCam != null)
                    {
                        topDownCam.target   = go.transform;
                        player.followCamera = topDownCam.GetComponent<Camera>();
                    }

                    // CheerKeywordEngine은 더 이상 Player 프리팹에 없음 — 0.Title의
                    // NetworkManager GameObject에 세션 싱글턴으로 배치되어 항상 동작함.
                }
            }

            Debug.Log($"[PlayerSpawnManager] 오프라인 스폰 — {color} at {pos}");
        }

        // GameSession에 방금 스폰된 플레이어를 등록
        var colorArr = new PlayerColorType[activeColors.Count];
        for (int i = 0; i < activeColors.Count; i++) colorArr[i] = activeColors[i];
        GameSession.Instance.SetActiveColors(colorArr);

        PlayerSpawnCoordinator.Instance?.NotifyPlayersReady();
    }

    // ── 씬 내 기존 Player 제거 ───────────────────────────────────

    void RemoveScenePlayers()
    {
        var existing = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (var p in existing)
        {
            // NetworkPlayerSetup이 붙어 있으면 이미 네트워크 스폰된 것 — 건너뜀
            if (p.GetComponent<NetworkObject>() != null) continue;
            Destroy(p.gameObject);
        }
        Debug.Log($"[PlayerSpawnManager] 씬 내 비네트워크 Player {existing.Length}개 제거 완료");
    }

    // ── 플레이어 스폰 ────────────────────────────────────────────

    void SpawnAllPlayers()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[PlayerSpawnManager] playerPrefab이 연결되지 않았습니다.");
            return;
        }

        if (PlayerSpawnCoordinator.EntryCount == 0)
        {
            Debug.LogWarning("[PlayerSpawnManager] PlayerSpawnCoordinator에 clientId→color 매핑이 없습니다. " +
                             "에디터 직접 실행 시에는 오프라인 경로를 사용하세요.");
            return;
        }

        var zones = FindObjectsByType<ColoredStartZone>(FindObjectsSortMode.None);

        foreach (var (clientId, colorType) in PlayerSpawnCoordinator.GetAllEntries())
        {
            ColoredStartZone zone = FindZone(zones, colorType);

            Vector3    pos = zone != null ? zone.SpawnPosition : new Vector3(0f, 0.5f, 0f);
            Quaternion rot = zone != null ? zone.SpawnRotation : Quaternion.identity;

            var go     = Instantiate(playerPrefab, pos, rot);
            var netObj = go.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogError("[PlayerSpawnManager] playerPrefab에 NetworkObject가 없습니다.");
                Destroy(go);
                continue;
            }

            netObj.SpawnWithOwnership(clientId, destroyWithScene: true);

            var setup = go.GetComponent<NetworkPlayerSetup>();
            if (setup != null)
                setup.SetColorIndex(ColorTypeToIndex(colorType));

            Debug.Log($"[PlayerSpawnManager] 스폰 완료 — clientId={clientId} color={colorType} pos={pos}");
        }
    }

    // ── 유틸 ─────────────────────────────────────────────────────

    static ColoredStartZone FindZone(ColoredStartZone[] zones, PlayerColorType colorType)
    {
        foreach (var z in zones)
            if (z.ColorType == colorType) return z;
        return null;
    }

    static int ColorTypeToIndex(PlayerColorType colorType)
    {
        var order = LobbyNetworkManager.ColorOrder;
        for (int i = 0; i < order.Length; i++)
            if (order[i] == colorType) return i;
        return 0;
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 세션 데이터 출력")]
    void Debug_SessionData()
    {
        Debug.Log($"[PlayerSpawnManager] PlayerSpawnCoordinator entry count={PlayerSpawnCoordinator.EntryCount}");
        foreach (var (id, color) in PlayerSpawnCoordinator.GetAllEntries())
            Debug.Log($"  clientId={id} → {color}");
    }
#endif
}
