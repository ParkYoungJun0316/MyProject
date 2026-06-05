using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Objective_Panel에 붙이는 스크립트.
/// StageManager의 objectives[] 를 읽어 목표별 슬롯을 자동 생성.
/// SurviveTime / ReachZone / OXQuiz / ColorTileRound / SequenceRing 지원.
/// </summary>
public class ObjectiveUI : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] StageManager stageManager;

    [Header("슬롯 크기/폰트")]
    [SerializeField] float  slotWidth      = 280f;
    [SerializeField] float  slotHeight     = 60f;
    [SerializeField] float  slotSpacing    = 8f;
    [SerializeField] float  titleFontSize  = 18f;
    [SerializeField] float  statusFontSize = 16f;
    [SerializeField] float  barHeight      = 12f;

    [Header("색상")]
    [SerializeField] Color barBgColor      = new Color(0.15f, 0.15f, 0.15f, 0.8f);
    [SerializeField] Color barFillColor    = new Color(0.2f, 0.8f, 0.3f, 1f);
    [SerializeField] Color completedColor  = new Color(0.9f, 0.9f, 0.2f, 1f);

    // ── 슬롯 데이터 ──────────────────────────────────────────────
    class ObjSlot
    {
        public StageObjective  objective;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI statusText;
        public Image           barFill;
        public Image           barBg;
    }

    ObjSlot[] slots;

    // ── 초기화 ───────────────────────────────────────────────────

    void Start()
    {
        if (stageManager == null)
            stageManager = FindFirstObjectByType<StageManager>();
        BuildSlots();
    }

    /// <summary>
    /// 스테이지 전환 시 UI 갱신.
    /// PhaseManager의 onPhaseEnter 이벤트에 연결해서 사용.
    /// </summary>
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
        RectTransform rootRt = root.AddComponent<RectTransform>();
        rootRt.sizeDelta = new Vector2(slotWidth, slotHeight);

        Image bg = root.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.45f);

        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(root.transform, false);
        slot.titleText           = titleObj.AddComponent<TextMeshProUGUI>();
        slot.titleText.fontSize  = titleFontSize;
        slot.titleText.fontStyle = FontStyles.Bold;
        slot.titleText.color     = Color.white;
        slot.titleText.text      = obj.objectiveName;
        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0.55f);
        titleRt.anchorMax = new Vector2(0.65f, 1f);
        titleRt.offsetMin = new Vector2(8f,  0f);
        titleRt.offsetMax = new Vector2(0f,  -4f);

        GameObject statusObj = new GameObject("Status");
        statusObj.transform.SetParent(root.transform, false);
        slot.statusText           = statusObj.AddComponent<TextMeshProUGUI>();
        slot.statusText.fontSize  = statusFontSize;
        slot.statusText.color     = Color.white;
        slot.statusText.alignment = TextAlignmentOptions.MidlineRight;
        RectTransform statusRt = statusObj.GetComponent<RectTransform>();
        statusRt.anchorMin = new Vector2(0.6f, 0.5f);
        statusRt.anchorMax = new Vector2(1f,   1f);
        statusRt.offsetMin = new Vector2(0f,  0f);
        statusRt.offsetMax = new Vector2(-8f, -4f);

        GameObject barBgObj = new GameObject("BarBG");
        barBgObj.transform.SetParent(root.transform, false);
        slot.barBg       = barBgObj.AddComponent<Image>();
        slot.barBg.color = barBgColor;
        RectTransform barBgRt = barBgObj.GetComponent<RectTransform>();
        barBgRt.anchorMin = new Vector2(0f, 0f);
        barBgRt.anchorMax = new Vector2(1f, 0f);
        barBgRt.offsetMin = new Vector2(8f,   4f);
        barBgRt.offsetMax = new Vector2(-8f,  4f + barHeight);

        GameObject barFillObj = new GameObject("BarFill");
        barFillObj.transform.SetParent(barBgObj.transform, false);
        slot.barFill             = barFillObj.AddComponent<Image>();
        slot.barFill.color       = barFillColor;
        slot.barFill.type        = Image.Type.Filled;
        slot.barFill.fillMethod  = Image.FillMethod.Horizontal;
        slot.barFill.fillOrigin  = (int)Image.OriginHorizontal.Left;
        slot.barFill.fillAmount  = 0f;
        RectTransform fillRt = barFillObj.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.sizeDelta = Vector2.zero;

        return slot;
    }

    // ── 이벤트 연결 ──────────────────────────────────────────────

    void DisconnectPreviousSlots()
    {
        if (slots == null) return;
        foreach (ObjSlot slot in slots)
        {
            if (slot == null) continue;
            StageObjective obj = slot.objective;
            if (obj == null) continue;

            obj.OnCompleted.RemoveAllListeners();
            obj.OnFailed.RemoveAllListeners();

            if (obj is SurviveTimeObjective survive)
                survive.OnTimeChanged.RemoveAllListeners();

            if (obj is OXQuizObjective ox)
                ox.OnProgressChanged.RemoveAllListeners();

            if (obj is ColorTileRoundObjective round)
                round.OnHistoryUpdated.RemoveAllListeners();

            if (obj is SequenceRingObjective ring)
                ring.OnProgressChanged.RemoveAllListeners();

            if (obj is RoundProgressObjective roundProg)
                roundProg.OnProgressChanged.RemoveAllListeners();
        }
        slots = null;
    }

    void ConnectEvents()
    {
        if (slots == null) return;
        foreach (var slot in slots)
        {
            if (slot == null) continue;
            var obj = slot.objective;

            if (obj is SurviveTimeObjective survive)
                survive.OnTimeChanged.AddListener(_ => UpdateSlot(slot));

            if (obj is OXQuizObjective ox)
                ox.OnProgressChanged.AddListener((_, __) => UpdateSlot(slot));

            if (obj is ColorTileRoundObjective round)
                round.OnHistoryUpdated.AddListener(() => UpdateSlot(slot));

            if (obj is SequenceRingObjective ring)
                ring.OnProgressChanged.AddListener(() => UpdateSlot(slot));

            if (obj is RoundProgressObjective roundProg)
                roundProg.OnProgressChanged.AddListener(() => UpdateSlot(slot));

            obj.OnCompleted.AddListener(() => OnObjectiveCompleted(slot));
            obj.OnFailed.AddListener(() => OnObjectiveFailed(slot));

            UpdateSlot(slot);
        }
    }

    // ── 슬롯 갱신 ────────────────────────────────────────────────

    void UpdateSlot(ObjSlot slot)
    {
        if (slot == null) return;
        var obj = slot.objective;

        if (obj is SurviveTimeObjective survive)
        {
            float fill = survive.targetTime > 0
                ? Mathf.Clamp01(survive.Elapsed / survive.targetTime)
                : 0f;
            SetBar(slot, fill, barFillColor);
            int rem = Mathf.CeilToInt(survive.Remaining);
            slot.statusText.text = $"{rem}초 남음";
        }
        else if (obj is ReachZoneObjective reach)
        {
            SetBar(slot, reach.IsCompleted ? 1f : 0f, barFillColor);
            slot.statusText.text = reach.IsCompleted ? "도달 완료" : "이동 중...";
        }
        else if (obj is OXQuizObjective ox)
        {
            float fill = ox.TotalQuestions > 0
                ? Mathf.Clamp01((float)ox.CurrentQuestion / ox.TotalQuestions)
                : 0f;
            SetBar(slot, fill, barFillColor);
            slot.statusText.text = ox.TotalQuestions > 0
                ? $"{ox.CurrentQuestion} / {ox.TotalQuestions} 문제"
                : "대기 중...";
        }
        else if (obj is ColorTileRoundObjective round)
        {
            int req   = round.RequiredSuccesses;
            int succ  = round.SuccessCount;
            int total = round.TotalRounds;
            float fill = req > 0 ? Mathf.Clamp01((float)succ / req) : 0f;
            SetBar(slot, fill, barFillColor);
            slot.statusText.text = total > 0
                ? $"{succ} / {req}성공  ({round.PlayedRounds}/{total}라운드)"
                : "대기 중...";
        }
        else if (obj is SequenceRingObjective ring)
        {
            int   remSteps = ring.RemainingSteps;
            int   total    = ring.TotalSteps;
            float remTime  = ring.TimeRemaining;
            float timeLimit = ring.TimeLimit;

            float fill = total > 0 ? Mathf.Clamp01(1f - (float)remSteps / total) : 0f;
            SetBar(slot, fill, barFillColor);

            int remSec = Mathf.CeilToInt(remTime);
            slot.statusText.text = $"{remSteps} step · {remSec}초";
        }
        else if (obj is RoundProgressObjective roundProg)
        {
            int played = roundProg.PlayedRounds;
            int total  = roundProg.TotalRounds;
            int cur    = roundProg.CurrentRoundIndex;

            float fill = total > 0 ? Mathf.Clamp01((float)played / total) : 0f;
            SetBar(slot, fill, barFillColor);
            slot.statusText.text = total > 0
                ? (cur >= 0 ? $"라운드 {cur + 1} / {total}" : $"{played} / {total}")
                : "대기 중...";
        }
    }

    void SetBar(ObjSlot slot, float fill, Color color)
    {
        if (slot.barFill == null) return;
        slot.barFill.fillAmount = fill;
        slot.barFill.color      = color;
    }

    void OnObjectiveCompleted(ObjSlot slot)
    {
        if (slot.barFill != null)  slot.barFill.color      = completedColor;
        if (slot.titleText != null) slot.titleText.color   = completedColor;
        if (slot.statusText != null) slot.statusText.text  = "완료 ✓";
        if (slot.barFill != null)  slot.barFill.fillAmount = 1f;
    }

    void OnObjectiveFailed(ObjSlot slot)
    {
        UpdateSlot(slot);
    }
}
