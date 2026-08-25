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

    [Tooltip("레거시 필드. 더 이상 도착 판정에 사용되지 않음(현재는 스텝 이동거리 기준으로 정확히 스냅). 하위호환을 위해 남겨둠")]
    [SerializeField] float waypointReachDistance = 0f;

    [Tooltip("마지막 웨이포인트 도달 후 동작")]
    [SerializeField] WaypointEndMode endMode = WaypointEndMode.Stop;

    [Header("활성화")]
    [Tooltip("true: 씬 시작 시 자동으로 이동 시작 / false: Activate() 호출 전까지 대기")]
    [SerializeField] bool autoStart = true;

    // B안: NetworkTransform 없이 Client 로컬 시뮬 시 사용.
    // SetWaypointPositions()로 설정하면 Transform[] waypoints 대신 이 배열을 사용.
    Vector3[] _positionWaypoints;
    bool      _usePositionWaypoints;

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
        waypoints             = newWaypoints;
        _usePositionWaypoints = false;
        _waypointIndex        = 0;
        _waypointFinished     = false;
        _pingPongDir          = 1;
    }

    /// <summary>
    /// B안 전용. ClientRpc로 받은 Vector3 위치 배열로 경로를 설정.
    /// Transform 참조 없이 Client 로컬 시뮬레이션에 사용.
    /// </summary>
    public void SetWaypointPositions(Vector3[] positions)
    {
        _positionWaypoints    = positions;
        _usePositionWaypoints = true;
        _waypointIndex        = 0;
        _waypointFinished     = false;
        _pingPongDir          = 1;
    }

    void ApplyInitialVelocity()
    {
        if (rb == null || initialSpeed <= 0f) return;
        Vector3 v = rb.linearVelocity;
        Vector3 velXZ = ComputeVelocityXZ();
        v.x = velXZ.x;
        v.z = velXZ.z;
        if (lockYVelocity) v.y = 0f;
        rb.linearVelocity = v;
    }

    bool HasWaypoints => _usePositionWaypoints
        ? (_positionWaypoints != null && _positionWaypoints.Length > 0)
        : (waypoints != null && waypoints.Length > 0);

    int WaypointCount => _usePositionWaypoints
        ? (_positionWaypoints?.Length ?? 0)
        : (waypoints?.Length ?? 0);

    Vector3 GetWaypointPos(int index)
    {
        if (_usePositionWaypoints) return _positionWaypoints[index];
        Transform wp = waypoints[index];
        return wp != null ? wp.position : transform.position;
    }

    /// <summary>
    /// 이번 FixedUpdate에 적용할 XZ 속도를 계산.
    /// 이번 스텝 이동거리(initialSpeed * fixedDeltaTime) 안에 목표 웨이포인트가 들어오면,
    /// 오버슈트 없이 좌표에 정확히 도착하도록 그 스텝만 속도를 남은 거리만큼 줄여서 스냅한다
    /// (예전엔 waypointReachDistance만큼 미리 다음 웨이포인트로 꺾여서 중심이 좌표에 못 미쳤음).
    /// </summary>
    Vector3 ComputeVelocityXZ()
    {
        if (!HasWaypoints) return transform.forward * initialSpeed;
        if (_waypointFinished) return Vector3.zero;

        Vector3 toWp = GetWaypointPos(_waypointIndex) - transform.position;
        toWp.y = 0f;

        float stepDist = initialSpeed * Time.fixedDeltaTime;

        if (toWp.magnitude <= stepDist)
        {
            Vector3 exactVelocity = stepDist > 0f ? toWp / Time.fixedDeltaTime : Vector3.zero;
            AdvanceWaypoint();
            return exactVelocity;
        }

        return toWp.normalized * initialSpeed;
    }

    void AdvanceWaypoint()
    {
        int count = WaypointCount;
        switch (endMode)
        {
            case WaypointEndMode.Stop:
                _waypointIndex++;
                if (_waypointIndex >= count)
                {
                    _waypointIndex = count - 1;
                    _waypointFinished = true;
                }
                break;

            case WaypointEndMode.Loop:
                _waypointIndex = (_waypointIndex + 1) % count;
                break;

            case WaypointEndMode.PingPong:
                _waypointIndex += _pingPongDir;
                if (_waypointIndex >= count)
                {
                    _waypointIndex = count - 2;
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

        Vector3 velXZ = ComputeVelocityXZ();

        Vector3 v = rb.linearVelocity;
        v.x = velXZ.x;
        v.z = velXZ.z;
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
