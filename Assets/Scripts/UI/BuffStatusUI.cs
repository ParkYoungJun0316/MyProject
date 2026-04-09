using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Buff_Panel에 붙이는 스크립트.
/// Inspector에서 버프 타입별로 아이콘 스프라이트를 등록.
/// 활성 버프만 슬롯이 표시되고, 비활성 버프는 자동으로 숨김.
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

    [Header("슬롯 크기/텍스트")]
    [Tooltip("버프 슬롯 한 칸의 크기 (픽셀)")]
    [SerializeField] float slotSize      = 56f;
    [Tooltip("남은 시간 숫자 폰트 크기")]
    [SerializeField] float textFontSize  = 24f;
    [Tooltip("슬롯 간 간격")]
    [SerializeField] float slotSpacing   = 6f;

    [Header("슬롯 색상")]
    [SerializeField] Color activeColor   = Color.white;
    [SerializeField] Color inactiveColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

    // 런타임 슬롯 데이터
    class BuffSlot
    {
        public PlayerBuffSystem.BuffType type;
        public GameObject root;
        public Image      iconImage;
        public Image      fillImage;
        public TextMeshProUGUI durationText;
        public float totalDuration;
    }

    BuffSlot[]        slots;
    PlayerBuffSystem  buffSystem;

    void Start()
    {
        buffSystem = player.GetComponent<PlayerBuffSystem>();
        if (buffSystem == null || buffIcons == null || buffIcons.Length == 0) return;

        BuildSlots();
    }

    void BuildSlots()
    {
        // 기존 자식 제거
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        // HorizontalLayoutGroup 자동 추가
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

            // 배경 (어두운 원형 또는 사각형)
            GameObject bg = new GameObject("BG");
            bg.transform.SetParent(slot.root.transform, false);
            Image bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.5f);
            RectTransform bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;

            // 쿨타임 fillImage (Filled)
            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(slot.root.transform, false);
            slot.fillImage             = fill.AddComponent<Image>();
            slot.fillImage.color       = new Color(1f, 1f, 1f, 0.25f);
            slot.fillImage.type        = Image.Type.Filled;
            slot.fillImage.fillMethod  = Image.FillMethod.Radial360;
            slot.fillImage.fillOrigin  = (int)Image.Origin360.Top;
            slot.fillImage.fillClockwise = true;
            RectTransform fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.sizeDelta = Vector2.zero;

            // 아이콘
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(slot.root.transform, false);
            slot.iconImage        = iconObj.AddComponent<Image>();
            slot.iconImage.sprite = buffIcons[i].icon;
            slot.iconImage.preserveAspect = true;
            RectTransform iconRt = iconObj.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.1f, 0.1f);
            iconRt.anchorMax = new Vector2(0.9f, 0.9f);
            iconRt.sizeDelta = Vector2.zero;

            // 남은 시간 텍스트
            GameObject textObj = new GameObject("DurationText");
            textObj.transform.SetParent(slot.root.transform, false);
            slot.durationText           = textObj.AddComponent<TextMeshProUGUI>();
            slot.durationText.fontSize  = textFontSize;
            slot.durationText.fontStyle = FontStyles.Bold;
            slot.durationText.alignment = TextAlignmentOptions.Center;
            slot.durationText.color     = Color.white;
            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;

            // 총 지속 시간 (Inspector BuffSettings에서)
            foreach (var s in buffSystem.buffSettings)
                if (s.type == buffIcons[i].type) { slot.totalDuration = s.duration; break; }

            slot.root.SetActive(false); // 처음엔 숨김
            slots[i] = slot;
        }
    }

    void Update()
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            BuffSlot slot  = slots[i];
            bool     active = buffSystem.IsActive(slot.type);

            slot.root.SetActive(active);
            if (!active) continue;

            float remaining = buffSystem.GetRemainingTime(slot.type);
            float fill      = slot.totalDuration > 0f
                ? Mathf.Clamp01(remaining / slot.totalDuration)
                : 1f;

            slot.fillImage.fillAmount  = fill;
            slot.iconImage.color       = activeColor;
            // 올림 정수로 표시 (2.9초 → "3")
            slot.durationText.text     = remaining > 0f
                ? Mathf.CeilToInt(remaining).ToString()
                : string.Empty;
        }
    }
}
