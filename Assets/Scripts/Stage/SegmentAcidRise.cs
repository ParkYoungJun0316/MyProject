using UnityEngine;

/// <summary>
/// 구역 시계 시간 초과 연출 — 위액 수면이 마지막 riseSeconds 동안 바닥 아래에서 차오른다.
///
/// 연출만 한다. 판정(ContactDamage)은 SegmentTimer.activateOnTimeout이 시간 초과 순간에 켠다 —
/// 그래서 "다 차오른 순간 = 아프기 시작하는 순간"이다.
///
/// SegmentTimer가 계산한 남은 시간(StageNetworkState 슬롯 기준)만 읽으므로 전 머신이 같은 높이를 본다.
/// 이 오브젝트는 Hazard.X 밖에 둔다 — Hazard.X는 시간 초과 전까지 꺼져 있어 차오르는 구간을 못 보여준다.
/// 끄기(정리)는 Hazard.X와 같이 다음 구역 SegmentTimer.onTimerStarted → SetActive(false)로 한다.
/// </summary>
public class SegmentAcidRise : MonoBehaviour
{
    [Tooltip("이 수면이 따라갈 구역 시계")]
    [SerializeField] SegmentTimer timer;

    [Tooltip("시간 초과 몇 초 전부터 차오르기 시작하는가")]
    [SerializeField] float riseSeconds = 3f;

    [Tooltip("시작 높이 = 배치 높이에서 이만큼 아래(m). 바닥 밑에 숨어 있어야 한다")]
    [SerializeField] float riseDepth = 1f;

    [Tooltip("차오르기 전에는 숨길 Renderer. 비우면 자신과 자식 전부")]
    [SerializeField] Renderer[] renderers = new Renderer[0];

    Vector3 _restLocalPos;
    bool    _visible = true;

    void Awake()
    {
        _restLocalPos = transform.localPosition;
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);
        Apply(0f);
    }

    void Update()
    {
        if (timer == null || !timer.HasStarted) { Apply(0f); return; }
        if (timer.HasFired) { Apply(1f); return; }

        float into = riseSeconds - timer.RemainingSeconds;
        Apply(riseSeconds <= 0f ? 0f : Mathf.Clamp01(into / riseSeconds));
    }

    void Apply(float t)
    {
        bool visible = t > 0f;
        if (visible != _visible)
        {
            _visible = visible;
            foreach (Renderer r in renderers)
                if (r != null) r.enabled = visible;
        }

        // 처음엔 빠르게, 끝에서 잦아들게 — 물이 차오르는 느낌.
        float eased = 1f - (1f - t) * (1f - t);
        transform.localPosition = _restLocalPos + Vector3.down * (riseDepth * (1f - eased));
    }
}
