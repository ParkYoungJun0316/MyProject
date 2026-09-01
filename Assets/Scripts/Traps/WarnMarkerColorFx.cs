using UnityEngine;

/// <summary>
/// 경고 마커 색 보간 공용 헬퍼 — SpikeLaneWarnMarker / WindWarnSign / ArrowWarnSign이 공유.
/// MaterialPropertyBlock으로 Renderer 색만 노랑→빨강(또는 지정한 두 색) 보간한다.
///
/// 각 컴포넌트는 자기 소유의 Inspector 필드(targetRenderer, colorProperty, warnStartColor,
/// warnEndColor)를 그대로 유지한 채 Awake에서 이 헬퍼를 생성해 쓴다 — 필드명·타입이 바뀌지
/// 않으므로 기존 씬/프리팹에 저장된 Inspector 값(이미 배치된 SpikeLaneWarnMarker 등)이
/// 그대로 유지된다. MonoBehaviour가 아닌 순수 C# 클래스라 직렬화 대상이 아니다.
/// </summary>
public sealed class WarnMarkerColorFx
{
    readonly Renderer _renderer;
    readonly Color _start;
    readonly Color _end;
    readonly int _colorId;
    readonly int _fillId;
    readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

    /// <param name="fillProperty">
    /// 경로 채움 셰이더 float(0~1). 비우면 색만 쓴다 — SpikeLane/Wind(URP Lit)는 기존처럼 색만.
    /// ArrowWarnMarker 셰이더는 "_Fill" (DropWarnMarker와 동일 이름).
    /// </param>
    public WarnMarkerColorFx(
        Renderer renderer, string colorProperty, Color startColor, Color endColor,
        string fillProperty = null)
    {
        _renderer = renderer;
        _start    = startColor;
        _end      = endColor;
        _colorId  = Shader.PropertyToID(colorProperty);
        _fillId   = string.IsNullOrEmpty(fillProperty) ? 0 : Shader.PropertyToID(fillProperty);
    }

    /// <summary>t=0(시작색·빈 외곽) ~ t=1(끝색·가득 채움). Clamp01 적용.</summary>
    public void SetProgress(float t)
    {
        if (_renderer == null) return;
        t = Mathf.Clamp01(t);
        _renderer.GetPropertyBlock(_block);
        _block.SetColor(_colorId, Color.Lerp(_start, _end, t));
        if (_fillId != 0)
            _block.SetFloat(_fillId, t);
        _renderer.SetPropertyBlock(_block);
    }

    /// <summary>Renderer.enabled 토글만 담당(오브젝트 SetActive 등 추가 처리는 호출부 책임).</summary>
    public void SetRendererVisible(bool visible)
    {
        if (_renderer != null) _renderer.enabled = visible;
    }
}
