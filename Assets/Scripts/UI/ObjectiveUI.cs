using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Objective_Panel에 붙이는 스크립트.
/// StageManager의 objectives[] 를 읽어 목표 슬롯을 자동 생성.
///
/// [Clear 표시 흐름]
/// - 중간 스테이지 클리어(OnStageClear) → 슬롯 갱신 없음 (문구 X)
/// - 씬 전체 클리어(onAllPhasesComplete) → ShowSceneClear() 연결 → "Clear"
/// - 다음 스테이지 전환(onPhaseEnter)    → Refresh() 연결 → 슬롯 재생성
///
/// [표시 모드 SSOT — 타입 분기 대신 모드로 통일. 새 Objective 추가 시 아래 4개 중 하나로 분류할 것]
/// - Time        : "275s"       — SurviveTimeObjective
/// - Count       : "3/5"        — RoundProgressObjective 전부 (OXQuiz/Grid/ColorTile/MemoryRound). 성공·실패 구분 없이 진행 라운드 수만 표시
/// - Count+Timer : "2/5 · 18s"  — SequenceRingObjective, Stage5TargetObjective
/// - Ratio       : 가로 트랙 바 + 마커 (0~1) — ReachZoneObjective만
/// 그 외(분류 안 된 StageObjective)는 지원하지 않음 — objectiveName 텍스트로 대체하지 않는다.
/// Boss(BossFightObjective)는 StageObjective가 아니며 이 UI가 다루지 않음 — BossHealthBarUI 별도 유지.
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

    [Header("ReachZone 바 (Ratio 모드)")]
    [Tooltip("트랙 바에 사용할 Sprite. 비우면 단색.")]
    [SerializeField] Sprite trackSprite;
    [Tooltip("트랙 색. 커스텀 스프라이트를 쓸 때는 흰색으로.")]
    [SerializeField] Color trackBgColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [Tooltip("트랙 가로 길이(px). 0이면 슬롯 너비에서 좌우 여백을 뺀 값.")]
    [SerializeField] float trackWidth = 0f;
    [Tooltip("트랙 세로 높이(px). 0이면 슬롯 높이의 30% 자동 적용.")]
    [SerializeField] float trackHeight = 0f;
    [Tooltip("마커에 사용할 Sprite. 비우면 기본 사각형.")]
    [SerializeField] Sprite markerSprite;
    [Tooltip("마커 색")]
    [SerializeField] Color markerColor  = new Color(1f, 1f, 1f, 1f);
    [Tooltip("마커 한 변 크기(px). 0이면 슬롯 높이의 60% 자동 적용.")]
    [SerializeField] float markerSize   = 0f;
    [Tooltip("마커 Y 오프셋(px). 양수면 위로.")]
    [SerializeField] float markerYOffset = 0f;

    [Header("Clear")]
    [Tooltip("씬 전체 클리어 시 슬롯에 표시할 문구")]
    [SerializeField] string clearMessage   = "Clear";
    [SerializeField] Color  clearBgColor   = new Color(1f, 0.4f, 0.7f, 0.9f);
    [SerializeField] Color  clearTextColor = Color.white;

    // ── 슬롯 데이터 ──────────────────────────────────────────────

    class ObjSlot
    {
        public StageObjective  objective;
        public Image           bgImage;
        public TextMeshProUGUI titleText;   // Time / Count / Count+Timer 공용
        public RectTransform   markerRect;  // Ratio(ReachZone) 전용

        public UnityAction<float>   surviveListener;
        public UnityAction<float>   reachListener;
        public UnityAction          roundListener;          // RoundProgressObjective (Count)
        public UnityAction          seqListener;             // SequenceRingObjective (Count+Timer)
        public UnityAction<int,int> stage5CaptureListener;   // Stage5TargetObjective (Count+Timer)
        public UnityAction<float>   stage5TimerListener;

        // Stage5는 캡처/타이머 이벤트가 서로 독립 발동 — 조합 표시를 위해 최신값 캐시
        public int   stage5Captured;
        public int   stage5Required;
        public float stage5Remaining;
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

        // ── 모드별 콘텐츠 ─────────────────────────────────────────
        if (obj is SurviveTimeObjective survive)
        {
            BuildTextContent(root, slot, FormatSeconds(survive.Remaining));
        }
        else if (obj is ReachZoneObjective reach)
        {
            BuildReachZoneContent(root, slot, reach);
        }
        else if (obj is SequenceRingObjective seq)
        {
            int done = Mathf.Max(0, seq.TotalSteps - seq.RemainingSteps);
            BuildTextContent(root, slot, FormatCountTimer(done, seq.TotalSteps, seq.TimeRemaining));
        }
        else if (obj is Stage5TargetObjective stage5)
        {
            slot.stage5Captured  = stage5.CapturedCount;
            slot.stage5Required  = stage5.requiredCaptures;
            slot.stage5Remaining = stage5.Remaining;
            BuildTextContent(root, slot, FormatCountTimer(slot.stage5Captured, slot.stage5Required, slot.stage5Remaining));
        }
        else if (obj is RoundProgressObjective round)
        {
            BuildTextContent(root, slot, FormatCount(round.PlayedRounds, round.TotalRounds));
        }
        else
        {
            Debug.LogWarning($"[ObjectiveUI] 지원하지 않는 Objective 타입: {obj.GetType().Name} ({obj.gameObject.name}) — " +
                              "Time/Count/Count+Timer/Ratio 중 하나로 분류해야 합니다.");
            BuildTextContent(root, slot, string.Empty);
        }

        return slot;
    }

    // ── 텍스트 슬롯 (Time / Count / Count+Timer 공용) ─────────────

    void BuildTextContent(GameObject root, ObjSlot slot, string initialText)
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

        slot.titleText.text = initialText;
    }

    // ── Ratio 슬롯 (트랙 바 + 마커, ReachZone 전용) ───────────────

    void BuildReachZoneContent(GameObject root, ObjSlot slot, ReachZoneObjective reach)
    {
        float tH = trackHeight > 0f ? trackHeight : slotHeight * 0.30f;
        float tW = trackWidth  > 0f ? trackWidth  : Mathf.Max(0f, slotWidth - 24f);
        float mS = markerSize   > 0f ? markerSize   : slotHeight * 0.60f;

        // 트랙 배경 — 고정 크기(가운데 정렬). 마커는 이 트랙의 0~1을 따라감.
        GameObject trackObj = new GameObject("Track");
        trackObj.transform.SetParent(root.transform, false);
        RectTransform trackRt = trackObj.AddComponent<RectTransform>();
        trackRt.anchorMin        = new Vector2(0.5f, 0.5f);
        trackRt.anchorMax        = new Vector2(0.5f, 0.5f);
        trackRt.pivot            = new Vector2(0.5f, 0.5f);
        trackRt.sizeDelta        = new Vector2(tW, tH);
        trackRt.anchoredPosition  = Vector2.zero;
        Image trackImg           = trackObj.AddComponent<Image>();
        trackImg.color           = trackBgColor;
        trackImg.raycastTarget   = false;
        if (trackSprite != null)
        {
            trackImg.sprite = trackSprite;
            trackImg.type   = trackSprite.border.sqrMagnitude > 0f
                ? Image.Type.Sliced
                : Image.Type.Simple;
        }

        // 마커 (트랙의 자식 — anchor X = Progress01)
        GameObject markerObj = new GameObject("Marker");
        markerObj.transform.SetParent(trackObj.transform, false);
        slot.markerRect              = markerObj.AddComponent<RectTransform>();
        slot.markerRect.anchorMin    = new Vector2(0f, 0.5f);
        slot.markerRect.anchorMax    = new Vector2(0f, 0.5f);
        slot.markerRect.pivot        = new Vector2(0.5f, 0.5f);
        slot.markerRect.sizeDelta    = new Vector2(mS, mS);
        slot.markerRect.anchoredPosition = new Vector2(0f, markerYOffset);
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
            if (slot.objective is SequenceRingObjective seq && slot.seqListener != null)
            {
                seq.OnProgressChanged.RemoveListener(slot.seqListener);
                slot.seqListener = null;
            }
            if (slot.objective is Stage5TargetObjective stage5)
            {
                if (slot.stage5CaptureListener != null)
                {
                    stage5.OnCaptureCountChanged.RemoveListener(slot.stage5CaptureListener);
                    slot.stage5CaptureListener = null;
                }
                if (slot.stage5TimerListener != null)
                {
                    stage5.OnTimerChanged.RemoveListener(slot.stage5TimerListener);
                    slot.stage5TimerListener = null;
                }
            }
            if (slot.objective is RoundProgressObjective round && slot.roundListener != null)
            {
                round.OnProgressChanged.RemoveListener(slot.roundListener);
                slot.roundListener = null;
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
            else if (slot.objective is SequenceRingObjective seq)
            {
                var captured = slot;
                captured.seqListener = () =>
                {
                    if (captured.titleText == null) return;
                    int done = Mathf.Max(0, seq.TotalSteps - seq.RemainingSteps);
                    captured.titleText.text = FormatCountTimer(done, seq.TotalSteps, seq.TimeRemaining);
                };
                seq.OnProgressChanged.AddListener(captured.seqListener);
            }
            else if (slot.objective is Stage5TargetObjective stage5)
            {
                var captured = slot;
                captured.stage5CaptureListener = (capturedCount, required) =>
                {
                    captured.stage5Captured = capturedCount;
                    captured.stage5Required = required;
                    RefreshStage5Text(captured);
                };
                captured.stage5TimerListener = remaining =>
                {
                    captured.stage5Remaining = remaining;
                    RefreshStage5Text(captured);
                };
                stage5.OnCaptureCountChanged.AddListener(captured.stage5CaptureListener);
                stage5.OnTimerChanged.AddListener(captured.stage5TimerListener);
            }
            else if (slot.objective is RoundProgressObjective round)
            {
                var captured = slot;
                captured.roundListener = () =>
                {
                    if (captured.titleText != null)
                        captured.titleText.text = FormatCount(round.PlayedRounds, round.TotalRounds);
                };
                round.OnProgressChanged.AddListener(captured.roundListener);
            }
        }
    }

    static void RefreshStage5Text(ObjSlot slot)
    {
        if (slot.titleText != null)
            slot.titleText.text = FormatCountTimer(slot.stage5Captured, slot.stage5Required, slot.stage5Remaining);
    }

    // ── 씬 전체 클리어 ───────────────────────────────────────────

    /// <summary>
    /// 씬 전체 클리어 시 Clear 문구 표시.
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

    // ── 표시 포맷 (모드별 SSOT — 여기서만 문자열을 만든다) ─────────

    static string FormatSeconds(float seconds) => Mathf.CeilToInt(seconds) + "s";

    static string FormatCount(int played, int total) => $"{played}/{total}";

    static string FormatCountTimer(int played, int total, float secondsRemaining) =>
        $"{played}/{total} · {Mathf.CeilToInt(secondsRemaining)}s";

    static void SetMarkerProgress(RectTransform markerRect, float progress01)
    {
        float p = Mathf.Clamp01(progress01);
        markerRect.anchorMin = new Vector2(p, 0.5f);
        markerRect.anchorMax = new Vector2(p, 0.5f);
    }
}
