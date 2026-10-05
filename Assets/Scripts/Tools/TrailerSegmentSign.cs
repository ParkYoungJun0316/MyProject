using TMPro;
using UnityEngine;

/// <summary>
/// 트레일러 촬영 전용(Marketing_ 씬): T.Stage3 구역 시계 간판. 그리기 규칙은 게임 SegmentTimerSign.Update 그대로
/// (채움 Renderer의 _Cutoff = 1 − 남은 비율, warnSeconds 이하면 경고색, 숫자는 올림 초).
/// 남은 시간만 "컷 시작 때 remainingAtStart초"에서 Timeline 시각만큼 줄어든다.
/// </summary>
public class TrailerSegmentSign : MonoBehaviour
{
    public Renderer[] fills;
    public TMP_Text secondsText;
    public Color fillColor, fillWarnColor;
    public float warnSeconds = 5f;
    public float duration = 40f;
    public float remainingAtStart = 24f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int CutoffId    = Shader.PropertyToID("_Cutoff");
    MaterialPropertyBlock _mpb;

    void Awake() => _mpb = new MaterialPropertyBlock();

    void Update()
    {
        float remaining = Mathf.Max(0f, remainingAtStart - Time.timeSinceLevelLoad);
        float fraction  = duration <= 0f ? 0f : Mathf.Clamp01(remaining / duration);
        Color color     = remaining <= warnSeconds ? fillWarnColor : fillColor;
        float cutoff    = Mathf.Clamp(1f - fraction, 0.002f, 1f);
        foreach (Renderer r in fills)
        {
            if (r == null) continue;
            r.enabled = fraction > 0f;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, color);
            _mpb.SetFloat(CutoffId, cutoff);
            r.SetPropertyBlock(_mpb);
        }
        if (secondsText != null) secondsText.text = Mathf.CeilToInt(remaining).ToString();
    }
}
