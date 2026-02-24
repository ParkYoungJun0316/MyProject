using UnityEngine;

/// <summary>
/// 마법진 웨이포인트.
/// MagicCircle의 자식으로 배치. Trigger Collider 필수.
/// 플레이어가 밟으면 MagicCircle에 보고해 순서 진행.
/// MagicCircle.Awake()에서 circle, stepIndex를 자동으로 주입한다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MagicCircleWaypoint : MonoBehaviour
{
    [Header("시각 피드백")]
    [Tooltip("아직 밟지 않은 상태의 색")]
    public Color inactiveColor = new Color(0.3f, 0.3f, 1f, 1f);

    [Tooltip("밟은 후 활성화된 색")]
    public Color activeColor = Color.cyan;

    // MagicCircle.Awake()에서 주입
    [HideInInspector] public MagicCircle circle;
    [HideInInspector] public int stepIndex;

    bool       _isActivated;
    Material[] _matInstances;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");

    void Awake()
    {
        // Trigger 강제 설정
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        // 머티리얼 인스턴스 캐시 (SRP Batcher 우회)
        var renderers = GetComponentsInChildren<MeshRenderer>(true);
        _matInstances = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                _matInstances[i] = renderers[i].material;

        ApplyColor(inactiveColor);
    }

    void OnDestroy()
    {
        for (int i = 0; i < _matInstances.Length; i++)
            if (_matInstances[i] != null)
                Destroy(_matInstances[i]);
    }

    void OnTriggerEnter(Collider other)
    {
        Player player = other.GetComponentInParent<Player>();
        if (player == null || player.IsDead) return;

        circle?.TryAdvance(stepIndex);
    }

    /// <summary>MagicCircle에서 호출. 시각 상태 변경.</summary>
    public void SetActivated(bool activated)
    {
        _isActivated = activated;
        ApplyColor(activated ? activeColor : inactiveColor);
    }

    void ApplyColor(Color color)
    {
        if (_matInstances == null) return;
        for (int i = 0; i < _matInstances.Length; i++)
        {
            var mat = _matInstances[i];
            if (mat == null) continue;
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, color);
            else if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, color);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = _isActivated ? activeColor : inactiveColor;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        // 순서 번호 표시 (에디터에서만 의미 있음)
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.7f, stepIndex.ToString());
#endif
    }
}
