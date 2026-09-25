using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 1개 부모 오브젝트에 붙여서 같은 라인의 벽 전체를 한꺼번에 제어.
///
/// [구조]
///  부모 (이 컴포넌트 + AdvancingWall)
///  └─ 자식 × N (각 ColorWall)
///
/// [벽]
///  고정 전진 거리, 후퇴 비율·전진/후퇴 소요 시간은 인스펙터 설정.
///  사이클 사이 대기는 wallIntervalMin~Max 사이 랜덤.
///
/// [색 — 벽과 동기]
///  colorPool에서만 논리 색 랜덤.
///  랜덤 색 → colorLeadBeforeWall 초 대기 → 벽 한 사이클(전진·후퇴 완료) 동안 색 유지
///  → 벽이 제자리(후퇴)까지 끝나는 시점에 default 색으로 복귀.
///  그 다음 wallInterval 랜덤 대기 동안은 계속 default.
///
/// [시작 시점]
///  startOnAwake=true(기본, T.Boss 자유런)면 Start()에서 바로 Begin().
///  게이트 이후에 돌리려면 startOnAwake=false 후 StageStartGate.OnCountdownComplete → Begin()
///  (T.Stage1 볼더 BeginSpawning과 동일). Host/Client 모두 로컬로 이벤트가 오므로 양쪽 Begin().
/// </summary>
public class WallLineRandomizer : MonoBehaviour
{
    [Header("벽 이동")]
    [Tooltip("체크 시: AdvancingWall 반복 발동.")]
    [SerializeField] bool useWall = true;

    [Tooltip("켜면 씬 로드 직후 사이클 시작. 끄면 Begin()을 외부에서 호출할 때까지 대기.\n" +
             "StageStartGate 이후 시작: 끄고 OnCountdownComplete → Begin() 연결.")]
    [SerializeField] bool startOnAwake = true;

    [Tooltip("Begin() 이후 첫 사이클까지 대기(초). 1회만. 게이트에 연결했으면 0~2.")]
    [SerializeField] float wallFirstDelay = 0f;

    [Tooltip("한 사이클(색 복귀·이동 완료) 후 다음 사이클까지 대기(초) — 매번 이 범위에서 랜덤.")]
    [SerializeField] float wallIntervalMin = 4f;
    [SerializeField] float wallIntervalMax = 10f;

    [Tooltip("전진 거리(m)")]
    [SerializeField] float wallAdvance = 5f;

    [Tooltip("후퇴 거리 = 전진 × 비율 (0~1). 1 = 전진만큼 후퇴(제자리 복귀).")]
    [SerializeField, Range(0f, 1f)] float wallRetreatRatio = 1f;

    [Tooltip("전진에 걸리는 시간(초). 0이면 AdvancingWall의 moveDuration 사용.")]
    [SerializeField] float wallAdvanceMoveDuration = 0f;

    [Tooltip("후퇴에 걸리는 시간(초). 0이면 AdvancingWall의 returnDuration 사용.")]
    [SerializeField] float wallReturnMoveDuration = 0f;

    [Header("계단식 압박 (T.Boss P4)")]
    [Tooltip("켜면 아래 규칙으로 돈다(끄면 위 '벽 이동' 값으로 기존 루틴 — T.Stage1·T.Stage3는 끈 채로 둘 것).\n" +
             "· 돌진 = 남은 바닥 한 변 × surgeRatio, 속도 surgeSpeed로 일정(거리가 줄면 전진 시간도 준다)\n" +
             "· 후퇴할 때마다 시작 지점이 한 칸씩 안으로 — 한 칸 = 남은 거리 ÷ 남은 시간에 들어갈 횟수\n" +
             "· Phase 시작 + squeezeDuration 초에 정확히 squeezeTotalDistance 도착\n" +
             "· 색 일치로 멈춰도 계단은 내려간다\n" +
             "wallFirstDelay·wallAdvance·wallRetreatRatio·wallAdvanceMoveDuration은 이 모드에서 안 쓴다.\n" +
             "wallIntervalMin/Max·wallReturnMoveDuration·colorLeadBeforeWall·colorPool은 그대로 쓴다.")]
    [SerializeField] bool useSqueeze = false;

    [Tooltip("시작 위치에서 최종 위치까지 이 벽이 다가올 총 거리(m). 100×100 → 10×10이면 45.")]
    [SerializeField] float squeezeTotalDistance = 45f;

    [Tooltip("Phase 시작 기준 이 시각(초)에 최종 위치에 정확히 닿는다. PhaseSurviveChallenge.targetTime과 맞출 것.")]
    [SerializeField] float squeezeDuration = 90f;

