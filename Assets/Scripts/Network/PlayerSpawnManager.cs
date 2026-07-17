using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 스폰 관리자. DontDestroyOnLoad 싱글턴 (MonoBehaviour).
///
/// [핵심 원칙 — A안]
/// 로비에서 확정된 _entries[] 를 1회 캐시.
/// 온라인: 스테이지 씬 진입마다 새로 SpawnWithOwnership(destroyWithScene: true).
///         씬 리로드(사망) → 이전 플레이어 자동 Despawn → 새 씬에서 클린 스폰.
///         별도 리셋 코드 불필요 — OnNetworkSpawn이 항상 초기 상태로 시작 (NetworkDesign §11).
///
/// [온라인 흐름]
/// LobbyNetworkManager.StartGameServerRpc
///   → InitializeOnline(clientColors) : _entries 확정 + NGO SceneEvent 구독
///   → 스테이지 씬 LoadEventCompleted → SpawnNetworkPlayers (씬마다)
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

        _lastHandledScene = null;
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
        _entries          = null;
        _lastHandledScene = null;
        UnsubscribeNgoSceneEvent();
        Debug.Log("[PlayerSpawnManager] 세션 리셋 완료");
    }

    void UnsubscribeNgoSceneEvent()
    {
        if (NetworkManager.Singleton?.SceneManager != null)
            NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnNgoSceneEvent;
    }

    // ── 온라인: NGO LoadEventCompleted ────────────────────────────────

    void OnNgoSceneEvent(SceneEvent e)
    {
        if (e.SceneEventType == SceneEventType.Load)
        {
            if (IsStageScene(e.SceneName))
            {
                _lastHandledScene = null;
                PlayerSpawnCoordinator.ResetReady();
            }
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

        // A안: 씬마다 새로 스폰. destroyWithScene:true이므로 이전 플레이어는 이미 자동 Despawn됨.
        SpawnNetworkPlayers();

        if (PlayerSpawnCoordinator.Instance == null)
            Debug.LogWarning("[PlayerSpawnManager] PlayerSpawnCoordinator.Instance null — OnPlayersReady 미발행");
        else
            PlayerSpawnCoordinator.Instance.NotifyPlayersReady();
    }

    // ── 온라인 스폰 (씬마다 클린 스폰 — A안) ─────────────────────────

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

            // destroyWithScene: true → 씬 언로드 시 자동 Despawn (A안: 씬 리로드 = 클린 스폰)
            netObj.SpawnWithOwnership(e.ClientId, destroyWithScene: true);

            var setup = go.GetComponent<NetworkPlayerSetup>();
            setup?.SetColorIndex(LobbyNetworkManager.ColorTypeToIndex(e.ColorType));
            if (setup != null) _spawnedSetups.Add(setup);

            Debug.Log($"[PlayerSpawnManager] 스폰 — clientId={e.ClientId} color={e.ColorType} pos={e.SpawnPos}");
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

    // ── 공개 유틸 ─────────────────────────────────────────────────────

    public Vector3 GetFixedSpawnPos(PlayerColorType colorType)
    {
        int idx = LobbyNetworkManager.ColorTypeToIndex(colorType);
        if (fixedSpawnPositions != null && idx >= 0 && idx < fixedSpawnPositions.Length)
        {
            Vector3 pos = fixedSpawnPositions[idx];
            pos.y += spawnHeightOffset;
            return pos;
        }
        Debug.LogWarning($"[PlayerSpawnManager] fixedSpawnPositions[{idx}] 없음 — 원점 사용");
        return new Vector3(0f, spawnHeightOffset, 0f);
    }

    static bool IsStageScene(string name) =>
        !string.IsNullOrEmpty(name) && name.Contains("Stage");

#if UNITY_EDITOR
    [ContextMenu("테스트: 엔트리 출력")]
    void Debug_Entries()
    {
        if (_entries == null) { Debug.Log("[PlayerSpawnManager] 엔트리 없음 (미초기화)"); return; }
        Debug.Log($"[PlayerSpawnManager] 엔트리 {_entries.Length}개");
        foreach (var e in _entries)
            Debug.Log($"  clientId={e.ClientId} color={e.ColorType} pos={e.SpawnPos}");
    }

    [ContextMenu("테스트: 고정 좌표 출력")]
    void Debug_SpawnPositions()
    {
        foreach (var color in LobbyNetworkManager.ColorOrder)
            Debug.Log($"  {color} → {GetFixedSpawnPos(color)}");
    }
#endif
}
