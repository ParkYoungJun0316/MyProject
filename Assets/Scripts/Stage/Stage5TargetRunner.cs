using UnityEngine;
using UnityEngine.AI;
using System;

/// <summary>
/// Stage5 도주 타겟 AI.
/// 자기 색과 매칭되는 플레이어(isUniqueColor 상태)만 피해 NavMesh 위에서 도주.
/// 맞는 색 + 고유색 상태 플레이어가 접촉하면 포획 성공 → OnCaptured 이벤트 발행.
///
/// [Inspector 필수 설정]
/// - Collider: isTrigger = true (포획 판정용)
/// - NavMeshAgent: 붙어 있어야 함
/// - targetColor: 담당 색상 설정
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class Stage5TargetRunner : MonoBehaviour
{
    [Header("색상")]
    [Tooltip("이 타겟이 피해야 할 플레이어의 색. playerColorType과 1:1 매칭")]
    public PlayerColorType targetColor = PlayerColorType.Blue;

    [Header("이동")]
    [Tooltip("이동 속도 (플레이어보다 약간 느리게 설정)")]
    [SerializeField] float moveSpeed = 0f;
    [Tooltip("도주 목적지 갱신 주기(초). 너무 짧으면 떨림, 너무 길면 멍청해짐")]
    [SerializeField] float retargetInterval = 0.25f;
    [Tooltip("이 거리 이내면 플레이어 반대 방향 우선 도주")]
    [SerializeField] float tooCloseDistance = 5f;
    [Tooltip("이 거리 이상이면 랜덤 도주")]
    [SerializeField] float tooFarDistance = 15f;
    [Tooltip("도주 목적지 탐색 반경(m)")]
    [SerializeField] float navSampleRadius = 10f;

    [Header("맵 중심 당김")]
    [Tooltip("도망 방향에 중심 쪽 벡터를 섞는 비율 (0=순수 반대방향, 1=중심으로만). 0.3~0.5 권장")]
    [SerializeField, Range(0f, 1f)] float centerPullWeight = 0.4f;
    [Tooltip("랜덤 도주 시 NavMesh.SamplePosition 원점을 '현재 위치'가 아니라 '현재~중심 사이'로 제한하는 비율.\n" +
             "1=완전히 중심 방향, 0=현재 위치 기준 (기존 동작). 0.5 권장")]
    [SerializeField, Range(0f, 1f)] float innerSampleBias = 0.5f;

    // 씬 오브젝트 참조 → 프리팹에 직접 못 넣으므로 Activate() 시 주입
    Transform _mapCenter;

    [Header("Stuck Recovery")]
    [Tooltip("이 시간(초) 동안 이동거리가 임계값 이하면 stuck 판정")]
    [SerializeField] float stuckCheckTime = 2f;
    [Tooltip("stuck 판정 이동거리 임계값(m)")]
    [SerializeField] float stuckDistanceThreshold = 0.05f;

    /// <summary>포획 성공 시 발행. Stage5TargetObjective가 구독</summary>
    public event Action<Stage5TargetRunner> OnCaptured;

    NavMeshAgent _agent;
    Player[] _allPlayers;
    Player _trackedPlayer;

    float _retargetTimer;
    float _stuckTimer;
    Vector3 _lastPosition;

    bool _isActive;
    bool _isCaptured;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.speed = moveSpeed;
        _agent.isStopped = true;
    }

    void OnEnable()
    {
        _isCaptured = false;
        _isActive = false;
        _retargetTimer = 0f;
        _stuckTimer = 0f;
        _lastPosition = transform.position;
    }

    /// <summary>
    /// Stage5TargetObjective.Begin()에서 호출. 플레이어 목록 + 맵 중심 주입 후 AI 시작.
    /// mapCenter는 씬 오브젝트라 프리팹에 직접 넣을 수 없으므로 여기서 주입.
    /// </summary>
    public void Activate(Player[] players, Transform mapCenterTransform = null)
    {
        _allPlayers = players;
        _mapCenter = mapCenterTransform;
        _isActive = true;
        _isCaptured = false;
        _lastPosition = transform.position;

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            _agent.isStopped = false;
    }

    public void Deactivate()
    {
        _isActive = false;
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;
        _agent.isStopped = true;
        _agent.ResetPath();
    }

    void Update()
    {
        if (!_isActive || _isCaptured) return;

        UpdateTrackedPlayer();

        _retargetTimer -= Time.deltaTime;
        if (_retargetTimer <= 0f)
        {
            _retargetTimer = retargetInterval;
            SetEscapeDestination();
        }

        CheckStuck();
    }

    // ── 추적 대상 플레이어 갱신 ──────────────────────────────────

    void UpdateTrackedPlayer()
    {
        if (_allPlayers == null) return;

        _trackedPlayer = null;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < _allPlayers.Length; i++)
        {
            Player p = _allPlayers[i];
            if (p == null || p.IsDead) continue;
            if (p.playerColorType != targetColor) continue;
            if (!p.isUniqueColor) continue;

            float dSq = (p.transform.position - transform.position).sqrMagnitude;
            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                _trackedPlayer = p;
            }
        }
    }

    // ── 도주 목적지 결정 ─────────────────────────────────────────

    void SetEscapeDestination()
    {
        if (_trackedPlayer == null)
        {
            TryMoveToInnerRandom();
            return;
        }

        float dist = Vector3.Distance(transform.position, _trackedPlayer.transform.position);

        if (dist >= tooFarDistance)
        {
            TryMoveToInnerRandom();
        }
        else
        {
            // 플레이어 반대 방향 + 맵 중심 쪽 벡터를 섞어서 가장자리 몰림 방지
            Vector3 awayDir = (transform.position - _trackedPlayer.transform.position).normalized;
            Vector3 blendDir = BlendWithCenter(awayDir);
            Vector3 awayTarget = transform.position + blendDir * navSampleRadius;

            if (!TryMoveTo(awayTarget))
                TryMoveToInnerRandom();
        }
    }

    /// <summary>
    /// awayDir에 맵 중심 방향을 centerPullWeight 비율로 섞어 반환.
    /// mapCenter 미설정 시 awayDir 그대로 반환(기존 동작 유지).
    /// </summary>
    Vector3 BlendWithCenter(Vector3 awayDir)
    {
        if (_mapCenter == null || centerPullWeight <= 0f) return awayDir;

        Vector3 toCenter = (_mapCenter.position - transform.position);
        toCenter.y = 0f;
        if (toCenter.sqrMagnitude < 0.001f) return awayDir;
        toCenter.Normalize();

        Vector3 blended = Vector3.Lerp(awayDir, toCenter, centerPullWeight);
        return blended.sqrMagnitude > 0.001f ? blended.normalized : awayDir;
    }

    bool TryMoveTo(Vector3 worldPos)
    {
        if (!_agent.isOnNavMesh) return false;

        if (NavMesh.SamplePosition(worldPos, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
        {
            _agent.isStopped = false;
            _agent.SetDestination(hit.position);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 랜덤 도주 시 샘플 원점을 '현재 위치 → 맵 중심' 방향으로 innerSampleBias만큼 당겨서
    /// 가장자리·코너보다 안쪽 영역에서 목적지가 나오도록 유도.
    /// </summary>
    void TryMoveToInnerRandom()
    {
        Vector3 sampleOrigin = transform.position;

        if (_mapCenter != null && innerSampleBias > 0f)
        {
            Vector3 toCenter = _mapCenter.position - transform.position;
            toCenter.y = 0f;
            sampleOrigin = transform.position + toCenter * innerSampleBias;
        }

        Vector3 rand = sampleOrigin + UnityEngine.Random.insideUnitSphere * navSampleRadius;
        rand.y = transform.position.y;

        if (!TryMoveTo(rand))
        {
            if (NavMesh.SamplePosition(sampleOrigin, out NavMeshHit hit, navSampleRadius * 2f, NavMesh.AllAreas))
            {
                _agent.isStopped = false;
                _agent.SetDestination(hit.position);
            }
        }
    }

    // ── Stuck Recovery ───────────────────────────────────────────

    void CheckStuck()
    {
        _stuckTimer += Time.deltaTime;
        if (_stuckTimer < stuckCheckTime) return;

        float moved = Vector3.Distance(transform.position, _lastPosition);
        _lastPosition = transform.position;
        _stuckTimer = 0f;

        if (moved < stuckDistanceThreshold)
            RecoverFromStuck();
    }

    void RecoverFromStuck()
    {
        if (_agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.ResetPath();
        }
        // stuck 복구 시에도 중심 쪽 랜덤으로 강제
        _retargetTimer = 0f;
        TryMoveToInnerRandom();
    }

    // ── 포획 판정 ────────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (_isCaptured || !_isActive) return;
        if (!other.CompareTag("Player")) return;

        Player p = other.GetComponent<Player>();
        if (p == null || p.IsDead) return;

        // 맞는 색 타입 + 고유색 상태여야만 성공 (흑/백 상태는 무효)
        if (p.playerColorType != targetColor) return;
        if (!p.isUniqueColor) return;

        _isCaptured = true;
        Deactivate();
        OnCaptured?.Invoke(this);
    }
}
