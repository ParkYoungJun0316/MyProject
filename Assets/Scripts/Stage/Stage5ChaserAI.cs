using UnityEngine;
using UnityEngine.AI;
using System.Collections;
/// <summary>
/// Stage5 추격 AI.
///
/// [타겟 모드]
/// - UniqueColor : playerColorType == targetColor 인 플레이어를 추격(흑백 표시 포함).
///                은신(PlayerStealth 레이어) 또는 사망 시 → 정지.
/// - NearestAlive: 씬 플레이어 목록에서 Player 레이어(비은신) 생존자만 후보,
///                Chaser와의 거리가 가장 가까운 1명 추격(맵 전역, 별도 반경 없음).
///                타겟이 은신으로 전환 → 다른 비은신 후보로 교체. 후보 0명 → 정지.
///
/// [이동 타입]
/// - A: 일정 속도 추격.
/// - B: 일정 속도 추격 + 주기적 차지(일시 가속). 차지 구간 전체에서 데미지 가능.
///
/// [데미지]
/// - 자식 Stage5ChaserHitbox 트리거에서 처리 (EnemyHitbox와 동일 패턴).
/// - 타입 B: 차지(_isCharging) 중에만 CanApplyDamage() true.
/// - 재피격 간격은 Player.TakeDamage 내부 isDamage(약 0.5초)로 제한.
/// - 데미지 판정 후 postHitStopDuration 초 동안 제자리 정지.
///
/// [Inspector 설정]
/// - NavMeshAgent 부착 필수
/// - 자식에 ChaserHitBox + Collider isTrigger + Stage5ChaserHitbox
/// - moveType, targetMode 설정
/// - UniqueColor 모드: targetColor 설정
/// - moveType = B: chargeInterval, chargeDuration, chargeSpeed 설정
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class Stage5ChaserAI : MonoBehaviour
{
    // ── Enum ──────────────────────────────────────────────────────

    public enum ChaserTargetMode { UniqueColor, NearestAlive }
    public enum ChaserMoveType   { A, B }

    // ── Inspector ─────────────────────────────────────────────────

    [Header("타입")]
    public ChaserTargetMode targetMode = ChaserTargetMode.UniqueColor;
    public ChaserMoveType   moveType   = ChaserMoveType.A;

    [Header("색상 (UniqueColor 모드 전용)")]
    [Tooltip("추격할 플레이어의 색 배정(playerColorType). 흑백 표시 중에도 배정이 같으면 추격. UniqueColor 모드 전용")]
    public PlayerColorType targetColor = PlayerColorType.Blue;

    [Header("이동")]
    [SerializeField] float moveSpeed       = 0f;
    [Tooltip("목적지 갱신 주기(초). 0이면 매 프레임")]
    [SerializeField] float retargetInterval = 0f;
    [Tooltip("NavMesh 위치 샘플링 검색 반경(m)")]
    [SerializeField] float navSampleRadius  = 0f;

    [Header("타입 B - 차지")]
    [Tooltip("차지 발동 주기(초)")]
    [SerializeField] float chargeInterval = 0f;
    [Tooltip("차지 지속 시간(초)")]
    [SerializeField] float chargeDuration = 0f;
    [Tooltip("차지 중 이동 속도")]
    [SerializeField] float chargeSpeed    = 0f;

    [Header("피격 후 정지")]
    [Tooltip("히트박스로 피격 판정 후 제자리 정지 시간(초)")]
    [SerializeField] float postHitStopDuration = 1f;

    // ── 내부 상태 ─────────────────────────────────────────────────

    NavMeshAgent _agent;
    Player[]     _allPlayers;
    Player       _currentTarget;

    int _playerLayer;

    bool _isActive;
    bool _isCharging;
    bool _isPostHitStop;

    float _retargetTimer;
    float _chargeTimer;

    Coroutine _chargeRoutine;
    Coroutine _postHitStopRoutine;

    // ── Unity 생명주기 ────────────────────────────────────────────

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.speed     = moveSpeed;
        _agent.isStopped = true;

        _playerLayer = LayerMask.NameToLayer("Player");
    }

    void OnEnable()
    {
        _isActive      = false;
        _isCharging    = false;
        _isPostHitStop = false;
        _retargetTimer = 0f;
        _chargeTimer   = chargeInterval;
    }

    void Update()
    {
        if (!_isActive || _isCharging || _isPostHitStop) return;

        _retargetTimer -= Time.deltaTime;
        if (_retargetTimer <= 0f)
        {
            _retargetTimer = retargetInterval;
            UpdateTarget();
            Chase();
        }

        if (moveType == ChaserMoveType.B)
        {
            _chargeTimer -= Time.deltaTime;
            if (_chargeTimer <= 0f && _currentTarget != null)
            {
                _chargeTimer   = chargeInterval;
                _chargeRoutine = StartCoroutine(ChargeRoutine());
            }
        }
    }

    // ── 외부 호출 ─────────────────────────────────────────────────

    /// <summary>Stage5ChaserSpawner에서 스폰 직후 호출.</summary>
    public void Activate(Player[] players)
    {
        _allPlayers    = players;
        _isActive      = true;
        _chargeTimer   = chargeInterval;
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
    }

    // ── 타겟 갱신 ─────────────────────────────────────────────────

    void UpdateTarget()
    {
        switch (targetMode)
        {
            case ChaserTargetMode.UniqueColor:   UpdateUniqueColorTarget();   break;
            case ChaserTargetMode.NearestAlive:  UpdateNearestAliveTarget();  break;
        }
    }

    /// <summary>
    /// playerColorType == targetColor 인 플레이어 중 가장 가까운 사람(흑백 표시 포함).
    /// 은신(Player 레이어가 아님) 또는 사망이면 후보에서 제외 → 타겟 null → 정지.
    /// </summary>
    void UpdateUniqueColorTarget()
    {
        if (_allPlayers == null) return;

        _currentTarget = null;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < _allPlayers.Length; i++)
        {
            Player p = _allPlayers[i];
            if (p == null || p.IsDead)            continue;
            if (p.playerColorType != targetColor)  continue;
            if (IsPlayerStealth(p))                continue;

            float dSq = (p.transform.position - transform.position).sqrMagnitude;
            if (dSq < bestDistSq)
            {
                bestDistSq     = dSq;
                _currentTarget = p;
            }
        }
    }

    /// <summary>
    /// Activate 시 받은 플레이어 배열 전체를 보며, Player 레이어(비은신) 생존자 중
    /// Chaser에 가장 가까운 1명을 선택. 거리 상한 없음(맵 전역).
    /// </summary>
    void UpdateNearestAliveTarget()
    {
        _currentTarget = null;

        if (_allPlayers == null || _allPlayers.Length == 0) return;

        float bestDistSq = float.MaxValue;

        for (int i = 0; i < _allPlayers.Length; i++)
        {
            Player p = _allPlayers[i];
            if (p == null || p.IsDead) continue;
            if (p.gameObject.layer != _playerLayer) continue;

            float dSq = (p.transform.position - transform.position).sqrMagnitude;
            if (dSq < bestDistSq)
            {
                bestDistSq     = dSq;
                _currentTarget = p;
            }
        }
    }

    /// <summary>플레이어가 Player 레이어가 아닌지로 은신 여부 판단.</summary>
    bool IsPlayerStealth(Player p) => p.gameObject.layer != _playerLayer;

    // ── 이동 ──────────────────────────────────────────────────────

    void Chase()
    {
        if (!_agent.isOnNavMesh) return;

        if (_currentTarget == null)
        {
            _agent.isStopped = true;
            _agent.ResetPath();
            return;
        }

        Vector3 dest = _currentTarget.transform.position;

        if (NavMesh.SamplePosition(dest, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
            dest = hit.position;

        _agent.speed     = moveSpeed;
        _agent.isStopped = false;
        _agent.SetDestination(dest);
    }

    // ── 타입 B 차지 ───────────────────────────────────────────────

    IEnumerator ChargeRoutine()
    {
        _isCharging      = true;
        _agent.speed     = chargeSpeed;

        if (_currentTarget != null && _agent.isOnNavMesh)
        {
            _agent.isStopped = false;
            _agent.SetDestination(_currentTarget.transform.position);
        }

        yield return new WaitForSeconds(chargeDuration);

        _isCharging    = false;
        _agent.speed   = moveSpeed;
        _chargeRoutine = null;
    }

    // ── 히트박스 연동 (Stage5ChaserHitbox) ─────────────────────────

    /// <summary>히트박스가 피해를 줄 수 있는지. 타입 B는 차지 중만.</summary>
    public bool CanApplyDamage()
    {
        if (!_isActive) return false;
        if (moveType == ChaserMoveType.B) return _isCharging;
        return true;
    }

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

        // 차지 중이었다면 즉시 중단
        if (_chargeRoutine != null)
        {
            StopCoroutine(_chargeRoutine);
            _chargeRoutine = null;
            _isCharging    = false;
        }

        if (_agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.ResetPath();
        }

        yield return new WaitForSeconds(postHitStopDuration);

        _isPostHitStop = false;
        _postHitStopRoutine = null;

        // 정지 해제 후 즉시 타겟 갱신 트리거 + 차지 타이머 리셋(연속 차지 방지)
        _retargetTimer = 0f;
        _chargeTimer   = chargeInterval;

        if (_agent.isOnNavMesh)
            _agent.isStopped = false;
    }

    // ── 유틸 ─────────────────────────────────────────────────────

    void StopAllRoutines()
    {
        if (_chargeRoutine != null)
        {
            StopCoroutine(_chargeRoutine);
            _chargeRoutine = null;
            _isCharging    = false;
        }
        if (_postHitStopRoutine != null)
        {
            StopCoroutine(_postHitStopRoutine);
            _postHitStopRoutine = null;
            _isPostHitStop      = false;
        }
    }
}
