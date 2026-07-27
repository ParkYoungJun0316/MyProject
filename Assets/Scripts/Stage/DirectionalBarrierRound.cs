using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 방향별 베리어 라운드 매니저.
///
/// [축 SSOT: NetworkDesign.md §11B — 챌린지 축(C 패턴), ColorTileChallenge와 동일 골격 복제]
/// Host만 라운드 시드를 StageNetworkState로 배포하고, 전 머신이 그 시드 기반 System.Random으로
/// 동일한 색 배치를 재생성한다 (버그 수정 2026-07-27 — 이전엔 전 피어가 각자 UnityEngine.Random으로
/// 셔플해 Host/Client 색 배치가 갈라졌음. 위치는 씬 공유 Transform이라 항상 맞았음).
///
/// [라운드 시작 시퀀스]
///  1. 베리어 스폰 포인트 4곳에 색상 프리팹을 시드 기반으로 배치
///  2. 전부 Open → revealDuration초 동안 색↔위치 매핑 공개
///  3. 전부 Close (하강)
///  4. 타일 스폰
///  5. 타일 밟으면 해당 색 베리어만 Open, 나머지 Close
///
/// [베리어 규칙]
///  - 한 번에 하나만 Open (토글 없음)
///
/// [Inspector 필수 설정]
///  barrierSpawnPoints : 동/서/남/북 스폰 위치 4개
///  barrierPrefabs     : Blue/Purple/Green/Yellow DoorController 포함 프리팹 4개
///  tileSpawnPoints    : 타일 스폰 위치 4개 이상
///  tilePrefabs        : Blue/Purple/Green/Yellow ColorTile 프리팹 4개
///  debugAllTiles      : true = 플레이어 체크 없이 누구나 밟기 가능 (테스트용)
/// </summary>
public class DirectionalBarrierRound : MonoBehaviour
{
    public enum SpawnDirection
    {
        NorthSouth, // 북/남 — 프리팹 회전 그대로
        EastWest,   // 동/서 — 프리팹 회전에 Y -90 추가
    }

    [System.Serializable]
    public class BarrierSpawnPoint
    {
        [Tooltip("베리어가 생성될 위치")]
        public Transform point;

        [Tooltip("NorthSouth: 프리팹 회전 그대로 / EastWest: Y -90 추가 적용")]
        public SpawnDirection direction = SpawnDirection.NorthSouth;
    }

    [System.Serializable]
    public class BarrierPrefabEntry
    {
        [Tooltip("이 프리팹이 대응하는 플레이어 고유색")]
        public PlayerColorType colorType;

        [Tooltip("DoorController가 포함된 베리어 프리팹 (색상 비주얼 포함)")]
        public GameObject prefab;
    }

    [System.Serializable]
    public class TilePrefabEntry
    {
        [Tooltip("이 프리팹이 대응하는 플레이어 고유색")]
        public PlayerColorType colorType;

        [Tooltip("ColorTile 프리팹")]
        public GameObject prefab;
    }

    [Header("베리어 스폰 위치 (동/서/남/북 4개)")]
    [Tooltip("point: 스폰 위치 / direction: NorthSouth=프리팹 그대로, EastWest=Y-90 추가")]
    [SerializeField] BarrierSpawnPoint[] barrierSpawnPoints = new BarrierSpawnPoint[4];

    [Header("베리어 프리팹 (색상별 4개)")]
    [Tooltip("Blue / Purple / Green / Yellow — DoorController + 색상 비주얼이 포함된 프리팹")]
    [SerializeField] BarrierPrefabEntry[] barrierPrefabs = new BarrierPrefabEntry[4];

    [Header("타일 스폰 위치 (4개 이상)")]
    [SerializeField] Transform[] tileSpawnPoints = new Transform[0];

    [Header("타일 프리팹 (색상별 4개)")]
    [Tooltip("Blue / Purple / Green / Yellow ColorTile 프리팹")]
    [SerializeField] TilePrefabEntry[] tilePrefabs = new TilePrefabEntry[4];

