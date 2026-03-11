using UnityEngine;

/// <summary>
/// 색상 바닥 타일.
///
/// [FloorType]
///  Common  : 모든 플레이어 통과
///  Blue/Red/Green/Yellow : 해당 색 플레이어만 통과, 불일치 즉사
///  Danger  : 모든 플레이어 즉사
/// </summary>
public class ColoredFloor : MonoBehaviour
{
    [Header("바닥 유형")]
    [Tooltip("Common: 모두 살기 / Blue, Red 등: 자기 색만 살기 / Danger: 모두 죽기")]
    public PlayerColorType floorType = PlayerColorType.Blue;

    [Header("시각 피드백")]
    [Tooltip("Inspector에서 설정한 색으로 바닥 색상을 적용. 투명(Alpha=0)이면 적용 안 함")]
    public Color tileColor = Color.clear;

    Renderer _rend;
    MaterialPropertyBlock _mpb;

    void Awake()
    {
        _rend = GetComponent<Renderer>();
        _mpb  = new MaterialPropertyBlock();

        if (tileColor.a > 0f)
            ApplyColor(tileColor);
    }

    void OnCollisionEnter(Collision col)
    {
        Player player = col.transform.GetComponentInParent<Player>();
        if (player == null || player.IsDead) return;

        if (ShouldKill(player))
            player.KillInstantly();
    }

    /// <summary>이 타일이 해당 플레이어를 즉사시켜야 하는지 판단</summary>
    bool ShouldKill(Player player)
    {
        switch (floorType)
        {
            case PlayerColorType.Common: return false; // 모두 살기
            case PlayerColorType.Danger: return true;  // 모두 죽기
            default: return player.playerColorType != floorType; // 자기 색만 살기
        }
    }

    void ApplyColor(Color color)
    {
        if (_rend == null) return;
        _rend.GetPropertyBlock(_mpb); 
        _mpb.SetColor("_BaseColor", color);
        _mpb.SetColor("_Color",     color);
        _rend.SetPropertyBlock(_mpb);
    }

    void OnDrawGizmos()
    {
        Color gizmoCol;
        switch (floorType)
        {
            case PlayerColorType.Common: gizmoCol = Color.white; break;
            case PlayerColorType.Danger: gizmoCol = Color.black; break;
            default:                     gizmoCol = tileColor.a > 0f ? tileColor : Color.cyan; break;
        }

        gizmoCol.a = 0.4f;
        Gizmos.color = gizmoCol;
        Gizmos.DrawCube(transform.position, transform.lossyScale * 0.99f);

        gizmoCol.a = 1f;
        Gizmos.color = gizmoCol;
        Gizmos.DrawWireCube(transform.position, transform.lossyScale * 1.01f);
    }
}
