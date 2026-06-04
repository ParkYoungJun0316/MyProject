using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 보스 체력 바 UI.
/// BossFightObjective.OnPhaseCleared(int cleared, int total) 에 연결.
///
/// [UI 구조 예시]
///   BossHealthBar_Panel  (이 컴포넌트 부착, RectTransform 필수)
///     ├─ BossNameText      (TextMeshProUGUI, 선택)
///     └─ Segments
///          ├─ Seg1  (Image)  ← segments[0]
///          ├─ Seg2  (Image)  ← segments[1]
///          ├─ Seg3  (Image)
///          ├─ Seg4  (Image)
///          └─ Seg5  (Image)  ← segments[4]
///
/// [Inspector 연결]
///  objective    : 씬의 BossFightObjective
///  segments[]   : 체력 칸 Image 배열, 왼→오 순서로 5개 등록
///  bossNameText : (선택) 보스 이름 표시 텍스트
///
/// [BossFightObjective 쪽 설정]
///  OnPhaseCleared → BossHealthBarUI.OnPhaseCleared 연결
/// </summary>
public class BossHealthBarUI : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("씬의 BossFightObjective. 비우면 씬에서 자동 탐색.")]
    [SerializeField] BossFightObjective objective;

    [Header("세그먼트 (왼→오 순서로 페이즈 수만큼 등록)")]
    [SerializeField] Image[] segments;

    [Header("색상")]
    [Tooltip("아직 클리어 안 된 페이즈 — 체력 남은 색")]
    [SerializeField] Color activeColor  = new Color(0.85f, 0.15f, 0.12f, 1f);
    [Tooltip("클리어된 페이즈 — 체력 깎인 색")]
    [SerializeField] Color clearedColor = new Color(0.22f, 0.22f, 0.22f, 0.75f);

    [Header("히트 연출 (페이즈 클리어 시 체력바 흔들림)")]
    [Tooltip("흔들림 강도 (픽셀). 0이면 흔들림 없음.")]
    [SerializeField] float shakeStrength = 10f;
    [Tooltip("흔들림 지속 시간(초)")]
    [SerializeField] float shakeDuration = 0.4f;

    [Header("보스 이름 (선택)")]
    [SerializeField] TextMeshProUGUI bossNameText;
    [SerializeField] string          bossName = "입 보스";

    RectTransform _rt;

    // ── Unity ────────────────────────────────────────────────────

    void Start()
    {
        _rt = GetComponent<RectTransform>();

        if (bossNameText != null)
            bossNameText.text = bossName;

        if (objective == null)
            objective = FindFirstObjectByType<BossFightObjective>();

        if (objective != null)
            objective.OnPhaseCleared.AddListener(OnPhaseCleared);

        // 초기 상태: 전 세그먼트 체력 풀
        int total = objective != null ? objective.TotalPhases : segments != null ? segments.Length : 0;
        RefreshSegments(0, total);
    }

    void OnDestroy()
    {
        if (objective != null)
            objective.OnPhaseCleared.RemoveListener(OnPhaseCleared);
    }

    // ── 이벤트 수신 ──────────────────────────────────────────────

    /// <summary>BossFightObjective.OnPhaseCleared 에 연결.</summary>
    public void OnPhaseCleared(int cleared, int total)
    {
        RefreshSegments(cleared, total);

        StopAllCoroutines();
        if (shakeStrength > 0f)
            StartCoroutine(ShakeRoutine());
    }

    // ── 내부 ─────────────────────────────────────────────────────

    void RefreshSegments(int cleared, int total)
    {
        if (segments == null) return;

        // 오른쪽부터 깎임 (마지막 세그먼트가 먼저 회색으로)
        int remaining = total - cleared;
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] == null) continue;
            segments[i].color = i < remaining ? activeColor : clearedColor;
        }
    }

    IEnumerator ShakeRoutine()
    {
        if (_rt == null) yield break;

        Vector2 origin  = _rt.anchoredPosition;
        float   elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - (elapsed / shakeDuration); // 점점 약해지는 흔들림
            _rt.anchoredPosition = origin + Random.insideUnitCircle * shakeStrength * t;
            yield return null;
        }

        _rt.anchoredPosition = origin;
    }
}
