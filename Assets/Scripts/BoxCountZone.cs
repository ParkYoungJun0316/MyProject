using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 박스 카운트 구역.
/// OverlapBox로 매 물리 프레임 구역 안의 박스를 세어,
/// requiredCount에 도달하면 OnFulfilled, 이탈하면 OnUnfulfilled 발동.
///
/// [A→B 박스 이동 스테이지 클리어 방법]
///   1. B 구역에 BoxCountZone 추가
///   2. requiredCount = 10 (옮길 박스 수)
///   3. OnFulfilled → DoorController.Open()  연결
///   4. OnUnfulfilled → DoorController.Close() 연결
///
/// requiredColor = Common 으로 두면 색 무관 모든 박스를 셈.
/// </summary>
public class BoxCountZone : MonoBehaviour
{
    [Header("카운트 조건")]
    [Tooltip("감지할 박스 색. Common = 색 무관 모든 박스")]
    public PlayerColorType requiredColor = PlayerColorType.Common;

    [Tooltip("충족으로 판정할 박스 최소 개수")]
    public int requiredCount = 10;

    [Header("시각 피드백")]
    [Tooltip("박스 수에 따라 색이 점점 변하는 진행률 보간 사용 여부")]
    public bool useProgressColor = false;

    [Tooltip("아직 충족 안 된 상태 색")]
    public Color inactiveColor = Color.gray;
    [Tooltip("충족된 상태 색")]
    public Color activeColor   = Color.green;

    [Header("이벤트")]
    [Tooltip("박스 수가 requiredCount에 도달했을 때")]
    public UnityEvent OnFulfilled;
    [Tooltip("충족 후 박스가 빠져나가 조건 미달이 됐을 때")]
    public UnityEvent OnUnfulfilled;
    [Tooltip("박스 수가 바뀔 때마다 현재 개수를 전달")]
    public UnityEvent<int> OnCountChanged;

    public bool IsFulfilled  => _isFulfilled;
    public int  CurrentCount => _currentCount;

    bool        _isFulfilled;
    int         _currentCount;
    BoxCollider _col;
    Material[]  _mats;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");

    void Awake()
    {
        _col = GetComponent<BoxCollider>();

        var renderers = GetComponentsInChildren<MeshRenderer>(true);
        _mats = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                _mats[i] = renderers[i].material;

        ApplyColor(inactiveColor);
    }

    void OnDestroy()
    {
        for (int i = 0; i < _mats.Length; i++)
            if (_mats[i] != null) Destroy(_mats[i]);
    }

    void FixedUpdate()
    {
        int count = CountBoxesInside();

        if (count == _currentCount) return; // 변화 없으면 무시

        _currentCount = count;
        OnCountChanged?.Invoke(_currentCount);

        bool fulfilled = _currentCount >= requiredCount;

        // 충족 상태 전환 시만 이벤트 발동
        if (fulfilled && !_isFulfilled)
        {
            _isFulfilled = true;
            ApplyColor(activeColor);
            OnFulfilled?.Invoke();
        }
        else if (!fulfilled && _isFulfilled)
        {
            _isFulfilled = false;
            ApplyColor(inactiveColor);
            OnUnfulfilled?.Invoke();
        }
        else if (useProgressColor)
        {
            // 충족 상태 변화 없이 수만 바뀐 경우: 진행 색상 보간
            float t = requiredCount > 0 ? (float)_currentCount / requiredCount : 0f;
            ApplyColor(Color.Lerp(inactiveColor, activeColor, t));
        }
    }

    int CountBoxesInside()
    {
        if (_col == null) return 0;

        Vector3 worldCenter = transform.TransformPoint(_col.center);
        Vector3 halfExtents = new Vector3(
            _col.size.x * transform.lossyScale.x,
            _col.size.y * transform.lossyScale.y,
            _col.size.z * transform.lossyScale.z) * 0.5f;

        Collider[] hits  = Physics.OverlapBox(worldCenter, halfExtents, transform.rotation);
        int        count = 0;

        for (int i = 0; i < hits.Length; i++)
        {
            var box = hits[i].GetComponent<PushableBox>();
            if (box == null) continue;
            if (requiredColor == PlayerColorType.Common || box.ownerColor == requiredColor)
                count++;
        }
        return count;
    }

    void ApplyColor(Color color)
    {
        if (_mats == null) return;
        for (int i = 0; i < _mats.Length; i++)
        {
            var mat = _mats[i];
            if (mat == null) continue;
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, color);
            else if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, color);
        }
    }

    void OnDrawGizmos()
    {
        float t = requiredCount > 0 ? Mathf.Clamp01((float)_currentCount / requiredCount) : 0f;
        Color c = Color.Lerp(inactiveColor, activeColor, t);
        c.a = 0.3f;
        Gizmos.color = c;
        Gizmos.DrawCube(transform.position, transform.lossyScale);

        c.a = 1f;
        Gizmos.color = c;
        Gizmos.DrawWireCube(transform.position, transform.lossyScale * 1.01f);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * (transform.lossyScale.y * 0.5f + 0.3f),
            $"{_currentCount} / {requiredCount}");
#endif
    }
}
