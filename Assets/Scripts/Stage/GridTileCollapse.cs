using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Random 모드: 박자당 깰 칸 수. afterRound는 GridSafePhase와 같은 규약(라운드 인덱스 0부터, 오름차순).
/// </summary>
[System.Serializable]
public class GridCollapsePhase
{
    [Tooltip("이 라운드 인덱스(0부터)부터 이 단계를 적용")]
    public int afterRound;

    [Tooltip("박자(beatInterval)마다 한 번에 깨지는 칸 수(랜덤 아님). 동시 구멍 ≈ 이 값 × 겹치는 묶음 수" +
             "(brokenLifetime / beatInterval, 0.8·2.5면 3~4묶음). 연결성이 안 맞으면 그 박자만 조용히 줄어든다")]
    public int breakCount = 3;
}

public enum GridCollapseMode
{
    /// <summary>박자마다 안전 칸 외 N칸 파괴 + 일정 시간 뒤 자동 복구. 남은 칸이 8방향 한 덩어리 + 안전 칸 전부 포함을 보장(M.Stage5).</summary>
    Random,
    /// <summary>
    /// T.Boss P3 — Random과 똑같이 박자 파괴·자동 복구를 하다가, 정산 finalSweepLead초 전에 안전 칸을 뺀 남은 칸 전부를
    /// 한 번에 깬다(연결성 무시). 그 시점 이미 깨져 있던 칸은 자동 복구를 취소해 정산까지 구멍으로 둔다.
    /// 정산 후 일괄 복구는 Random과 같다. (2026-09-23 재정의 — 구 "처음부터 전부 한 번에, 복구 없음"은 폐기)
    /// </summary>
    AllExceptSafe,
}

/// <summary>
/// GridChallenge 라운드에 묶인 바닥 붕괴 — M.Stage5 / T.Boss P3 공용.
///
/// [흐름 — 2026-09-22 3차: 계속 파괴 모델] 붕괴 구간 시작(GridChallenge.OnRoundPreReveal — 안전 칸은 계산만,
/// 숨김)부터 **beatInterval 박자마다 breakCount칸 묶음**을 깨고, 깨진 칸은 **brokenLifetime 뒤 자동 복구**한다.
/// 공개 뒤에도 계속되며 **정산 stopBeforeSettle초 전부터는 새로 깨지 않는다**(마지막 자리 잡기는 공정하게).
/// 묶음마다 warnLead초 전 경고. 정산 → settleHoldSeconds 멈춤 → 남은 칸 일괄 복구(되감기). 안전 칸은 절대 안 깨진다.
/// 깨진 칸 위 = 공허 낙하 → Player.fallDeathY 즉사(공용 낙사 경로 — 여기엔 사망 코드 없음).
/// AllExceptSafe(T.Boss P3)는 위 흐름을 그대로 돌고, 정산 finalSweepLead초 전에 안전 칸 외 전부를 한 번에 깬다.
/// 이전 모델(라운드당 N칸 고정 → 공개 전 전부 파괴, 정적 구멍)은 "눈만 있으면 깨는 라운드"라 폐기 —
/// 정적 구멍은 연결성 때문에 동시 12칸이 한계였고, 계속 파괴는 동시 한계를 지키며 라운드당 총 파괴 수를 늘린다.
///
/// [연결성 = 8방향] 대각선으로 칸 모서리를 건너갈 수 있다(사용자 확정). 매 박자 "그 순간 깨져 있는 칸 + 새 묶음"을
/// 빼고 남은 칸이 8방향 한 덩어리 + 안전 칸 전부 포함인지 확인하고, 안 되면 같은 rng로 다시 뽑고, 계속 실패하면 한 칸 줄인다.
///
/// [네트워크] 새 RPC/NV 없음. 라운드 전체 스케줄(파괴·복구 칸과 시각)을 붕괴 시작 때 라운드 시드
/// (GridChallenge.CurrentRoundSeed)와 계획된 안전 칸으로 전 머신이 똑같이 미리 계산한다. 시각은
/// GridChallenge.PreRevealServerTime(절대 ServerTime) 기준이라 이벤트를 늦게 받은 머신도 같은 순간에 깬다.
/// 이동이 Owner 권한이라 낙하는 자기 화면의 붕괴 시각대로 일어나고, Host 판정은 안전 칸(절대 안 깨짐)만 본다.
///
/// [칸 구조] 깨는 대상은 GridTile의 GameObject 자체(SetActive false) — 그 자식에 **밟는 고체 콜라이더**가
/// 있어야 구멍이 된다. GridTile 트리거만 있고 아래 통짜 바닥이 받치고 있으면 깨져도 안 떨어진다.
/// </summary>
public class GridTileCollapse : MonoBehaviour
{
    [Header("대상")]
    [SerializeField] GridChallenge grid;

