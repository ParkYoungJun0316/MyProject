using UnityEngine;

/// <summary>
/// Pioneer Path 발판 하나.
///
/// [타일 종류]
///  Path : 담당 구역 pioneer 색이 먼저 밟으면 영구 개방 → 이후 모든 고유색 통과
///         pioneer가 아닌 색이 미개방 타일 밟으면 즉사
///  Trap : 누가 밟아도 즉사 (영구 함정)
///
/// [isUniqueColor 규칙]
///  isUniqueColor = false (흑/백 모드) → 어떤 타일이든 즉사
/// </summary>
[RequireComponent(typeof(Collider))]
public class PioneerPathTile : MonoBehaviour
{
    public enum TileType { Path, Trap }

    [Header("타일 역할")]
    [Tooltip("Path: pioneer가 먼저 밟아야 개방 / Trap: 항상 즉사")]
    public TileType tileType = TileType.Path;

    // PioneerPathZone.Init()에서 주입
    [HideInInspector] public PioneerPathZone zone;

    // 색상 — PioneerPathZone.Init()에서 일괄 설정
    [HideInInspector] public Color normalColor   = new Color(0.45f, 0.45f, 0.45f);
    [HideInInspector] public Color unlockedColor = new Color(0.27f, 1f,    0.27f);
    [HideInInspector] public Color trapColor     = new Color(1f,    0.2f,  0.2f);

    bool _isUnlocked;
    bool _isDisabled;

    Material[] _mats;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");

    // ── Unity 라이프사이클 ───────────────────────────────────────

    void Awake()
    {
        var renderers = GetComponentsInChildren<MeshRenderer>(true);
        _mats = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                _mats[i] = renderers[i].material;
    }

    void OnDestroy()
    {
        if (_mats == null) return;
        for (int i = 0; i < _mats.Length; i++)
            if (_mats[i] != null) Destroy(_mats[i]);
    }

    void OnCollisionEnter(Collision col)
    {
        if (zone == null || zone.Manager == null) return;
        if (zone.Manager.State != PioneerPathManager.PathState.Challenge) return;

        Player player = col.transform.GetComponentInParent<Player>();
        if (player == null || player.IsDead) return;

        // ── Trap 타일 — 항상 즉사 ──────────────────────────────
        if (tileType == TileType.Trap)
        {
            if (_isDisabled) return;
            _isDisabled = true;
            ApplyColor(trapColor);
            player.KillInstantly();
            return;
        }

        // ── Path 타일 ──────────────────────────────────────────

        // 고유색 모드 꺼져있으면 즉사
        if (!player.isUniqueColor)
        {
            if (_isDisabled) return;
            _isDisabled = true;
            player.KillInstantly();
            return;
        }

        // 이미 개방된 타일 — 모든 고유색 통과
        if (_isUnlocked) return;

        // 미개방 타일 — pioneer 색이면 개방, 아니면 즉사
        if (player.playerColorType == zone.EffectivePioneerColor)
        {
            Unlock();
        }
        else
        {
            if (_isDisabled) return;
            _isDisabled = true;
            player.KillInstantly();
        }
    }

    // ── 상태 전환 (PioneerPathZone에서 호출) ─────────────────────

    /// <summary>미리보기: 지정 색으로 발광</summary>
    public void ShowPreview(Color color) => ApplyColor(color);

    /// <summary>미리보기 종료: 개방 여부에 따라 색 복귀</summary>
    public void HidePreview() => ApplyColor(_isUnlocked ? unlockedColor : normalColor);

    /// <summary>리셋 시 원상복구</summary>
    public void Restore()
    {
        _isUnlocked = false;
        _isDisabled = false;

        var col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        var renderers = GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = true;

        ApplyColor(normalColor);
    }

    // ── 내부 ─────────────────────────────────────────────────────

    void Unlock()
    {
        _isUnlocked = true;
        ApplyColor(unlockedColor);
    }

    void ApplyColor(Color color)
    {
        if (_mats == null) return;
        for (int i = 0; i < _mats.Length; i++)
        {
            if (_mats[i] == null) continue;
            if (_mats[i].HasProperty(BaseColorId))      _mats[i].SetColor(BaseColorId, color);
            else if (_mats[i].HasProperty(ColorId))     _mats[i].SetColor(ColorId,     color);
        }
    }

    void OnDrawGizmos()
    {
        Color gc = tileType == TileType.Trap
            ? new Color(1f, 0.2f, 0.2f, 0.3f)
            : new Color(0.2f, 0.8f, 0.3f, 0.3f);
        Gizmos.color = gc;
        Gizmos.DrawCube(transform.position, transform.lossyScale * 0.9f);

        gc.a = 1f;
        Gizmos.color = gc;
        Gizmos.DrawWireCube(transform.position, transform.lossyScale * 1.01f);
    }
}
