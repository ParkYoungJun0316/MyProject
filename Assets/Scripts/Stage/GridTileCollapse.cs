using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Random: 이번 단계에 깰 칸 수. afterRound는 GridSafePhase와 같은 규약(라운드 인덱스 0부터, 오름차순).
/// </summary>
[System.Serializable]
public class GridCollapsePhase
{
    [Tooltip("이 라운드 인덱스(0부터)부터 이 단계를 적용")]
    public int afterRound;

    [Tooltip("이번 단계에 깨질 칸 수. 안전 칸 제외 후보보다 크면 후보 수로 클램프")]
    public int breakCount = 3;
}

public enum GridCollapseMode
{
    /// <summary>안전 칸이 아닌 칸 중 N칸. 남은 칸이 4방향으로 한 덩어리 + 안전 칸 전부 포함을 보장(M.Stage5).</summary>
    Random,
    /// <summary>안전 칸을 뺀 전부(T.Boss P3). 살 길 = 안전 칸 위.</summary>
    AllExceptSafe,
}

/// <summary>
/// GridChallenge 라운드에 묶인 바닥 붕괴 — M.Stage5 / T.Boss P3 공용.
///
/// [흐름] 라운드 시작(안전 칸 확정) → 깰 칸 결정 → 시드로 섞은 순서대로 groupSize개씩 interval 간격
/// 순차 파괴(칸마다 warnLead초 전 경고) → 정산 순간 전부 복구(되감기). 안전 칸은 절대 안 깨진다.
/// 깨진 칸 위 = 공허 낙하 → Player.fallDeathY 즉사(공용 낙사 경로 — 여기엔 사망 코드 없음).
///
/// [네트워크] 새 RPC/NV 없음. GridChallenge.OnRoundStarted / OnRoundSettled는 전 머신에서 발생하고,
/// 칸 선택·순서는 GridChallenge.CurrentRoundSeed(라운드 시드 NV)로 전 머신이 똑같이 계산한다.
/// 시각은 각 머신이 라운드 시작을 받은 순간 기준(로컬) — 이동이 Owner 권한이라 낙하는 자기 화면의
/// 붕괴 시각대로 일어나고, Host 판정은 안전 칸(절대 안 깨짐)만 보므로 머신 간 수십 ms 차이는 판정을 가르지 않는다.
///
/// [칸 구조] 깨는 대상은 GridTile의 GameObject 자체(SetActive false) — 그 자식에 **밟는 고체 콜라이더**가
/// 있어야 구멍이 된다. GridTile 트리거만 있고 아래 통짜 바닥이 받치고 있으면 깨져도 안 떨어진다.
/// </summary>
public class GridTileCollapse : MonoBehaviour
{
    [Header("대상")]
    [SerializeField] GridChallenge grid;

    [Tooltip("Random = 안전 칸 외 N칸(연결성 보장) / AllExceptSafe = 안전 칸 외 전부")]
    [SerializeField] GridCollapseMode mode = GridCollapseMode.Random;

    [Tooltip("Random 전용. 라운드별 깰 칸 수. afterRound 오름차순. 비어 있으면 붕괴 없음")]
    [SerializeField] GridCollapsePhase[] breakCountPhases = new GridCollapsePhase[0];

    [Tooltip("grid.Tiles와 같은 인덱스의 경고 마커(노랑→빨강). 비우면 경고 생략")]
    [SerializeField] SpikeLaneWarnMarker[] warnMarkers = new SpikeLaneWarnMarker[0];

    [Header("순차 파괴 타이밍 (초, 라운드 시작 기준)")]
    [Tooltip("첫 그룹이 깨지는 시각")]
    [SerializeField] float firstBreakDelay = 1.5f;

    [Tooltip("그룹 사이 간격")]
    [SerializeField] float interval = 0.3f;

    [Tooltip("한 번에 깨지는 칸 수")]
    [SerializeField] [Min(1)] int groupSize = 1;

    [Tooltip("칸마다 깨지기 이 시간 전부터 경고(라운드 시작보다 앞으로는 못 감)")]
    [SerializeField] float warnLead = 1f;

    [Tooltip("마지막 파괴 ~ 정산(복구) 최소 여유. 이보다 붙으면 떨어지는 중인 플레이어 위로 칸이 다시 생긴다 — 넘으면 경고 로그")]
    [SerializeField] float minFallClearance = 0.5f;

    [Header("파괴음 (3D) — 기존 Breakable_Destroy 재사용")]
    [SerializeField] float breakSfxMinDistance = 5f;
    [SerializeField] float breakSfxMaxDistance = 50f;
    [SerializeField] AudioRolloffMode breakSfxRolloffMode = AudioRolloffMode.Logarithmic;