    [Tooltip("시작 시 플레이어가 밟을 수 있는 바닥 한 변(m). 남은 바닥 = 이 값 − 2 × 지금까지 다가온 거리.")]
    [SerializeField] float squeezeFloorSize = 100f;

    [Tooltip("돌진 거리 = 남은 바닥 한 변 × 이 비율. 0.9면 100→90, 90→81 …")]
    [SerializeField, Range(0f, 1f)] float squeezeSurgeRatio = 0.9f;

    [Tooltip("돌진 속도(m/s) — 일정. 30이면 90을 3초, 18을 0.6초에 간다.")]
    [SerializeField] float squeezeSurgeSpeed = 30f;

    [Tooltip("Phase 시작 후 첫 돌진까지 대기(초) — 벽마다 이 범위에서 시드 랜덤. 네 벽이 동시에 출발하지 않게 하는 값.")]
    [SerializeField] float squeezeFirstDelayMin = 3f;
    [SerializeField] float squeezeFirstDelayMax = 7f;

    [Header("색상")]
    [Tooltip("체크 시: colorPool에서만 랜덤 논리 색. 벽과 동기.")]
    [SerializeField] bool useColor = true;

    [Tooltip("씬 시작 후 첫 색 경고까지 추가 대기(초). 1회만. (wallFirstDelay 이후)")]
    [SerializeField] float colorFirstDelay = 0f;

    [Tooltip("랜덤 색 표시 후, 벽 전진 시작까지 대기(초).")]
    [SerializeField] float colorLeadBeforeWall = 1.5f;

    [Tooltip("랜덤으로 뽑을 논리 색 목록. 비우면 Default 제외 전체.")]
    [SerializeField] ColorWall.WallColorType[] colorPool = new ColorWall.WallColorType[0];

    [Header("네트워크 시드 (Host/Client 동기화)")]
    [Tooltip("보통은 안 건드려도 됨 — 인스턴스 구분은 씬 계층 경로로 자동 처리됨(_netIndex).\n" +
             "그래도 특정 벽에 의도적으로 같은/다른 패턴을 강제하고 싶을 때만 값 지정.")]
    [SerializeField] int cycleSeedSalt = 0;

    // 다른 파일의 salt: 0x050AD5E7, 0x43484153, 0x5716D000, 0x4D4F5554, 0x5B1DE000, 0x52554E52, 0x434F4C57(ColorWall)
    const int WallLineSeedBaseSalt = unchecked((int)0x574C525A);

    AdvancingWall _wall;
    ColorWall[]   _colorWalls;

    Coroutine _mainCoroutine;
    int       _cycleCount;

    // ── 인스턴스 구분용 안정적 index (씬 편집 없이 자동 배정) ─────────────
    // 같은 seed 재료(NetworkSessionData.Seed 등)를 쓰는 WallLineRandomizer가 씬에 여럿 있어도
    // 서로 다른 랜덤 시퀀스를 갖도록 SceneStableRegistry로 계층 경로 정렬 index를 배정한다.
    static readonly SceneStableRegistry<WallLineRandomizer> _registry = new SceneStableRegistry<WallLineRandomizer>();
    int _netIndex = -1;

    void Awake()
    {
        _wall       = GetComponent<AdvancingWall>();
        _colorWalls = GetComponentsInChildren<ColorWall>(true);
        _netIndex   = _registry.Register(this);
    }

    void OnDestroy() => _registry.Unregister(this, _netIndex);

    void Start()
    {
        if (startOnAwake)
            Begin();
    }

    // 계단식 압박은 Phase가 켜줄 때마다(PhaseManager objectsToEnable) 새 Phase 앵커로 시작해야 한다.
    // Start는 첫 활성화에 한 번뿐이라, 씬 로드 때 켜져 있다가 꺼진 뒤 다시 켜지면 영영 안 돈다.
    // 기존 모드는 동작을 바꾸지 않도록 이 경로를 타지 않는다.
    void OnEnable()
    {
        if (useSqueeze && startOnAwake)
            Begin();
    }

    /// <summary>
    /// 사이클 시작. StageStartGate.OnCountdownComplete에 연결.
    /// startOnAwake=false일 때 사용. 이미 돌고 있으면 무시.
    /// </summary>
    public void Begin()
    {
        if (_mainCoroutine != null) return;
        if (!isActiveAndEnabled) return;

        bool hasWall  = useWall && _wall != null;
        bool hasColor = useColor && _colorWalls != null && _colorWalls.Length > 0;

        if (useSqueeze && hasWall)
            _mainCoroutine = StartCoroutine(SqueezeRoutine(hasColor));
        else if (hasWall && hasColor)
            _mainCoroutine = StartCoroutine(SyncedRoutine());
        else if (hasWall)
            _mainCoroutine = StartCoroutine(WallOnlyRoutine());
        else if (hasColor)
            _mainCoroutine = StartCoroutine(ColorOnlyRoutine());
    }

