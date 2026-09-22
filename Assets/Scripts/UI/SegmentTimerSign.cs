using TMPro;
using UnityEngine;

/// <summary>
/// 구역 시계를 월드 간판에 표시한다 — 화면 UI·문구 로컬라이제이션 없음(숫자만 쓴다).
///
/// 표시 방식은 SideSplitWorldDisplay와 같다: 간판 글씨 뒤 채움 Renderer의 머티리얼 _Cutoff를
/// 올려 한쪽부터 잘라낸다(채움 텍스처 알파 = 위치 그라데이션). 그래서 **간판 프리팹·머티리얼을
/// 그대로 재사용**하고, 이 컴포넌트는 SegmentTimer가 계산해 둔 남은 시간을 읽어 그리기만 한다.
///
/// 판정·시각은 소유하지 않는다 — 전부 SegmentTimer(→ StageNetworkState 슬롯) 쪽이다.
/// 그래서 전 머신이 같은 숫자를 본다.
/// </summary>
public class SegmentTimerSign : MonoBehaviour
{
    [Header("대상")]
    [Tooltip("이 간판이 보여줄 구역 시계")]
    [SerializeField] SegmentTimer timer;

    [Header("표시")]
    [Tooltip("남은 시간 채움 — 간판 글씨 뒤 배경")]
    [SerializeField] Renderer[] timerFills = new Renderer[0];

    [Tooltip("남은 초를 쓰는 TextMeshPro 3D. 없어도 된다(멀리서는 채움만 읽힌다)")]
    [SerializeField] TMP_Text secondsText;

    [Tooltip("시계 시작 전에도 간판을 보일지. 끄면 시작 전까지 채움·숫자를 숨긴다")]
    [SerializeField] bool showBeforeStart = true;

    [Header("색")]
    [SerializeField] Color fillColor = new Color(0.25f, 0.72f, 0.62f, 1f);
    [SerializeField] Color fillWarnColor = new Color(0.95f, 0.2f, 0.2f, 1f);
    [SerializeField] Color fillFailColor = new Color(0.95f, 0.2f, 0.2f, 1f);

    [Tooltip("남은 시간이 이 값 이하면 경고색")]
    [SerializeField] float warnSeconds = 5f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int CutoffId    = Shader.PropertyToID("_Cutoff");

    MaterialPropertyBlock _mpb;
    int _lastShownSecond = -1;

    void Awake() => _mpb = new MaterialPropertyBlock();

    void Update()
    {
        if (timer == null) return;

        if (!timer.HasStarted)
        {
            SetFills(showBeforeStart, 1f, fillColor);
            SetSeconds(showBeforeStart ? Mathf.CeilToInt(timer.Duration) : -1);
            return;
        }

        if (timer.HasFired)
        {
            // 시간 초과 = 이 구역이 함정밭이 된 순간. 실패색으로 꽉 채워 둔다.
            SetFills(true, 1f, fillFailColor);
            SetSeconds(0);
            return;
        }

        float remaining = timer.RemainingSeconds;
        SetFills(true, timer.Remaining01, remaining <= warnSeconds ? fillWarnColor : fillColor);
        SetSeconds(Mathf.CeilToInt(remaining));
    }

    void SetFills(bool visible, float fraction, Color color)
    {
        // 채움 텍스처 알파는 위치 그라데이션(한쪽 끝=1 → 반대쪽 끝≈0).
        // cutoff를 올리면 반대쪽부터 잘려 줄어든다. 알파 0인 바깥은 cutoff가 0보다 커야 잘리므로 하한을 둔다.
        float cutoff = Mathf.Clamp(1f - fraction, 0.002f, 1f);
        foreach (Renderer r in timerFills)
        {
            if (r == null) continue;
            r.enabled = visible && fraction > 0f;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, color);
            _mpb.SetFloat(CutoffId, cutoff);
            r.SetPropertyBlock(_mpb);
        }
    }

    void SetSeconds(int seconds)
    {
        if (secondsText == null) return;
        if (seconds < 0)
        {
            if (secondsText.gameObject.activeSelf) secondsText.gameObject.SetActive(false);
            _lastShownSecond = -1;
            return;
        }

        if (!secondsText.gameObject.activeSelf) secondsText.gameObject.SetActive(true);
        if (seconds == _lastShownSecond) return;
        _lastShownSecond  = seconds;
        secondsText.text  = seconds.ToString();
    }
}
