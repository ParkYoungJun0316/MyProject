using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Objective_Panel에 붙이는 스크립트.
/// StageManager의 objectives[] 를 읽어 목표별 슬롯을 자동 생성.
/// KillAll / SurviveTime / HoldZone / HoldColorTiles / ReachZone / BoxCount 지원.
/// </summary>
public class ObjectiveUI : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] StageManager stageManager;
    [Tooltip("박스 이동 목표가 있을 때 연결 (StageObjective가 아니므로 별도 연결)")]
    [SerializeField] BoxCountZone boxCountZone;

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
    [SerializeField] Color barHoldColor    = new Color(0.2f, 0.6f, 1f,   1f);
    [SerializeField] Color completedColor  = new Color(0.9f, 0.9f, 0.2f, 1f);

    // ── 슬롯 데이터 ──────────────────────────────────────────────
    class ObjSlot
    {
        public StageObjective objective;  // null이면 BoxCountZone 슬롯
        public BoxCountZone   boxZone;
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
    /// 현재 활성화된 StageManager를 자동으로 찾아 슬롯을 재구성.
    /// </summary>
    public void Refresh()
    {
        stageManager  = FindFirstObjectByType<StageManager>();
        boxCountZone  = FindFirstObjectByType<BoxCountZone>();
        BuildSlots();
    }


    void BuildSlots()
    {
        // 이전 슬롯의 이벤트 리스너를 먼저 제거 (중복 누적 방지)
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

        int totalCount = (stageManager != null ? stageManager.objectives.Length : 0)
                       + (boxCountZone != null ? 1 : 0);
        if (totalCount == 0) return;

        slots = new ObjSlot[totalCount];
        int idx = 0;

        if (stageManager != null)
        {
            foreach (var obj in stageManager.objectives)
            {
                if (obj == null) continue;
                slots[idx++] = CreateSlot(obj, null);
            }
        }
        if (boxCountZone != null)
            slots[idx] = CreateSlot(null, boxCountZone);

        // 이벤트 연결
        ConnectEvents();
    }

    ObjSlot CreateSlot(StageObjective obj, BoxCountZone box)
    {
        ObjSlot slot = new ObjSlot { objective = obj, boxZone = box };

        // 슬롯 루트
        GameObject root = new GameObject(obj != null ? obj.objectiveName : "BoxCount");
        root.transform.SetParent(transform, false);
        RectTransform rootRt = root.AddComponent<RectTransform>();
        rootRt.sizeDelta = new Vector2(slotWidth, slotHeight);

        // 배경
        Image bg = root.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.45f);

        // 제목 텍스트
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(root.transform, false);
        slot.titleText           = titleObj.AddComponent<TextMeshProUGUI>();
        slot.titleText.fontSize  = titleFontSize;
        slot.titleText.fontStyle = FontStyles.Bold;
        slot.titleText.color     = Color.white;
        slot.titleText.text      = obj != null ? obj.objectiveName : "박스 이동";
        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0.55f);
        titleRt.anchorMax = new Vector2(0.65f, 1f);
        titleRt.offsetMin = new Vector2(8f,  0f);
        titleRt.offsetMax = new Vector2(0f,  -4f);

        // 상태 텍스트 (우측)
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

        // 진행 바 배경
        GameObject barBgObj = new GameObject("BarBG");
        barBgObj.transform.SetParent(root.transform, false);
        slot.barBg       = barBgObj.AddComponent<Image>();
        slot.barBg.color = barBgColor;
        RectTransform barBgRt = barBgObj.GetComponent<RectTransform>();
        barBgRt.anchorMin = new Vector2(0f, 0f);
        barBgRt.anchorMax = new Vector2(1f, 0f);
        barBgRt.offsetMin = new Vector2(8f,   4f);
        barBgRt.offsetMax = new Vector2(-8f,  4f + barHeight);

        // 진행 바 fill
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

    /// <summary>이전 BuildSlots()에서 등록한 리스너를 전부 제거. Refresh() 시 중복 방지.</summary>
    void DisconnectPreviousSlots()
    {
        if (slots == null) return;
        foreach (ObjSlot slot in slots)
        {
            if (slot == null) continue;

            if (slot.boxZone != null)
            {
                slot.boxZone.OnCountChanged.RemoveAllListeners();
                continue;
            }

            StageObjective obj = slot.objective;
            if (obj == null) continue;

            obj.OnCompleted.RemoveAllListeners();
            obj.OnFailed.RemoveAllListeners();

            if (obj is SurviveTimeObjective survive)
                survive.OnTimeChanged.RemoveAllListeners();
            else if (obj is KillAllEnemiesObjective kill)
                kill.OnKillCountChanged.RemoveAllListeners();
            else if (obj is HoldZoneObjective hold)
            {
                hold.OnHoldTimeChanged.RemoveAllListeners();
                hold.OnHoldBroken.RemoveAllListeners();
            }
            else if (obj is HoldColorTilesObjective holdColor)
            {
                holdColor.OnHoldTimeChanged.RemoveAllListeners();
                holdColor.OnHoldBroken.RemoveAllListeners();
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
            if (slot.boxZone != null)
            {
                slot.boxZone.OnCountChanged.AddListener(_ => UpdateSlot(slot));
                UpdateSlot(slot);
                continue;
            }

            var obj = slot.objective;
            if (obj is KillAllEnemiesObjective kill)
                kill.OnKillCountChanged.AddListener((_, __) => UpdateSlot(slot));
            else if (obj is SurviveTimeObjective survive)
                survive.OnTimeChanged.AddListener(_ => UpdateSlot(slot));
            else if (obj is HoldZoneObjective hold)
            {
                hold.OnHoldTimeChanged.AddListener(_ => UpdateSlot(slot));
                hold.OnHoldBroken.AddListener(() => UpdateSlot(slot));
            }
            else if (obj is HoldColorTilesObjective holdColor)
            {
                holdColor.OnHoldTimeChanged.AddListener(_ => UpdateSlot(slot));
                holdColor.OnHoldBroken.AddListener(() => UpdateSlot(slot));
            }

            // 완료/실패 이벤트
            obj.OnCompleted.AddListener(() => OnObjectiveCompleted(slot));
            obj.OnFailed.AddListener(() => OnObjectiveFailed(slot));

            UpdateSlot(slot);
        }
    }

    // ── 슬롯 갱신 ────────────────────────────────────────────────

    void UpdateSlot(ObjSlot slot)
    {
        if (slot == null) return;

        // BoxCountZone
        if (slot.boxZone != null)
        {
            int cur   = slot.boxZone.CurrentCount;
            int total = slot.boxZone.requiredCount;
            float fill = total > 0 ? Mathf.Clamp01((float)cur / total) : 0f;
            SetBar(slot, fill, barFillColor);
            slot.statusText.text = $"{cur} / {total}";
            return;
        }

        var obj = slot.objective;

        if (obj is KillAllEnemiesObjective kill)
        {
            float fill = kill.TotalCount > 0
                ? Mathf.Clamp01((float)kill.KilledCount / kill.TotalCount)
                : 0f;
            SetBar(slot, fill, barFillColor);
            slot.statusText.text = $"{kill.Remaining}마리 남음";
        }
        else if (obj is SurviveTimeObjective survive)
        {
            float fill = survive.targetTime > 0
                ? Mathf.Clamp01(survive.Elapsed / survive.targetTime)
                : 0f;
            SetBar(slot, fill, barFillColor);
            int rem = Mathf.CeilToInt(survive.Remaining);
            slot.statusText.text = $"{rem}초 남음";
        }
        else if (obj is HoldZoneObjective hold)
        {
            float fill = hold.holdDuration > 0
                ? Mathf.Clamp01(hold.Elapsed / hold.holdDuration)
                : 0f;
            Color c = hold.IsHolding ? barHoldColor : barFillColor;
            SetBar(slot, fill, c);
            slot.statusText.text = hold.IsHolding
                ? $"점령 중 {Mathf.CeilToInt(hold.Remaining)}초"
                : "구역 이탈";
        }
        else if (obj is HoldColorTilesObjective holdColor)
        {
            float fill = holdColor.holdDuration > 0
                ? Mathf.Clamp01(holdColor.Elapsed / holdColor.holdDuration)
                : 0f;
            SetBar(slot, fill, barHoldColor);
            slot.statusText.text = $"{Mathf.CeilToInt(holdColor.Remaining)}초";
        }
        else if (obj is ReachZoneObjective reach)
        {
            SetBar(slot, reach.IsCompleted ? 1f : 0f, barFillColor);
            slot.statusText.text = reach.IsCompleted ? "도달 완료" : "이동 중...";
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
        if (slot.barFill != null) slot.barFill.color = completedColor;
        if (slot.titleText != null) slot.titleText.color = completedColor;
        if (slot.statusText != null) slot.statusText.text = "완료 ✓";
        if (slot.barFill != null) slot.barFill.fillAmount = 1f;
    }

    void OnObjectiveFailed(ObjSlot slot)
    {
        // 실패도 리셋과 동일하게 처리 — 죽거나 실패나 결과가 같으므로 구분 없음
        UpdateSlot(slot);
    }
}
