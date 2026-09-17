using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// Stage5 추격 AI.
///
/// [동작 — 러너 재설계 2026-09-18, TStage5RunnerRedesign.md §1.3]
/// - 추격 대상은 **이번 라운드의 러너 1명뿐**. 2층 안내자는 아무리 가까워도 무시한다.
///   (구 동작 "가장 가까운 생존자 추격"은 폐기 — 2층 인원이 체이서를 끌어당기면 미로가 성립하지 않음)
/// - 러너가 은신(PlayerStealth 레이어)이거나 사망 상태면 대체 타겟 없이 정지한다.
/// - 일정 속도로 추격. 데미지는 자식 Stage5ChaserHitbox에서 항상 판정.
/// - **피격을 주면 데미지 1 후 스스로 소멸**(Host Despawn). 살아있는 수 유지·리스폰은
///   Stage5ChaserSpawner가 담당한다.
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
/// - postHitStopDuration: 피격 후 정지하다 소멸하기까지의 시간
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

    [Header("피격 후 소멸")]
    [Tooltip("히트박스로 피격 판정 후 제자리 정지하는 시간(초). 이 시간이 지나면 Host가 Despawn한다.\n" +
             "예전에는 이 시간 뒤 추격을 재개했지만, 러너 재설계 이후에는 소멸까지의 연출 길이다.")]
    [SerializeField] float postHitStopDuration = 1f;

    [Header("애니메이션")]
    [Tooltip("비워두면 자식에서 자동 탐색")]
    [SerializeField] Animator _anim;

    [Header("사운드 (Run 루프 — 3D)")]
    [Tooltip("0 = 완전 2D, 1 = 완전 3D")]
    [SerializeField] [Range(0f, 1f)] float runSpatialBlend = 1f;
    [Tooltip("이 거리(m) 이내에서는 최대 볼륨")]
    [SerializeField] float runMinDistance = 10f;
    [Tooltip("이 거리(m) 밖에서는 완전 무음. 0이면 500으로 처리")]
    [SerializeField] float runMaxDistance = 30f;
    [SerializeField] AudioRolloffMode runRolloffMode = AudioRolloffMode.Logarithmic;

    [Header("사운드 (공격 단발음 — 3D)")]
    [Tooltip("이 거리(m) 이내에서는 최대 볼륨")]
    [SerializeField] float attackMinDistance = 10f;
    [Tooltip("이 거리(m) 밖에서는 완전 무음. 0이면 500으로 처리")]
    [SerializeField] float attackMaxDistance = 30f;
    [SerializeField] AudioRolloffMode attackRolloffMode = AudioRolloffMode.Logarithmic;

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
    /// 이번 라운드의 러너 1명만 타겟으로 잡는다 (§1.3). 거리 비교 없음 — 후보가 하나뿐이다.
    ///
    /// NV에서 매번 다시 읽는 이유: 러너는 라운드마다 바뀌는데 스폰 시점에 한 번만 받아두면
    /// 라운드가 넘어간 뒤에도 옛 러너를 쫓는다. 라운드 전환 때 스포너가 전부 치우긴 하지만,
    /// "타겟의 진실은 NV 하나"로 두는 편이 그 순서에 기대지 않아 안전하다.
    /// 러너가 죽었거나 은신 중이면 대체 타겟 없이 정지한다.
    /// </summary>
    void UpdateTarget()
    {
        _currentTarget = null;

        var net = StageNetworkState.Instance;
        if (net == null || _allPlayers == null || _allPlayers.Length == 0) return;

        ulong runnerId = net.T5CurrentRunnerClientId;

        for (int i = 0; i < _allPlayers.Length; i++)
        {
            Player p = _allPlayers[i];
            if (p == null || p.IsDead)              continue;
            if (p.gameObject.layer != _playerLayer) continue; // 은신 중이면 레이어가 다르다

            NetworkObject netObj = p.GetComponent<NetworkObject>();
            if (netObj == null || netObj.OwnerClientId != runnerId) continue;

            _currentTarget = p;
            return;
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

    /// <summary>히트박스가 피해를 줄 수 있는지. 활성 상태이고 피격 후 정지(쿨다운) 중이 아닐 때만 true.</summary>
    public bool CanApplyDamage() => _isActive && !_isPostHitStop;

    /// <summary>
    /// 히트박스에서 TakeDamage 직후 호출 — 정지 후 **소멸**(§1.3).
    /// OnTriggerStay로 매 프레임 재호출될 수 있으므로 `_isPostHitStop`으로 1회만 받는다
    /// (두 번째 호출부터는 이미 소멸 진행 중이라 무시 — 사운드 스팸·중복 Despawn 방지).
    /// </summary>
    public void NotifyHitFromHitbox()
    {
        if (!_isActive || _isPostHitStop) return;

        PlayAttackSfxClientRpc(transform.position);
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
        SFXManager.Instance?.PlayAtPoint(SFXId.Stage5_Chaser_Attack, position, attackMinDistance, attackMaxDistance, attackRolloffMode);
    }

    /// <summary>
    /// 피격 연출(정지 + doHit) 후 Host가 Despawn한다. 예전처럼 추격을 재개하지 않는다 — §1.3의
    /// "데미지 1 후 소멸". 정지하는 동안 `_isPostHitStop`이 CanApplyDamage를 막으므로 사라지기 전에
    /// 러너를 한 번 더 때리지 않는다.
    /// </summary>
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

        _postHitStopRoutine = null;
        _isActive           = false;

        // 스포너가 매 프레임 살아있는 수를 세어 리스폰 타이머를 건다 — 여기서는 사라지기만 하면 된다.
        if (IsServer && IsSpawned) NetworkObject.Despawn(true);
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