    void OnDisable()
    {
        if (_mainCoroutine != null)
        {
            StopCoroutine(_mainCoroutine);
            _mainCoroutine = null;
        }
    }

    /// <summary>
    /// 이번 사이클의 시드를 생성하고 카운터를 올린다. 전 머신이 같은 순서로 이 루틴을 도니
    /// _cycleCount가 항상 같은 시점에 같은 값이 되고, 결과적으로 항상 같은 System.Random을 만든다
    /// (WindTrap.WindCycle / MouthController.AutoCycle과 동일 관례 — RPC 없이 시드만 맞추면 됨).
    /// _netIndex를 섞어 씬에 여러 인스턴스가 있어도 서로 다른 시퀀스가 나오게 한다
    /// (안 섞으면 같은 NetworkSessionData.Seed를 공유하는 인스턴스끼리 동일한 색·간격이 나옴 —
    /// 2026-08 실기 테스트에서 실제로 겪은 버그, TStageNetworkBoard.md 참고).
    /// 로컬 System.Random 인스턴스를 쓰는 이유: 한 사이클에서 간격+색 두 값을 뽑아야 하는데,
    /// 전역 UnityEngine.Random을 쓰면 그 사이 yield(대기)에서 다른 시스템이 Random을 호출해
    /// 전역 상태가 오염될 수 있다(GameSessionColorDistribution.Distribute와 동일 이유).
    /// </summary>
    System.Random NewCycleRng()
    {
        int seed = NetworkSessionData.Seed ^ WallLineSeedBaseSalt ^ (_netIndex * unchecked((int)0x9E3779B9))
                 ^ cycleSeedSalt ^ (_cycleCount * 0x2545F491);
        _cycleCount++;
        return new System.Random(seed);
    }

    float NextWallGap(System.Random rng)
    {
        float a = Mathf.Min(wallIntervalMin, wallIntervalMax);
        float b = Mathf.Max(wallIntervalMin, wallIntervalMax);
        return a + (float)rng.NextDouble() * (b - a);
    }

    IEnumerator SyncedRoutine()
    {
        ColorWall.WallColorType[] pool = BuildPool();

        if (wallFirstDelay > 0f) yield return new WaitForSeconds(wallFirstDelay);
        if (colorFirstDelay > 0f) yield return new WaitForSeconds(colorFirstDelay);

        bool firstCycle = true;

        while (true)
        {
            while (_wall.IsMoving) yield return null;

            // 간격+색을 한 사이클 시드에서 연달아 뽑는다(순서 고정 — 두 값 사이 yield 없음).
            System.Random rng = NewCycleRng();
            float gap = firstCycle ? 0f : NextWallGap(rng);
            ColorWall.WallColorType pick = pool[rng.Next(0, pool.Length)];

            if (!firstCycle && gap > 0f) yield return new WaitForSeconds(gap);
            firstCycle = false;

            ApplyColor(pick);

            float lead = Mathf.Max(0f, colorLeadBeforeWall);
            if (lead > 0f) yield return new WaitForSeconds(lead);

            _wall.RunOnce(
                wallAdvance,
                wallRetreatRatio,
                wallAdvanceMoveDuration,
                wallReturnMoveDuration);

            while (_wall.IsMoving) yield return null;

            ResetColors();
        }
    }

    IEnumerator WallOnlyRoutine()
    {
        if (wallFirstDelay > 0f) yield return new WaitForSeconds(wallFirstDelay);

        bool first = true;
        while (true)
        {
            while (_wall.IsMoving) yield return null;

            System.Random rng = NewCycleRng();
            float gap = first ? 0f : NextWallGap(rng);

            if (!first && gap > 0f) yield return new WaitForSeconds(gap);
            first = false;

            _wall.RunOnce(
                wallAdvance,
                wallRetreatRatio,
                wallAdvanceMoveDuration,
                wallReturnMoveDuration);

            while (_wall.IsMoving) yield return null;
        }
    }

    // ── 계단식 압박 ─────────────────────────────────────────────
    //
    // [결정론] 사이클 시각·돌진 거리·한 칸 크기를 전부 "계획 시각"(Phase 앵커 + 시드로 뽑은
    // 첫 대기·간격 + 고정 이동 시간)만으로 계산한다. 실제 시각이나 색 멈춤 여부는 계산에
    // 들어가지 않으므로 Host/Client가 같은 계단을 밟는다. 멈춤 때문에 늦어진 머신은 다음 계획
    // 시각까지 기다렸다가 같은 값으로 이어간다.

