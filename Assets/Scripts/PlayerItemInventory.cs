using UnityEngine;

/// <summary>
/// 소비 아이템 인벤토리 (슬롯 5개, 슬롯당 최대 5개 스택)
/// - 수류탄 / 체력 포션 / 쉴드
/// - 숫자키(1~5)로 슬롯 선택, 마우스 좌클릭으로 사용
/// - 쉴드는 패시브: 인벤토리에 보유 중이면 피격 시 자동 1회 방어
/// - 슬롯 선택 시 handPoint 위치에 아이템 비주얼 표시 (애니메이터 불필요)
/// </summary>
public class PlayerItemInventory : MonoBehaviour
{
    public enum ConsumableType { None, Grenade, HealthPotion, Shield }

    [System.Serializable]
    public class ItemSlot
    {
        public ConsumableType type = ConsumableType.None;
        public int count = 0;
    }

    public const int SlotCount = 5;
    public const int MaxStack  = 5;

    [Header("슬롯 현황")]
    public ItemSlot[] slots = new ItemSlot[SlotCount];

    [Header("체력 포션 설정")]
    public int healAmount = 0;

    [Header("손 위치 및 아이템 비주얼")]
    [Tooltip("손 위치 Transform (캐릭터 손 본 또는 빈 GameObject를 인스펙터에서 연결)")]
    public Transform handPoint;

    [Tooltip("손에 들릴 수류탄 비주얼 프리팹 (Rigidbody/Collider 없는 메시 전용)")]
    public GameObject grenadeHeldVisual;

    [Tooltip("손에 들릴 체력 포션 비주얼 프리팹")]
    public GameObject potionHeldVisual;

    [Tooltip("손에 들릴 쉴드 비주얼 프리팹 (선택)")]
    public GameObject shieldHeldVisual;

    /// <summary> 현재 선택된 슬롯 인덱스 (-1 = 없음) </summary>
    public int SelectedSlot { get; private set; } = -1;

    GameObject heldItemInstance;

    void Awake()
    {
        if (slots == null || slots.Length != SlotCount)
            slots = new ItemSlot[SlotCount];

        for (int i = 0; i < slots.Length; i++)
            if (slots[i] == null) slots[i] = new ItemSlot();
    }

    // ── 픽업 ────────────────────────────────────────────────

