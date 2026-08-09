using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 바람 함정 — Push(밀어냄) / Pull(당김) 두 모드 지원.
/// fireAtSeconds 스케줄에 맞춰 windDuration 동안 범위 내 대상에 힘을 가함.
/// loopSchedule=true이면 schedulePeriod마다 패턴을 반복.
/// speedPhases로 시간 경과에 따른 힘 단계 상승을 지원.
///
/// [설정 방법]
/// 1. 이 스크립트를 붙인 GameObject에 Collider(Trigger) 추가 → 바람 범위
///    (BoxCollider = 방향성 바람 통로, SphereCollider = 방사형 바람 권장)
/// 2. Push 모드: transform.forward 방향으로 밀어냄 → GameObject 회전으로 방향 조절
/// 3. Pull 모드: 이 오브젝트 중심으로 당김 → 흡입구
/// 4. pushParticle/pullParticle에 각각 파티클 시스템 연결 시, 이번 사이클의 확정 모드(Random이면
///    발동 시점에 뽑힌 값)에 맞는 쪽만 자동 재생됨. Wind_Push/Wind_Pull SFX도 같은 타이밍에 자동 재생.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WindTrap : TrapBase
{
    public enum WindMode { Push, Pull, Random }

    [Header("Wind Trap")]
    [Tooltip("Push = 밀어냄 / Pull = 당김 / Random = 발동마다 랜덤 결정")]
    [SerializeField] private WindMode windMode = WindMode.Push;

    [Tooltip("기본 힘 세기 (N). 클수록 강하게 밀림/당겨짐")]
    [SerializeField] private float baseForce = 0f;

    [Tooltip("바람이 지속되는 시간(초). 0이면 순간 Impulse로 처리")]
    [SerializeField] private float windDuration = 0f;

    [Tooltip("Y축 힘 포함 여부. false이면 수평(XZ)만 적용 (일반 바닥 함정 권장)")]
    [SerializeField] private bool applyVerticalForce = false;

    [Tooltip("영향을 줄 대상 레이어. Player 레이어만 선택 권장")]
    [SerializeField] private LayerMask targetMask = ~0;

    [Header("발사 스케줄 (초 단위)")]
    [Tooltip("바람이 발동할 시각 목록 (스케줄 시작 기준, 초). 예: [0.5, 1.2, 2.0]")]
    [SerializeField] private float[] fireAtSeconds = new float[0];

    [Tooltip("스케줄 반복 여부")]
    [SerializeField] private bool loopSchedule = false;

    [Tooltip("반복 시 한 사이클 길이 (초). loopSchedule=true일 때만 사용")]
    [SerializeField] private float schedulePeriod = 3f;

    [Header("난이도 단계 (시간 경과 → 힘 배율 상승)")]
    [Tooltip("afterSeconds 이후 speedMultiplier 배율을 적용. afterSeconds 오름차순 입력")]
    [SerializeField] private SpeedPhase[] speedPhases = new SpeedPhase[0];

    [Header("Wind Visual (선택)")]
    [Tooltip("Push 모드일 때 재생할 파티클. Push/Pull은 입자 흐름 방향이 반대라 따로 필요. 없으면 생략")]
    [SerializeField] private ParticleSystem pushParticle = null;

    [Tooltip("Pull 모드일 때 재생할 파티클. 없으면 생략")]
    [SerializeField] private ParticleSystem pullParticle = null;

    float _scheduleStartTime;
    float _phaseForceMultiplier = 1f;
    bool _windActive;
    Collider _zone;

    // Host/Client가 같은 Push/Pull(Random)을 뽑도록 OnTrapTrigger 발동 횟수를 시드 salt로 사용.
    // TrapLoop()가 PhaseStartServerTime에 앵커링돼 있어 이 값은 Host/Client에서 항상 같은
    // 시점에 같은 값으로 증가함 (SpikeLaneField와 동일 관례).
    int _fireCount;

    float _windChargeTime = 0f;
    bool  _forceActive = false;    // charge 완료 후 FixedUpdate 힘 적용 허용 플래그
    float _windForceElapsed = 0f;

    // Random 모드일 때 이번 사이클에서 확정된 모드. MouthWindAnimator가 이 값을 읽음
    WindMode _activeWindMode = WindMode.Push;

    readonly List<Rigidbody> _targetsInZone = new List<Rigidbody>();

    /// <summary>
    /// 현재 사이클에서 실제로 적용 중인 모드 (Random이면 발동 시 확정된 Push/Pull 반환).
    /// MouthWindAnimator가 올바른 Shape Key를 선택하는 데 사용.
    /// </summary>
    public WindMode CurrentWindMode => _activeWindMode;

    /// <summary>현재 바람이 활성화 중인지 여부 (MouthController 충돌 방지용)</summary>
    public bool IsWindActive => _windActive;

    /// <summary>
    /// MouthWindAnimator가 Awake에서 설정.
    /// 이 시간만큼 바람 발동을 지연시켜 입 오므림 애니메이션과 동기화.
    /// </summary>
    public void SetWindChargeTime(float t) => _windChargeTime = Mathf.Max(0f, t);

    /// <summary>바람 발동 _windChargeTime 전에 호출. MouthWindAnimator가 구독.</summary>
    public event System.Action OnWindCharge;

    /// <summary>바람 효과 종료 시 호출. MouthWindAnimator가 구독.</summary>
    public event System.Action OnWindEnd;

    /// <summary>
    /// WindCycle 시작 직전에 실행할 선택적 대기 훅.
    /// MouthWindAnimator가 MouthController 상태를 기다리는 코루틴을 등록.
    /// null이면 즉시 발동.
    /// </summary>
    public System.Func<IEnumerator> PreChargeHook = null;

    // ── Mouth 연출(Pull/Push Open/Hold/Close) 네트워크 동기화 (stable ID 레지스트리) ──
    // WindTrap의 바람 판정(WindCycle/FixedUpdate 힘 적용)은 Owner 물리 권한이라 그대로 각
    // 피어가 로컬로 실행한다(안 건드림). 오직 MouthWindAnimator의 연출 트리거만 Host 로컬
    // 이벤트 + ClientRpc로 통일한다 — 각 피어가 자기 로컬 OnWindCharge/OnWindEnd로 직접
    // 재생하면 Client의 ServerTime 추정 오차·백그라운드 스로틀링에 따라 애니메이션 타이밍이
    // Host와 어긋난다 (ArrowTrap Mouth 동기화와 동일 이유 — 2026-07-27).
    // ID는 씬 계층 경로(+sibling index tie-break)로 정렬한 결정적 순서로 배정한다 — Awake
    // 호출 순서로 매기면 PhaseManager.objectsToEnable로 늦게 활성화되는 그룹의 Host/Client
    // 활성화 순서가 달라져 ID가 뒤바뀔 수 있다 (ArrowTrap에서 실제로 겪은 버그, 동일 예방).
    static readonly Dictionary<int, WindTrap> _registry = new Dictionary<int, WindTrap>();
    static bool _registryBuilt = false;
    int _netIndex = -1;

    static void EnsureRegistryBuilt()
    {
        if (_registryBuilt) return;
        _registryBuilt = true;

        WindTrap[] all = FindObjectsByType<WindTrap>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OrderBy(a => GetHierarchyPath(a.transform), StringComparer.Ordinal)
            .ToArray();

        for (int i = 0; i < all.Length; i++)
        {
            all[i]._netIndex = i;
            _registry[i] = all[i];
        }
    }

    static string GetHierarchyPath(Transform t)
    {
        string path = t.name + "#" + t.GetSiblingIndex().ToString("D4");
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "#" + t.GetSiblingIndex().ToString("D4") + "/" + path;
        }
        return path;
    }

    /// <summary>StageNetworkState.SyncWindChargeClientRpc 수신 시 Client에서 호출. Mouth 오므림 연출만 재생.</summary>
    public static void PlayChargeById(int id)
    {
        _registry.TryGetValue(id, out WindTrap t);
        t?.GetComponent<MouthWindAnimator>()?.PlayChargeFromNetwork();
    }

    /// <summary>StageNetworkState.SyncWindEndClientRpc 수신 시 Client에서 호출. Mouth 복귀 연출만 재생.</summary>
    public static void PlayEndById(int id)
    {
        _registry.TryGetValue(id, out WindTrap t);
        t?.GetComponent<MouthWindAnimator>()?.PlayEndFromNetwork();
    }

    void RelayWindChargeToClients()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;
        StageNetworkState.Instance?.SyncWindChargeClientRpc(_netIndex);
    }

    void RelayWindEndToClients()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;
        StageNetworkState.Instance?.SyncWindEndClientRpc(_netIndex);
    }

    /// <summary>
    /// PhaseManager가 Phase 전환 시 호출.
    /// 이 배율이 baseForce × timeForceMultiplier 에 추가로 곱해짐.
    /// 1.0 = 기본 힘, 2.0 = 2배 강하게
    /// </summary>
    public void SetPhaseSpeedMultiplier(float mult) => _phaseForceMultiplier = mult;

    protected override void Awake()
    {
        base.Awake();
        EnsureRegistryBuilt();
    }

    void OnDestroy()
    {
        _registry.Remove(_netIndex);
        // 씬의 마지막 WindTrap이 사라지면 레지스트리를 비워 다음 씬 로드 시 재구성되게 한다.
        if (_registry.Count == 0) _registryBuilt = false;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        OnWindCharge += RelayWindChargeToClients;
        OnWindEnd    += RelayWindEndToClients;
    }

    protected override void Start()
    {
        _zone = GetComponent<Collider>();
        _zone.isTrigger = true;
        base.Start();
    }

    /// <summary>
    /// SetActive(false) 시 _windActive 플래그를 즉시 초기화.
    /// TrapBase.OnDisable은 StopAllCoroutines만 하므로 _windActive가 stale(true)로 남는 버그 방지.
    /// → 재활성화 후 OnTrapTrigger에서 if(_windActive) return 으로 Wind가 아예 발동 안 되는 현상 해결.
    /// </summary>
    protected override void OnDisable()
    {
        OnWindCharge -= RelayWindChargeToClients;
        OnWindEnd    -= RelayWindEndToClients;
        base.OnDisable();
        _windActive = false;
        _forceActive = false;
        _windForceElapsed = 0f;
        _targetsInZone.Clear();
        _fireCount = 0;
        StopAllParticles();
    }

    protected override IEnumerator TrapLoop()
    {
        if (fireAtSeconds == null || fireAtSeconds.Length == 0)
        {
            isRunning = false;
            yield break;
        }

        var nm = NetworkManager.Singleton;

        // ── 스케줄 기준 시각 결정 ─────────────────────────────────────────
        // ArrowTrap/DropTrap과 동일한 이유로 PhaseStartServerTime(Host가 이 Phase 진입 직전에
        // 기록한 절대 ServerTime)을 앵커로 사용 — Host/Client가 동일한 절대 시각을 기준으로 삼아,
        // Client의 Activate() 호출이 Phase NV 전파 지연만큼 늦게 와도 스케줄이 밀리지 않는다.
        // StageStartServerTime이 아니라 별도 슬롯인 PhaseStartServerTime을 쓴다 — StageStartGate가
        // 그 값을 "이 방 게이트 완료" 1회성 신호로 배타적으로 쓰므로 같이 쓰면 안 된다.
        // StageNetworkState가 없는 씬(테스트 등)에서는 로컬 Activate() 시각으로 폴백.
        if (StageNetworkState.Instance != null && StageNetworkState.Instance.PhaseStartServerTime > 0)
        {
            _scheduleStartTime = (float)StageNetworkState.Instance.PhaseStartServerTime + initialDelay;
            while (nm != null && (float)nm.ServerTime.Time < _scheduleStartTime)
                yield return null;
        }
        else
        {
            if (initialDelay > 0f)
                yield return new WaitForSeconds(initialDelay);
            _scheduleStartTime = nm != null ? (float)nm.ServerTime.Time : Time.time;
        }

        float cycleOffset = 0f;

        while (isRunning)
        {
            foreach (float t in fireAtSeconds)
            {
                if (!isRunning) yield break;

                float targetTime = _scheduleStartTime + cycleOffset + t;
                if (ScheduleTimeUtil.IsPastEvent(targetTime, nm)) continue;
                float now        = nm != null ? (float)nm.ServerTime.Time : 0f;
                float waitTime   = targetTime - now;

                if (waitTime > 0f)
                    yield return new WaitForSeconds(waitTime);

                if (!isRunning) yield break;

                OnTrapTrigger();
            }

            if (!loopSchedule) break;

            cycleOffset += schedulePeriod;
        }

        isRunning = false;
    }

    protected override void OnTrapTrigger()
    {
        if (_windActive) return;
        StartCoroutine(WindCycle());
    }

    IEnumerator WindCycle()
    {
        _windActive = true;

        // Random 모드: 이번 사이클의 Push/Pull을 먼저 확정 → MouthWindAnimator가 읽기 전에 설정.
        // 전 머신이 같은 모드를 뽑도록 공유 세션 시드 + 발동 횟수로 로컬 RNG를 동기화
        // (SpikeLaneField.OnTrapTrigger()와 동일 관례 — Host가 RPC로 모드를 뿌릴 필요 없음).
        if (windMode == WindMode.Random)
        {
            const int seedSalt = 0x5716D000;
            int mixedSeed = NetworkSessionData.Seed ^ seedSalt ^ (_fireCount * 0x2545F491);
            UnityEngine.Random.InitState(mixedSeed);
            _activeWindMode = UnityEngine.Random.value < 0.5f ? WindMode.Push : WindMode.Pull;
        }
        else
        {
            _activeWindMode = windMode;
        }
        _fireCount++;

        // 선택적 선행 대기: MouthController 애니메이션이 끝날 때까지 대기 (MouthWindAnimator가 등록)
        if (PreChargeHook != null)
            yield return StartCoroutine(PreChargeHook());

        // 사전 충전: MouthWindAnimator가 SetWindChargeTime을 설정했을 때 입 오므림과 동기화
        OnWindCharge?.Invoke();
        if (_windChargeTime > 0f)
            yield return new WaitForSeconds(_windChargeTime);

        GetActiveParticle()?.Play();
        SFXManager.Instance?.Play(
            _activeWindMode == WindMode.Push ? SFXId.Wind_Push : SFXId.Wind_Pull,
            transform.position);

        if (windDuration <= 0f)
        {
            ApplyForceToAll(ForceMode.Impulse);
            _windActive = false;
            GetActiveParticle()?.Stop();
            OnWindEnd?.Invoke();
            yield break;
        }

        // charge 완료 → FixedUpdate 힘 적용 시작
        _windForceElapsed = 0f;
        _forceActive = true;
    }

    void FixedUpdate()
    {
        if (!_forceActive) return;

        if (_windForceElapsed < windDuration)
        {
            ApplyForceToAll(ForceMode.Force);
            _windForceElapsed += Time.fixedDeltaTime;
        }
        else
        {
            _windActive = false;
            _forceActive = false;
            _windForceElapsed = 0f;
            GetActiveParticle()?.Stop();
            OnWindEnd?.Invoke();
        }
    }

    /// <summary>이번 사이클에서 확정된 모드(_activeWindMode)에 대응하는 파티클.</summary>
    ParticleSystem GetActiveParticle() => _activeWindMode == WindMode.Push ? pushParticle : pullParticle;

    /// <summary>비활성화/중단 시 안전하게 둘 다 정지 — 어느 쪽이 재생 중이었는지 추적할 필요 없음.</summary>
    void StopAllParticles()
    {
        if (pushParticle != null) pushParticle.Stop();
        if (pullParticle != null) pullParticle.Stop();
    }

    void ApplyForceToAll(ForceMode mode)
    {
        float force = GetCurrentForce();
        if (force <= 0f) return;

        for (int i = _targetsInZone.Count - 1; i >= 0; i--)
        {
            if (_targetsInZone[i] == null)
            {
                _targetsInZone.RemoveAt(i);
                continue;
            }

            Vector3 dir = GetWindDirection(_targetsInZone[i].position);
            if (dir.sqrMagnitude < 0.001f) continue;

            _targetsInZone[i].AddForce(dir * force, mode);
        }
    }

    Vector3 GetWindDirection(Vector3 targetPos)
    {
        // Push: +Z(forward) 방향으로 밀어냄
        // Pull: -Z(forward 반대) 방향으로 당김
        // Random은 WindCycle 시작 시 _activeWindMode로 이미 확정됨
        Vector3 dir = _activeWindMode == WindMode.Push ? transform.forward : -transform.forward;

        if (!applyVerticalForce) dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return Vector3.zero;

        return dir.normalized;
    }

    float GetCurrentForce()
    {
        if (baseForce <= 0f) return 0f;

        var   nm      = NetworkManager.Singleton;
        float now     = nm != null ? (float)nm.ServerTime.Time : Time.time;
        float elapsed = now - _scheduleStartTime;
        float mult    = 1f;

        foreach (SpeedPhase phase in speedPhases)
        {
            if (elapsed >= phase.afterSeconds)
                mult = phase.speedMultiplier;
        }

        return baseForce * mult * _phaseForceMultiplier;
    }

    void OnTriggerEnter(Collider other)
    {
        if ((targetMask.value & (1 << other.gameObject.layer)) == 0) return;

        Rigidbody rb = other.GetComponent<Rigidbody>()
                    ?? other.GetComponentInParent<Rigidbody>();

        if (rb == null) return;
        if (!IsLocalOwnerRigidbody(rb)) return;

        if (!_targetsInZone.Contains(rb))
            _targetsInZone.Add(rb);
    }

    /// <summary>
    /// 이동은 Owner + ClientNetworkTransform이 진실이므로, 바람 힘도 각 머신이 자기 Owner
    /// 캐릭터에만 적용한다. Host가 원격 플레이어의 물리 복사본(§9A — 함정 판정용으로 동적 유지)에
    /// 힘을 넣으면 Owner가 CNT로 보내는 실제 위치와 어긋나므로 여기서 걸러낸다.
    /// NetworkManager가 없는 씬(테스트 등)에서는 필터 없이 통과.
    /// </summary>
    bool IsLocalOwnerRigidbody(Rigidbody rb)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return true;

        NetworkObject netObj = rb.GetComponent<NetworkObject>()
                             ?? rb.GetComponentInParent<NetworkObject>();
        return netObj != null && netObj.IsOwner;
    }

    void OnTriggerExit(Collider other)
    {
        if ((targetMask.value & (1 << other.gameObject.layer)) == 0) return;

        Rigidbody rb = other.GetComponent<Rigidbody>()
                    ?? other.GetComponentInParent<Rigidbody>();

        if (rb != null)
            _targetsInZone.Remove(rb);
    }

    protected override void OnDeactivated()
    {
        bool wasActive = _windActive || _forceActive;
        _windActive = false;
        _forceActive = false;
        _windForceElapsed = 0f;
        _targetsInZone.Clear();
        _fireCount = 0;
        StopAllParticles();

        // Wind 발동 중 Deactivate() 직접 호출 시(SetActive 사이클 없이 소프트 중단)
        // OnWindEnd를 명시적으로 발행 → MouthWindAnimator가 입 열기 복귀를 처리하도록 통보
        if (wasActive) OnWindEnd?.Invoke();
    }
}