    [Header("파편 (TileDebrisUtil 공용)")]
    [Tooltip("FloorTileShards(5 × 1 × 5). 비우면 파편·되감기 생략")]
    [SerializeField] GameObject tileDebrisPrefab = null;
    [SerializeField] float tileDebrisLifetime = 2f;
    [SerializeField] float tileDebrisImpulseMin = 2f;
    [SerializeField] float tileDebrisImpulseMax = 5f;

    [Header("복구 연출 — 파편 되감기 (TileRestoreRewindGroup 공용, 복구음 ReverseTime)")]
    [SerializeField] TileRewindSettings restoreRewind = new TileRewindSettings();

    [Header("시드")]
    [Tooltip("GridChallenge 보드 추첨과 스트림이 겹치지 않게 섞는 값")]
    [SerializeField] int seedSalt = 0x47434C50;

    const int MaxRerolls = 32;

    readonly List<int> _broken = new List<int>();
    readonly List<GameObject> _spawnedDebris = new List<GameObject>();
    TileRestoreRewindGroup _restoreRewind;
    Coroutine _routine;
    bool _subscribed;
    bool _warnedTiming;
    StageNetworkState _netState;

    // 칸 인접(4방향) — 보드 배치에서 한 번 계산. 인덱스는 grid.Tiles 기준
    List<int>[] _neighbors;

    void Awake()
    {
        _restoreRewind = new TileRestoreRewindGroup(this);
        if (grid == null) grid = GetComponent<GridChallenge>() ?? GetComponentInParent<GridChallenge>();
        if (grid == null)
            Debug.LogWarning($"[GridTileCollapse] GridChallenge가 연결되지 않았다 — 붕괴가 돌지 않는다. ({name})", this);
    }

    void OnEnable() => Subscribe();

    // StageNetworkState는 OnEnable 시점에 아직 없을 수 있다(GridChallenge.Start 주석과 같은 이유) — Start가 안전망.
    void Start() => Subscribe();

    void OnDisable()
    {
        Unsubscribe();
        StopSequence();
        RestoreAll(playRewind: false);
        _restoreRewind.ResetAll();
    }

    void Subscribe()
    {
        if (_subscribed || grid == null) return;
        grid.OnRoundStarted.AddListener(HandleRoundStarted);
        grid.OnRoundSettled.AddListener(HandleRoundSettled);
        grid.OnChallengeComplete.AddListener(HandleChallengeEnded);
        grid.OnChallengeCancelled.AddListener(HandleChallengeEnded);

        _netState = StageNetworkState.Instance;
        if (_netState != null)
            _netState.OnDeathReloadStarted += HandleDeathReloadStarted;
        _subscribed = true;
    }

    void Unsubscribe()
    {
        if (!_subscribed) return;
        if (grid != null)
        {
            grid.OnRoundStarted.RemoveListener(HandleRoundStarted);
            grid.OnRoundSettled.RemoveListener(HandleRoundSettled);
            grid.OnChallengeComplete.RemoveListener(HandleChallengeEnded);
            grid.OnChallengeCancelled.RemoveListener(HandleChallengeEnded);
        }
        if (_netState != null)
            _netState.OnDeathReloadStarted -= HandleDeathReloadStarted;
        _netState = null;
        _subscribed = false;
    }

    // ── 라운드 연동 ─────────────────────────────────────────────

    void HandleRoundStarted(int round)
    {
        StopSequence();
        RestoreAll(playRewind: false); // 이전 라운드 정산을 못 받은 경우(레이스) 방어 — 정상 흐름에선 이미 비어 있다

        List<int> order = BuildBreakOrder(round);
        if (order.Count == 0) return;

        WarnIfTimingTight(order.Count);
        _routine = StartCoroutine(SequenceRoutine(order));
    }

    void HandleRoundSettled(int round, bool success)
    {
        StopSequence();
        RestoreAll(playRewind: true);
    }

    void HandleChallengeEnded()
    {
        StopSequence();
        RestoreAll(playRewind: false);
    }

    // 사망 리로드 문이 열리면 씬이 곧 리셋된다 — 암전 중에 계속 깨지 않게 멈추기만 한다.
    void HandleDeathReloadStarted() => StopSequence();