    /// <summary>
    /// 아이템 픽업 시도. 동일 타입 슬롯에 스택, 없으면 빈 슬롯에 추가.
    /// 슬롯이 가득 찼으면 false 반환 → 아이템 오브젝트를 파괴하지 않음.
    /// </summary>
    public bool TryPickup(ConsumableType type)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i].type == type && slots[i].count < MaxStack)
            {
                slots[i].count++;
                RefreshHandDisplayIfSelected(i);
                return true;
            }
        }

        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i].type == ConsumableType.None)
            {
                slots[i].type  = type;
                slots[i].count = 1;
                RefreshHandDisplayIfSelected(i);
                return true;
            }
        }

        return false;
    }

    // ── 슬롯 선택 ───────────────────────────────────────────

    /// <summary> 슬롯 선택/해제 토글. index -1 이면 무조건 해제. </summary>
    public void SelectSlot(int zeroBasedIndex)
    {
        if (zeroBasedIndex < 0)
        {
            SelectedSlot = -1;
            UpdateHandDisplay();
            return;
        }
        if (zeroBasedIndex >= SlotCount) return;

        SelectedSlot = (SelectedSlot == zeroBasedIndex) ? -1 : zeroBasedIndex;
        UpdateHandDisplay();
    }

    /// <summary> 선택된 슬롯에 사용 가능한 아이템이 있는지 여부 </summary>
    public bool SelectedSlotHasItem()
    {
        if (SelectedSlot < 0 || SelectedSlot >= SlotCount) return false;
        ItemSlot slot = slots[SelectedSlot];
        return slot.type != ConsumableType.None && slot.count > 0;
    }

    // ── 사용 ─────────────────────────────────────────────────

    /// <summary> 선택된 슬롯의 ConsumableType 반환. 없으면 None. </summary>
    public ConsumableType GetSelectedType()
    {
        if (SelectedSlot < 0 || SelectedSlot >= SlotCount) return ConsumableType.None;
        return slots[SelectedSlot].type;
    }

    /// <summary>
    /// 선택된 슬롯 아이템 1개 소모. 비면 슬롯 초기화 + 선택 해제 + 손 비주얼 갱신.
    /// Player.cs의 HandleGrenadeInput / HandlePotionInput 에서 성공 후 호출.
    /// </summary>
    public void ConsumeSelected()
    {
        if (SelectedSlot < 0 || SelectedSlot >= SlotCount) return;

        slots[SelectedSlot].count--;
        if (slots[SelectedSlot].count <= 0)
        {
            slots[SelectedSlot].type  = ConsumableType.None;
            slots[SelectedSlot].count = 0;
            SelectedSlot = -1;  // 슬롯이 완전히 비었을 때만 선택 해제
        }
        // 아이템이 남아있으면 슬롯 유지 → 다시 키를 누르지 않아도 연속 사용 가능
        UpdateHandDisplay();
    }

    // ── 쉴드 패시브 ─────────────────────────────────────────

    /// <summary>
    /// TakeDamage 시 자동 호출.
    /// 쉴드 보유 중이면 1개 소모 후 true 반환 → 데미지 0.
    /// </summary>
    public bool TryConsumeShield()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i].type == ConsumableType.Shield && slots[i].count > 0)
            {
                slots[i].count--;
                if (slots[i].count <= 0)
                {
                    slots[i].type  = ConsumableType.None;
                    slots[i].count = 0;
                }
                RefreshHandDisplayIfSelected(i);
                return true;
            }
        }
        return false;
    }

    /// <summary> 쉴드 보유 여부 </summary>
    public bool HasShield()
    {
        for (int i = 0; i < SlotCount; i++)
            if (slots[i].type == ConsumableType.Shield && slots[i].count > 0)
                return true;
        return false;
    }

    // ── 손 아이템 비주얼 ────────────────────────────────────

    /// <summary>
    /// 현재 선택 슬롯에 맞게 손 위치의 아이템 비주얼을 갱신.
    /// handPoint가 null이면 무시.
    /// </summary>
    void UpdateHandDisplay()
    {
        if (heldItemInstance != null)
        {
            Destroy(heldItemInstance);
            heldItemInstance = null;
        }

        if (handPoint == null) return;
        if (SelectedSlot < 0 || SelectedSlot >= SlotCount) return;

        ItemSlot slot = slots[SelectedSlot];
        if (slot.type == ConsumableType.None || slot.count <= 0) return;

        GameObject prefab = GetHeldPrefab(slot.type);
        if (prefab == null) return;

        heldItemInstance = Instantiate(prefab, handPoint);
        heldItemInstance.transform.localPosition = Vector3.zero;
        heldItemInstance.transform.localRotation = Quaternion.identity;

        // 물리/충돌 비활성화 (시각 전용)
        Rigidbody rb = heldItemInstance.GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);

        Collider[] colls = heldItemInstance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colls.Length; i++)
            colls[i].enabled = false;
    }

    GameObject GetHeldPrefab(ConsumableType type)
    {
        switch (type)
        {
            case ConsumableType.Grenade:      return grenadeHeldVisual;
            case ConsumableType.HealthPotion: return potionHeldVisual;
            case ConsumableType.Shield:       return shieldHeldVisual;
            default:                          return null;
        }
    }

    /// <summary> 특정 슬롯이 현재 선택 중이면 손 비주얼을 갱신 </summary>
    void RefreshHandDisplayIfSelected(int slotIndex)
    {
        if (SelectedSlot == slotIndex)
            UpdateHandDisplay();
    }
}
