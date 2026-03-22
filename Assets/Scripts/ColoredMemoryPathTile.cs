using System.Collections;
using UnityEngine;

/// <summary>
/// 색 기억 경로 발판 하나.
///
/// safeColors에 등록된 색의 플레이어가 고유색 활성 상태(isUniqueColor=true)일 때만 통과.
/// 고유색 미활성(흑/백 모드) 상태이거나 색이 불일치하면 즉사.
/// safeColors에 여러 색 입력 시 해당 색 모두 통과.
///
/// [단독 사용]
///  ColoredMemoryPath 없이 배치 가능. 항상 활성 상태로 색 판정.
/// [ColoredMemoryPath와 연결]
///  Awake에서 자동 주입. Challenge 단계에서만 판정.
/// </summary>
public class ColoredMemoryPathTile : MonoBehaviour
{
    [Header("안전 색 목록 (복수 지정 가능)")]
    [Tooltip("이 타일이 안전한 플레이어 색. 여러 개 지정 가능 (예: Yellow + Blue)")]
    public PlayerColorType[] safeColors = new PlayerColorType[0];

    [Header("시각 피드백")]
    [Tooltip("도전 중(미리보기 끝난 뒤) 표시 색")]
    public Color normalColor = new Color(0.45f, 0.45f, 0.45f);

    // ColoredMemoryPath.Awake()에서 주입
    [HideInInspector] public ColoredMemoryPath coloredMemoryPath;

    Collider   _col;
    Material[] _mats;
    bool _isDisabled;
    bool _isSafeTriggered;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");

    void Awake()
    {
        _col = GetComponent<Collider>();

        var renderers = GetComponentsInChildren<MeshRenderer>(true);
        _mats = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                _mats[i] = renderers[i].material;

        ApplyColor(normalColor);
    }

    void OnDestroy()
    {
        for (int i = 0; i < _mats.Length; i++)
            if (_mats[i] != null) Destroy(_mats[i]);
    }

    void OnCollisionEnter(Collision col)
    {
        if (_isDisabled) return;

        // ColoredMemoryPath와 연결된 경우: Challenge 단계에서만 판정
        if (coloredMemoryPath != null &&
            coloredMemoryPath.State != ColoredMemoryPath.PathState.Challenge) return;

        Player player = col.transform.GetComponentInParent<Player>();
        if (player == null || player.IsDead) return;

        // 안전 조건: 고유색 활성 상태(isUniqueColor=true) + 색 일치
        bool safe = player.isUniqueColor && IsSafeFor(player.playerColorType);

        if (!safe)
        {
            _isDisabled = true;
            player.KillInstantly();
            coloredMemoryPath?.OnWrongTileStepped(this, player);
        }
        else if (!_isSafeTriggered)
        {
            StartCoroutine(SafeRoutine(player));
        }
    }

    IEnumerator SafeRoutine(Player player)
    {
        _isSafeTriggered = true;
        // 1프레임 대기: 같은 프레임에 다른 충돌이 있어도 State 확인
        yield return null;
        if (coloredMemoryPath != null &&
            coloredMemoryPath.State == ColoredMemoryPath.PathState.Challenge)
            coloredMemoryPath.OnSafeTileStepped(this, player);
    }

    // ── 쿼리 ────────────────────────────────────────────────────

    /// <summary>해당 플레이어 색이 안전한지 여부</summary>
    public bool IsSafeFor(PlayerColorType color)
    {
        if (safeColors == null) return false;
        for (int i = 0; i < safeColors.Length; i++)
            if (safeColors[i] == color) return true;
        return false;
    }

    /// <summary>미리보기 순서상 이 타일이 해당 색에 포함되는지 여부</summary>
    public bool HasColor(PlayerColorType color)
    {
        if (safeColors == null) return false;
        for (int i = 0; i < safeColors.Length; i++)
            if (safeColors[i] == color) return true;
        return false;
    }

    // ── 상태 전환 (ColoredMemoryPath에서 호출) ─────────────────

    /// <summary>미리보기: 해당 색으로 발광</summary>
    public void ShowForColor(Color displayColor) => ApplyColor(displayColor);

    /// <summary>미리보기 종료: 평상시 색으로 복귀</summary>
    public void HidePreview() => ApplyColor(normalColor);

    /// <summary>리셋 시 원상복구</summary>
    public void Restore()
    {
        StopAllCoroutines();
        _isDisabled      = false;
        _isSafeTriggered = false;

        if (_col != null) _col.enabled = true;

        var renderers = GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = true;

        ApplyColor(normalColor);
    }

    // ── 내부 ────────────────────────────────────────────────────

    void ApplyColor(Color color)
    {
        if (_mats == null) return;
        for (int i = 0; i < _mats.Length; i++)
        {
            if (_mats[i] == null) continue;
            if (_mats[i].HasProperty(BaseColorId))      _mats[i].SetColor(BaseColorId, color);
            else if (_mats[i].HasProperty(ColorId))     _mats[i].SetColor(ColorId, color);
        }
    }

    void OnDrawGizmos()
    {
        Color gc = safeColors != null && safeColors.Length > 0
            ? ColoredMemoryPath.GetDefaultColorFor(safeColors[0])
            : Color.white;
        gc.a = 0.35f;
        Gizmos.color = gc;
        Gizmos.DrawCube(transform.position, transform.lossyScale * 0.95f);

        gc.a = 1f;
        Gizmos.color = gc;
        Gizmos.DrawWireCube(transform.position, transform.lossyScale * 1.01f);
    }
}