    [Tooltip("Random = 박자마다 N칸 + 자동 복구(연결성 보장) / AllExceptSafe = Random + 정산 finalSweepLead초 전 안전 칸 외 전부")]
    [SerializeField] GridCollapseMode mode = GridCollapseMode.Random;

    [Tooltip("AllExceptSafe 전용. 정산 이 시간(초) 전에 안전 칸 외 전부를 깬다. 경고는 그보다 warnLead 앞서 뜬다")]
    [SerializeField] float finalSweepLead = 1f;

    [Tooltip("라운드별 박자당 깰 칸 수(Random·AllExceptSafe 공통). afterRound 오름차순. 비어 있으면 박자 파괴 없음")]
    [SerializeField] GridCollapsePhase[] breakCountPhases = new GridCollapsePhase[0];

    [Tooltip("grid.Tiles와 같은 인덱스의 경고 마커(탠저린→진홍). 비우면 경고 생략")]
    [SerializeField] SpikeLaneWarnMarker[] warnMarkers = new SpikeLaneWarnMarker[0];

    [Header("박자 (초) — 붕괴 시작 +warnLead부터 beatInterval마다, 정산 −stopBeforeSettle까지")]
    [Tooltip("묶음마다 깨지기 이 시간 전부터 경고. 첫 묶음은 붕괴 시작 순간 경고가 뜨고 이 시간 뒤에 깨진다")]
    [SerializeField] float warnLead = 0.8f;

    [Tooltip("묶음 사이 간격(박자)")]
    [SerializeField] float beatInterval = 0.8f;

    [Tooltip("깨진 칸이 자동 복구되기까지(초). 판정(콜라이더)은 복구 순간 즉시, 되감기는 그 위 연출")]
    [SerializeField] float brokenLifetime = 2.5f;

    [Tooltip("정산 이 시간 전부터는 새로 깨지 않는다(복구는 계속). 마지막 자리 잡기를 공정하게")]
    [SerializeField] float stopBeforeSettle = 1f;

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

    [Header("복구 연출 — 정산 후 일괄 복구 (TileRestoreRewindGroup 공용, 복구음 ReverseTime)")]
    [SerializeField] TileRewindSettings restoreRewind = new TileRewindSettings();

    [Header("복구 연출 — 라운드 중 자동 복구 (박자마다 나므로 짧고 조용하게)")]
    [SerializeField] TileRewindSettings autoRestoreRewind = new TileRewindSettings
    {
        duration = 0.5f, distanceMin = 1f, distanceMax = 2.5f, stagger = 0.05f, sfxId = SFXId.None,
    };

    [Header("시드")]
    [Tooltip("GridChallenge 보드 추첨과 스트림이 겹치지 않게 섞는 값")]
    [SerializeField] int seedSalt = 0x47434C50;

    const int MaxRerolls = 32;

    enum EventKind { Restore = 0, Break = 1, Warn = 2 } // 같은 시각이면 복구 → 파괴 → 경고 순

    struct CollapseEvent
    {
        public float time;      // 붕괴 시작 기준 초
        public EventKind kind;
        public int index;
        public float breakAt;   // Warn 전용 — 경고가 꽉 차는(깨지는) 시각
    }

    readonly List<int> _broken = new List<int>();
    readonly List<GameObject> _spawnedDebris = new List<GameObject>();
    TileRestoreRewindGroup _restoreRewind;
    Coroutine _routine;
    Coroutine _restoreRoutine;
    bool _subscribed;
    bool _warnedRestore;
    StageNetworkState _netState;

