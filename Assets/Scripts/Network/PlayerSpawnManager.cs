using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 스폰 관리자. DontDestroyOnLoad 싱글턴 (MonoBehaviour).
///
/// [핵심 원칙 — B+C]
/// 로비에서 확정된 _entries[] 를 1회 캐시.
/// 온라인: M.Stage1에서 1회 스폰(destroyWithScene: false) 후 씬 전환 시 유지.
///         이후 스테이지 진입은 ResetForNewStage(위치·HP 리셋)만 수행.
/// 오프라인: 씬마다 재생성 (DontDestroyOnLoad 없음).
///
/// [온라인 흐름]
/// LobbyNetworkManager.StartGameServerRpc
///   → InitializeOnline(clientColors) : _entries 1회 확정 + NGO SceneEvent 구독
///   → M.Stage1 LoadEventCompleted → SpawnNetworkPlayers (1회)
///   → T.Stage1 / 사망 리로드 LoadEventCompleted → ResetNetworkPlayersForStage
///
/// [배치]
/// 0.Title 씬의 빈 GameObject에 이 컴포넌트 추가.
/// M.Stage1 / T.Stage1 씬의 PlayerSpawnManager는 제거.
/// </summary>
public class PlayerSpawnManager : MonoBehaviour, ISessionResettable
{
    public static PlayerSpawnManager Instance { get; private set; }

    [Header("Player Prefab")]
    [Tooltip("NetworkObject + Player + NetworkPlayerSetup 포함 Prefab")]
    [SerializeField] private GameObject playerPrefab;

    [Header("고정 스폰 좌표 (Blue / Purple / Green / Yellow)")]
    [Tooltip("LobbyNetworkManager.ColorOrder 인덱스 순서 일치 필수.\n모든 스테이지 씬 원점(0,0,0) 정렬 후 설정.")]
    [SerializeField] private Vector3[] fixedSpawnPositions = new Vector3[]
    {
        new Vector3( 0f, 0f,  5f),
        new Vector3( 5f, 0f,  0f),
        new Vector3(-5f, 0f,  0f),
        new Vector3( 0f, 0f, -5f),
    };

    [Tooltip("스폰 Y 오프셋 (바닥 관통 방지)")]
    [SerializeField] private float spawnHeightOffset = 0f;

    struct PlayerEntry
    {
        public ulong           ClientId;
        public PlayerColorType ColorType;
        public Vector3         SpawnPos;
    }

    PlayerEntry[] _entries;
    readonly List<NetworkPlayerSetup> _spawnedSetups = new();
    bool   _networkPlayersSpawned;
    string _lastHandledScene;

