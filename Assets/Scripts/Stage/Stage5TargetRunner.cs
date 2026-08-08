using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using System;

/// <summary>
/// Stage5 도주 타겟 AI — 노드 방식.
///
/// [동작]
/// - 도망 대상: 가장 가까운 생존 플레이어 (색 무관)
/// - 추적 대상이 있으면 → 노드 중 플레이어에서 가장 먼 것으로 이동
///   단, 러너→노드 방향이 러너→플레이어 방향과 minDeviationDegrees 이내면 제외(정면 박치기 방지)
///   각도 조건을 통과하는 노드가 없으면 폴백으로 거리 기준 최대 노드 사용
/// - 추적 대상 없으면 → 노드 중 랜덤 하나로 이동
/// - 포획(트리거): 활성 플레이어 누구나 접촉 시 성공 (색·고유색 조건 없음)
/// - 노드는 Stage5TargetObjective에서 Activate() 시 주입 (씬 오브젝트 → 프리팹에 못 넣음)
///
/// [네트워크 — Host 전권 시뮬 + NetworkTransform 복제 (TStageNetworkBoard.md §3.2 확정)]
/// - Update()의 노드 선택·추적 판단은 Host 전용. Client는 프리팹의 서버 권한 NetworkTransform이
///   위치만 받아 재생 — NavMeshAgent는 Client에서 비활성화.
/// - 포획 판정(OnTriggerEnter)도 Host-only — 함정 본체와 동일한 기존 표준(§9A.3)을 그대로 적용.
///   Client 로컬 트리거는 무시된다(Client의 콜라이더는 보간 위치라 완전 정확하지 않을 수 있음 —
///   기존 ContactDamage 등에서도 이미 감수 중인 동일한 원격 함정 체감 이슈, 새 문제 아님).
/// - isRun/isWalk 애니 파라미터는 NetworkVariable(byte 상태)로 전파.
///
/// [Inspector 설정]
/// - NavMeshAgent 부착 필수
/// - Collider isTrigger = true (포획 판정)
/// - moveSpeed 0 이상으로 설정
/// - minDeviationDegrees: 플레이어 방향과의 최소 허용 각도(기본 30°)
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class Stage5TargetRunner : NetworkBehaviour
{
    [Header("이동")]
    [SerializeField] float moveSpeed = 0f;
    [Tooltip("목적지 갱신 주기(초)")]
    [SerializeField] float retargetInterval = 0.3f;
    [Tooltip("노드 위치를 NavMesh 위 점으로 붙일 때 검색 반경(m)")]
    [SerializeField] float navSampleRadius = 3f;
    [Tooltip("러너→노드 방향과 러너→플레이어 방향 사이 최소 허용 각도(도).\n" +
             "이 각도보다 좁으면(정면 박치기 위험) 해당 노드 제외.")]
    [SerializeField] float minDeviationDegrees = 30f;

    [Header("Stuck Recovery")]
    [SerializeField] float stuckCheckTime = 1f;
    [SerializeField] float stuckDistanceThreshold = 0.05f;

    [Header("애니메이션")]
    [Tooltip("비워두면 자식에서 자동 탐색")]
    [SerializeField] Animator _anim;

    public event Action<Stage5TargetRunner> OnCaptured;

    NavMeshAgent _agent;
    Player[] _allPlayers;
    Player _trackedPlayer;
    Transform[] _nodes;

    float _retargetTimer;
    float _stuckTimer;
    Vector3 _lastPosition;
    float _cosDeviationThreshold;

    bool _isActive;
    bool _isCaptured;

    // Host가 쓰고 전 머신이 읽는 이동 애니 상태 (0=Idle, 1=Walk, 2=Run) — 연출 전용, 판정 아님.
    readonly NetworkVariable<byte> _moveStateNV = new NetworkVariable<byte>(0);

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.speed = moveSpeed;
        _agent.isStopped = true;
        _cosDeviationThreshold = Mathf.Cos(minDeviationDegrees * Mathf.Deg2Rad);

        if (_anim == null)
            _anim = GetComponentInChildren<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        // Client는 NetworkTransform이 위치를 전담 — Agent를 켜두면 NavMesh 스냅과 충돌한다.
        if (!IsServer)
            _agent.enabled = false;

        _moveStateNV.OnValueChanged += HandleMoveStateChanged;
        HandleMoveStateChanged(0, _moveStateNV.Value);
    }

    public override void OnNetworkDespawn()
    {
        _moveStateNV.OnValueChanged -= HandleMoveStateChanged;
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
    /// Stage5TargetObjective.Begin()에서 호출.
    /// nodes: 도망 목표 후보(씬 오브젝트 → 프리팹에서 못 넣으므로 여기서 주입).
    /// </summary>
    public void Activate(Player[] players, Transform[] nodes = null)
    {
        _allPlayers = players;
        _nodes = nodes;
        _isActive = true;
        _isCaptured = false;
        _lastPosition = transform.position;

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            _agent.isStopped = false;
    }

    public void Deactivate()
    {
        _isActive = false;
        UpdateAnim();
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;
        _agent.isStopped = true;
        _agent.ResetPath();
    }

    void Update()
    {
        if (!IsServer) return; // Host 전권 시뮬 (TStageNetworkBoard.md §3.2)
        if (!_isActive || _isCaptured) return;

        UpdateTrackedPlayer();
        UpdateAnim();

        _retargetTimer -= Time.deltaTime;
        if (_retargetTimer <= 0f)
        {
            _retargetTimer = retargetInterval;
            PickDestination();
        }

        CheckStuck();
    }

    // ── 플레이어 탐색 ────────────────────────────────────────────

    void UpdateTrackedPlayer()
    {
        if (_allPlayers == null) return;

        _trackedPlayer = null;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < _allPlayers.Length; i++)
        {
            Player p = _allPlayers[i];
            if (p == null || p.IsDead) continue;

            float dSq = (p.transform.position - transform.position).sqrMagnitude;
            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                _trackedPlayer = p;
            }
        }
    }

    // ── 목적지 선택 ──────────────────────────────────────────────

    void PickDestination()
    {
        if (_nodes == null || _nodes.Length == 0) return;
        if (!_agent.isOnNavMesh) return;

        Transform chosen = _trackedPlayer != null
            ? FarthestNodeFromPlayer()
            : RandomNode();

        if (chosen == null) return;

        if (NavMesh.SamplePosition(chosen.position, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
        {
            _agent.isStopped = false;
            _agent.SetDestination(hit.position);
        }
    }

    /// <summary>
    /// 플레이어에서 가장 먼 노드를 반환.
    /// 단, 러너→노드 방향이 러너→플레이어 방향과 minDeviationDegrees 이내인 노드는 제외.
    /// 조건을 통과하는 노드가 없으면 폴백으로 거리만 기준으로 선택.
    /// </summary>
    Transform FarthestNodeFromPlayer()
    {
        Vector3 runnerXZ = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 playerXZ = new Vector3(_trackedPlayer.transform.position.x, 0f, _trackedPlayer.transform.position.z);
        Vector3 dirToPlayer = (playerXZ - runnerXZ).normalized;

        Transform best = null;
        float bestDistSq = -1f;
        Transform fallback = null;
        float fallbackDistSq = -1f;
        Vector3 pPos = _trackedPlayer.transform.position;

        for (int i = 0; i < _nodes.Length; i++)
        {
            if (_nodes[i] == null) continue;

            float dSq = (_nodes[i].position - pPos).sqrMagnitude;

            // 폴백: 각도 무시하고 거리 최대
            if (dSq > fallbackDistSq)
            {
                fallbackDistSq = dSq;
                fallback = _nodes[i];
            }

            // 러너→노드 방향 XZ 투영
            Vector3 nodeXZ = new Vector3(_nodes[i].position.x, 0f, _nodes[i].position.z);
            Vector3 dirToNode = (nodeXZ - runnerXZ).normalized;

            // 두 방향이 거의 같으면(러너가 노드 위에 있으면) 내적 계산 skip
            if (dirToNode.sqrMagnitude < 0.001f) continue;

            // dot > _cosDeviationThreshold → 두 방향 사이 각 < minDeviationDegrees → 정면 박치기 위험 → 제외
            float dot = Vector3.Dot(dirToPlayer, dirToNode);
            if (dot > _cosDeviationThreshold) continue;

            if (dSq > bestDistSq)
            {
                bestDistSq = dSq;
                best = _nodes[i];
            }
        }

        return best != null ? best : fallback;
    }

    /// <summary>노드 중 랜덤 하나 반환</summary>
    Transform RandomNode()
    {
        int idx = UnityEngine.Random.Range(0, _nodes.Length);
        return _nodes[idx];
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
        _retargetTimer = 0f;
        PickDestination();
    }

    // ── 애니메이션 ────────────────────────────────────────────────

    /// <summary>
    /// isRun / isWalk 상태를 현재 추적 여부에 따라 갱신 (Host 전용 — NV에 씀).
    /// - 플레이어 추적 중 → Run
    /// - 랜덤 배회 중    → Walk
    /// - 비활성/포획     → Idle (둘 다 false)
    /// </summary>
    void UpdateAnim()
    {
        if (!IsServer) return;
        bool active = _isActive && !_isCaptured;
        byte state = !active ? (byte)0 : (_trackedPlayer != null ? (byte)2 : (byte)1);
        if (_moveStateNV.Value != state) _moveStateNV.Value = state;
    }

    void HandleMoveStateChanged(byte previous, byte current)
    {
        if (_anim == null) return;
        _anim.SetBool("isRun",  current == 2);
        _anim.SetBool("isWalk", current == 1);
    }

    // ── 포획 판정 ────────────────────────────────────────────────
    // Host-only 직접 감지 (함정 본체와 동일한 기존 표준, §9A.3) — Arrow류 발사체의
    // Client 보고 방식(B안)과는 다르다. Runner는 NetworkTransform으로 Host authoritative
    // 위치가 복제되므로, Host가 자신의 진짜 위치로 직접 판정하는 쪽이 표준과 일치한다.

    void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (_isCaptured || !_isActive) return;
        if (!other.CompareTag("Player")) return;

        Player p = other.GetComponent<Player>();
        if (p == null || p.IsDead) return;

        _isCaptured = true;
        SFXManager.Instance?.Play(SFXId.Stage5_Runner_Captured, transform.position);
        Deactivate();
        OnCaptured?.Invoke(this);
    }
}