    void StopSequence()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        ResetAllWarnMarkers();
    }

    // ── 깰 칸 결정 (전 머신 동일 — 라운드 시드) ──────────────────

    List<int> BuildBreakOrder(int round)
    {
        var result = new List<int>();
        IReadOnlyList<GridTile> tiles = grid.Tiles;
        if (tiles == null || tiles.Count == 0) return result;

        var candidates = new List<int>();
        for (int i = 0; i < tiles.Count; i++)
            if (tiles[i] != null && !grid.IsSafeTileThisRound(i))
                candidates.Add(i);

        var rng = new System.Random(grid.CurrentRoundSeed ^ seedSalt);

        if (mode == GridCollapseMode.AllExceptSafe)
        {
            result.AddRange(candidates);
        }
        else
        {
            int count = Mathf.Clamp(GetBreakCount(round), 0, candidates.Count);
            if (count == 0) return result;

            EnsureNeighbors(tiles);

            // 남은 칸이 한 덩어리가 아니면 같은 rng로 다시 뽑는다(결정적). 계속 실패하면 한 칸 줄여서.
            bool found = false;
            while (!found && count > 0)
            {
                for (int attempt = 0; attempt < MaxRerolls; attempt++)
                {
                    List<int> pick = PickSubset(candidates, count, rng);
                    if (IsIntactConnected(tiles, pick))
                    {
                        result = pick;
                        found = true;
                        break;
                    }
                }
                if (!found) count--;
            }
        }

        Shuffle(result, rng); // 순서는 위치와 무관하게 랜덤(1행이 다 깨져도 3-5-1-4-2)
        return result;
    }

    int GetBreakCount(int round)
    {
        int count = 0;
        if (breakCountPhases == null) return 0;
        foreach (GridCollapsePhase phase in breakCountPhases)
            if (phase != null && round >= phase.afterRound)
                count = phase.breakCount;
        return count;
    }

    static List<int> PickSubset(List<int> candidates, int count, System.Random rng)
    {
        var pool = new List<int>(candidates);
        var pick = new List<int>(count);
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int k = rng.Next(0, pool.Count);
            pick.Add(pool[k]);
            pool.RemoveAt(k);
        }
        return pick;
    }

    static void Shuffle(List<int> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>
    /// 인접 = 보드 배치에서 가장 가까운 칸 간격(피치)의 1.2배 이내. 대각선(피치 × 1.41)은 제외 —
    /// 칸 모서리 한 점으로는 캡슐이 못 건너가므로 4방향만 길로 친다. 5×5가 아니어도 동작.
    /// </summary>
    void EnsureNeighbors(IReadOnlyList<GridTile> tiles)
    {
        if (_neighbors != null && _neighbors.Length == tiles.Count) return;

        int n = tiles.Count;
        _neighbors = new List<int>[n];
        for (int i = 0; i < n; i++) _neighbors[i] = new List<int>(4);

        float pitch = float.MaxValue;
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                if (tiles[i] != null && tiles[j] != null)
                    pitch = Mathf.Min(pitch, FlatDistance(tiles[i], tiles[j]));
        if (pitch == float.MaxValue || pitch <= 0.001f) return;

        float limit = pitch * 1.2f;
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                if (tiles[i] != null && tiles[j] != null && FlatDistance(tiles[i], tiles[j]) <= limit)
                {
                    _neighbors[i].Add(j);
                    _neighbors[j].Add(i);
                }
    }

    static float FlatDistance(GridTile a, GridTile b)
    {
        Vector3 d = a.transform.position - b.transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    /// <summary>깨지지 않을 칸들이 4방향으로 한 덩어리인가. 안전 칸은 절대 안 깨지므로 자동으로 그 덩어리에 포함된다.</summary>
    bool IsIntactConnected(IReadOnlyList<GridTile> tiles, List<int> broken)
    {
        var brokenSet = new HashSet<int>(broken);
        int start = -1, intactCount = 0;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i] == null || brokenSet.Contains(i)) continue;
            intactCount++;
            if (start < 0) start = i;
        }
        if (start < 0) return false;

        var seen = new HashSet<int> { start };
        var stack = new Stack<int>();
        stack.Push(start);
        while (stack.Count > 0)
        {
            int cur = stack.Pop();
            foreach (int nb in _neighbors[cur])
                if (!brokenSet.Contains(nb) && tiles[nb] != null && seen.Add(nb))
                    stack.Push(nb);
        }
        return seen.Count == intactCount;
    }

    // ── 순차 파괴 ─────────────────────────────────────────────

    void WarnIfTimingTight(int breakTotal)
    {
        if (_warnedTiming || grid == null) return;
        int groups = Mathf.CeilToInt(breakTotal / (float)Mathf.Max(1, groupSize));
        float lastBreak = firstBreakDelay + (groups - 1) * interval;
        if (lastBreak + minFallClearance <= grid.RoundDuration) return;

        _warnedTiming = true;
        Debug.LogWarning($"[GridTileCollapse] 마지막 파괴 {lastBreak:0.##}s + 여유 {minFallClearance:0.##}s가 " +
                         $"라운드 {grid.RoundDuration:0.##}s를 넘는다 — 정산 복구 뒤엔 안 깨지고, 떨어지는 중인 플레이어 위로 칸이 다시 생길 수 있다. " +
                         "firstBreakDelay/interval/groupSize 조정.", this);
    }

    IEnumerator SequenceRoutine(List<int> order)
    {
        int size = Mathf.Max(1, groupSize);
        var events = new List<(float time, int index, bool isBreak, float breakAt)>(order.Count * 2);
        for (int k = 0; k < order.Count; k++)
        {
            float breakAt = Mathf.Max(0f, firstBreakDelay + (k / size) * interval);
            events.Add((Mathf.Max(0f, breakAt - warnLead), order[k], false, breakAt));
            events.Add((breakAt, order[k], true, breakAt));
        }
        // 같은 시각이면 경고 먼저. List.Sort는 불안정 정렬이라 키를 전부 비교한다.
        events.Sort((a, b) =>
        {
            int c = a.time.CompareTo(b.time);
            if (c != 0) return c;
            c = a.isBreak.CompareTo(b.isBreak);
            return c != 0 ? c : a.index.CompareTo(b.index);
        });

        float t0 = Time.time;
        foreach (var e in events)
        {
            float wait = t0 + e.time - Time.time;
            if (wait > 0f) yield return new WaitForSeconds(wait);

            if (e.isBreak) BreakTile(e.index);
            else PlayWarn(e.index, e.breakAt - e.time);
        }
        _routine = null;
    }

    // 노랑→빨강이 꽉 차는 순간 = 깨지는 순간
    void PlayWarn(int index, float duration)
    {
        if (warnMarkers == null || index < 0 || index >= warnMarkers.Length || warnMarkers[index] == null) return;
        warnMarkers[index].PlayWarning(Mathf.Max(0f, duration));
    }

    void BreakTile(int index)
    {
        IReadOnlyList<GridTile> tiles = grid.Tiles;
        if (index < 0 || index >= tiles.Count || tiles[index] == null) return;
        if (grid.IsSafeTileThisRound(index)) return; // 방어 — 안전 칸은 절대 안 깬다

        GameObject tile = tiles[index].gameObject;
        if (!tile.activeSelf) return;

        if (warnMarkers != null && index < warnMarkers.Length && warnMarkers[index] != null)
            warnMarkers[index].ResetWarning();

        _restoreRewind.OnTileBroken(tile);
        SFXManager.Instance?.PlayAtPoint(SFXId.Breakable_Destroy, tile.transform.position,
            breakSfxMinDistance, breakSfxMaxDistance, breakSfxRolloffMode);

        GameObject debris = TileDebrisUtil.BreakTile(tile, tileDebrisPrefab, tileDebrisLifetime,
            tileDebrisImpulseMin, tileDebrisImpulseMax, DebrisSeed(index));
        if (debris != null) _spawnedDebris.Add(debris);

        tile.SetActive(false);
        _broken.Add(index);
    }

    int DebrisSeed(int index) => grid.CurrentRoundSeed ^ seedSalt ^ (index * 0x2545F491);

    // ── 복구 ─────────────────────────────────────────────────

    void RestoreAll(bool playRewind)
    {
        if (grid != null && grid.Tiles != null)
        {
            IReadOnlyList<GridTile> tiles = grid.Tiles;
            foreach (int i in _broken)
            {
                if (i < 0 || i >= tiles.Count || tiles[i] == null) continue;
                GameObject tile = tiles[i].gameObject;
                if (tile.activeSelf) continue;

                // 판정(콜라이더)은 즉시 — 되감기는 그 위에 얹는 순수 연출(TongueController와 동일 규약).
                tile.SetActive(true);
                if (playRewind)
                    _restoreRewind.Play(tile, tileDebrisPrefab, restoreRewind);
            }
        }
        _broken.Clear();

        for (int i = 0; i < _spawnedDebris.Count; i++)
            if (_spawnedDebris[i] != null) Destroy(_spawnedDebris[i]);
        _spawnedDebris.Clear();
    }

    void ResetAllWarnMarkers()
    {
        if (warnMarkers == null) return;
        foreach (SpikeLaneWarnMarker m in warnMarkers)
            m?.ResetWarning();
    }
}
