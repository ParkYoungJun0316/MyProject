using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// Stage5 추격 AI.
///
/// [동작]
/// - Player 레이어(비은신) 생존자 중 가장 가까운 1명 추격 (맵 전역, 거리 상한 없음)
/// - 타겟이 은신(PlayerStealth 레이어)으로 전환 → 다른 비은신 후보로 교체. 후보 없으면 정지.
/// - 일정 속도로 추격. 데미지는 자식 Stage5ChaserHitbox에서 항상 판정.
///
/// [Inspector 설정]
/// - NavMeshAgent 부착 필수
/// - 자식에 ChaserHitBox + Collider isTrigger + Stage5ChaserHitbox
/// - moveSpeed, retargetInterval, navSampleRadius 설정
/// - postHitStopDuration: 피격 후 정지 시간
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class Stage5ChaserAI : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] float moveSpeed        = 0f;
    [Tooltip("목적지 갱신 주기(초). 0이면 매 프레임")]
    [SerializeField] float retargetInterval = 0f;
    [Tooltip("NavMesh 위치 샘플링 검색 반경(m)")]
    [SerializeField] float navSampleRadius  = 0f;

    [Header("피격 후 정지")]
    [Tooltip("히트박스로 피격 판정 후 제자리 정지 시간(초)")]
    [SerializeField] float postHitStopDuration = 1f;

    [Header("애니메이션")]
    [Tooltip("비워두면 자식에서 자동 탐색")]
    [SerializeField] Animator _anim;

    // ── 내부 상태 ─────────────────────────────────────────────────

    NavMeshAgent _agent;
    Player[]     _allPlayers;
    Player       _currentTarget;

    int _playerLayer;

    bool  _isActive;
    bool  _isPostHitStop;
    float _retargetTimer;

    Coroutine _postHitStopRoutine;

    // ── Unity 생명주기 ────────────────────────────────────────────

    void Awake()
    {
        _agent           = GetComponent<NavMeshAgent>();
        _agent.speed     = moveSpeed;
        _agent.isStopped = true;

        _playerLayer = LayerMask.NameToLayer("Player");

        if (_anim == null)
            _anim = GetComponentInChildren<Animator>();
    }

    void OnEnable()
    {
        _isActive      = false;
        _isPostHitStop = false;
        _retargetTimer = 0f;
    }

    void Update()
    {
        if (!_isActive || _isPostHitStop) return;

        _retargetTimer -= Time.deltaTime;
        if (_retargetTimer <= 0f)
        {
            _retargetTimer = retargetInterval;
            UpdateTarget();
            Chase();
        }
    }

    // ── 외부 호출 ─────────────────────────────────────────────────

    /// <summary>Stage5ChaserSpawner에서 스폰 직후 호출.</summary>
    public void Activate(Player[] players)
    {
        _allPlayers    = players;
        _isActive      = true;
        _retargetTimer = 0f;

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            _agent.isStopped = false;
    }

    public void Deactivate()
    {
        _isActive = false;
        StopAllRoutines();

        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.ResetPath();
        }

        SetAnimChase(false);
    }

    // ── 타겟 갱신 ─────────────────────────────────────────────────

    /// <summary>
    /// Player 레이어(비은신) 생존자 중 가장 가까운 1명 선택.
    /// 은신 전환 시 다른 후보로 자동 교체. 후보 없으면 null.
    /// </summary>
    void UpdateTarget()
    {
        _currentTarget = null;

        if (_allPlayers == null || _allPlayers.Length == 0) return;

        float bestDistSq = float.MaxValue;

        for (int i = 0; i < _allPlayers.Length; i++)
        {
            Player p = _allPlayers[i];
            if (p == null || p.IsDead)                   continue;
            if (p.gameObject.layer != _playerLayer)       continue;

            float dSq = (p.transform.position - transform.position).sqrMagnitude;
            if (dSq < bestDistSq)
            {
                bestDistSq     = dSq;
                _currentTarget = p;
            }
        }
    }

    // ── 이동 ──────────────────────────────────────────────────────

    void Chase()
    {
        if (!_agent.isOnNavMesh) return;

        if (_currentTarget == null)
        {
            _agent.isStopped = true;
            _agent.ResetPath();
            SetAnimChase(false);
            return;
        }

        Vector3 dest = _currentTarget.transform.position;

        if (NavMesh.SamplePosition(dest, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
            dest = hit.position;

        _agent.speed     = moveSpeed;
        _agent.isStopped = false;
        _agent.SetDestination(dest);
        SetAnimChase(true);
    }

    // ── 히트박스 연동 (Stage5ChaserHitbox) ─────────────────────────

    /// <summary>히트박스가 피해를 줄 수 있는지. 활성 상태면 항상 true.</summary>
    public bool CanApplyDamage() => _isActive;

    /// <summary>히트박스에서 TakeDamage 직후 호출 — 정지 코루틴 시작.</summary>
    public void NotifyHitFromHitbox()
    {
        if (!_isActive) return;
        if (_postHitStopRoutine != null) StopCoroutine(_postHitStopRoutine);
        _postHitStopRoutine = StartCoroutine(PostHitStopRoutine());
    }

    IEnumerator PostHitStopRoutine()
    {
        _isPostHitStop = true;

        if (_agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.ResetPath();
        }

        SetAnimChase(false);
        if (_anim != null) _anim.SetTrigger("doHit");

        yield return new WaitForSeconds(postHitStopDuration);

        _isPostHitStop      = false;
        _postHitStopRoutine = null;

        _retargetTimer = 0f;

        if (_agent.isOnNavMesh)
            _agent.isStopped = false;
    }

    // ── 애니메이션 ────────────────────────────────────────────────

    void SetAnimChase(bool chase)
    {
        if (_anim == null) return;
        _anim.SetBool("isChase", chase);
    }

    // ── 유틸 ─────────────────────────────────────────────────────

    void StopAllRoutines()
    {
        if (_postHitStopRoutine != null)
        {
            StopCoroutine(_postHitStopRoutine);
            _postHitStopRoutine = null;
            _isPostHitStop      = false;
        }
    }
}
