using UnityEngine;

/// <summary>
/// ColorWall 관련 GameSession 연동 공통 유틸.
///
/// [RemapSchedule — colorSchedule 재매핑]
///  Black / White / Default → 그대로 유지 (플레이어 색이 아님)
///  Blue / Purple / Green / Yellow → 활성 플레이어 색으로 재배정 (GameSessionColorDistribution)
///  atSeconds 는 그대로 유지. 색만 재배정.
///
/// [FilterPool — WallLineRandomizer colorPool 필터]
///  비활성 플레이어 색 제거. Black / White 는 항상 유지.
///  풀이 완전히 비면 Black 1개 fallback.
///
/// [fallback]
///  GameSession 없으면 원본 그대로 반환 (에디터 단독 테스트).
///
/// [사용처]
///  ColorWall.StartSchedule()
///  WallLineRandomizer.BuildPool()
/// </summary>
public static class GameSessionWallColorRemap
{
    // ── 변환 유틸 ────────────────────────────────────────────────

    /// <summary>WallColorType이 플레이어 고유색(Blue/Purple/Green/Yellow) 슬롯인지.</summary>
    public static bool IsPlayerColor(ColorWall.WallColorType w)
    {
        return w == ColorWall.WallColorType.Blue   ||
               w == ColorWall.WallColorType.Purple ||
               w == ColorWall.WallColorType.Green  ||
               w == ColorWall.WallColorType.Yellow;
    }

    /// <summary>WallColorType → PlayerColorType. 플레이어색 아니면 Common.</summary>
    public static PlayerColorType ToPlayerColor(ColorWall.WallColorType w)
    {
        switch (w)
        {
            case ColorWall.WallColorType.Blue:   return PlayerColorType.Blue;
            case ColorWall.WallColorType.Purple: return PlayerColorType.Purple;
            case ColorWall.WallColorType.Green:  return PlayerColorType.Green;
            case ColorWall.WallColorType.Yellow: return PlayerColorType.Yellow;
            default:                             return PlayerColorType.Common;
        }
    }

    /// <summary>PlayerColorType → WallColorType. 해당 없으면 Default.</summary>
    public static ColorWall.WallColorType ToWallColor(PlayerColorType p)
    {
        switch (p)
        {
            case PlayerColorType.Blue:   return ColorWall.WallColorType.Blue;
            case PlayerColorType.Purple: return ColorWall.WallColorType.Purple;
            case PlayerColorType.Green:  return ColorWall.WallColorType.Green;
            case PlayerColorType.Yellow: return ColorWall.WallColorType.Yellow;
            default:                     return ColorWall.WallColorType.Default;
        }
    }

    // ── 핵심 API ─────────────────────────────────────────────────

    /// <summary>
    /// colorSchedule의 플레이어 색 슬롯을 GameSessionColorDistribution으로 재매핑한
    /// 새 배열을 반환한다.
    ///
    /// · atSeconds 는 원본 그대로 유지.
    /// · Black / White / Default 이벤트는 변경 없음.
    /// · 플레이어 색 슬롯 수만큼 Distribute() 호출 → 활성 색으로 채움.
    /// · GameSession 없으면 schedule 원본 반환 (fallback).
    /// · rng: 여분 슬롯 배정까지 결정론적으로 고정하려면 반드시 넘길 것 (예: ColorWall이
    ///   NetworkSessionData.Seed 기반 System.Random을 생성해 전달). null이면 GameSessionColorDistribution이
    ///   UnityEngine.Random을 써서 Host/Client가 다른 결과를 낼 수 있다(로컬 전용 호출자 하위호환용).
    /// </summary>
    public static ColorWall.ColorChangeEvent[] RemapSchedule(ColorWall.ColorChangeEvent[] schedule, System.Random rng = null)
    {
        if (schedule == null || schedule.Length == 0)
            return schedule;

        // fallback: GameSession 없으면 원본 그대로
        if (GameSession.Instance == null)
            return schedule;

        // 플레이어 색 슬롯 수 파악
        int slotCount = 0;
        foreach (var evt in schedule)
            if (IsPlayerColor(evt.color)) slotCount++;

        // 플레이어 색 슬롯이 없으면 원본 그대로 (Black/White/Default만 있는 벽)
        if (slotCount == 0)
            return schedule;

        // 활성 색 기준 균등 분배 (rng 전달 — 안 넘기면 UnityEngine.Random이라 머신마다 갈라짐)
        PlayerColorType[] distributed = GameSessionColorDistribution.Distribute(slotCount, rng);

        // 원본 순서 유지, 플레이어 색 슬롯만 재매핑
        var result = new ColorWall.ColorChangeEvent[schedule.Length];
        int slotIdx = 0;
        for (int i = 0; i < schedule.Length; i++)
        {
            ColorWall.WallColorType remapped = IsPlayerColor(schedule[i].color)
                ? ToWallColor(distributed[slotIdx++])
                : schedule[i].color;

            result[i] = new ColorWall.ColorChangeEvent
            {
                atSeconds = schedule[i].atSeconds,
                color     = remapped,
            };
        }

        return result;
    }

    // ── WallLineRandomizer용 ─────────────────────────────────────

    /// <summary>
    /// colorPool에서 비활성 플레이어 색을 제거한 배열을 반환한다.
    ///
    /// · Black / White → 항상 유지 (플레이어 색이 아님)
    /// · Blue / Purple / Green / Yellow → GameSession.IsColorActive()가 true인 경우만 유지
    /// · Default → 제거 (WallLineRandomizer 원본 로직과 동일)
    /// · GameSession 없으면 pool 원본 반환 (fallback)
    /// · 필터 후 풀이 비면 Black 1개 반환
    /// </summary>
    public static ColorWall.WallColorType[] FilterPool(ColorWall.WallColorType[] pool)
    {
        if (pool == null || pool.Length == 0)
            return pool;

        if (GameSession.Instance == null)
            return pool;

        var result = new System.Collections.Generic.List<ColorWall.WallColorType>(pool.Length);
        foreach (ColorWall.WallColorType w in pool)
        {
            if (IsPlayerColor(w))
            {
                // 플레이어 색 → 활성일 때만 유지
                if (GameSession.Instance.IsColorActive(ToPlayerColor(w)))
                    result.Add(w);
            }
            else if (w != ColorWall.WallColorType.Default)
            {
                // Black / White → 항상 유지 / Default → 제거
                result.Add(w);
            }
        }

        return result.Count > 0
            ? result.ToArray()
            : new[] { ColorWall.WallColorType.Black };
    }
}