    // ── 초기화 ────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("[PlayerSpawnManager] Awake — Instance 등록 + DontDestroyOnLoad");
    }

    void Start()
    {
        TitleReturnFlow.Instance?.Register(this);
    }

    void OnDestroy()
    {
        TitleReturnFlow.Instance?.Unregister(this);
        UnsubscribeNgoSceneEvent();
        if (Instance == this) Instance = null;
    }

    void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    // ── 온라인 초기화 ─────────────────────────────────────────────────

    /// <summary>
    /// Host: LoadScene 직전 1회 호출.
    /// clientColors → _entries 캐시 + NGO SceneEvent 구독.
    /// </summary>
    public void InitializeOnline(Dictionary<ulong, PlayerColorType> clientColors)
    {
        if (clientColors == null || clientColors.Count == 0)
        {
            Debug.LogError("[PlayerSpawnManager] InitializeOnline — clientColors 비어 있음");
            return;
        }

        _entries = new PlayerEntry[clientColors.Count];
        int i = 0;
        foreach (var kv in clientColors)
        {
            _entries[i++] = new PlayerEntry
            {
                ClientId  = kv.Key,
                ColorType = kv.Value,
                SpawnPos  = GetFixedSpawnPos(kv.Value),
            };
        }

        _networkPlayersSpawned = false;
        _lastHandledScene      = null;
        _spawnedSetups.Clear();

        var nm = NetworkManager.Singleton;
        if (nm == null || nm.SceneManager == null)
        {
            Debug.LogError("[PlayerSpawnManager] InitializeOnline — NetworkManager/SceneManager 없음");
            return;
        }

        if (!nm.IsHost)
        {
            Debug.LogError("[PlayerSpawnManager] InitializeOnline — Host가 아님. 스폰 구독 스킵");
            return;
        }

        // 중복 구독 방지
        nm.SceneManager.OnSceneEvent -= OnNgoSceneEvent;
        nm.SceneManager.OnSceneEvent += OnNgoSceneEvent;

        Debug.Log($"[PlayerSpawnManager] 온라인 초기화 — {_entries.Length}명, SceneEvent 구독 완료");
        foreach (var e in _entries)
            Debug.Log($"  clientId={e.ClientId} color={e.ColorType} pos={e.SpawnPos}");
    }

    // ── ISessionResettable ────────────────────────────────────────────

    public void OnSessionReset(TitleReturnScope scope)
    {
        DespawnNetworkPlayers();
        _entries               = null;
        _networkPlayersSpawned = false;
        _lastHandledScene      = null;
        UnsubscribeNgoSceneEvent();
        Debug.Log("[PlayerSpawnManager] 세션 리셋 완료");
    }

    void UnsubscribeNgoSceneEvent()
    {
        if (NetworkManager.Singleton?.SceneManager != null)
            NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnNgoSceneEvent;
    }

    // ── 오프라인: sceneLoaded / 온라인: 이미 스폰된 경우 fallback ─────

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsStageScene(scene.name)) return;

        PlayerSpawnCoordinator.ResetReady();
        // 같은 씬 사망 리로드도 다시 처리되도록 허용
        _lastHandledScene = null;

        if (LobbyContext.IsOffline)
        {
            if (_entries == null)
            {
                var colors = GameSession.Instance?.GetActiveColors();
                if (colors == null || colors.Count == 0)
                {
                    Debug.LogWarning("[PlayerSpawnManager] 오프라인: GameSession 활성 색 없음 — 스킵");
                    return;
                }

                _entries = new PlayerEntry[colors.Count];
                for (int i = 0; i < colors.Count; i++)
                    _entries[i] = new PlayerEntry
                    {
                        ColorType = colors[i],
                        SpawnPos  = GetFixedSpawnPos(colors[i]),
                    };
            }

            StartCoroutine(HandleStageLoadedNextFrame());
            return;
        }

        // 온라인 + 이미 1회 스폰됨: LoadEventCompleted를 놓쳐도 플레이어는 유지되므로
        // 2프레임 뒤에도 이 씬을 처리 안 했으면 Reset fallback.
        if (_networkPlayersSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            StartCoroutine(OnlineResetFallback(scene.name));
    }

    IEnumerator HandleStageLoadedNextFrame()
    {
        yield return null;
        HandleStageLoaded(SceneManager.GetActiveScene().name);
    }

    IEnumerator OnlineResetFallback(string sceneName)
    {
        yield return null;
        yield return null;
        if (_lastHandledScene == sceneName) yield break;

        Debug.LogWarning($"[PlayerSpawnManager] LoadEventCompleted 미수신 — sceneLoaded fallback ({sceneName})");
        HandleStageLoaded(sceneName);
    }

    // ── 온라인: NGO LoadEventCompleted ────────────────────────────────

    void OnNgoSceneEvent(SceneEvent e)
    {
        if (e.SceneEventType == SceneEventType.Load)
        {
            if (IsStageScene(e.SceneName))
                _lastHandledScene = null;
            return;
        }

        if (e.SceneEventType != SceneEventType.LoadEventCompleted) return;

        string sceneName = string.IsNullOrEmpty(e.SceneName)
            ? SceneManager.GetActiveScene().name
            : e.SceneName;

        Debug.Log($"[PlayerSpawnManager] LoadEventCompleted — scene={sceneName}");

        if (!IsStageScene(sceneName)) return;
        HandleStageLoaded(sceneName);
    }

    // ── 스테이지 진입 처리 ────────────────────────────────────────────

    void HandleStageLoaded(string sceneName)
    {
        if (_entries == null)
        {
            Debug.LogWarning($"[PlayerSpawnManager] HandleStageLoaded — _entries null (InitializeOnline 미호출). scene={sceneName}");
            return;
        }

        if (_lastHandledScene == sceneName)
        {
            Debug.Log($"[PlayerSpawnManager] HandleStageLoaded — 이미 처리됨, 스킵 ({sceneName})");
            return;
        }

        PlayerSpawnCoordinator.ResetReady();
        _lastHandledScene = sceneName;

        if (LobbyContext.IsOffline)
        {
            SpawnOfflinePlayers();
            PlayerSpawnCoordinator.NotifyOffline();
            return;
        }

        if (!_networkPlayersSpawned)
        {
            SpawnNetworkPlayers();
            _networkPlayersSpawned = true;
        }
        else
        {
            ResetNetworkPlayersForStage();
        }

        if (PlayerSpawnCoordinator.Instance == null)
            Debug.LogWarning("[PlayerSpawnManager] PlayerSpawnCoordinator.Instance null — OnPlayersReady 미발행");
        else
            PlayerSpawnCoordinator.Instance.NotifyPlayersReady();
    }

    // ── 온라인 스폰 (1회, 씬 전환 후에도 유지) ────────────────────────

    void SpawnNetworkPlayers()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[PlayerSpawnManager] playerPrefab 미설정");
            return;
        }

        _spawnedSetups.Clear();

        for (int i = 0; i < _entries.Length; i++)
        {
            ref var e  = ref _entries[i];
            var go     = Instantiate(playerPrefab, e.SpawnPos, Quaternion.identity);
            var netObj = go.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogError("[PlayerSpawnManager] playerPrefab에 NetworkObject 없음");
                Destroy(go);
                continue;
            }

            // destroyWithScene: false → M→T·사망 리로드에서도 유지 (B안)
            netObj.SpawnWithOwnership(e.ClientId, destroyWithScene: false);

            var setup = go.GetComponent<NetworkPlayerSetup>();
            setup?.SetColorIndex(ColorTypeToIndex(e.ColorType));
            if (setup != null) _spawnedSetups.Add(setup);

            Debug.Log($"[PlayerSpawnManager] 스폰(유지) — clientId={e.ClientId} color={e.ColorType} pos={e.SpawnPos}");
        }
    }

    void ResetNetworkPlayersForStage()
    {
        Debug.Log($"[PlayerSpawnManager] 스테이지 리셋 — {_spawnedSetups.Count}명");

        for (int i = 0; i < _entries.Length; i++)
        {
            NetworkPlayerSetup setup = null;
            if (i < _spawnedSetups.Count) setup = _spawnedSetups[i];

            // 리스트 슬롯이 비었으면 OwnerClientId로 재탐색
            if (setup == null)
            {
                ulong targetId = _entries[i].ClientId;
                var all = FindObjectsByType<NetworkPlayerSetup>(FindObjectsSortMode.None);
                for (int j = 0; j < all.Length; j++)
                {
                    if (all[j] != null && all[j].OwnerClientId == targetId)
                    {
                        setup = all[j];
                        break;
                    }
                }
            }

            if (setup == null)
            {
                Debug.LogError($"[PlayerSpawnManager] 리셋 대상 없음 — clientId={_entries[i].ClientId}");
                continue;
            }

            setup.ResetForNewStage(_entries[i].SpawnPos);
        }
    }

    void DespawnNetworkPlayers()
    {
        for (int i = 0; i < _spawnedSetups.Count; i++)
        {
            var setup = _spawnedSetups[i];
            if (setup == null) continue;

            var netObj = setup.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
                netObj.Despawn(true);
            else
                Destroy(setup.gameObject);
        }
        _spawnedSetups.Clear();
    }

    // ── 오프라인 스폰 ─────────────────────────────────────────────────

    void SpawnOfflinePlayers()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[PlayerSpawnManager] playerPrefab 미설정");
            return;
        }

        var  topDownCam = FindAnyObjectByType<TopDownCamera>();
        bool first      = true;

        for (int i = 0; i < _entries.Length; i++)
        {
            ref var e  = ref _entries[i];
            var go     = Instantiate(playerPrefab, e.SpawnPos, Quaternion.identity);
            var player = go.GetComponent<Player>();

            if (player != null)
            {
                PlayerColorUtil.ApplyToPlayer(player, e.ColorType);
                player.isOwnerControlled = true;
                player.isUniqueColor     = true;
                player.ForceSetSpawnPoint(e.SpawnPos, Quaternion.identity);
            }

            if (first)
            {
                first = false;
                if (topDownCam != null && player != null)
                {
                    topDownCam.target   = go.transform;
                    player.followCamera = topDownCam.GetComponent<Camera>();
                }
                var cheer = go.GetComponent<CheerKeywordEngine>();
                if (cheer != null) cheer.enabled = true;
            }

            Debug.Log($"[PlayerSpawnManager] 오프라인 스폰 — {e.ColorType} at {e.SpawnPos}");
        }
    }

    // ── 공개 유틸 ─────────────────────────────────────────────────────

    public Vector3 GetFixedSpawnPos(PlayerColorType colorType)
    {
        int idx = ColorTypeToIndex(colorType);
        if (fixedSpawnPositions != null && idx >= 0 && idx < fixedSpawnPositions.Length)
        {
            Vector3 pos = fixedSpawnPositions[idx];
            pos.y += spawnHeightOffset;
            return pos;
        }
        Debug.LogWarning($"[PlayerSpawnManager] fixedSpawnPositions[{idx}] 없음 — 원점 사용");
        return new Vector3(0f, spawnHeightOffset, 0f);
    }

    static int ColorTypeToIndex(PlayerColorType colorType)
    {
        var order = LobbyNetworkManager.ColorOrder;
        for (int i = 0; i < order.Length; i++)
            if (order[i] == colorType) return i;
        return 0;
    }

    static bool IsStageScene(string name) =>
        !string.IsNullOrEmpty(name) && name.Contains("Stage");

#if UNITY_EDITOR
    [ContextMenu("테스트: 엔트리 출력")]
    void Debug_Entries()
    {
        if (_entries == null) { Debug.Log("[PlayerSpawnManager] 엔트리 없음 (미초기화)"); return; }
        Debug.Log($"[PlayerSpawnManager] 엔트리 {_entries.Length}개 / spawned={_networkPlayersSpawned}");
        foreach (var e in _entries)
            Debug.Log($"  clientId={e.ClientId} color={e.ColorType} pos={e.SpawnPos}");
    }

    [ContextMenu("테스트: 고정 좌표 출력")]
    void Debug_SpawnPositions()
    {
        var order = LobbyNetworkManager.ColorOrder;
        for (int i = 0; i < order.Length; i++)
            Debug.Log($"  {order[i]} → {GetFixedSpawnPos(order[i])}");
    }
#endif
}
