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
    readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

    public WarnMarkerColorFx(Renderer renderer, string colorProperty, Color startColor, Color endColor)
    {
        _renderer = renderer;
        _start    = startColor;
        _end      = endColor;
        _colorId  = Shader.PropertyToID(colorProperty);
    }

    /// <summary>t=0(시작색) ~ t=1(끝색)로 보간해 즉시 반영. Clamp01 적용.</summary>
    public void SetProgress(float t)
    {
        if (_renderer == null) return;
        _renderer.GetPropertyBlock(_block);
        _block.SetColor(_colorId, Color.Lerp(_start, _end, Mathf.Clamp01(t)));
        _renderer.SetPropertyBlock(_block);
    }

    /// <summary>Renderer.enabled 토글만 담당(오브젝트 SetActive 등 추가 처리는 호출부 책임).</summary>
    public void SetRendererVisible(bool visible)
    {
        if (_renderer != null) _renderer.enabled = visible;
    }
}
