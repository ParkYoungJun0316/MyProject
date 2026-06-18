using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Buff_Panel에 붙이는 스크립트.
/// Inspector에서 버프 타입별로 아이콘 스프라이트를 등록.
/// 활성 버프만 슬롯이 표시되고, 비활성 버프는 자동으로 숨김.
///
/// [쿨다운 표시 방식]
/// BG → Icon → Fill 레이어 순서.
/// bgSprite와 동일한 원 스프라이트를 검정색으로 Fill(Vertical Top)에 사용.
/// 시간이 줄수록 원 안에서 검정이 위에서 아래로 내려옴.
/// 버프가 끝나면 슬롯 자체가 사라짐.
/// </summary>
public class BuffStatusUI : MonoBehaviour
{
    [System.Serializable]
    public class BuffIconEntry
    {
        [Tooltip("버프 타입")]
        public PlayerBuffSystem.BuffType type;
        [Tooltip("이 버프의 아이콘 스프라이트")]
        public Sprite icon;
    }

    [Header("연결")]
    [SerializeField] Player player;

    [Header("버프 아이콘 등록 (타입별 스프라이트 연결)")]
    [SerializeField] BuffIconEntry[] buffIcons;

    [Header("배경 스프라이트")]
    [Tooltip("슬롯 원형 배경 스프라이트 (원 밖은 투명). BG와 Fill 둘 다 이 스프라이트를 사용.")]
    [SerializeField] Sprite bgSprite;
    [Tooltip("배경 색상")]
    [SerializeField] Color  bgColor   = new Color(0.9f, 0.87f, 0.75f, 1f);

    [Header("쿨다운 Fill 색상")]
    [Tooltip("위에서 내려오는 덮개 색상. 보통 검정이나 어두운 회색.")]
    [SerializeField] Color  fillColor = new Color(0f, 0f, 0f, 0.85f);

    [Header("슬롯 크기")]
    [Tooltip("버프 슬롯 한 칸의 크기 (픽셀)")]
    [SerializeField] float slotSize    = 56f;
    [Tooltip("슬롯 간 간격")]
    [SerializeField] float slotSpacing = 6f;

    [Header("슬롯 색상")]
    [SerializeField] Color activeColor = Color.white;

    // 런타임 슬롯 데이터
    class BuffSlot
    {
        public PlayerBuffSystem.BuffType type;
        public GameObject root;
        public Image      iconImage;
        public Image      fillImage;
        public float      totalDuration;
    }

    BuffSlot[]       slots;
    PlayerBuffSystem buffSystem;

    void Start()
    {
        if (player == null)
            player = FindFirstObjectByType<Player>();
        if (player == null) return;

        buffSystem = player.GetComponent<PlayerBuffSystem>();
        if (buffSystem == null || buffIcons == null || buffIcons.Length == 0) return;

        BuildSlots();
        buffSystem.OnBuffApplied += OnBuffApplied;
    }

    void OnDestroy()
    {
        if (buffSystem != null)
            buffSystem.OnBuffApplied -= OnBuffApplied;
    }

    void OnBuffApplied(PlayerBuffSystem.BuffType type, float duration)
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].type != type) continue;
            slots[i].totalDuration        = duration;
            slots[i].fillImage.fillAmount = 0f;
            break;
        }
    }

    void BuildSlots()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        HorizontalLayoutGroup hlg = GetComponent<HorizontalLayoutGroup>();
        if (hlg == null) hlg = gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = slotSpacing;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.childAlignment         = TextAnchor.MiddleLeft;

        slots = new BuffSlot[buffIcons.Length];

        for (int i = 0; i < buffIcons.Length; i++)
        {
            BuffSlot slot = new BuffSlot();
            slot.type = buffIcons[i].type;

            slot.root = new GameObject($"BuffSlot_{buffIcons[i].type}");
            slot.root.transform.SetParent(transform, false);
            RectTransform rootRt = slot.root.AddComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(slotSize, slotSize);

            // ── 레이어 1: 배경 (항상 밝음) ───────────────────────
            GameObject bg = new GameObject("BG");
            bg.transform.SetParent(slot.root.transform, false);
            Image bgImg          = bg.AddComponent<Image>();
            bgImg.sprite         = bgSprite;
            bgImg.color          = bgColor;
            bgImg.preserveAspect = false;
            RectTransform bgRt   = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;

            // ── 레이어 2: 아이콘 ─────────────────────────────────
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(slot.root.transform, false);
            slot.iconImage                = iconObj.AddComponent<Image>();
            slot.iconImage.sprite         = buffIcons[i].icon;
            slot.iconImage.preserveAspect = true;
            RectTransform iconRt = iconObj.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.1f, 0.1f);
            iconRt.anchorMax = new Vector2(0.9f, 0.9f);
            iconRt.sizeDelta = Vector2.zero;

            // ── 레이어 3: 쿨다운 Fill (원 모양, 위에서 아래로 덮음) ──
            // bgSprite와 같은 원 스프라이트를 사용 → 원 밖은 투명, 원 안만 검정
            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(slot.root.transform, false);
            slot.fillImage               = fillObj.AddComponent<Image>();
            slot.fillImage.sprite        = bgSprite;
            slot.fillImage.color         = fillColor;
            slot.fillImage.type          = Image.Type.Filled;
            slot.fillImage.fillMethod    = Image.FillMethod.Vertical;
            slot.fillImage.fillOrigin    = (int)Image.OriginVertical.Top;
            slot.fillImage.fillClockwise = true;
            slot.fillImage.fillAmount    = 0f;
            RectTransform fillRt = fillObj.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.sizeDelta = Vector2.zero;

            slot.totalDuration = 0f;

            slot.root.SetActive(false);
            slots[i] = slot;
        }
    }

    void Update()
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            BuffSlot slot   = slots[i];
            bool     active = buffSystem.IsActive(slot.type);

            slot.root.SetActive(active);

            if (!active)
                continue;

            float remaining = buffSystem.GetRemainingTime(slot.type);

            // 시간이 줄수록 fillAmount 증가: 0(처음) → 1(만료 직전, 원 전체 검정)
            slot.fillImage.fillAmount = slot.totalDuration > 0f
                ? Mathf.Clamp01(1f - remaining / slot.totalDuration)
                : 0f;

            slot.iconImage.color = activeColor;
        }
    }
}