    [Header("라운드 시작")]
    [Tooltip("베리어를 Open해서 색↔위치 매핑을 보여주는 시간(초)")]
    [SerializeField] float revealDuration = 0f;

    [Header("테스트")]
    [Tooltip("true: 플레이어 색/isUniqueColor 체크 없이 누구든 타일 밟기 가능")]
    [SerializeField] bool debugAllTiles = true;

    [Header("이벤트")]
    public UnityEvent OnRoundStarted;

    // 색 → 이번 라운드에 스폰된 DoorController 목록 (동일 색이 여러 슬롯에 배정될 수 있음)
    readonly Dictionary<PlayerColorType, List<DoorController>> _colorToDoors = new();

    readonly List<GameObject> _spawnedBarriers = new();
    readonly List<ColorTile>  _activeTiles      = new();

    // 이번 라운드 4슬롯에 배정된 색 목록 (GameSession 활성색 기준 균등 분배)
    PlayerColorType[] _roundColors;

    StageNetworkState _netState;

    // ── 생명주기 ─────────────────────────────────────────────────

    void Start()
    {
        // StageNetworkState.Awake()가 이 컴포넌트의 Start()보다 먼저 실행되는 것을
        // Unity 전역 Awake→Start 순서로 보장받음 (OXQuizManager/ColorTileChallenge와 동일 전제).
        _netState = StageNetworkState.Instance;
        if (_netState != null)
            _netState.OnChallengeStepChanged += HandleChallengeStepChanged;

        // Host 레인만 라운드를 발동(§11B ①Trigger+②RoundStart) — Client는 NV 전파만 관찰.
        if (!IsClientOnly())
            Activate();
    }

    void OnDestroy()
    {
        if (_netState != null)
            _netState.OnChallengeStepChanged -= HandleChallengeStepChanged;
    }

    void OnDisable()
    {
        StopAllCoroutines();
        ClearBarriers();
        ClearTiles();
    }

