using UnityEngine;

/// <summary>
/// PlayerColorType → 고유색 Color 매핑.
/// 몸통 MPB 틴트(PlayerVisualController)와 Player.uniqueColor에 공통 사용.
///
/// ColorOrder/ColorTypeToIndex/DefaultCheerNames: 구 LobbyNetworkManager의 색 인덱스 상수를
/// 여기로 이전(NetworkDesign.md §6B.7 P8, 2026-08-20) — 로비 삭제 후에도 전 시스템이 참조하는
/// colorIndex(0=Blue/1=Purple/2=Green/3=Yellow) ↔ PlayerColorType 매핑의 단일 소스.
/// </summary>
public static class PlayerColorUtil
{
    // 파랑 #2384C4, 노랑 #DCA524, 보라 #4B1857, 초록 #4C6C48
    static readonly Color Blue   = new(0x23 / 255f, 0x84 / 255f, 0xC4 / 255f);
    static readonly Color Yellow = new(0xDC / 255f, 0xA5 / 255f, 0x24 / 255f);
    static readonly Color Purple = new(0x4B / 255f, 0x18 / 255f, 0x57 / 255f);
    static readonly Color Green  = new(0x4C / 255f, 0x6C / 255f, 0x48 / 255f);

    /// <summary>colorIndex(0~3) → PlayerColorType 매핑. GetSessionCheerName 등 전 시스템의 단일 소스.</summary>
    public static readonly PlayerColorType[] ColorOrder =
    {
        PlayerColorType.Blue,
        PlayerColorType.Purple,
        PlayerColorType.Green,
        PlayerColorType.Yellow,
    };

    // ColorIndex 순 기본 CheerName — GameSession.GetSessionCheerName 등 전 시스템의 단일 소스.
    public static readonly string[] DefaultCheerNames = { "berry", "guma", "sook", "dan" };

    /// <summary>PlayerColorType → ColorOrder 인덱스 변환. 미매칭 시 -1 반환.</summary>
    public static int ColorTypeToIndex(PlayerColorType colorType)
    {
        for (int i = 0; i < ColorOrder.Length; i++)
            if (ColorOrder[i] == colorType) return i;
        return -1;
    }

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
