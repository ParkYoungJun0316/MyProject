using UnityEngine;

/// <summary>
/// 경고 마커 색 보간 공용 헬퍼 — ArrowWarnSign / SpikeLaneWarnMarker /
/// Breakable / CapacityTile이 공유. MaterialPropertyBlock으로 Renderer 색을 두 색 사이에서 보간한다.
/// 색은 호출부가 WarnPalette에서 넘긴다(인스펙터 색 필드 없음). WarnMarker 셰이더용으로
/// 바깥 테두리색(_BorderColor)도 WarnPalette.Border로 항상 같이 넣는다 — 해당 프로퍼티가
/// 없는 셰이더(URP Lit 등)에서는 무시된다. MonoBehaviour가 아닌 순수 C# 클래스라 직렬화 대상이 아니다.
/// </summary>
public sealed class WarnMarkerColorFx
{
    readonly Renderer _renderer;
    readonly Color _start;
    readonly Color _end;
    readonly int _colorId;
    readonly int _fillId;
    readonly int _timeId;
    static readonly int BorderColorId = Shader.PropertyToID("_BorderColor");
    readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

    /// <param name="fillProperty">
    /// 경로 채움 셰이더 float(0~1). 비우면 색만 쓴다 — SpikeLane/Wind(URP Lit)는 기존처럼 색만.
    /// WarnMarker 셰이더는 "_Fill" (DropWarnMarker와 동일 이름).
    /// </param>
    /// <param name="timeProperty">
    /// 채움 위치와 별개인 시간 진행도(0~1) 셰이더 float. 채움 곡선을 비선형으로 바꿔도 펄스 등
    /// 시간 기준 연출이 발사 시각에 정확히 맞도록 따로 넘긴다. WarnMarker 셰이더는 "_Progress".
    /// </param>
    public WarnMarkerColorFx(
        Renderer renderer, string colorProperty, Color startColor, Color endColor,
        string fillProperty = null, string timeProperty = null)
    {
        _renderer = renderer;
        _start    = startColor;
        _end      = endColor;
        _colorId  = Shader.PropertyToID(colorProperty);
        _fillId   = string.IsNullOrEmpty(fillProperty) ? 0 : Shader.PropertyToID(fillProperty);
        _timeId   = string.IsNullOrEmpty(timeProperty) ? 0 : Shader.PropertyToID(timeProperty);
    }

    /// <summary>t=0(시작색·빈 외곽) ~ t=1(끝색·가득 채움). Clamp01 적용.</summary>
    /// <param name="alphaMultiplier">색 보간 결과 알파에 추가로 곱하는 배율(기본 1=변화 없음).</param>
    /// <param name="fill">채움 위치(0~1). 음수면 t를 그대로 쓴다(선형 채움).</param>
    public void SetProgress(float t, float alphaMultiplier = 1f, float fill = -1f)
    {
        if (_renderer == null) return;
        t = Mathf.Clamp01(t);
        Color c = Color.Lerp(_start, _end, t);
        c.a *= Mathf.Clamp01(alphaMultiplier);
        _renderer.GetPropertyBlock(_block);
        _block.SetColor(_colorId, c);
        _block.SetColor(BorderColorId, WarnPalette.Border);
        if (_fillId != 0)
            _block.SetFloat(_fillId, fill < 0f ? t : Mathf.Clamp01(fill));
        if (_timeId != 0)
            _block.SetFloat(_timeId, t);
        _renderer.SetPropertyBlock(_block);
    }

    /// <summary>Renderer.enabled 토글만 담당(오브젝트 SetActive 등 추가 처리는 호출부 책임).</summary>
    public void SetRendererVisible(bool visible)
    {
        if (_renderer != null) _renderer.enabled = visible;
    }
}
