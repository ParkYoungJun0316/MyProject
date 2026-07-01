using UnityEngine;

/// <summary>
/// PlayerColorType → 고유색 Color 매핑.
/// 몸통 MPB 틴트(PlayerVisualController)와 Player.uniqueColor에 공통 사용.
/// </summary>
public static class PlayerColorUtil
{
    // 파랑 #2384C4, 노랑 #DCA524, 보라 #4B1857, 초록 #4C6C48
    static readonly Color Blue   = new(0x23 / 255f, 0x84 / 255f, 0xC4 / 255f);
    static readonly Color Yellow = new(0xDC / 255f, 0xA5 / 255f, 0x24 / 255f);
    static readonly Color Purple = new(0x4B / 255f, 0x18 / 255f, 0x57 / 255f);
    static readonly Color Green  = new(0x4C / 255f, 0x6C / 255f, 0x48 / 255f);

    public static Color GetUniqueColor(PlayerColorType type) => type switch
    {
        PlayerColorType.Blue   => Blue,
        PlayerColorType.Yellow => Yellow,
        PlayerColorType.Purple => Purple,
        PlayerColorType.Green  => Green,
        _                      => Color.white,
    };

    /// <summary>playerColorType·uniqueColor 설정 후 몸통 MPB 갱신.</summary>
    public static void ApplyToPlayer(Player player, PlayerColorType type)
    {
        if (player == null) return;

        player.playerColorType = type;
        player.uniqueColor     = GetUniqueColor(type);
        player.GetComponent<PlayerVisualController>()?.RefreshColor();
    }
}
