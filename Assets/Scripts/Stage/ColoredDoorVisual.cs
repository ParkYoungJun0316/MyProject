using UnityEngine;

/// <summary>
/// 색상 문(Door.B/Y/G/P)의 머티리얼을 런타임에 교체한다.
///
/// [개념]
///  - 프리팹 Destroy/Instantiate 없이 MeshRenderer 슬롯만 교체 → DoorController.requiredPads 참조 유지
///  - Apply(color) 한 번 호출로 비주얼 변경 완료
///  - Common·Door.1~7 등 비색상 문에는 부착하지 않는다
///
/// [Inspector 설정]
///  colorMaterials[] : PlayerColorType → Material 매핑 (색마다 1개씩 등록)
///  materialSlotIndex : 머티리얼 슬롯 번호 (기본값 0, 단일 슬롯 문에는 그대로)
///
/// [사용 흐름]
///  StagePressurePadSetup → 패드 effectiveColor 결정 →
///  같은 문의 ColoredDoorVisual.Apply(effectiveColor) 호출
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class ColoredDoorVisual : MonoBehaviour
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
    [Tooltip("Blue/Yellow/Green/Purple 각각 1개씩 등록.\n" +
             "Apply() 호출 시 해당 색의 머티리얼로 교체된다.")]
    [SerializeField] MaterialEntry[] colorMaterials = new MaterialEntry[4];

    [Header("머티리얼 슬롯")]
    [Tooltip("교체할 MeshRenderer 슬롯 인덱스. 단일 슬롯 문은 0.")]
    [SerializeField] int materialSlotIndex = 0;

    MeshRenderer _renderer;
    Material     _originalMaterial;

    void Awake()
    {
        _renderer         = GetComponent<MeshRenderer>();
        _originalMaterial = _renderer.sharedMaterials.Length > materialSlotIndex
            ? _renderer.sharedMaterials[materialSlotIndex]
            : null;
    }

    /// <summary>
    /// 지정한 색에 대응하는 머티리얼로 교체한다.
    /// 매핑에 없는 색이거나 Common이면 원래 머티리얼로 복원한다.
    /// </summary>
    public void Apply(PlayerColorType color)
    {
        Material mat = FindMaterial(color);

        if (mat == null)
        {
            RestoreOriginal();
            return;
        }

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
        {
            if (entry != null && entry.colorType == color)
                return entry.material;
        }
        return null;
    }

    void SetSlot(Material mat)
    {
        if (_renderer == null) return;

        Material[] mats = _renderer.materials;
        if (materialSlotIndex < 0 || materialSlotIndex >= mats.Length)
        {
            Debug.LogWarning($"[ColoredDoorVisual] {name}: materialSlotIndex={materialSlotIndex}가 범위를 벗어납니다 (슬롯 수={mats.Length}).");
            return;
        }

        mats[materialSlotIndex] = mat;
        _renderer.materials = mats;
    }
}
