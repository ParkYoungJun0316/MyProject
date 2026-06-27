using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Objective_Panel에 붙이는 스크립트.
/// StageManager의 objectives[] 를 읽어 목표 이름 슬롯을 자동 생성.
///
/// [Stage Clear 표시 흐름]
/// - 중간 스테이지 클리어(OnStageClear) → 슬롯 갱신 없음 (문구 X)
/// - 씬 전체 클리어(onAllPhasesComplete) → ShowSceneClear() 연결 → 문구 표시
/// - 다음 스테이지 전환(onPhaseEnter)    → Refresh() 연결 → 슬롯 재생성
///
/// [타입별 표시]
/// - SurviveTimeObjective : "275s" 형태로 남은 시간 표시. OnTimeChanged 구독.
/// - ReachZoneObjective   : 가로 트랙 바 + 마커. OnProgressChanged 구독.
/// - 그 외                : objectiveName 표시 (기존 동작 유지)
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

    [Header("ReachZone 바")]
    [Tooltip("트랙 배경 색")]
    [SerializeField] Color trackBgColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [Tooltip("마커에 사용할 Sprite. 비우면 기본 사각형.")]
    [SerializeField] Sprite markerSprite;
    [Tooltip("마커 색")]
    [SerializeField] Color markerColor  = new Color(1f, 1f, 1f, 1f);
    [Tooltip("트랙 세로 높이(px). 0이면 슬롯 높이의 30% 자동 적용.")]
    [SerializeField] float trackHeight  = 0f;
    [Tooltip("마커 한 변 크기(px). 0이면 슬롯 높이의 60% 자동 적용.")]
    [SerializeField] float markerSize   = 0f;

    [Header("Stage Clear")]
    [Tooltip("스테이지 클리어 시 표시할 문구")]
    [SerializeField] string clearMessage   = "Stage Clear !!";
    [SerializeField] Color  clearBgColor   = new Color(1f, 0.4f, 0.7f, 0.9f);
    [SerializeField] Color  clearTextColor = Color.white;

    // ── 슬롯 데이터 ──────────────────────────────────────────────
    class ObjSlot
    {
        public StageObjective     objective;
        public Image              bgImage;
        public TextMeshProUGUI    titleText;      // Survive 등 텍스트 슬롯
        public RectTransform      markerRect;     // ReachZone 전용
        public UnityAction<float> surviveListener;
        public UnityAction<float> reachListener;
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

        // ── 슬롯 루트 ────────────────────────────────────────────
        GameObject root = new GameObject(obj.objectiveName);
        root.transform.SetParent(transform, false);
        root.AddComponent<RectTransform>().sizeDelta = new Vector2(slotWidth, slotHeight);

        slot.bgImage        = root.AddComponent<Image>();
        slot.bgImage.sprite = slotBgSprite;
        slot.bgImage.color  = slotBgColor;
        if (slotBgSprite != null)
            slot.bgImage.type = Image.Type.Sliced;

        // ── 타입별 콘텐츠 ─────────────────────────────────────────
        if (obj is ReachZoneObjective reach)
            BuildReachZoneContent(root, slot, reach);
        else
            BuildTextContent(root, slot, obj);

        return slot;
    }

    // ── 텍스트 슬롯 (Survive / 기본) ──────────────────────────────

    void BuildTextContent(GameObject root, ObjSlot slot, StageObjective obj)
    {
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(root.transform, false);
        slot.titleText           = titleObj.AddComponent<TextMeshProUGUI>();
        slot.titleText.fontSize  = fontSize;
        slot.titleText.fontStyle = FontStyles.Bold;
        slot.titleText.color     = textColor;
        slot.titleText.alignment = TextAlignmentOptions.Center;
        if (font != null) slot.titleText.font = font;
        RectTransform rt = titleObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8f,  4f);
        rt.offsetMax = new Vector2(-8f, -4f);

        if (obj is SurviveTimeObjective survive)
            slot.titleText.text = FormatSeconds(survive.Remaining);
        else
            slot.titleText.text = obj.objectiveName;
    }

    // ── ReachZone 슬롯 (트랙 바 + 마커) ─────────────────────────

    void BuildReachZoneContent(GameObject root, ObjSlot slot, ReachZoneObjective reach)
    {
        float tH = trackHeight  > 0f ? trackHeight  : slotHeight * 0.30f;
        float mS = markerSize   > 0f ? markerSize   : slotHeight * 0.60f;

        // 트랙 배경
        GameObject trackObj = new GameObject("Track");
        trackObj.transform.SetParent(root.transform, false);
        RectTransform trackRt = trackObj.AddComponent<RectTransform>();
        trackRt.anchorMin        = new Vector2(0f, 0.5f);
        trackRt.anchorMax        = new Vector2(1f, 0.5f);
        trackRt.pivot            = new Vector2(0.5f, 0.5f);
        trackRt.offsetMin        = new Vector2(12f, -tH * 0.5f);
        trackRt.offsetMax        = new Vector2(-12f, tH * 0.5f);
        Image trackImg           = trackObj.AddComponent<Image>();
        trackImg.color           = trackBgColor;

        // 마커 (트랙의 자식 — anchor X = Progress01)
        GameObject markerObj = new GameObject("Marker");
        markerObj.transform.SetParent(trackObj.transform, false);
        slot.markerRect              = markerObj.AddComponent<RectTransform>();
        slot.markerRect.anchorMin    = new Vector2(0f, 0.5f);
        slot.markerRect.anchorMax    = new Vector2(0f, 0.5f);
        slot.markerRect.pivot        = new Vector2(0.5f, 0.5f);
        slot.markerRect.sizeDelta    = new Vector2(mS, mS);
        slot.markerRect.anchoredPosition = Vector2.zero;
        Image markerImg              = markerObj.AddComponent<Image>();
        markerImg.color              = markerColor;
        if (markerSprite != null) markerImg.sprite = markerSprite;

        // 초기 위치
        SetMarkerProgress(slot.markerRect, reach.Progress01);
    }

    // ── 이벤트 연결 ──────────────────────────────────────────────

    void DisconnectPreviousSlots()
    {
        if (slots == null) return;
        foreach (var slot in slots)
        {
            if (slot == null) continue;

            if (slot.objective is SurviveTimeObjective survive && slot.surviveListener != null)
            {
                survive.OnTimeChanged.RemoveListener(slot.surviveListener);
                slot.surviveListener = null;
            }
            if (slot.objective is ReachZoneObjective reach && slot.reachListener != null)
            {
                reach.OnProgressChanged.RemoveListener(slot.reachListener);
                slot.reachListener = null;
            }
        }
        slots = null;
    }

    void ConnectEvents()
    {
        if (slots == null) return;
        foreach (var slot in slots)
        {
            if (slot == null) continue;

            if (slot.objective is SurviveTimeObjective survive)
            {
                var captured = slot;
                captured.surviveListener = remaining =>
                {
                    if (captured.titleText != null)
                        captured.titleText.text = FormatSeconds(remaining);
                };
                survive.OnTimeChanged.AddListener(captured.surviveListener);
            }
            else if (slot.objective is ReachZoneObjective reach)
            {
                var captured = slot;
                captured.reachListener = progress =>
                {
                    if (captured.markerRect != null)
                        SetMarkerProgress(captured.markerRect, progress);
                };
                reach.OnProgressChanged.AddListener(captured.reachListener);
            }
        }
    }

    // ── 씬 전체 클리어 ───────────────────────────────────────────

    /// <summary>
    /// 씬 전체 클리어 시 Stage Clear 문구 표시.
    /// PhaseManager.onAllPhasesComplete 에 연결.
    /// </summary>
    public void ShowSceneClear()
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

    // ── 유틸 ─────────────────────────────────────────────────────

    static string FormatSeconds(float seconds) => Mathf.CeilToInt(seconds) + "s";

    static void SetMarkerProgress(RectTransform markerRect, float progress01)
    {
        float p = Mathf.Clamp01(progress01);
        markerRect.anchorMin = new Vector2(p, 0.5f);
        markerRect.anchorMax = new Vector2(p, 0.5f);
    }
}
