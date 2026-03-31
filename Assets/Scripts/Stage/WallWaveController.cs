using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 여러 벽을 사인 파형으로 제어하는 파형 컨트롤러.
/// WallMover를 벽마다 붙이는 대신 하나의 스크립트로 모든 벽을 관리.
/// 식도 연동 운동처럼 파도가 배열 순서대로 전파되는 효과.
///
/// [동작 원리]
///  position[i] = 시작위치 + moveAxis * amplitude * sin(2π * frequency * (t - i / waveSpeed) + phaseOffset)
///
/// [설정 방법]
///  1. walls[] 에 이동시킬 벽 Transform 등록 (배열 순서 = 파형 전파 순서)
///  2. 각 WallEntry의 moveAxis를 로컬 좌표로 설정
///     예) 왼쪽 벽들: (1,0,0), 오른쪽 벽들: (-1,0,0) → 양쪽이 서로 조여드는 효과
///  3. Rigidbody가 있는 벽 → MovePosition 사용 (플레이어 밀침 가능)
///     Rigidbody가 없는 벽 → transform.position 직접 이동
/// </summary>
public class WallWaveController : MonoBehaviour
{
    [System.Serializable]
    public class WallEntry
    {
        [Tooltip("이동할 벽 Transform")]
        public Transform wall;

        [Tooltip("벽이 움직이는 방향 (로컬 좌표). 예: (1,0,0) = 로컬 오른쪽\n" +
                 "반대쪽 벽은 (-1,0,0)으로 설정하면 서로 조여드는 효과")]
        public Vector3 moveAxis = Vector3.right;
    }

    [Header("벽 목록 (배열 순서 = 파형 전파 순서)")]
    [SerializeField] WallEntry[] walls = new WallEntry[0];

    [Header("파형 설정")]
    [Tooltip("벽의 최대 이동 거리 (m). 0이면 움직이지 않음")]
    [SerializeField] float amplitude = 0f;

    [Tooltip("초당 진동 횟수 (Hz). 1 = 1초에 한 번 왕복")]
    [SerializeField] float frequency = 0f;

    [Tooltip("파형이 배열 순서대로 전파되는 속도 (index/초).\n" +
             "0이면 모든 벽이 동시에 움직임. 값이 클수록 파도가 빠르게 전파")]
    [SerializeField] float waveSpeed = 0f;

    [Tooltip("파형 초기 위상 오프셋 (도). 시작 위치 조정용 (0 = sin 시작, 90 = cos 시작)")]
    [SerializeField] float phaseOffset = 0f;

    [Header("활성화")]
    [Tooltip("true: 플레이어가 Collider에 진입하면 자동으로 파형 시작\n" +
             "false: Play()를 PlayerTriggerZone 등 외부에서 직접 호출")]
    [SerializeField] bool activateOnPlayerTrigger = true;

    [Tooltip("true: 최초 1회만 발동 / false: 플레이어 재진입 시마다 재발동")]
    [SerializeField] bool activateOnce = true;

    [Tooltip("true: 씬 시작 시 자동으로 파형 시작 (트리거 없이 즉시)")]
    [SerializeField] bool playOnStart = false;

    [Header("이벤트")]
    public UnityEvent OnPlay;
    public UnityEvent OnStop;

    bool _isPlaying;
    bool _hasActivated;

    Vector3[] _startPositions;
    Rigidbody[] _rbs;

    void Awake()
    {
        CacheWalls();
    }

    void Start()
    {
        if (playOnStart) Play();
    }

    void CacheWalls()
    {
        int count = walls.Length;
        _startPositions = new Vector3[count];
        _rbs = new Rigidbody[count];

        for (int i = 0; i < count; i++)
        {
            if (walls[i].wall == null) continue;
            _startPositions[i] = walls[i].wall.position;
            _rbs[i] = walls[i].wall.GetComponent<Rigidbody>();
            if (_rbs[i] != null) _rbs[i].isKinematic = true;
        }
    }

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>파형 시작. PlayerTriggerZone.OnPlayerEnter 또는 외부에서 직접 호출.</summary>
    public void Play()
    {
        if (_isPlaying) return;
        if (activateOnce && _hasActivated) return;
        _hasActivated = true;
        _isPlaying = true;
        OnPlay?.Invoke();
    }

    /// <summary>파형 정지. 벽을 원래 위치로 되돌림.</summary>
    public void Stop()
    {
        _isPlaying = false;
        ResetWalls();
        OnStop?.Invoke();
    }

    /// <summary>정지 후 재사용 가능 상태로 초기화.</summary>
    public void ResetAndReady()
    {
        Stop();
        _hasActivated = false;
    }

    // ── 트리거 감지 ──────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (!activateOnPlayerTrigger) return;

        Player player = other.GetComponentInParent<Player>();
        if (player == null || player.IsDead) return;

        Play();
    }

    // ── 내부 ────────────────────────────────────────────────────

    void FixedUpdate()
    {
        if (!_isPlaying) return;

        float t = Time.time;
        float phaseRad = phaseOffset * Mathf.Deg2Rad;
        float twoPiFreq = 2f * Mathf.PI * frequency;

        for (int i = 0; i < walls.Length; i++)
        {
            if (walls[i].wall == null) continue;

            float phaseDelay = (waveSpeed > 0f) ? (i / waveSpeed) : 0f;
            float wave = Mathf.Sin(twoPiFreq * (t - phaseDelay) + phaseRad);

            Vector3 worldAxis = walls[i].wall.TransformDirection(walls[i].moveAxis.normalized);
            Vector3 targetPos = _startPositions[i] + worldAxis * (amplitude * wave);

            if (_rbs[i] != null)
                _rbs[i].MovePosition(targetPos);
            else
                walls[i].wall.position = targetPos;
        }
    }

    void ResetWalls()
    {
        for (int i = 0; i < walls.Length; i++)
        {
            if (walls[i].wall == null) continue;

            if (_rbs[i] != null)
                _rbs[i].MovePosition(_startPositions[i]);
            else
                walls[i].wall.position = _startPositions[i];
        }
    }

    // ── 에디터 지원 ──────────────────────────────────────────────

    [ContextMenu("테스트: 재생")]
    void Debug_Play() => Play();

    [ContextMenu("테스트: 정지")]
    void Debug_Stop() => Stop();

    void OnDrawGizmos()
    {
        if (walls == null) return;

        for (int i = 0; i < walls.Length; i++)
        {
            if (walls[i].wall == null) continue;

            Vector3 origin = Application.isPlaying ? _startPositions[i] : walls[i].wall.position;
            Vector3 axis   = walls[i].wall.TransformDirection(walls[i].moveAxis.normalized);

            // 이동 범위 선
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
            Gizmos.DrawLine(origin - axis * amplitude, origin + axis * amplitude);

            // 원점 구
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.5f);
            Gizmos.DrawWireSphere(origin, 0.12f);

            // 이동 방향 화살표 끝점
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.9f);
            Gizmos.DrawWireSphere(origin + axis * amplitude, 0.08f);
        }
    }
}
