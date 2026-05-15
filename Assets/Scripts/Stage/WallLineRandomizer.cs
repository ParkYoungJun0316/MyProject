using System.Collections;
using System.Collections.Generic;
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

    AdvancingWall _wall;
    ColorWall[]   _colorWalls;

    Coroutine _mainCoroutine;

    void Awake()
    {
        _wall       = GetComponent<AdvancingWall>();
        _colorWalls = GetComponentsInChildren<ColorWall>(true);
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

    float NextWallGap()
    {
        float a = Mathf.Min(wallIntervalMin, wallIntervalMax);
        float b = Mathf.Max(wallIntervalMin, wallIntervalMax);
        return Random.Range(a, b);
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

            if (!firstCycle)
            {
                float gap = NextWallGap();
                if (gap > 0f) yield return new WaitForSeconds(gap);
            }
            firstCycle = false;

            ColorWall.WallColorType pick = pool[Random.Range(0, pool.Length)];
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

            if (!first)
            {
                float gap = NextWallGap();
                if (gap > 0f) yield return new WaitForSeconds(gap);
            }
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
            if (!first)
            {
                float gap = NextWallGap();
                if (gap > 0f) yield return new WaitForSeconds(gap);
            }
            first = false;

            ColorWall.WallColorType pick = pool[Random.Range(0, pool.Length)];
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
        if (colorPool != null && colorPool.Length > 0)
            return colorPool;

        var list = new List<ColorWall.WallColorType>();
        foreach (ColorWall.WallColorType v in System.Enum.GetValues(typeof(ColorWall.WallColorType)))
        {
            if (v != ColorWall.WallColorType.Default)
                list.Add(v);
        }
        return list.Count > 0 ? list.ToArray() : new[] { ColorWall.WallColorType.Black };
    }

    [ContextMenu("테스트: 색상 즉시 발동")]
    void Debug_FireColor()
    {
        if (_colorWalls == null || _colorWalls.Length == 0) return;
        ColorWall.WallColorType[] pool = BuildPool();
        ApplyColor(pool[Random.Range(0, pool.Length)]);
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
