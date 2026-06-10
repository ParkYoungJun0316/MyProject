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
///  DirectionalBarrierRound, PioneerPathManager, StagePressurePadSetup
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
    /// GameSession.GetActiveColors()를 가져와 totalSlots칸에 균등 분배한 배열을 반환한다.
    /// GameSession이 없으면 PlayableColors 4색으로 fallback.
    /// </summary>
    public static PlayerColorType[] Distribute(int totalSlots)
    {
        IReadOnlyList<PlayerColorType> activeColors = GameSession.Instance != null
            ? GameSession.Instance.GetActiveColors()
            : (IReadOnlyList<PlayerColorType>)PlayableColors;

        return Distribute(activeColors, totalSlots);
    }

    /// <summary>
    /// 주어진 activeColors 목록을 totalSlots칸에 균등 분배한 배열을 반환한다.
    /// activeColors가 null이거나 비어 있으면 PlayableColors 4색으로 fallback.
    /// </summary>
    public static PlayerColorType[] Distribute(IReadOnlyList<PlayerColorType> activeColors, int totalSlots)
    {
        if (activeColors == null || activeColors.Count == 0)
            return Distribute(PlayableColors, totalSlots);

        int n         = activeColors.Count;
        int baseCount = totalSlots / n;
        int remainder = totalSlots % n;

        // 여분 슬롯을 줄 색을 랜덤으로 결정 (인덱스 피셔-예이츠 셔플)
        int[] indices = new int[n];
        for (int i = 0; i < n; i++) indices[i] = i;
        for (int i = n - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
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
