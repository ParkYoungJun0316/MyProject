using UnityEngine;

public enum WaypointEndMode
{
    Stop,
    Loop,
    PingPong
}

/// <summary>
/// 웨이포인트 경로 추종 + Y축 고정 이동 컴포넌트.
/// SpinRoller에서 분리된 이동 전담 컴포넌트.
///
/// [사용법]
/// - Boulder 등 경로 이동 오브젝트에 부착
/// - initialSpeed: 이동 속도. 0이면 이동 없음
/// - BoulderSpawner가 런타임에 SetWaypoints()로 경로 주입
/// - SpinRoller는 이 컴포넌트의 rb.linearVelocity를 읽어 회전축만 계산
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class WaypointMover : MonoBehaviour
{
    [Tooltip("이동 속도 (m/s). 0이면 이동 없음")]
    public float initialSpeed = 0f;

    [Tooltip("true = Y 속도를 0으로 고정 (바닥 굴림용 권장)")]
    public bool lockYVelocity = true;

    [Header("웨이포인트 경로")]
    [Tooltip("순서대로 이동할 웨이포인트. 비어있으면 transform.forward 방향으로 직진")]
    [SerializeField] Transform[] waypoints;

    [Tooltip("웨이포인트 도달 판정 거리 (m). 0이면 속도에 따라 자동 계산")]
    [SerializeField] float waypointReachDistance = 0f;

    [Tooltip("마지막 웨이포인트 도달 후 동작")]
    [SerializeField] WaypointEndMode endMode = WaypointEndMode.Stop;

    [Header("활성화")]
    [Tooltip("true: 씬 시작 시 자동으로 이동 시작 / false: Activate() 호출 전까지 대기")]
    [SerializeField] bool autoStart = true;

    Rigidbody rb;
    int _waypointIndex;
    bool _waypointFinished;
    int _pingPongDir = 1;
    bool _isActive;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        _isActive = autoStart;
    }

    void Start()
    {
        if (_isActive) ApplyInitialVelocity();
    }

    public void Activate()
    {
        if (_isActive) return;
        _isActive = true;
        _waypointIndex = 0;
        _waypointFinished = false;
        _pingPongDir = 1;
        ApplyInitialVelocity();
    }

    public void Deactivate() => _isActive = false;

    public void SetWaypoints(Transform[] newWaypoints)
    {
        waypoints      = newWaypoints;
        _waypointIndex = 0;
        _waypointFinished = false;
        _pingPongDir   = 1;
    }

    void ApplyInitialVelocity()
    {
        if (rb == null || initialSpeed <= 0f) return;
        Vector3 dir = GetMoveDir();
        if (dir != Vector3.zero)
            rb.linearVelocity = dir * initialSpeed;
    }

    bool HasWaypoints => waypoints != null && waypoints.Length > 0;

    Vector3 GetMoveDir()
    {
        if (!HasWaypoints) return transform.forward;
        if (_waypointFinished) return Vector3.zero;

        Transform wp = waypoints[_waypointIndex];
        if (wp == null) return transform.forward;

        Vector3 toWp = wp.position - transform.position;
        toWp.y = 0f;

        float threshold = waypointReachDistance > 0f
            ? waypointReachDistance
            : Mathf.Max(0.1f, initialSpeed * Time.fixedDeltaTime * 2f);

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
        if (!_isActive || rb == null || initialSpeed <= 0f) return;

        Vector3 dir = GetMoveDir();

        Vector3 v = rb.linearVelocity;
        if (dir != Vector3.zero)
        {
            v.x = dir.x * initialSpeed;
            v.z = dir.z * initialSpeed;
        }
        if (lockYVelocity) v.y = 0f;
        rb.linearVelocity = v;
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

        if (endMode == WaypointEndMode.Loop &&
            waypoints[0] != null && waypoints[waypoints.Length - 1] != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
            Gizmos.DrawLine(waypoints[waypoints.Length - 1].position, waypoints[0].position);
        }
    }
}
