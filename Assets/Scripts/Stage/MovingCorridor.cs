using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 이동하는 복도 컴포넌트.
///
/// [동작]
///  뒤에서 밀려오는 벽(backWall)과 앞에서 이동하는 벽(frontWall)이
///  같은 방향·속도로 이동하며 두 벽 간 거리를 유지.
///  플레이어는 두 벽 사이를 달려서 빠져나가야 함.
///
///  뒤 벽: Rigidbody로 밀어오므로 플레이어를 실제로 밀어냄 (물리 충돌)
///  앞 벽: 플레이어가 앞으로 도망갈 한계선 역할 (선택)
///
/// [트리거 활성화]
///  activateOnPlayerTrigger = true 시:
///  이 GameObject에 BoxCollider(Is Trigger = true)를 추가하면
///  플레이어 진입 시 자동으로 복도가 시작됨.
///
/// [필수 컴포넌트 — 각 벽 오브젝트에]
///  Rigidbody: Is Kinematic = true, Interpolate = Interpolate
///  Collider:  Is Trigger = false
///
/// [속도 단계]
///  speedPhases: 시간이 지날수록 복도 속도를 높이는 단계적 배율
/// </summary>
public class MovingCorridor : MonoBehaviour
{
    [Header("벽 참조")]
    [Tooltip("뒤에서 플레이어를 쫓아오는 벽 (Rigidbody 필수)")]
    public Rigidbody backWall;

    [Tooltip("앞에서 이동하는 벽 — 없으면 뒤 벽만 이동 (선택)")]
    public Rigidbody frontWall;

    [Header("이동 설정")]
    [Tooltip("복도가 이동하는 방향 (월드 기준). 예: (0,0,1) = 앞쪽")]
    public Vector3 moveDirection = Vector3.zero;

    [Tooltip("기본 이동 속도 (m/s)")]
    public float baseSpeed = 0f;

    [Header("속도 단계 (시간 경과 → 속도 증가)")]
    [Tooltip("afterSeconds 이후 speedMultiplier 배율 적용. afterSeconds 오름차순 입력")]
    public SpeedPhase[] speedPhases = new SpeedPhase[0];

    [Header("활성화")]
    [Tooltip("씬 시작 시 자동 활성화 여부. activateOnPlayerTrigger와 함께 사용 불가 (둘 중 하나만)")]
    public bool startActive = false;

    [Tooltip("true: 플레이어가 이 GameObject의 Trigger Collider에 진입하면 자동 시작\n" +
             "→ 이 GameObject에 BoxCollider(Is Trigger = true) 추가 필요")]
    public bool activateOnPlayerTrigger = false;

    [Tooltip("true: 한 번만 트리거 허용. false: 플레이어 재진입 시 재활성화")]
    public bool activateOnce = true;

    [Header("이벤트")]
    [Tooltip("복도 활성화 시 호출")]
    public UnityEvent OnActivated;

    [Tooltip("복도 비활성화 시 호출")]
    public UnityEvent OnDeactivated;

    [Header("Runtime (확인용)")]
    [SerializeField] bool  _isActive;
    [SerializeField] float _currentSpeed;
    [SerializeField] float _elapsed;
    [SerializeField] bool  _hasTriggered;

    float    _activatedTime;

    void Start()
    {
        if (moveDirection == Vector3.zero)
            moveDirection = Vector3.forward;

        if (startActive) Activate();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!activateOnPlayerTrigger) return;
        if (activateOnce && _hasTriggered) return;

        Player player = other.GetComponentInParent<Player>();
        if (player == null || player.IsDead) return;

        _hasTriggered = true;
        Activate();
    }

    void FixedUpdate()
    {
        if (!_isActive) return;

        _elapsed      = Time.time - _activatedTime;
        _currentSpeed = GetCurrentSpeed();

        Vector3 delta = moveDirection.normalized * (_currentSpeed * Time.fixedDeltaTime);

        if (backWall  != null) backWall.MovePosition(backWall.position   + delta);
        if (frontWall != null) frontWall.MovePosition(frontWall.position + delta);
    }

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>복도 이동 시작.</summary>
    public void Activate()
    {
        if (_isActive) return;
        _isActive      = true;
        _activatedTime = Time.time;
        OnActivated?.Invoke();
    }

    /// <summary>복도 이동 중단.</summary>
    public void Deactivate()
    {
        if (!_isActive) return;
        _isActive = false;
        OnDeactivated?.Invoke();
    }

    // ── 내부 ────────────────────────────────────────────────────

    float GetCurrentSpeed()
    {
        if (baseSpeed <= 0f) return 0f;

        float mult = 1f;
        for (int i = 0; i < speedPhases.Length; i++)
            if (_elapsed >= speedPhases[i].afterSeconds)
                mult = speedPhases[i].speedMultiplier;

        return baseSpeed * mult;
    }

    // ── 에디터 지원 ──────────────────────────────────────────────

    [ContextMenu("테스트: 활성화")]
    void Debug_Activate() => Activate();

    [ContextMenu("테스트: 비활성화")]
    void Debug_Deactivate() => Deactivate();

    void OnDrawGizmos()
    {
        if (backWall == null) return;

        Vector3 dir = (moveDirection == Vector3.zero ? Vector3.forward : moveDirection).normalized;

        // 이동 방향 화살표
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        Vector3 from = backWall.transform.position;
        Vector3 to   = from + dir * 3f;
        Gizmos.DrawLine(from, to);
        Gizmos.DrawSphere(to, 0.15f);

        // frontWall 연결선
        if (frontWall != null)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
            Gizmos.DrawLine(backWall.transform.position, frontWall.transform.position);
        }
    }
}