    // PhaseStartServerTime이 전파될 때까지 기다리는 한도 — SalivaHazard와 같은 값·같은 폴백.
    const float AnchorWaitTimeout = 3f;

    double _squeezeAnchor;

    IEnumerator SqueezeRoutine(bool hasColor)
    {
        ColorWall.WallColorType[] pool = hasColor ? BuildPool() : null;

        yield return ResolveSqueezeAnchor();
        double end = _squeezeAnchor + squeezeDuration;

        float lead     = pool != null ? Mathf.Max(0f, colorLeadBeforeWall) : 0f;
        float fixedDur = _wall.TelegraphDuration + _wall.ReturnDelay + Mathf.Max(0.05f, wallReturnMoveDuration);
        float retDur   = Mathf.Max(0.05f, wallReturnMoveDuration);
        float avgGap   = (wallIntervalMin + wallIntervalMax) * 0.5f;

        // 첫 대기는 벽마다 다르다(_netIndex가 시드에 섞임) — 네 벽이 같은 순간에 출발하지 않게.
        System.Random firstRng = NewCycleRng();
        float firstMin = Mathf.Min(squeezeFirstDelayMin, squeezeFirstDelayMax);
        float firstMax = Mathf.Max(squeezeFirstDelayMin, squeezeFirstDelayMax);
        double t = _squeezeAnchor + firstMin + firstRng.NextDouble() * (firstMax - firstMin);

        double lastEnd = t;
        float  reached = 0f;

        while (true)
        {
            // 간격+색을 한 사이클 시드에서 연달아 뽑는다(SyncedRoutine과 같은 순서 고정 원칙).
            System.Random rng = NewCycleRng();
            float gap = NextWallGap(rng);
            ColorWall.WallColorType pick = pool != null ? pool[rng.Next(0, pool.Length)] : default;

            float surge = SurgeDistance(reached);
            float busy  = lead + SurgeDuration(surge) + fixedDur;
            if (t + busy > end) break;

            int   n      = FittingCycles(t, end, reached, lead, fixedDur, avgGap);
            float target = reached + (squeezeTotalDistance - reached) / n;

            while (ServerNow() < t) yield return null;
            while (_wall.IsMoving || _wall.IsPausedByColor) yield return null;

            if (pool != null) ApplyColor(pick);
            if (lead > 0f) yield return new WaitForSeconds(lead);
            // 경고색 대기 중에 색을 맞추면 벽이 서 있어도 멈춤이 걸린다 — 그대로 부르면 RunSurge가
            // 무시돼 원점이 한 칸 뒤처지고, 마지막 회차면 최종 위치가 이 머신만 모자란다.
            while (_wall.IsPausedByColor) yield return null;

            _wall.RunSurge(surge, target, SurgeDuration(surge), retDur);
            reached = target;

            while (_wall.IsMoving) yield return null;
            if (pool != null) ResetColors();

            lastEnd = t + busy;
            t       = lastEnd + gap;
        }

        if (pool != null) ResetColors();

        // 마무리 — 돌진 한 번이 더 들어갈 시간이 없으면 남은 거리를 종료 시각 정각에 닿게 천천히 붙인다.
        // RunOnceSynced는 서버 시각으로 진행도를 잡아서 늦게 들어온 머신도 같은 순간에 도착한다.
        float rest = squeezeTotalDistance - reached;
        if (rest > 0.001f)
        {
            while (_wall.IsMoving || _wall.IsPausedByColor) yield return null;
            _wall.RunOnceSynced(rest, (float)(end - lastEnd), lastEnd);
        }

        _mainCoroutine = null;
    }

    float SurgeDistance(float reached) =>
        Mathf.Max(0f, squeezeFloorSize - 2f * reached) * squeezeSurgeRatio;

    float SurgeDuration(float surge) =>
        Mathf.Max(0.05f, surge / Mathf.Max(0.01f, squeezeSurgeSpeed));

