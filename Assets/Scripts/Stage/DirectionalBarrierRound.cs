using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 방향별 베리어 라운드 매니저.
///
/// [라운드 시작 시퀀스]
///  1. 베리어 스폰 포인트 4곳에 색상 프리팹을 랜덤 배치
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
    [Tooltip("각 방향에 베리어가 생성될 Transform. 순서는 무관 — 매 라운드마다 랜덤 배치")]
    [SerializeField] Transform[] barrierSpawnPoints = new Transform[4];

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

    // 색 → 이번 라운드에 스폰된 DoorController
    readonly Dictionary<PlayerColorType, DoorController> _colorToDoor = new();

    readonly List<GameObject> _spawnedBarriers = new();
    readonly List<ColorTile>  _activeTiles      = new();

    static readonly PlayerColorType[] PlayableColors =
        { PlayerColorType.Blue, PlayerColorType.Purple, PlayerColorType.Green, PlayerColorType.Yellow };

    // ── 생명주기 ─────────────────────────────────────────────────

    void Start()
    {
        StartCoroutine(RoundRoutine());
    }

    void OnDisable()
    {
        ClearBarriers();
        ClearTiles();
    }

    // ── 라운드 흐름 ───────────────────────────────────────────────

    IEnumerator RoundRoutine()
    {
        SpawnBarriers();

        // 전체 Open → 매핑 공개
        foreach (DoorController door in _colorToDoor.Values)
            door.Open();

        OnRoundStarted?.Invoke();

        yield return new WaitForSeconds(revealDuration);

        // 전체 Close → 하강
        foreach (DoorController door in _colorToDoor.Values)
            door.Close();

        SpawnTiles();
    }

    // ── 베리어 스폰 ──────────────────────────────────────────────

    void SpawnBarriers()
    {
        ClearBarriers();

        if (barrierSpawnPoints.Length == 0 || barrierPrefabs.Length == 0)
        {
            Debug.LogWarning("[DirectionalBarrierRound] barrierSpawnPoints 또는 barrierPrefabs가 비어 있습니다.");
            return;
        }

        // 색상 배열 셔플 (Fisher-Yates)
        PlayerColorType[] shuffledColors = (PlayerColorType[])PlayableColors.Clone();
        for (int i = shuffledColors.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (shuffledColors[i], shuffledColors[j]) = (shuffledColors[j], shuffledColors[i]);
        }

        int count = Mathf.Min(shuffledColors.Length, barrierSpawnPoints.Length);
        for (int i = 0; i < count; i++)
        {
            PlayerColorType color  = shuffledColors[i];
            Transform       point  = barrierSpawnPoints[i];
            GameObject      prefab = GetBarrierPrefabForColor(color);

            if (prefab == null)
            {
                Debug.LogWarning($"[DirectionalBarrierRound] {color} 베리어 프리팹이 등록되지 않았습니다.");
                continue;
            }
            if (point == null)
            {
                Debug.LogWarning($"[DirectionalBarrierRound] barrierSpawnPoints[{i}]가 null입니다.");
                continue;
            }

            GameObject     obj  = Instantiate(prefab, point.position, point.rotation);
            DoorController door = obj.GetComponent<DoorController>();

            if (door == null)
            {
                Debug.LogWarning($"[DirectionalBarrierRound] {color} 베리어 프리팹에 DoorController가 없습니다.");
                Destroy(obj);
                continue;
            }

            _colorToDoor[color]   = door;
            _spawnedBarriers.Add(obj);
        }
    }

    // ── 타일 스폰 ────────────────────────────────────────────────

    void SpawnTiles()
    {
        ClearTiles();

        if (tileSpawnPoints.Length == 0 || tilePrefabs.Length == 0)
        {
            Debug.LogWarning("[DirectionalBarrierRound] tileSpawnPoints 또는 tilePrefabs가 비어 있습니다.");
            return;
        }

        // 타일 스폰 포인트 셔플 (Fisher-Yates)
        List<Transform> shuffled = new List<Transform>(tileSpawnPoints);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        int count = Mathf.Min(PlayableColors.Length, shuffled.Count);
        for (int i = 0; i < count; i++)
        {
            PlayerColorType color  = PlayableColors[i];
            GameObject      prefab = GetTilePrefabForColor(color);

            if (prefab == null)
            {
                Debug.LogWarning($"[DirectionalBarrierRound] {color} 타일 프리팹이 등록되지 않았습니다.");
                continue;
            }

            GameObject obj  = Instantiate(prefab, shuffled[i].position, Quaternion.identity);
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
        if (!_colorToDoor.TryGetValue(color, out DoorController target)) return;

        foreach (KeyValuePair<PlayerColorType, DoorController> pair in _colorToDoor)
        {
            if (pair.Value == null) continue;

            if (pair.Value == target)
                pair.Value.Open();
            else
                pair.Value.Close();
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
        _colorToDoor.Clear();
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
    void Debug_StartRound()
    {
        StopAllCoroutines();
        ClearBarriers();
        ClearTiles();
        StartCoroutine(RoundRoutine());
    }

    void OnDrawGizmos()
    {
        if (barrierSpawnPoints != null)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.7f);
            foreach (Transform sp in barrierSpawnPoints)
            {
                if (sp != null)
                    Gizmos.DrawWireCube(sp.position, new Vector3(1f, 2f, 0.2f));
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