    /// <summary>Client/Host 공통. Host 레인 여부만 다르게 취급 (OXQuizManager/ColorTileChallenge와 동일).</summary>
    static bool IsClientOnly()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && !nm.IsServer;
    }

    // ── 외부 호출 (Host 전용 — §11B ①Trigger) ─────────────────────

    /// <summary>
    /// 라운드 즉시 시작. Host 레인만 실제로 시드를 배포한다 — Client의 직접 호출은 무시된다.
    /// 시드를 StageNetworkState로 배포하면 전 머신이 HandleChallengeStepChanged에서 동일한
    /// 시드로 베리어·타일을 재생성한다 (§11B ②RoundStart).
    /// </summary>
    public void Activate()
    {
        if (IsClientOnly()) return;
        if (_netState == null) return;

        int seed = Random.Range(int.MinValue, int.MaxValue);
        _netState.ChallengeStart(seed);
        _netState.ChallengeStepBegin(0);
    }

    // ── 라운드 흐름 (전 머신 공통 — StageNetworkState NV 구독, §11B ③Generate) ──

    /// <summary>
    /// StageNetworkState.OnChallengeStepChanged 구독 핸들러. Host/Client 동일 코드로 라운드를 재생한다.
    /// 색 배치는 ChallengeSeed 기반 System.Random이라 전 머신이 항상 같은 결과를 낸다.
    /// </summary>
    void HandleChallengeStepChanged(int stepIndex)
    {
        if (stepIndex < 0) return; // ChallengeStart()의 초기화 신호 — 무시

        StopAllCoroutines();
        ClearBarriers();
        ClearTiles();
        StartCoroutine(RoundRoutine());
    }

    IEnumerator RoundRoutine()
    {
        // 베리어 스폰 + 타일 스폰이 같은 시드 시퀀스를 이어서 소비 — 전 머신이 항상 같은 순서로
        // 같은 System.Random 호출을 하므로 결과가 일치한다(UnityEngine.Random 전역 상태 오염 없음,
        // OXQuizManager.RegenerateQuestionOrder와 동일 원칙).
        int seed = _netState != null ? _netState.ChallengeSeed : 0;
        var rng  = new System.Random(seed);

        SpawnBarriers(rng);

        // 전체 Open → 매핑 공개
        foreach (List<DoorController> doors in _colorToDoors.Values)
            foreach (DoorController door in doors)
                door?.Open();

        OnRoundStarted?.Invoke();

        yield return new WaitForSeconds(revealDuration);

        // 전체 Close → 하강
        foreach (List<DoorController> doors in _colorToDoors.Values)
            foreach (DoorController door in doors)
                door?.Close();

        SpawnTiles(rng);
    }

    // ── 베리어 스폰 ──────────────────────────────────────────────

    void SpawnBarriers(System.Random rng)
    {
        ClearBarriers();

        if (barrierSpawnPoints == null || barrierSpawnPoints.Length == 0 || barrierPrefabs.Length == 0)
        {
            Debug.LogWarning("[DirectionalBarrierRound] barrierSpawnPoints 또는 barrierPrefabs가 비어 있습니다.");
            return;
        }

        // GameSession 활성색 기준 4슬롯 균등 분배 (2인→2+2, 3인→2+1+1, 4인→1+1+1+1)
        // rng를 넘겨 여분 슬롯 배정까지 시드로 결정 — 안 넘기면 UnityEngine.Random이라 갈라짐.
        int totalSlots = barrierSpawnPoints.Length > 0 ? barrierSpawnPoints.Length : 4;
        _roundColors = GameSessionColorDistribution.Distribute(totalSlots, rng);

        // 어떤 방향 슬롯에 어떤 색이 배치될지 셔플
        PlayerColorType[] shuffledForBarriers = (PlayerColorType[])_roundColors.Clone();
        for (int i = shuffledForBarriers.Length - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (shuffledForBarriers[i], shuffledForBarriers[j]) = (shuffledForBarriers[j], shuffledForBarriers[i]);
        }

        int count = Mathf.Min(shuffledForBarriers.Length, barrierSpawnPoints.Length);
        for (int i = 0; i < count; i++)
        {
            PlayerColorType   color  = shuffledForBarriers[i];
            BarrierSpawnPoint entry  = barrierSpawnPoints[i];
            GameObject        prefab = GetBarrierPrefabForColor(color);

            if (prefab == null)
            {
                Debug.LogWarning($"[DirectionalBarrierRound] {color} 베리어 프리팹이 등록되지 않았습니다.");
                continue;
            }
            if (entry == null || entry.point == null)
            {
                Debug.LogWarning($"[DirectionalBarrierRound] barrierSpawnPoints[{i}]가 null입니다.");
                continue;
            }

            // NorthSouth: 프리팹 회전 그대로 / EastWest: 월드 Y축 기준 -90 추가
            Quaternion baseRot  = prefab.transform.rotation;
            Quaternion spawnRot = entry.direction == SpawnDirection.EastWest
                ? Quaternion.Euler(0f, -90f, 0f) * baseRot
                : baseRot;

            GameObject     obj  = Instantiate(prefab, entry.point.position, spawnRot);
            DoorController door = obj.GetComponent<DoorController>();

            if (door == null)
            {
                Debug.LogWarning($"[DirectionalBarrierRound] {color} 베리어 프리팹에 DoorController가 없습니다.");
                Destroy(obj);
                continue;
            }

            if (!_colorToDoors.TryGetValue(color, out List<DoorController> list))
            {
                list = new List<DoorController>();
                _colorToDoors[color] = list;
            }
            list.Add(door);
            _spawnedBarriers.Add(obj);
        }
    }

    // ── 타일 스폰 ────────────────────────────────────────────────

    void SpawnTiles(System.Random rng)
    {
        ClearTiles();

        if (tileSpawnPoints.Length == 0 || tilePrefabs.Length == 0)
        {
            Debug.LogWarning("[DirectionalBarrierRound] tileSpawnPoints 또는 tilePrefabs가 비어 있습니다.");
            return;
        }
        if (_roundColors == null || _roundColors.Length == 0)
        {
            Debug.LogWarning("[DirectionalBarrierRound] _roundColors가 없습니다. SpawnBarriers를 먼저 호출하세요.");
            return;
        }

        // 스폰 포인트 셔플
        List<Transform> shuffledPoints = new List<Transform>(tileSpawnPoints);
        for (int i = shuffledPoints.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (shuffledPoints[i], shuffledPoints[j]) = (shuffledPoints[j], shuffledPoints[i]);
        }

        // 타일 색 셔플 (베리어 배치와 독립적으로 랜덤화)
        PlayerColorType[] shuffledColors = (PlayerColorType[])_roundColors.Clone();
        for (int i = shuffledColors.Length - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (shuffledColors[i], shuffledColors[j]) = (shuffledColors[j], shuffledColors[i]);
        }

        int count = Mathf.Min(shuffledColors.Length, shuffledPoints.Count);
        for (int i = 0; i < count; i++)
        {
            PlayerColorType color  = shuffledColors[i];
            GameObject      prefab = GetTilePrefabForColor(color);

            if (prefab == null)
            {
                Debug.LogWarning($"[DirectionalBarrierRound] {color} 타일 프리팹이 등록되지 않았습니다.");
                continue;
            }

            GameObject obj  = Instantiate(prefab, shuffledPoints[i].position, Quaternion.identity);
            ColorTile  tile = obj.GetComponent<ColorTile>() ?? obj.AddComponent<ColorTile>();

            tile.Setup(color);
            tile.ignorePlayerCheck   = debugAllTiles;
            tile.OnActivatedCallback = HandleTileActivated;
            _activeTiles.Add(tile);
        }
    }

    // ── 타일 활성화 처리 ─────────────────────────────────────────

    void HandleTileActivated(PlayerColorType color)
    {
        if (!_colorToDoors.ContainsKey(color)) return;

        foreach (KeyValuePair<PlayerColorType, List<DoorController>> pair in _colorToDoors)
        {
            bool isTarget = pair.Key == color;
            foreach (DoorController door in pair.Value)
            {
                if (door == null) continue;
                if (isTarget) door.Open();
                else          door.Close();
            }
        }
    }

    // ── 정리 ─────────────────────────────────────────────────────

    void ClearBarriers()
    {
        foreach (GameObject obj in _spawnedBarriers)
        {
            if (obj != null) Destroy(obj);
        }
        _spawnedBarriers.Clear();
        _colorToDoors.Clear();
    }

    void ClearTiles()
    {
        foreach (ColorTile t in _activeTiles)
        {
            if (t != null) Destroy(t.gameObject);
        }
        _activeTiles.Clear();
    }

    // ── 유틸 ─────────────────────────────────────────────────────

    GameObject GetBarrierPrefabForColor(PlayerColorType color)
    {
        foreach (BarrierPrefabEntry e in barrierPrefabs)
        {
            if (e.colorType == color) return e.prefab;
        }
        return null;
    }

    GameObject GetTilePrefabForColor(PlayerColorType color)
    {
        foreach (TilePrefabEntry e in tilePrefabs)
        {
            if (e.colorType == color) return e.prefab;
        }
        return null;
    }

    // ── 에디터 ──────────────────────────────────────────────────

    [ContextMenu("테스트: 라운드 시작")]
    void Debug_StartRound() => Activate();

    void OnDrawGizmos()
    {
        if (barrierSpawnPoints != null)
        {
            foreach (BarrierSpawnPoint entry in barrierSpawnPoints)
            {
                if (entry == null || entry.point == null) continue;
                Gizmos.color = entry.direction == SpawnDirection.EastWest
                    ? new Color(0.3f, 0.6f, 1f, 0.7f)
                    : new Color(1f, 0.3f, 0.3f, 0.7f);
                Gizmos.DrawWireCube(entry.point.position, new Vector3(1f, 2f, 0.2f));
            }
        }

        if (tileSpawnPoints != null)
        {
            Gizmos.color = new Color(0.8f, 0.4f, 1f, 0.6f);
            foreach (Transform sp in tileSpawnPoints)
            {
                if (sp != null)
                    Gizmos.DrawWireCube(sp.position, Vector3.one * 0.8f);
            }
        }
    }
}
