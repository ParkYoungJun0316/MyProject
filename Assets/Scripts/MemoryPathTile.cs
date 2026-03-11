using System.Collections;
using UnityEngine;

/// <summary>
/// 기억 경로 발판 하나.
/// Safe : 미리보기 때 잠깐 빛났다가 꺼짐 → 밟아도 통과
/// Trap : 미리보기에 표시 안 됨 → 조금이라도 닿으면 즉사
///
/// MemoryPath 자식으로 배치. MemoryPath.Awake()가 자동으로 참조·역할을 주입.
/// </summary>
public class MemoryPathTile : MonoBehaviour
{
    public enum TileRole { Safe, Trap }

    [Header("발판 역할")]
    [Tooltip("Safe: 미리보기에 표시되는 안전 경로 / Trap: 닿으면 즉사")]
    public TileRole role = TileRole.Trap;

    [Header("시각 피드백")]
    [Tooltip("평상시(도전 중) 색")]
    public Color normalColor  = new Color(0.45f, 0.45f, 0.45f);
    [Tooltip("미리보기 때 Safe 발판에 표시되는 색")]
    public Color previewColor = new Color(1f, 0.85f, 0f);

    // MemoryPath.Awake()에서 주입
    [HideInInspector] public MemoryPath memoryPath;

    Collider   _col;
    Material[] _mats;
    bool       _isDisabled;
    bool       _isSafeTriggered;

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
        if (memoryPath == null || memoryPath.State != MemoryPath.PathState.Challenge) return;

        Player player = col.transform.GetComponentInParent<Player>();
        if (player == null || player.IsDead) return;

        if (role == TileRole.Trap)
        {
            _isDisabled = true;
            player.KillInstantly();          // 무적/쿨다운 무시하고 즉사
            memoryPath.OnTrapStepped(this);  // 스테이지 실패 처리
        }
        else if (!_isSafeTriggered)
        {
            StartCoroutine(SafeRoutine());
        }
    }

    IEnumerator SafeRoutine()
    {
        _isSafeTriggered = true;
        // 1프레임 대기: 같은 프레임에 Trap이 발동됐으면 State가 Failed로 바뀐 상태
        yield return null;
        if (memoryPath != null && memoryPath.State == MemoryPath.PathState.Challenge)
            memoryPath.OnSafeTileStepped(this);
    }

    // ── 상태 전환 (MemoryPath에서 호출) ─────────────────────────

    /// <summary>미리보기: Safe 발판만 빛나게</summary>
    public void ShowPreview() => ApplyColor(previewColor);

    /// <summary>미리보기 종료: 모든 발판 동일하게 보이게</summary>
    public void HidePreview() => ApplyColor(normalColor);

    /// <summary>리셋 시 발판 원상복구</summary>
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

    void ApplyColor(Color color)
    {
        if (_mats == null) return;
        for (int i = 0; i < _mats.Length; i++)
        {
            var mat = _mats[i];
            if (mat == null) continue;
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, color);
            else if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, color);
        }
    }

    void OnDrawGizmos()
    {
        Color c = role == TileRole.Safe ? previewColor : Color.red;
        c.a = 0.35f;
        Gizmos.color = c;
        Gizmos.DrawCube(transform.position, transform.lossyScale * 0.95f);

        c.a = 1f;
        Gizmos.color = c;
        Gizmos.DrawWireCube(transform.position, transform.lossyScale * 1.01f);
    }
}
