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
/// </summary>
public class WallLineRandomizer : MonoBehaviour
{
    [Header("벽 이동")]
    [Tooltip("체크 시: AdvancingWall 반복 발동.")]
    [SerializeField] bool useWall = true;

    [Tooltip("씬 시작 후 첫 사이클까지 대기(초). 1회만.")]
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
    // 서로 다른 랜덤 시퀀스를 갖도록, WindTrap._registry/GetHierarchyPath와 동일한 방식으로
    // 계층 경로 정렬 index를 자동 배정한다(Host/Client가 같은 씬 계층을 가지므로 항상 같은 순서 —
    // Awake 호출 순서 대신 경로 정렬을 쓰는 이유도 WindTrap과 동일: 늦은 활성화로 Awake 순서가
    // 갈릴 수 있어서).
    static bool _registryBuilt = false;
    static int  _aliveCount = 0;
    int _netIndex = -1;

    static void EnsureRegistryBuilt()
    {
        if (_registryBuilt) return;
        _registryBuilt = true;

        WallLineRandomizer[] all = FindObjectsByType<WallLineRandomizer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OrderBy(a => GetHierarchyPath(a.transform), StringComparer.Ordinal)
            .ToArray();

        for (int i = 0; i < all.Length; i++)
            all[i]._netIndex = i;
    }

    static string GetHierarchyPath(Transform t)
    {
        string path = t.name + "#" + t.GetSiblingIndex().ToString("D4");
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "#" + t.GetSiblingIndex().ToString("D4") + "/" + path;
        }
        return path;
    }

    void Awake()
    {
        _wall       = GetComponent<AdvancingWall>();
        _colorWalls = GetComponentsInChildren<ColorWall>(true);

        _aliveCount++;
        EnsureRegistryBuilt();
    }

    void OnDestroy()
    {
        _aliveCount--;
        // 씬의 마지막 인스턴스가 사라지면 다음 씬 로드 시 재구성되게 플래그 리셋
        if (_aliveCount <= 0)
        {
            _aliveCount = 0;
            _registryBuilt = false;
        }
    }

    void Start()
    {
        bool hasWall  = useWall && _wall != null;
        bool hasColor = useColor && _colorWalls != null && _colorWalls.Length > 0;

        if (hasWall && hasColor)
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