    // 칸 인접(8방향) — 보드 배치에서 한 번 계산. 인덱스는 grid.Tiles 기준
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
        StopRestoreRoutine();
        RestoreAll(playRewind: false);
        _restoreRewind.ResetAll();
    }

    void Subscribe()
    {
        if (_subscribed || grid == null) return;
        grid.OnRoundPreReveal.AddListener(HandleRoundPreReveal);
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
            grid.OnRoundPreReveal.RemoveListener(HandleRoundPreReveal);
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

    void HandleRoundPreReveal(int round)
    {
        StopSequence();
        StopRestoreRoutine();
        RestoreAll(playRewind: false); // 이전 라운드 정산·복구를 못 받은 경우(레이스) 방어 — 정상 흐름에선 이미 비어 있다

        List<CollapseEvent> events = BuildSchedule(round);
        if (events.Count == 0) return;

        _routine = StartCoroutine(SequenceRoutine(events, grid.PreRevealServerTime));
    }

    // 정산 → 멈춤(settleHoldSeconds) → 남은 칸 일괄 복구. 판정(콜라이더)은 복구 시작 순간 즉시, 되감기는 그 위 연출.
    void HandleRoundSettled(int round, bool success)
    {
        StopSequence();
        StopRestoreRoutine();
        WarnIfRestoreTooLong();
        _restoreRoutine = StartCoroutine(RestoreAfterHold(grid != null ? grid.SettleHoldSeconds : 0f));
    }

    IEnumerator RestoreAfterHold(float hold)
    {
        if (hold > 0f) yield return new WaitForSeconds(hold);
        _restoreRoutine = null;
        RestoreAll(playRewind: true);
    }

    void StopRestoreRoutine()
    {
        if (_restoreRoutine != null) { StopCoroutine(_restoreRoutine); _restoreRoutine = null; }
    }

    void HandleChallengeEnded()
    {
        StopSequence();
        StopRestoreRoutine();
        RestoreAll(playRewind: false);
    }

    // 사망 리로드 문이 열리면 씬이 곧 리셋된다 — 암전 중에 계속 깨지 않게 멈추기만 한다.
    void HandleDeathReloadStarted() => StopSequence();

    void StopSequence()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
        ResetAllWarnMarkers();
    }

    // ── 스케줄 (전 머신 동일 — 라운드 시드 + 계획된 안전 칸) ──────────

    /// <summary>
    /// 라운드 전체의 경고·파괴·복구 이벤트(붕괴 시작 기준 초). Random: 박자마다 그 순간 깨져 있는 칸을 반영해
    /// 연결성을 지키며 breakCount칸을 뽑는다. AllExceptSafe: 같은 박자 파괴 뒤 정산 finalSweepLead초 전에 남은 칸 전부.
    /// 정산 이후의 복구 이벤트는 넣지 않는다 — 정산 후 일괄 복구가 처리한다.
    /// </summary>
    List<CollapseEvent> BuildSchedule(int round)
    {
        var events = new List<CollapseEvent>();
        IReadOnlyList<GridTile> tiles = grid.Tiles;
        if (tiles == null || tiles.Count == 0) return events;

        var rng = new System.Random(grid.CurrentRoundSeed ^ seedSalt);
        float first    = Mathf.Max(0f, warnLead);
        float settleAt = grid.PreRevealSeconds + grid.CurrentRoundDuration;
        bool sweep     = mode == GridCollapseMode.AllExceptSafe;
        float sweepAt  = Mathf.Max(0f, settleAt - Mathf.Max(0f, finalSweepLead));

        // 깨진 칸 → 복구 시각
        var restoreAt = new Dictionary<int, float>();

        BuildBeatBreaks(events, restoreAt, tiles, round, rng, first, settleAt,
            sweep ? Mathf.Min(sweepAt, settleAt - Mathf.Max(0f, stopBeforeSettle)) : settleAt - Mathf.Max(0f, stopBeforeSettle));

        if (sweep)
            AddFinalSweep(events, restoreAt, tiles, sweepAt, settleAt);

        return events;
    }

    /// <summary>
    /// AllExceptSafe 마무리: sweepAt에 안전 칸 외 전부. 경고가 뜨는 순간(sweepAt − warnLead) 이후에 복구될 예정이던
    /// 칸은 복구를 취소해 구멍으로 둔다 — 구멍 위에 경고가 뜨거나, 전체 파괴 뒤 자동 복구로 되살아나는 칸이 없게.
    /// </summary>
    void AddFinalSweep(List<CollapseEvent> events, Dictionary<int, float> restoreAt, IReadOnlyList<GridTile> tiles,
        float sweepAt, float settleAt)
    {
        float warnAt = sweepAt - Mathf.Max(0f, warnLead);
        var keepBroken = new HashSet<int>();

        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i] == null || grid.IsSafeTileThisRound(i)) continue;
            if (restoreAt.TryGetValue(i, out float r) && r > warnAt)
                keepBroken.Add(i);
            else
                AddBreak(events, i, sweepAt, float.PositiveInfinity, settleAt);
        }

        if (keepBroken.Count > 0)
            events.RemoveAll(e => e.kind == EventKind.Restore && keepBroken.Contains(e.index) && e.time > warnAt);
    }

    void BuildBeatBreaks(List<CollapseEvent> events, Dictionary<int, float> restoreAt, IReadOnlyList<GridTile> tiles,
        int round, System.Random rng, float first, float settleAt, float lastBreak)
    {
        int perBeat = GetBreakCount(round);
        float beat  = Mathf.Max(0.05f, beatInterval);
        float life  = Mathf.Max(0.05f, brokenLifetime);
        if (perBeat <= 0 || lastBreak < first) return;

        EnsureNeighbors(tiles);

        var candidates = new List<int>(tiles.Count);
        var brokenNow = new HashSet<int>();

        for (float t = first; t <= lastBreak + 0.0001f; t += beat)
        {
            // 연결성은 깨지는 순간(t) 기준, 후보는 경고가 뜨는 순간(t − warnLead)에 이미 멀쩡한 칸만 —
            // 아직 구멍인 칸 위에 경고가 뜨지 않게.
            float warnAt = t - Mathf.Max(0f, warnLead);
            brokenNow.Clear();
            candidates.Clear();
            foreach (var kv in restoreAt)
                if (kv.Value > t) brokenNow.Add(kv.Key);

            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i] == null || grid.IsSafeTileThisRound(i)) continue;
                if (restoreAt.TryGetValue(i, out float r) && r > warnAt) continue;
                candidates.Add(i);
            }

            List<int> pick = PickConnected(tiles, candidates, brokenNow, Mathf.Min(perBeat, candidates.Count), rng);
            foreach (int i in pick)
            {
                AddBreak(events, i, t, t + life, settleAt);
                restoreAt[i] = t + life;
            }
        }
    }

    void AddBreak(List<CollapseEvent> events, int index, float breakAt, float restoreTime, float settleAt)
    {
        events.Add(new CollapseEvent { time = Mathf.Max(0f, breakAt - warnLead), kind = EventKind.Warn, index = index, breakAt = breakAt });
        events.Add(new CollapseEvent { time = breakAt, kind = EventKind.Break, index = index });
        if (restoreTime < settleAt)
            events.Add(new CollapseEvent { time = restoreTime, kind = EventKind.Restore, index = index });
    }

    /// <summary>
    /// candidates에서 count칸을 뽑되, brokenNow + 뽑은 칸을 빼고 남은 칸이 8방향 한 덩어리가 되게 한다
    /// (안전 칸은 후보에 없으니 자동으로 남는 덩어리에 포함된다). 같은 rng로 재추첨, 계속 실패하면 한 칸씩 줄인다.
    /// </summary>
    List<int> PickConnected(IReadOnlyList<GridTile> tiles, List<int> candidates, HashSet<int> brokenNow, int count, System.Random rng)
    {
        while (count > 0)
        {
            for (int attempt = 0; attempt < MaxRerolls; attempt++)
            {
                List<int> pick = PickSubset(candidates, count, rng);
                if (IsIntactConnected(tiles, brokenNow, pick)) return pick;
            }
            count--;
        }
        return new List<int>();
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

    /// <summary>
    /// 인접 = 보드 배치에서 가장 가까운 칸 간격(피치)의 1.5배 이내 — 4방향(피치)과 대각선(피치 × 1.41) 모두.
    /// 대각선 모서리로 캡슐이 건너갈 수 있으므로 8방향을 길로 친다(사용자 확정 2026-09-22). 5×5가 아니어도 동작.
    /// </summary>
    void EnsureNeighbors(IReadOnlyList<GridTile> tiles)
    {
        if (_neighbors != null && _neighbors.Length == tiles.Count) return;

        int n = tiles.Count;
        _neighbors = new List<int>[n];
        for (int i = 0; i < n; i++) _neighbors[i] = new List<int>(8);

        float pitch = float.MaxValue;
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                if (tiles[i] != null && tiles[j] != null)
                    pitch = Mathf.Min(pitch, FlatDistance(tiles[i], tiles[j]));
        if (pitch == float.MaxValue || pitch <= 0.001f) return;

        float limit = pitch * 1.5f;
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

    /// <summary>brokenNow와 pick을 뺀 칸들이 8방향으로 한 덩어리인가.</summary>
    bool IsIntactConnected(IReadOnlyList<GridTile> tiles, HashSet<int> brokenNow, List<int> pick)
    {
        var gone = new HashSet<int>(brokenNow);
        foreach (int i in pick) gone.Add(i);

        int start = -1, intactCount = 0;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i] == null || gone.Contains(i)) continue;
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
                if (!gone.Contains(nb) && tiles[nb] != null && seen.Add(nb))
                    stack.Push(nb);
        }
        return seen.Count == intactCount;
    }

    // ── 재생 ─────────────────────────────────────────────────

    void WarnIfRestoreTooLong()
    {
        if (_warnedRestore || grid == null) return;
        float rewind = restoreRewind.duration + restoreRewind.stagger;
        if (rewind <= grid.RestoreSeconds) return;

        _warnedRestore = true;
        Debug.LogWarning($"[GridTileCollapse] 복구 연출 {rewind:0.##}s(duration+stagger)가 GridChallenge.restoreSeconds " +
                         $"{grid.RestoreSeconds:0.##}s보다 길다 — 휴식 구간까지 파편이 날아다닌다.", this);
    }

    static double NowServerTime()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening ? nm.ServerTime.Time : Time.timeAsDouble;
    }

    IEnumerator SequenceRoutine(List<CollapseEvent> events, double anchorServerTime)
    {
        // List.Sort는 불안정 정렬이라 키를 전부 비교한다.
        events.Sort((a, b) =>
        {
            int c = a.time.CompareTo(b.time);
            if (c != 0) return c;
            c = a.kind.CompareTo(b.kind);
            return c != 0 ? c : a.index.CompareTo(b.index);
        });

        // 전 머신 공통 앵커(붕괴 시작 ServerTime). 없으면(테스트 씬) 지금 기준.
        double t0 = anchorServerTime >= 0.0 ? anchorServerTime : NowServerTime();
        foreach (CollapseEvent e in events)
        {
            float wait = (float)(t0 + e.time - NowServerTime());
            if (wait > 0f) yield return new WaitForSeconds(wait);

            switch (e.kind)
            {
                case EventKind.Break:
                    BreakTile(e.index);
                    break;
                case EventKind.Restore:
                    RestoreTile(e.index, autoRestoreRewind);
                    break;
                case EventKind.Warn:
                    // 이벤트를 늦게 받은 머신은 이미 지난 경고를 남은 시간만큼 짧게 보여준다(깨지는 순간은 동일).
                    float elapsed = (float)(NowServerTime() - t0);
                    PlayWarn(e.index, e.breakAt - Mathf.Max(e.time, elapsed));
                    break;
            }
        }
        _routine = null;
    }

    // 탠저린→진홍이 꽉 차는 순간 = 깨지는 순간
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
        if (!_broken.Contains(index)) _broken.Add(index);
    }

    int DebrisSeed(int index) => grid.CurrentRoundSeed ^ seedSalt ^ (index * 0x2545F491);

    // ── 복구 ─────────────────────────────────────────────────

    /// <summary>한 칸 복구. 판정(콜라이더)은 즉시 — 되감기는 그 위에 얹는 순수 연출(TongueController와 동일 규약).</summary>
    void RestoreTile(int index, TileRewindSettings rewind)
    {
        IReadOnlyList<GridTile> tiles = grid != null ? grid.Tiles : null;
        _broken.Remove(index);
        if (tiles == null || index < 0 || index >= tiles.Count || tiles[index] == null) return;

        GameObject tile = tiles[index].gameObject;
        if (tile.activeSelf) return;

        tile.SetActive(true);
        if (rewind != null)
            _restoreRewind.Play(tile, tileDebrisPrefab, rewind);
    }

    void RestoreAll(bool playRewind)
    {
        var pending = new List<int>(_broken);
        foreach (int i in pending)
            RestoreTile(i, playRewind ? restoreRewind : null);
        _broken.Clear();

        _spawnedDebris.RemoveAll(d => d == null);
        for (int i = 0; i < _spawnedDebris.Count; i++)
            Destroy(_spawnedDebris[i]);
        _spawnedDebris.Clear();
    }

    void ResetAllWarnMarkers()
    {
        if (warnMarkers == null) return;
        foreach (SpikeLaneWarnMarker m in warnMarkers)
            m?.ResetWarning();
    }
}
