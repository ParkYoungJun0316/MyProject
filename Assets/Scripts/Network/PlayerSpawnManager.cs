using Unity.Netcode;
using UnityEngine;

/// <summary>
/// M.Stage1 (및 이후 스테이지) 씬에 배치해 플레이어를 동적으로 스폰.
/// NetworkBehaviour — Host에서만 실행.
///
/// [동작]
/// OnNetworkSpawn (Host) :
///   1. 씬에 미리 배치된 비네트워크 Player 인스턴스 제거
///   2. NetworkSessionData.ClientColors 기반으로 각 clientId에 맞는 색의
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
    [Tooltip("NetworkObject + Player + NetworkPlayerSetup + ClientNetworkTransform 포함 Prefab.")]
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
        SpawnAllPlayers();
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
            Vector3    pos = zone != null ? zone.SpawnPosition : Vector3.zero;
            Quaternion rot = zone != null ? zone.SpawnRotation : Quaternion.identity;

            var go     = Instantiate(playerPrefab, pos, rot);
            var player = go.GetComponent<Player>();

            if (player != null)
            {
                player.playerColorType   = color;
                player.isOwnerControlled = true;
                player.ForceSetSpawnPoint(pos, rot);

                // 솔로(1인)에서 첫 번째 플레이어를 카메라가 따라가도록 설정
                if (firstPlayer && topDownCam != null)
                {
                    topDownCam.target     = go.transform;
                    player.followCamera   = topDownCam.GetComponent<Camera>();
                    firstPlayer           = false;
                }
            }

            Debug.Log($"[PlayerSpawnManager] 오프라인 스폰 — {color} at {pos}");
        }

        // GameSession에 방금 스폰된 플레이어를 등록
        var colorArr = new PlayerColorType[activeColors.Count];
        for (int i = 0; i < activeColors.Count; i++) colorArr[i] = activeColors[i];
        GameSession.Instance.SetActiveColors(colorArr);
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

        if (NetworkSessionData.ClientColors.Count == 0)
        {
            Debug.LogWarning("[PlayerSpawnManager] NetworkSessionData.ClientColors가 비어 있습니다. " +
                             "에디터 직접 실행 시에는 오프라인 경로를 사용하세요.");
            return;
        }

        var zones = FindObjectsByType<ColoredStartZone>(FindObjectsSortMode.None);

        foreach (var (clientId, colorType) in NetworkSessionData.ClientColors)
        {
            // 해당 색의 ColoredStartZone 탐색
            ColoredStartZone zone = FindZone(zones, colorType);

            Vector3    pos = zone != null ? zone.SpawnPosition : Vector3.zero;
            Quaternion rot = zone != null ? zone.SpawnRotation : Quaternion.identity;

            // 스폰
            var go     = Instantiate(playerPrefab, pos, rot);
            var netObj = go.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogError("[PlayerSpawnManager] playerPrefab에 NetworkObject가 없습니다.");
                Destroy(go);
                continue;
            }

            netObj.SpawnWithOwnership(clientId, destroyWithScene: true);

            // 색 설정 (NetworkVariable → 전원 동기화)
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
        Debug.Log($"[PlayerSpawnManager] ClientColors count={NetworkSessionData.ClientColors.Count}");
        foreach (var (id, color) in NetworkSessionData.ClientColors)
            Debug.Log($"  clientId={id} → {color}");
    }
#endif
}
