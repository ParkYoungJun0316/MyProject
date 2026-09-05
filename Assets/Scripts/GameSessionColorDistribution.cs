using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameSession 활성색을 N슬롯에 균등 분배하는 공통 정적 유틸.
///
/// [분배 규칙]
///  슬롯 4 기준:
///   2인 → [A, A, B, B]
///   3인 → [A, A, B, C]  (여분 1개는 랜덤 색에 배정)
///   4인 → [A, B, C, D]
///  GameSession이 없으면 Blue/Purple/Green/Yellow 4색으로 fallback.
///
/// [사용처]
///  Distribute(균등분배): PioneerPathManager, StagePressurePadSetup
///  GetActiveColorsOrFallback(목록만): DirectionalBarrierRound — §2.1 고정 표라 균등분배를 안 씀
/// </summary>
public static class GameSessionColorDistribution
{
    static readonly PlayerColorType[] PlayableColors =
    {
        PlayerColorType.Blue,
        PlayerColorType.Purple,
        PlayerColorType.Green,
        PlayerColorType.Yellow,
    };

    /// <summary>
    /// 활성 색을 totalSlots칸에 균등 분배한 배열을 반환한다.
    ///
    /// rng를 넘기면(예: System.Random(ChallengeSeed)) 여분 슬롯 배정까지 그 시드로 결정된다 —
    /// 네트워크 동기화가 필요한 호출자(PioneerPathManager 등)는 반드시 넘길 것.
    /// null이면 기존 동작 그대로 UnityEngine.Random 사용(로컬 전용 호출자 하위호환).
    /// </summary>
    public static PlayerColorType[] Distribute(int totalSlots, System.Random rng = null)
    {
        return Distribute(GetActiveColorsOrFallback(), totalSlots, rng);
    }

    /// <summary>
    /// 활성 색 목록을 우선순위대로 조회한다 (분배 없이 목록만).
    ///
    /// 색 소스 우선순위:
    ///  1. PlayerSpawnCoordinator.GetActiveColors() — NetworkList SSOT, 레이스 없음
    ///  2. GameSession.GetActiveColors()            — Editor 직접 Play 등 PSC 없을 때 fallback
    ///  3. PlayableColors 4색                       — 둘 다 없을 때 최종 fallback
    ///
    /// DirectionalBarrierRound처럼 균등 분배가 아니라 고정 표(§2.1)로 슬롯을 짜야 하는
    /// 호출자를 위해 노출 — Distribute(int,rng)와 같은 소스를 공유한다.
    /// </summary>
    public static IReadOnlyList<PlayerColorType> GetActiveColorsOrFallback()
    {
        PlayerColorType[] psColors = PlayerSpawnCoordinator.GetActiveColors();
        if (psColors.Length > 0)
            return psColors;

        return GameSession.Instance != null
            ? GameSession.Instance.GetActiveColors()
            : (IReadOnlyList<PlayerColorType>)PlayableColors;
    }

    /// <summary>
    /// 주어진 activeColors 목록을 totalSlots칸에 균등 분배한 배열을 반환한다.
    /// activeColors가 null이거나 비어 있으면 PlayableColors 4색으로 fallback.
    /// </summary>
    public static PlayerColorType[] Distribute(IReadOnlyList<PlayerColorType> activeColors, int totalSlots, System.Random rng = null)
    {
        if (activeColors == null || activeColors.Count == 0)
            return Distribute(PlayableColors, totalSlots, rng);

        int n         = activeColors.Count;
        int baseCount = totalSlots / n;
        int remainder = totalSlots % n;

        // 여분 슬롯을 줄 색을 결정 (인덱스 피셔-예이츠 셔플) — rng가 있으면 그 시드로, 없으면 UnityEngine.Random으로
        int[] indices = new int[n];
        for (int i = 0; i < n; i++) indices[i] = i;
        for (int i = n - 1; i > 0; i--)
        {
            int j = rng != null ? rng.Next(0, i + 1) : Random.Range(0, i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        var result = new List<PlayerColorType>(totalSlots);
        for (int i = 0; i < n; i++)
        {
            PlayerColorType color = activeColors[indices[i]];
            int count = baseCount + (i < remainder ? 1 : 0);
            for (int k = 0; k < count; k++)
                result.Add(color);
        }

        return result.ToArray();
    }
}
