using UnityEngine;

/// <summary>
/// TrapPlayerTracker가 붙은 함정(ArrowTrap 전용) 위에 붙이는 "누구를 조준 중인지" 표시 아이콘.
///
/// [사용법]
/// - Mouth(또는 함정) 위쪽에 아이콘용 오브젝트(Sphere/Quad 등)를 자식으로 배치하고 이 컴포넌트를 부착
/// - 부모(또는 조상)에 TrapPlayerTracker가 있어야 동작 (없으면 항상 noTargetColor로 표시)
/// - targetRenderer를 비워두면 이 오브젝트의 Renderer를 자동 사용
///
/// [동작]
/// - 매 프레임 로컬 카메라(Camera.main)를 향해 billboard 회전
/// - TrapPlayerTracker.OnTargetChanged 구독 → 타겟 플레이어의 Player.uniqueColor로 색 전환
///   (Blue/Purple/Green/Yellow), 타겟이 없으면(스텔스 등) noTargetColor(회색)
/// - GameSession.IsColorActive()로 검증 후 색을 적용 — 이번 판에 실제로 없는 색(예: 3인 플레이 중
///   Yellow 미참가)이 표시되는 걸 원천 차단. GameSession이 없는 테스트 씬에서는 검증을 건너뛴다.
/// - URP Lit/Unlit(_BaseColor)와 Sprite/Legacy(_Color) 셰이더 모두 대응
/// </summary>
public class TrapTrackerIndicator : MonoBehaviour
{
    [Header("표시할 렌더러")]
    [Tooltip("색상을 바꿀 렌더러. 비워두면 이 오브젝트의 Renderer를 자동으로 사용")]
    [SerializeField] Renderer targetRenderer;

    [Header("타겟 없음일 때 색상")]
    [Tooltip("타겟이 없거나(스텔스 등), 함정이 비활성이거나, 타겟 색이 이번 판 활성 색이 아닐 때")]
    [SerializeField] Color noTargetColor = Color.gray;

    [Header("Billboard")]
    [Tooltip("true면 Y축만 회전(수평 billboard). false면 카메라를 항상 정면으로 바라봄")]
    [SerializeField] bool yAxisOnly = true;

    TrapPlayerTracker      _tracker;
    Camera                 _cam;
    MaterialPropertyBlock  _mpb;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");

    void Awake()
    {
        _tracker = GetComponentInParent<TrapPlayerTracker>();
        if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        if (_tracker == null) return;
        _tracker.OnTargetChanged += ApplyTarget;
        ApplyTarget(_tracker.CurrentTarget);
    }

    void OnDisable()
    {
        if (_tracker == null) return;
        _tracker.OnTargetChanged -= ApplyTarget;
    }

    void LateUpdate()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        Vector3 dir = transform.position - _cam.transform.position;
        if (yAxisOnly) dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.LookRotation(dir.normalized);
    }

    void ApplyTarget(Player target)
    {
        if (targetRenderer == null) return;

        Color color = noTargetColor;
        if (target != null && IsColorCurrentlyActive(target.playerColorType))
            color = target.uniqueColor;

        targetRenderer.GetPropertyBlock(_mpb);
        if (targetRenderer.sharedMaterial != null && targetRenderer.sharedMaterial.HasProperty(BaseColorId))
            _mpb.SetColor(BaseColorId, color);
        else
            _mpb.SetColor(ColorId, color);
        targetRenderer.SetPropertyBlock(_mpb);
    }

    /// <summary>
    /// GameSession(SSOT)에 등록된 이번 판 활성 색인지 확인.
    /// 예전 치어 버그(3인 플레이에서 없는 Yellow가 계속 인식되던 문제)와 동일한 부류를
    /// 재발시키지 않기 위해, 대상 플레이어가 실존해도 색 검증은 항상 GameSession을 거친다.
    /// GameSession이 없는 씬(단독 테스트 등)에서는 검증을 건너뛰고 항상 통과시킨다.
    /// </summary>
    static bool IsColorCurrentlyActive(PlayerColorType type)
    {
        if (GameSession.Instance == null) return true;
        return GameSession.Instance.IsColorActive(type);
    }
}
