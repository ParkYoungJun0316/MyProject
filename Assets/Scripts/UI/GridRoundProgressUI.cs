using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GridRoundObjective의 라운드 진행을 원(●)—막대(━)—원 체인으로 표시.
///
/// [시각 규칙]
/// - 정산 완료 라운드 : 원 + 왼쪽 막대 → filledColor
/// - 현재 진행 중    : 원 → activeColor (강조)
/// - 미진행          : 원 + 막대 → emptyColor
///
/// [레이아웃 예시 — 7라운드, 3라운드 진행 중]
///  ●━━━●━━━◎━━━○━━━○━━━○━━━○
///  (0 done)(1 done)(2 active)(3~6 pending)
///
/// [Inspector 설정]
/// - objective     : GridRoundObjective 연결
/// - circleSprite  : 원 형태 Sprite (null이면 기본 사각형)
/// - 크기/색상     : Inspector에서 조정
/// </summary>
public class GridRoundProgressUI : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("감시할 RoundProgressObjective (GridRoundObjective 또는 MemoryRoundObjective)")]
    [SerializeField] RoundProgressObjective objective;

    [Header("크기")]
    [Tooltip("원 한 변의 길이(px)")]
    [SerializeField] float circleSize = 20f;

    [Tooltip("원과 원 사이 막대의 가로 길이(px)")]
    [SerializeField] float barWidth = 40f;

    [Tooltip("막대의 세로 높이(px)")]
    [SerializeField] float barHeight = 6f;

    [Header("스프라이트")]
    [Tooltip("원에 사용할 Sprite. null이면 Unity 기본 사각형 적용 (원형 Sprite 권장)")]
    [SerializeField] Sprite circleSprite;

    [Header("색상")]
    [Tooltip("정산 완료된 라운드 색")]
    [SerializeField] Color filledColor = new Color(0.27f, 1f, 0.27f, 1f);

    [Tooltip("현재 진행 중 라운드 강조 색")]
    [SerializeField] Color activeColor = new Color(1f, 0.9f, 0.2f, 1f);

    [Tooltip("아직 진행되지 않은 라운드 색")]
    [SerializeField] Color emptyColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);

    // ── 런타임 참조 ──────────────────────────────────────────────

    Image[] _circles;
    Image[] _bars;

    // ── Unity 라이프사이클 ────────────────────────────────────────

    void Start()
    {
        if (objective == null)
        {
            Debug.LogWarning($"[GridRoundProgressUI] objective가 연결되지 않았습니다. ({gameObject.name})");
            return;
        }

        objective.OnProgressChanged.AddListener(Refresh);
        Refresh();
    }

    void OnDestroy()
    {
        if (objective != null)
            objective.OnProgressChanged.RemoveListener(Refresh);
    }

    // ── 빌드 ─────────────────────────────────────────────────────

    /// <summary>
    /// TotalRounds 기준으로 원·막대 Image를 동적 생성.
    /// TotalRounds가 0이거나 이전과 동일하면 재빌드 생략.
    /// </summary>
    void Build(int total)
    {
        // 기존 자식 제거
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        _circles = null;
        _bars    = null;

        if (total <= 0) return;

        // HorizontalLayoutGroup — 이미 있으면 재사용
        HorizontalLayoutGroup hlg = GetComponent<HorizontalLayoutGroup>();
        if (hlg == null) hlg = gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = 0f;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.childAlignment         = TextAnchor.MiddleCenter;

        _circles = new Image[total];
        _bars    = total > 1 ? new Image[total - 1] : new Image[0];

        // 원 — 막대 — 원 — 막대 — … — 원
        for (int i = 0; i < total; i++)
        {
            _circles[i] = CreateImage($"Circle{i}", circleSize, circleSize, circleSprite, emptyColor);

            if (i < total - 1)
                _bars[i] = CreateImage($"Bar{i}", barWidth, barHeight, null, emptyColor);
        }
    }

    Image CreateImage(string objName, float w, float h, Sprite sprite, Color color)
    {
        var go = new GameObject(objName);
        go.transform.SetParent(transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);

        var img   = go.AddComponent<Image>();
        img.color = color;
        if (sprite != null)
            img.sprite = sprite;

        return img;
    }

    // ── 갱신 ─────────────────────────────────────────────────────

    void Refresh()
    {
        if (objective == null) return;

        int total = objective.TotalRounds;

        // 라운드 수가 바뀌었거나 아직 빌드 안 됐으면 재빌드
        if (_circles == null || _circles.Length != total)
        {
            Build(total);
            if (_circles == null) return;
        }

        int played  = objective.PlayedRounds;
        int current = objective.CurrentRoundIndex;

        // 원 색상
        for (int i = 0; i < _circles.Length; i++)
        {
            if (_circles[i] == null) continue;

            if (i < played)
                _circles[i].color = filledColor;
            else if (i == current)
                _circles[i].color = activeColor;
            else
                _circles[i].color = emptyColor;
        }

        // 막대 색상 — 막대 i는 원 i → 원 i+1 사이
        // 원 i(왼쪽)가 정산 완료되면 해당 막대 채움
        for (int i = 0; i < _bars.Length; i++)
        {
            if (_bars[i] == null) continue;
            _bars[i].color = (played > i) ? filledColor : emptyColor;
        }
    }
}
