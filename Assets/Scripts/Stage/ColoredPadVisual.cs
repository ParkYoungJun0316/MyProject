using UnityEngine;

/// <summary>
/// 압력 발판(PressurePad)의 머티리얼을 effectiveColor에 맞게 런타임 교체한다.
/// ColoredDoorVisual과 동일한 패턴.
///
/// [Inspector 설정]
///  colorMaterials[]  : PlayerColorType → Material 매핑 (색마다 1개)
///  targetRenderer    : 머티리얼을 교체할 Renderer. 비우면 자신 또는 자식에서 자동 탐색.
///  materialSlotIndex : 교체할 머티리얼 슬롯 (기본 0)
///
/// [사용 흐름]
///  StagePressurePadSetup → effectiveColor 결정 → ColoredPadVisual.Apply(effectiveColor)
/// </summary>
public class ColoredPadVisual : MonoBehaviour
{
    [System.Serializable]
    public class MaterialEntry
    {
        [Tooltip("이 머티리얼이 대응하는 플레이어 색")]
        public PlayerColorType colorType;

        [Tooltip("해당 색에 사용할 머티리얼")]
        public Material material;
    }

    [Header("색상 → 머티리얼 매핑")]
    [Tooltip("Blue/Yellow/Green/Purple 각각 1개씩 등록.")]
    [SerializeField] MaterialEntry[] colorMaterials = new MaterialEntry[4];

    [Header("렌더러")]
    [Tooltip("머티리얼을 교체할 Renderer.\n" +
             "비워두면 자신 또는 자식 오브젝트에서 자동 탐색.")]
    [SerializeField] Renderer targetRenderer;

    [Tooltip("교체할 머티리얼 슬롯 인덱스. 단일 슬롯이면 0.")]
    [SerializeField] int materialSlotIndex = 0;

    Material _originalMaterial;

    void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>(true);

        if (targetRenderer != null && materialSlotIndex < targetRenderer.sharedMaterials.Length)
            _originalMaterial = targetRenderer.sharedMaterials[materialSlotIndex];
    }

    /// <summary>
    /// 지정한 색에 대응하는 머티리얼로 교체한다.
    /// 매핑에 없거나 Common이면 원래 머티리얼로 복원한다.
    /// </summary>
    public void Apply(PlayerColorType color)
    {
        Material mat = FindMaterial(color);

        if (mat == null) { RestoreOriginal(); return; }

        SetSlot(mat);
    }

    /// <summary>원래 머티리얼로 복원한다.</summary>
    public void RestoreOriginal()
    {
        if (_originalMaterial != null)
            SetSlot(_originalMaterial);
    }

    // ── 내부 ────────────────────────────────────────────────

    Material FindMaterial(PlayerColorType color)
    {
        if (colorMaterials == null) return null;
        foreach (MaterialEntry entry in colorMaterials)
            if (entry != null && entry.colorType == color) return entry.material;
        return null;
    }

    void SetSlot(Material mat)
    {
        if (targetRenderer == null) return;

        Material[] mats = targetRenderer.materials;
        if (materialSlotIndex < 0 || materialSlotIndex >= mats.Length)
        {
            Debug.LogWarning($"[ColoredPadVisual] {name}: materialSlotIndex={materialSlotIndex}가 범위 초과 (슬롯 수={mats.Length}).");
            return;
        }

        mats[materialSlotIndex] = mat;
        targetRenderer.materials = mats;
    }
}
