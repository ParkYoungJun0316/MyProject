using Unity.Netcode;
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
/// [네트워크 — Host 전권 시뮬 + NetworkTransform 복제 (TStageNetworkBoard.md §3.2 확정)]
/// - Update()의 NavMeshAgent 추적 판단은 Host 전용. Client는 프리팹의 서버 권한
///   NetworkTransform이 위치만 받아 재생 — NavMeshAgent는 Client에서 비활성화해
///   NetworkTransform과의 위치 갱신 충돌을 막는다.
/// - isRun 애니 파라미터는 NetworkVariable로 전파(연출용, §9.0 원칙). Host가 값을 쓰면
///   OnValueChanged가 전 머신(Host 포함)에서 동일하게 Animator에 반영.
///
/// [Inspector 설정]
/// - NavMeshAgent 부착 필수
/// - 자식에 ChaserHitBox + Collider isTrigger + Stage5ChaserHitbox
/// - moveSpeed, retargetInterval, navSampleRadius 설정
/// - postHitStopDuration: 피격 후 정지 시간
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class Stage5ChaserAI : NetworkBehaviour
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

    [Header("사운드 (Run 루프 — 3D)")]
    [Tooltip("0 = 완전 2D, 1 = 완전 3D")]
    [SerializeField] [Range(0f, 1f)] float runSpatialBlend = 1f;
    [Tooltip("이 거리(m) 이내에서는 최대 볼륨")]
    [SerializeField] float runMinDistance = 1f;
    [Tooltip("이 거리(m) 밖에서는 완전 무음. 0이면 500으로 처리")]
    [SerializeField] float runMaxDistance = 0f;
    [SerializeField] AudioRolloffMode runRolloffMode = AudioRolloffMode.Logarithmic;

    // ── 내부 상태 ─────────────────────────────────────────────────

    NavMeshAgent _agent;
    Player[]     _allPlayers;
    Player       _currentTarget;

    int _playerLayer;

    bool  _isActive;
    bool  _isPostHitStop;
    float _retargetTimer;

    Coroutine _postHitStopRoutine;

    AudioSource _runLoopSource;

    // Host가 쓰고 전 머신이 읽는 추격 애니 상태 (연출 전용, 판정 아님).
    readonly NetworkVariable<bool> _isChasingNV = new NetworkVariable<bool>(false);

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

    public override void OnNetworkSpawn()
    {
        // Client는 NetworkTransform이 위치를 전담 — Agent를 켜두면 NavMesh 스냅과 충돌한다.
        if (!IsServer)
            _agent.enabled = false;

        _isChasingNV.OnValueChanged += HandleChaseChanged;
        HandleChaseChanged(false, _isChasingNV.Value);
    }

    public override void OnNetworkDespawn()
    {
        _isChasingNV.OnValueChanged -= HandleChaseChanged;
        StopRunLoop();
    }

    void OnEnable()
    {
        _isActive      = false;
        _isPostHitStop = false;
        _retargetTimer = 0f;
    }

    void OnDisable()
    {
        StopRunLoop();
    }

    void Update()
    {
        // 볼륨 실시간 반영(옵션 메뉴 마스터/SFX 슬라이더) — 전 머신에서 실행, Host 전용 로직과 무관.
        if (_runLoopSource != null && _runLoopSource.isPlaying && SFXManager.Instance != null)
            _runLoopSource.volume = SFXManager.Instance.GetEffectiveVolume(SFXId.Stage5_Chaser_Run);

        if (!IsServer) return; // Host 전권 시뮬 (TStageNetworkBoard.md §3.2)
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

    /// <summary>
    /// 히트박스에서 TakeDamage 직후 호출 — 정지 코루틴 시작.
    /// OnTriggerStay로 매 프레임 재호출될 수 있어, 사운드는 아직 postHitStop 중이 아닐 때(=새
    /// 히트 에피소드 시작 시점)만 1회 재생 — 붙어있는 동안 계속 겹쳐 불려도 스팸되지 않는다.
    /// </summary>
    public void NotifyHitFromHitbox()
    {
        if (!_isActive) return;
        if (!_isPostHitStop) PlayAttackSfxClientRpc(transform.position);
        if (_postHitStopRoutine != null) StopCoroutine(_postHitStopRoutine);
        _postHitStopRoutine = StartCoroutine(PostHitStopRoutine());
    }

    /// <summary>
    /// 공격(피격) 사운드를 전 머신에서 재생. NotifyHitFromHitbox()는 Stage5ChaserHitbox.TryHit()의
    /// Host-only 가드를 거쳐 Host에서만 호출되므로, WindTrap/DropTrap과 동일하게 여기서 전체
    /// 브로드캐스트로 통일한다(Host 로컬 Play() 직접 호출 시 Host만 들리는 문제 방지).
    /// </summary>
    [ClientRpc]
    void PlayAttackSfxClientRpc(Vector3 position)
    {
        SFXManager.Instance?.Play(SFXId.Stage5_Chaser_Attack, position);
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

    /// <summary>Host 전용 호출부(Chase/Deactivate/PostHitStopRoutine)에서만 쓰인다.</summary>
    void SetAnimChase(bool chase)
    {
        if (!IsServer) return;
        _isChasingNV.Value = chase;
    }

    void HandleChaseChanged(bool previous, bool current)
    {
        if (_anim != null) _anim.SetBool("isRun", current);

        if (current) StartRunLoop();
        else StopRunLoop();
    }

    // ── 사운드 (Run 루프) ─────────────────────────────────────────
    // _isChasingNV는 Host가 쓰고 전 머신(Host 포함)이 OnValueChanged로 동일하게 수신 —
    // 이미 네트워크로 동기화된 상태라 별도 RPC 없이 여기 얹기만 하면 전 머신에서 안전하게 재생됨.

    void StartRunLoop()
    {
        if (_runLoopSource != null && _runLoopSource.isPlaying) return;
        if (SFXManager.Instance == null) return;

        AudioClip clip = SFXManager.Instance.GetClip(SFXId.Stage5_Chaser_Run);
        if (clip == null) return;

        if (_runLoopSource == null)
        {
            _runLoopSource               = gameObject.AddComponent<AudioSource>();
            _runLoopSource.loop          = true;
            _runLoopSource.playOnAwake   = false;
            _runLoopSource.spatialBlend  = runSpatialBlend;
            _runLoopSource.rolloffMode   = runRolloffMode;
            _runLoopSource.minDistance   = runMinDistance > 0f ? runMinDistance : 1f;
            _runLoopSource.maxDistance   = runMaxDistance > 0f ? runMaxDistance : 500f;
        }

        _runLoopSource.clip   = clip;
        _runLoopSource.volume = SFXManager.Instance.GetEffectiveVolume(SFXId.Stage5_Chaser_Run);
        _runLoopSource.Play();
    }

    void StopRunLoop()
    {
        if (_runLoopSource != null && _runLoopSource.isPlaying)
            _runLoopSource.Stop();
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
