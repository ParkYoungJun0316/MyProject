using UnityEngine;

/// <summary>
/// Shield 버프 막 연출. 플레이어 프리팹 Buff/Shield에 붙고 PlayerBuffVisual이 호출한다.
///
///   SetColor(c) : 막·파편 색 = 플레이어 고유색 (흑백 모드와 무관하게 고정).
///   Show()      : 막 표시.
///   Break()     : 막을 즉시 숨기고 파편이 흩어짐. 피격 소진 / 시간 만료 둘 다 이걸로.
/// </summary>
public class ShieldBubbleFx : MonoBehaviour
{
    [SerializeField] Renderer membrane;
    [SerializeField] ParticleSystem breakShards;

    static readonly int ColorId = Shader.PropertyToID("_Color");

    MaterialPropertyBlock _mpb;

    void Awake() => _mpb = new MaterialPropertyBlock();

    public void SetColor(Color c)
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        if (membrane != null)
        {
            membrane.GetPropertyBlock(_mpb);
            _mpb.SetColor(ColorId, c);
            membrane.SetPropertyBlock(_mpb);
        }
        if (breakShards != null)
        {
            var main = breakShards.main;
            main.startColor = new Color(c.r, c.g, c.b, 0.9f);
        }
    }

    [ContextMenu("Show")]
    public void Show()
    {
        if (membrane != null) membrane.enabled = true;
    }

    [ContextMenu("Break")]
    public void Break()
    {
        if (membrane == null || !membrane.enabled) return;
        if (breakShards != null)
        {
            breakShards.transform.position = membrane.bounds.center;
            breakShards.Play(true);
        }
        membrane.enabled = false;
    }
}