    /// <summary>
    /// 지금(계획 시각 t)부터 종료까지 돌진이 몇 번 들어가는지 — 한 칸을 "남은 거리 ÷ 이 값"으로 잡는다.
    /// 칸 크기에 따라 돌진 거리가 줄고 전진 시간도 주므로, n마다 끝까지 굴려 보고 들어가는 최대 n을 쓴다.
    /// 간격은 평균값으로 가정한다 — 매 회차 다시 계산하므로 실제 간격과의 차이는 다음 회차가 흡수한다.
    /// </summary>
    int FittingCycles(double t, double end, float reached, float lead, float fixedDur, float avgGap)
    {
        const int MaxCycles = 64;
        int best = 1;
        for (int n = 2; n <= MaxCycles; n++)
        {
            float  step = (squeezeTotalDistance - reached) / n;
            float  r    = reached;
            double tt   = t;
            bool   fits = true;
            for (int i = 0; i < n; i++)
            {
                double busy = lead + SurgeDuration(SurgeDistance(r)) + fixedDur;
                if (tt + busy > end) { fits = false; break; }
                r  += step;
                tt += busy + avgGap;
            }
            if (!fits) break;
            best = n;
        }
        return best;
    }

    /// <summary>
    /// 이 Phase의 시작 서버 시각을 잡는다. PhaseManager.EnterPhase()는 objectsToEnable을 켠 다음에
    /// MarkAndSyncPhase()를 찍으므로 한 프레임 양보해야 Host가 직전 Phase의 낡은 앵커를 안 잡는다
    /// (SalivaHazard.ResolveFirstWindow와 같은 이유·같은 폴백).
    /// </summary>
    IEnumerator ResolveSqueezeAnchor()
    {
        yield return null;

        float waited = 0f;
        while (waited < AnchorWaitTimeout)
        {
            var sns = StageNetworkState.Instance;
            if (sns != null && sns.PhaseStartServerTime > 0d)
            {
                _squeezeAnchor = sns.PhaseStartServerTime;
                yield break;
            }
            waited += Time.deltaTime;
            yield return null;
        }

        _squeezeAnchor = ServerNow();
    }

    static double ServerNow()
    {
        var nm = Unity.Netcode.NetworkManager.Singleton;
        return nm != null && nm.IsListening ? nm.ServerTime.Time : Time.timeAsDouble;
    }

    /// <summary>벽 없음: 사이클 간격만 랜덤, 색은 colorLeadBeforeWall 초 유지 후 default.</summary>
    IEnumerator ColorOnlyRoutine()
    {
        ColorWall.WallColorType[] pool = BuildPool();

        if (colorFirstDelay > 0f) yield return new WaitForSeconds(colorFirstDelay);

        bool first = true;
        while (true)
        {
            System.Random rng = NewCycleRng();
            float gap = first ? 0f : NextWallGap(rng);
            ColorWall.WallColorType pick = pool[rng.Next(0, pool.Length)];

            if (!first && gap > 0f) yield return new WaitForSeconds(gap);
            first = false;

            ApplyColor(pick);

            float hold = Mathf.Max(0.05f, colorLeadBeforeWall);
            yield return new WaitForSeconds(hold);

            ResetColors();
        }
    }

    void ApplyColor(ColorWall.WallColorType pick)
    {
        foreach (ColorWall cw in _colorWalls)
            if (cw != null) cw.SetColor(pick);
    }

    void ResetColors()
    {
        foreach (ColorWall cw in _colorWalls)
            if (cw != null) cw.ResetToDefault();
    }

    ColorWall.WallColorType[] BuildPool()
    {
        ColorWall.WallColorType[] raw;

        if (colorPool != null && colorPool.Length > 0)
        {
            raw = colorPool;
        }
        else
        {
            // colorPool 비어있으면 Default 제외 전체 enum
            var list = new List<ColorWall.WallColorType>();
            foreach (ColorWall.WallColorType v in System.Enum.GetValues(typeof(ColorWall.WallColorType)))
            {
                if (v != ColorWall.WallColorType.Default)
                    list.Add(v);
            }
            raw = list.Count > 0 ? list.ToArray() : new[] { ColorWall.WallColorType.Black };
        }

        // 비활성 플레이어 색 제거 (GameSession 없으면 raw 그대로)
        return GameSessionWallColorRemap.FilterPool(raw);
    }

    [ContextMenu("테스트: 색상 즉시 발동")]
    void Debug_FireColor()
    {
        if (_colorWalls == null || _colorWalls.Length == 0) return;
        ColorWall.WallColorType[] pool = BuildPool();
        ApplyColor(pool[UnityEngine.Random.Range(0, pool.Length)]);
    }

    [ContextMenu("테스트: 색상 복귀")]
    void Debug_ResetColor() => ResetColors();

    [ContextMenu("테스트: 벽 즉시 발동")]
    void Debug_FireWall()
    {
        if (_wall == null) return;
        _wall.RunOnce(wallAdvance, wallRetreatRatio, wallAdvanceMoveDuration, wallReturnMoveDuration);
    }
}
