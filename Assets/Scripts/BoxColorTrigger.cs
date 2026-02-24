using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 색상 일치 박스 감지 트리거.
/// OnTriggerEnter 대신 Physics.OverlapBox polling 방식 사용.
/// → kinematic Rigidbody 박스도 안정적으로 감지.
/// DoorController.requiredTriggers[]에 등록해 문과 연결.
/// </summary>
public class BoxColorTrigger : MonoBehaviour
{
    [Header("색상 조건")]
    [Tooltip("감지할 박스 색. PushableBox.ownerColor와 일치해야 활성화")]
    public PlayerColorType requiredColor = PlayerColorType.Red;

    [Header("시각 피드백 (MeshRenderer가 있을 때)")]
    public Color inactiveColor = Color.gray;
    public Color activeColor   = Color.red;

    [Header("이벤트")]
    public UnityEvent OnActivated;
    public UnityEvent OnDeactivated;

    public bool IsActive => _isActive;

    bool        _isActive;
    BoxCollider _col;
    Material[]  _matInstances;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");

    void Awake()
    {
        _col = GetComponent<BoxCollider>();

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

    void FixedUpdate()
    {
        // Physics.OverlapBox로 직접 폴링 → kinematic 박스도 안정적으로 감지
        bool found = CheckBoxInside();

        if (found == _isActive) return; // 상태 변화 없으면 무시

        _isActive = found;
        ApplyColor(_isActive ? activeColor : inactiveColor);

        if (_isActive) OnActivated?.Invoke();
        else           OnDeactivated?.Invoke();
    }

    bool CheckBoxInside()
    {
        if (_col == null) return false;

        Vector3 worldCenter  = transform.TransformPoint(_col.center);
        Vector3 halfExtents  = new Vector3(
            _col.size.x * transform.lossyScale.x,
            _col.size.y * transform.lossyScale.y,
            _col.size.z * transform.lossyScale.z) * 0.5f;

        Collider[] hits = Physics.OverlapBox(worldCenter, halfExtents, transform.rotation);
        for (int i = 0; i < hits.Length; i++)
        {
            var box = hits[i].GetComponent<PushableBox>();
            if (box != null && box.ownerColor == requiredColor)
                return true;
        }
        return false;
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
        Gizmos.color = _isActive ? activeColor : inactiveColor;
        Gizmos.DrawWireCube(transform.position, transform.lossyScale);
    }
}
