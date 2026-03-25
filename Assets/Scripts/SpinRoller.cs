using UnityEngine;

public enum WaypointEndMode
{
    Stop,     // 마지막 웨이포인트에서 정지
    Loop,     // 처음으로 돌아가 무한 반복
    PingPong  // 역방향으로 왕복 반복
}

// 범용 회전·가속 컴포넌트 — 시간에 따라 속도 단계가 올라가며 회전
// 어떤 투사체/오브젝트에도 부착 가능
public class SpinRoller : MonoBehaviour
{
    [Tooltip("이동 방향 (정규화된 벡터). 웨이포인트가 설정된 경우 무시됨")]
    public Vector3 moveDir;

    [Tooltip("초기 속도 (m/s)")]
    public float initialSpeed = 0;

    [Tooltip("시간 경과에 따른 속도 배율 단계. afterSeconds 오름차순으로 입력\n" +
             "예: afterSeconds=3, speedMultiplier=2 → 생성 3초 후 initialSpeed×2 로 증가")]
    public SpeedPhase[] speedPhases = new SpeedPhase[0];

    [Tooltip("회전 배율 — 속도 1일 때 rad/s")]
    public float spinSpeed = 0;

    public int damage = 0;

    [Tooltip("0 = 무제한")]
    public float lifetime = 0;

    [Tooltip(
        "true  = Y 속도를 0으로 고정 (바닥 함정용 권장 — 공중 부유·충돌 후 튕김 방지)\n" +
        "false = Y 속도를 물리 엔진(중력)에 맡김")]
    public bool lockYVelocity = true;

    [Header("웨이포인트 경로 (비어있으면 moveDir 직진)")]
    [Tooltip("순서대로 이동할 웨이포인트. 비어있으면 moveDir 방향으로 직진")]
    [SerializeField] Transform[] waypoints;

    [Tooltip("웨이포인트 도달 판정 거리 (m). 0이면 속도에 따라 자동 계산")]
    [SerializeField] float waypointReachDistance = 0f;

    [Tooltip("마지막 웨이포인트 도달 후 동작")]
    [SerializeField] WaypointEndMode endMode = WaypointEndMode.Stop;

    [Header("활성화")]
    [Tooltip("true: 씬 시작 시 자동으로 이동 시작 / false: Activate() 호출 전까지 대기")]
    [SerializeField] bool autoStart = true;

    Rigidbody rb;
    float _spawnTime;
    int _waypointIndex;
    bool _waypointFinished;
    int _pingPongDir = 1;
    bool _isActive;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        _spawnTime = Time.time;
        _isActive = autoStart;
    }

    void Start()
    {
        if (lifetime > 0f) Destroy(gameObject, lifetime);
    }

    /// <summary>이동을 시작합니다. PlayerTriggerZone 또는 외부에서 호출.</summary>
    public void Activate()
    {
        if (_isActive) return;
        _isActive = true;
        _spawnTime = Time.time;
    }

    /// <summary>이동을 중단합니다.</summary>
    public void Deactivate() => _isActive = false;

    float GetCurrentSpeed()
    {
        if (initialSpeed <= 0f) return 0f;

        float elapsed = Time.time - _spawnTime;
        float mult = 1f;

        for (int i = 0; i < speedPhases.Length; i++)
            if (elapsed >= speedPhases[i].afterSeconds)
                mult = speedPhases[i].speedMultiplier;

        return initialSpeed * mult;
    }

    bool HasWaypoints => waypoints != null && waypoints.Length > 0;

    Vector3 GetMoveDir(float currentSpeed)
    {
        if (!HasWaypoints) return moveDir.normalized;
        if (_waypointFinished) return Vector3.zero;

        Transform wp = waypoints[_waypointIndex];
        if (wp == null) return moveDir.normalized;

        Vector3 toWp = wp.position - transform.position;
        toWp.y = 0f;

        float threshold = waypointReachDistance > 0f
            ? waypointReachDistance
            : Mathf.Max(0.1f, currentSpeed * Time.fixedDeltaTime * 2f);

        if (toWp.magnitude <= threshold)
        {
            AdvanceWaypoint();
            if (_waypointFinished) return Vector3.zero;

            toWp = waypoints[_waypointIndex].position - transform.position;
            toWp.y = 0f;
        }

        return toWp.sqrMagnitude > 0.0001f ? toWp.normalized : Vector3.zero;
    }

    void AdvanceWaypoint()
    {
        switch (endMode)
        {
            case WaypointEndMode.Stop:
                _waypointIndex++;
                if (_waypointIndex >= waypoints.Length)
                {
                    _waypointIndex = waypoints.Length - 1;
                    _waypointFinished = true;
                }
                break;

            case WaypointEndMode.Loop:
                _waypointIndex = (_waypointIndex + 1) % waypoints.Length;
                break;

            case WaypointEndMode.PingPong:
                _waypointIndex += _pingPongDir;
                if (_waypointIndex >= waypoints.Length)
                {
                    _waypointIndex = waypoints.Length - 2;
                    _pingPongDir = -1;
                }
                else if (_waypointIndex < 0)
                {
                    _waypointIndex = 1;
                    _pingPongDir = 1;
                }
                break;
        }
    }

    void FixedUpdate()
    {
        if (!_isActive) return;

        float currentSpeed = GetCurrentSpeed();
        Vector3 dir = GetMoveDir(currentSpeed);

        Vector3 spinAxis = Vector3.Cross(dir, Vector3.up).normalized;

        if (rb != null)
        {
            Vector3 v = rb.linearVelocity;
            v.x = dir.x * currentSpeed;
            v.z = dir.z * currentSpeed;
            // lockYVelocity=true: Y를 0으로 고정 → 바닥 굴림, 충돌 튕김 방지
            // lockYVelocity=false: Y는 gravity 등 물리 엔진에 맡김
            if (lockYVelocity) v.y = 0f;
            rb.linearVelocity  = v;
            if (spinAxis.sqrMagnitude > 0.001f)
                rb.angularVelocity = spinAxis * spinSpeed * currentSpeed;
        }
        else
        {
            transform.position += dir * currentSpeed * Time.fixedDeltaTime;
            if (spinAxis.sqrMagnitude > 0.001f)
                transform.Rotate(spinAxis, spinSpeed * currentSpeed * Mathf.Rad2Deg * Time.fixedDeltaTime, Space.World);
        }
    }

    // non-trigger Collider(물리 충돌)에서도 데미지 처리
    void OnCollisionEnter(Collision collision)
    {
        if (damage <= 0) return;
        if (!collision.gameObject.CompareTag("Player")) return;
        Player p = collision.gameObject.GetComponent<Player>()
                   ?? collision.gameObject.GetComponentInParent<Player>();
        if (p != null) p.TakeDamage(damage, false);
    }

    // Trigger Collider에서도 데미지 처리 (기존 호환 유지)
    void OnTriggerEnter(Collider other)
    {
        if (damage <= 0) return;
        if (!other.CompareTag("Player")) return;
        Player p = other.GetComponent<Player>()
                   ?? other.GetComponentInParent<Player>();
        if (p != null) p.TakeDamage(damage, false);
    }

    void OnDrawGizmos()
    {
        if (!HasWaypoints) return;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            Gizmos.DrawWireSphere(waypoints[i].position, 0.25f);

            if (i < waypoints.Length - 1 && waypoints[i + 1] != null)
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
        }

        if (endMode == WaypointEndMode.Loop && waypoints[0] != null && waypoints[waypoints.Length - 1] != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
            Gizmos.DrawLine(waypoints[waypoints.Length - 1].position, waypoints[0].position);
        }
    }
}
