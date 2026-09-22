using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 보스 체력 바 UI.
/// BossFightObjective.OnPhaseCleared(int cleared, int total) 에 연결.
///
/// [UI 구조 예시]
///   BossHealthBar_Panel  (이 컴포넌트 부착, RectTransform 필수 — UI 루트 하위 독립 패널.
///                         ObjectiveUI/Objective_Panel과는 별개로 둔다)
///     ├─ BossNameText      (TextMeshProUGUI, 선택)
///     ├─ SegmentsBG        (Image, 선택)  ← segmentsBg — 세그먼트 전체 뒤 고정 배경 1장
///     └─ Segments
///          ├─ Seg1  (Image)  ← segments[0]
///          ├─ Seg2  (Image)  ← segments[1]
///          ├─ Seg3  (Image)
///          ├─ Seg4  (Image)
///          └─ Seg5  (Image)  ← segments[4]
///     └─ SphereTrack       (Image, T.Boss 전용/선택) — Sphere 하강 진행도 트랙
///          ├─ SphereTrackBg  (Image)  ← sphereTrackBg — 남은 구간(트랙 배경), 스프라이트 자유 교체
///          ├─ SpherePinkFill (Image, Type=Filled/Horizontal)  ← spherePinkFill — 지나온 구간
///          └─ SphereMarker  (Image, 사탕 스프라이트)  ← sphereMarkerRect (트랙의 자식으로 둘 것)
///
/// [Inspector 연결]
///  objective        : 씬의 BossFightObjective
///  segments[]       : 체력 칸 Image 배열, 왼→오 순서로 5개 등록 (M.Boss 전용, T.Boss는 비워둘 것)
///  segmentsBg       : (선택) 세그먼트 뒤에 까는 고정 배경 — color tint 대상 아님 (M.Boss 전용)
///  bossNameText     : (선택) 보스 이름 표시 텍스트
///  sphereDriver     : (T.Boss 전용) 씬의 BossSpherePhaseDriver — 진행도·전체거리 SSOT
///  sphereMarkerRect : (T.Boss 전용) 트랙 위를 움직이는 사탕 마커
///  sphereTrackBg    : (T.Boss 전용) 트랙 배경(남은 구간) — 존재만 확인, 값 갱신 없음
///  spherePinkFill   : (T.Boss 전용) 지나온 구간 Fill — fillAmount를 진행도로 매 프레임 갱신
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

    [Header("배경 (선택)")]
    [Tooltip("세그먼트 전체 뒤에 까는 고정 배경 이미지 (BG). 색 tint 대상 아님 — RefreshSegments가 건드리지 않음.")]
    [SerializeField] Image segmentsBg;

    [Header("히트 연출 (페이즈 클리어 시 체력바 흔들림)")]
    [Tooltip("흔들림 강도 (픽셀). 0이면 흔들림 없음.")]
    [SerializeField] float shakeStrength = 10f;
    [Tooltip("흔들림 지속 시간(초)")]
    [SerializeField] float shakeDuration = 0.4f;

    [Header("보스 이름 (선택)")]
    [SerializeField] TextMeshProUGUI bossNameText;
    [SerializeField] string          bossName = "입 보스";

    [Header("Sphere 진행도 마커 (T.Boss 전용, 선택)")]
    [Tooltip("씬의 BossSpherePhaseDriver. 진행도(0~1)와 전체 하강거리를 여기서만 읽는다 —\n" +
             "전체 거리를 UI에 다시 적지 않기 위함(SSOT는 driver의 checkpointDistances[]).\n" +
             "비우면 마커 갱신을 하지 않음 — M.Boss 등 Sphere가 없는 씬은 비워둘 것.")]
    [SerializeField] BossSpherePhaseDriver sphereDriver;
    [Tooltip("트랙(Track) 위를 움직이는 마커 RectTransform. 마커는 트랙의 자식으로 두고,\n" +
             "이 컴포넌트가 anchorMin/Max.x를 진행도(0~1)로 갱신한다\n" +
             "(ObjectiveUI의 Ratio 슬롯 Track/Marker 구조와 동일).")]
    [SerializeField] RectTransform sphereMarkerRect;
    [Tooltip("트랙 배경(남은 구간) Image. 스프라이트/색은 에디터에서 자유롭게 교체 —\n" +
             "이 컴포넌트는 존재 여부만 보고 아무 값도 갱신하지 않는다(색 tint 대상 아님).\n" +
             "비우면 배경 없이 진행 — M.Boss 등 Sphere가 없는 씬은 비워둘 것.")]
    [SerializeField] Image sphereTrackBg;
    [Tooltip("지나온 구간을 나타내는 가로 Fill 이미지 (Image Type=Filled, Fill Method=Horizontal).\n" +
             "fillAmount을 진행도(0~1)로 매 프레임 갱신한다. 비우면 갱신하지 않음.")]
    [SerializeField] Image spherePinkFill;

    [Tooltip("마커가 트랙 양 끝에서 더 안쪽으로 들어올 여백(px). 마커 반지름은 이미 자동으로\n" +
             "보정되므로(진행도 0에서 마커 왼쪽 변이 트랙 왼쪽 끝에 맞음) 0으로 둬도 마커가\n" +
             "트랙 밖으로 나가지 않는다. 식도 그림처럼 끝이 좁아지는 배경에서 더 안쪽에\n" +
             "붙이고 싶을 때만 값을 준다.")]
    [SerializeField] float markerEdgePadding = 0f;

    RectTransform _rt;
    float         _lastMarkerProgress = -1f;

    // ── Unity ────────────────────────────────────────────────────

    void Start()
    {
        _rt = GetComponent<RectTransform>();

        if (bossNameText != null)
            bossNameText.text = bossName;

        if (objective == null)
            objective = FindFirstObjectByType<BossFightObjective>();

        // OnPhaseCleared 구독은 Inspector 연결(BossFightObjective.OnPhaseCleared →
        // BossHealthBarUI.OnPhaseCleared)만 사용한다. 여기서 AddListener까지 추가하면
        // Inspector 연결이 이미 된 씬에서 한 클리어당 이 메서드가 2번 불려 ShakeRoutine이
        // 시작한 지 한 프레임도 안 돼 StopAllCoroutines에 죽고 재시작되며 anchoredPosition
        // 기준점이 어긋나 흔들림이 겹쳐 튀었다(이중 구독 버그 — 티켓 B #4). Inspector 연결이
        // 누락된 씬(예: T.Boss)에서는 직접 연결할 것.

        // 초기 상태: 전 세그먼트 체력 풀
        int total = objective != null ? objective.TotalPhases : segments != null ? segments.Length : 0;
        RefreshSegments(0, total);

        if (sphereMarkerRect != null || spherePinkFill != null)
            SetSphereMarkerProgress(0f);
    }

    void Update()
    {
        // sphereDriver가 비어있으면(M.Boss 등 Sphere 없는 씬) 갱신하지 않음.
        // 이벤트 구독 없이 매 프레임 값을 직접 읽는다(§H.4) — 단 값이 실제로 바뀐 프레임만
        // anchor/fillAmount를 다시 써서 UI를 매 프레임 dirty로 만들지 않는다.
        if (sphereDriver == null || (sphereMarkerRect == null && spherePinkFill == null)) return;

        SetSphereMarkerProgress(sphereDriver.Progress01);
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

        // 오른쪽부터 깎임 (마지막 세그먼트가 먼저 사라짐) — 색 tint 없이 껐다 켰다만
        int remaining = total - cleared;
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] == null) continue;
            segments[i].gameObject.SetActive(i < remaining);
        }
    }

    /// <summary>ObjectiveUI.SetMarkerProgress와 동일한 코드 형태 — 트랙 위 마커를 진행도(0~1)로 이동하고
    /// 지나온 구간 핑크 Fill을 같은 값으로 채운다.</summary>
    void SetSphereMarkerProgress(float progress01)
    {
        float p = Mathf.Clamp01(progress01);
        if (Mathf.Approximately(p, _lastMarkerProgress)) return;

        _lastMarkerProgress = p;

        if (sphereMarkerRect != null)
        {
            // 마커 중심을 그대로 0~1에 놓으면 양 끝에서 마커가 트랙 밖으로 절반 삐져나온다.
            // 마커 반지름(+여백)만큼 안쪽으로 좁힌 구간에 매핑해 항상 트랙 안에 머물게 한다.
            float x = ApplyMarkerEdgeInset(p);
            sphereMarkerRect.anchorMin = new Vector2(x, 0.5f);
            sphereMarkerRect.anchorMax = new Vector2(x, 0.5f);
        }

        if (spherePinkFill != null)
            spherePinkFill.fillAmount = p;
    }

    /// <summary>진행도(0~1)를 "마커가 트랙 안에 완전히 들어오는" 구간으로 다시 매핑한다.
    /// 트랙 폭을 못 구하거나 마커가 트랙보다 크면 원래 값을 그대로 쓴다.</summary>
    float ApplyMarkerEdgeInset(float p)
    {
        var track = sphereMarkerRect.parent as RectTransform;
        if (track == null) return p;

        float trackWidth = track.rect.width;
        if (trackWidth <= 0f) return p;

        float inset = (sphereMarkerRect.rect.width * 0.5f + markerEdgePadding) / trackWidth;
        if (inset <= 0f || inset >= 0.5f) return p;

        return Mathf.Lerp(inset, 1f - inset, p);
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
