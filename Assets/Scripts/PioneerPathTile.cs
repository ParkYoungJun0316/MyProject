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

    // PioneerPathManager가 Start()에서 배정 (Path 타일만, zone 순서 → zone 내 순서).
    // -1이면 미배정 — 네트워크 동기화 대상 아님(예: 매니저 없이 단독 테스트하는 경우 방어용).
    [HideInInspector] public int networkIndex = -1;

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
            NetworkDamageUtil.ApplyInstantKill(player);
            return;
        }

        // ── Path 타일 ──────────────────────────────────────────

        // 고유색 모드 꺼져있으면 즉사
        if (!player.isUniqueColor)
        {
            if (_isDisabled) return;
            _isDisabled = true;
            NetworkDamageUtil.ApplyInstantKill(player);
            return;
        }

        // 이미 개방된 타일 — 모든 고유색 통과
        if (_isUnlocked) return;

        // 미개방 타일 — pioneer 색이면 Host에 개방 확정 요청, 아니면 즉사
        if (player.playerColorType == zone.EffectivePioneerColor)
        {
            // Unlock()을 여기서 직접 호출하지 않는다 — 원격 플레이어는 Rigidbody가 kinematic이라
            // 이 콜백 자체가 Owner/Host에서만 발생하고(NetworkPlayerSetup.ApplyPhysicsAuthority),
            // 타일엔 Rigidbody가 없어 다른 Client는 이 이벤트를 영원히 못 받는다. Host가 감지한
            // 것만 진실로 확정해 StageNetworkState._pioneerTileUnlocked로 브로드캐스트하면
            // 전 머신(Host 포함)이 OnPioneerTileUnlocked 에코로 Unlock()을 받는다.
            if (networkIndex >= 0)
                StageNetworkState.Instance?.SetPioneerTileUnlocked(networkIndex);
        }
        else
        {
            if (_isDisabled) return;
            _isDisabled = true;
            NetworkDamageUtil.ApplyInstantKill(player);
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

    // ── 네트워크 에코 (PioneerPathManager가 StageNetworkState.OnPioneerTileUnlocked 구독 후 호출) ──

    /// <summary>Host 확정 해금 신호를 받아 로컬 상태·색을 적용한다. Host 자신도 이 경로로 처리(직접 호출 금지).</summary>
    public void Unlock()
    {
        if (_isUnlocked) return;
        _isUnlocked = true;
        ApplyColor(unlockedColor);
    }

    // ── 내부 ─────────────────────────────────────────────────────

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
