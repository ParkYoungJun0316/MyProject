using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Objective_Panel에 붙이는 스크립트.
/// StageManager의 objectives[] 를 읽어 목표 이름 슬롯을 자동 생성.
/// Stage Clear 시 슬롯 배경색이 clearBgColor로 전환됨.
/// </summary>
public class ObjectiveUI : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] StageManager stageManager;

    [Header("슬롯 크기")]
    [SerializeField] float slotWidth   = 280f;
    [SerializeField] float slotHeight  = 60f;
    [SerializeField] float slotSpacing = 8f;

    [Header("폰트")]
    [Tooltip("비우면 TMP 기본 폰트 사용")]
    [SerializeField] TMP_FontAsset font;
    [SerializeField] float fontSize  = 24f;
    [SerializeField] Color textColor = Color.white;

    [Header("배경")]
    [Tooltip("비우면 단색으로만 표시")]
    [SerializeField] Sprite slotBgSprite;
    [SerializeField] Color  slotBgColor = new Color(0f, 0f, 0f, 0.45f);

    [Header("Stage Clear")]
    [Tooltip("스테이지 클리어 시 표시할 문구")]
    [SerializeField] string clearMessage  = "Stage Clear !!";
    [SerializeField] Color  clearBgColor   = new Color(1f, 0.4f, 0.7f, 0.9f);
    [SerializeField] Color  clearTextColor = Color.white;

    // ── 슬롯 데이터 ──────────────────────────────────────────────
    class ObjSlot
    {
        public StageObjective  objective;
        public Image           bgImage;
        public TextMeshProUGUI titleText;
    }

    ObjSlot[] slots;

    // ── 초기화 ───────────────────────────────────────────────────

    void Start()
    {
        if (stageManager == null)
            stageManager = FindFirstObjectByType<StageManager>();
        BuildSlots();
    }

    /// <summary>스테이지 전환 시 UI 갱신. PhaseManager의 onPhaseEnter에 연결.</summary>
    public void Refresh()
    {
        stageManager = FindFirstObjectByType<StageManager>();
        BuildSlots();
    }

    void BuildSlots()
    {
        DisconnectPreviousSlots();

        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        VerticalLayoutGroup vlg = GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing                = slotSpacing;
        vlg.childControlWidth      = false;
        vlg.childControlHeight     = false;
        vlg.childForceExpandWidth  = false;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment         = TextAnchor.UpperLeft;

        if (stageManager == null || stageManager.objectives.Length == 0) return;

        slots = new ObjSlot[stageManager.objectives.Length];
        int idx = 0;
        foreach (var obj in stageManager.objectives)
        {
            if (obj == null) continue;
            slots[idx++] = CreateSlot(obj);
        }

        ConnectEvents();
    }

    ObjSlot CreateSlot(StageObjective obj)
    {
        ObjSlot slot = new ObjSlot { objective = obj };

        GameObject root = new GameObject(obj.objectiveName);
        root.transform.SetParent(transform, false);
        root.AddComponent<RectTransform>().sizeDelta = new Vector2(slotWidth, slotHeight);

        slot.bgImage        = root.AddComponent<Image>();
        slot.bgImage.sprite = slotBgSprite;
        slot.bgImage.color  = slotBgColor;
        if (slotBgSprite != null)
            slot.bgImage.type = Image.Type.Sliced;

        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(root.transform, false);
        slot.titleText                = titleObj.AddComponent<TextMeshProUGUI>();
        slot.titleText.text           = obj.objectiveName;
        slot.titleText.fontSize       = fontSize;
        slot.titleText.fontStyle      = FontStyles.Bold;
        slot.titleText.color          = textColor;
        slot.titleText.alignment      = TextAlignmentOptions.Center;
        if (font != null) slot.titleText.font = font;
        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = Vector2.zero;
        titleRt.anchorMax = Vector2.one;
        titleRt.offsetMin = new Vector2(8f,  4f);
        titleRt.offsetMax = new Vector2(-8f, -4f);

        return slot;
    }

    // ── 이벤트 연결 ──────────────────────────────────────────────

    void DisconnectPreviousSlots()
    {
        if (stageManager != null)
            stageManager.OnStageClear.RemoveListener(OnStageClear);

        slots = null;
    }

    void ConnectEvents()
    {
        if (stageManager != null)
            stageManager.OnStageClear.AddListener(OnStageClear);
    }

    // ── Stage Clear ───────────────────────────────────────────────

    void OnStageClear()
    {
        if (slots == null) return;
        foreach (var slot in slots)
        {
            if (slot == null) continue;
            if (slot.bgImage   != null) slot.bgImage.color   = clearBgColor;
            if (slot.titleText != null)
            {
                slot.titleText.text  = clearMessage;
                slot.titleText.color = clearTextColor;
            }
        }
    }

    void OnDestroy()
    {
        if (stageManager != null)
            stageManager.OnStageClear.RemoveListener(OnStageClear);
    }
}
