using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 발사형 함정 범용 컴포넌트.
/// fireAtSeconds에 지정한 초(스케줄 시작 기준)에 프리팹을 발사.
/// loopSchedule=true이면 schedulePeriod마다 패턴을 반복.
/// speedPhases로 시간 경과에 따른 속도 단계 상승을 지원.
///
/// [속도 설정]
/// baseSpeed > 0 이면 발사 시 rb.linearVelocity를 직접 설정.
/// baseSpeed = 0 이면 프리팹 Rigidbody 초기 상태(정지 또는 중력) 그대로.
///
/// [회전이 필요한 투사체]
/// 프리팹에 SpinRoller를 부착. SpinRoller는 rb.linearVelocity 방향을 읽어 angularVelocity만 설정.
///
/// [경로 이동 투사체]
/// 프리팹에 WaypointMover를 부착.
/// </summary>
public class ArrowTrap : TrapBase
{
    [Header("Arrow Trap")]
    [Tooltip("발사할 화살 프리팹 (TrapProjectile 컴포넌트 필수)")]
    [SerializeField] private GameObject arrowPrefab = null;

    [Tooltip("화살이 생성될 위치/방향 기준 Transform. 없으면 이 GameObject 사용")]
    [SerializeField] private Transform firePoint = null;

    [Header("발사 스케줄 (초 단위)")]
    [Tooltip("발사할 시각 목록 (스케줄 시작 기준, 초). 예: [0.5, 1.2, 2.0]")]
    [SerializeField] private float[] fireAtSeconds = new float[0];

    [Tooltip("스케줄 반복 여부")]
    [SerializeField] private bool loopSchedule = false;

    [Tooltip("반복 시 한 사이클 길이 (초). loopSchedule=true일 때만 사용")]
    [SerializeField] private float schedulePeriod = 3f;

    [Header("화살 속도")]
    [Tooltip("기본 화살 속도 (m/s). 0이면 프리팹 기본값 사용")]
    [SerializeField] private float baseSpeed = 0f;

    [Header("난이도 단계 (시간 경과 → 속도 배율 상승)")]
    [Tooltip("afterSeconds 이후 speedMultiplier 배율을 적용. afterSeconds 오름차순 입력")]
    [SerializeField] private SpeedPhase[] speedPhases = new SpeedPhase[0];

    [Header("발사 사운드")]
    [Tooltip("발사 시 재생할 SFX. None이면 무음.")]
    [SerializeField] private SFXId fireSfxId = SFXId.None;

    float scheduleStartTime;
    float _phaseSpeedMultiplier = 1f;

    // ── Mouth 연출(Open/Hold) 네트워크 동기화 (stable ID 레지스트리) ──────────
    // [버그 수정 2026-07-27] ID를 Awake 호출 순서로 매기면 안 된다 — ArrowTrap/MouthTrap이
    // PhaseManager.objectsToEnable로 늦게 SetActive(true)되는 경우, Host는 EnterPhase()에서
    // 로컬로 즉시 활성화하고 Client는 Phase NetworkVariable 수신 후 EnterPhaseOnClient()에서
    // 활성화한다 — 두 그룹(예: MouthTrap2 묶음 vs MouthTrap3_Side 묶음)이 어느 쪽이 먼저
    // 활성화되는지가 Host/Client 간 다를 수 있어 Awake 순서 기반 ID가 서로 뒤바뀌었다
    // (실기 확인: Host id=0→MouthTrap2, Client id=0→MouthTrap3_Side).
    // 대신 씬 계층 경로로 정렬한 결정적 순서로 전체를 한 번에 배정한다 — 비활성 오브젝트도
    // 씬 로드 시 이미 메모리에 존재하므로(Awake만 지연) FindObjectsByType(Include inactive)로
    // 활성화 여부와 무관하게 전부 찾을 수 있고, Host/Client가 같은 씬 파일을 로드하므로
    // 경로 문자열 정렬 결과가 항상 일치한다.
    static readonly Dictionary<int, ArrowTrap> _registry = new Dictionary<int, ArrowTrap>();
    static bool _registryBuilt = false;
    int _netIndex = -1;

    static void EnsureRegistryBuilt()
    {
        if (_registryBuilt) return;
        _registryBuilt = true;

        ArrowTrap[] all = FindObjectsByType<ArrowTrap>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OrderBy(a => GetHierarchyPath(a.transform), StringComparer.Ordinal)
            .ToArray();

        for (int i = 0; i < all.Length; i++)
        {
            all[i]._netIndex = i;
            _registry[i] = all[i];
        }
    }

    // [버그 수정 2026-07-27] 이름이 완전히 같은 형제(예: 같은 프리팹을 같은 부모 아래 여러 번
    // 배치하고 이름을 안 바꾼 M.Stage2의 MouthTrap2 x5)가 있으면 문자열 정렬이 동률이 되고,
    // OrderBy(stable sort)는 동률일 때 FindObjectsByType이 반환한 원래 순서를 그대로 쓴다 —
    // 이 원래 순서(엔진 내부 열거 순서)가 Host/Client 프로세스 간에 항상 같다는 보장이 없어
    // 서로 다른 물리적 오브젝트에 같은 _netIndex가 배정될 수 있었다(Client에서만 "엉뚱한
    // 입"이 "엉뚱한 시점"에 열리는 증상으로 나타남 — Host는 레지스트리를 거치지 않고 자기
    // 자신의 로컬 이벤트로 재생하므로 항상 정확했다). 각 계층 세그먼트에 GetSiblingIndex()를
    // 붙여 키를 완전히 결정적으로 만든다 — sibling 순서는 씬 파일에 저장된 그대로 로드되므로
    // Host/Client가 항상 동일하다.
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

    /// <summary>
    /// PhaseManager가 Phase 전환 시 호출.
    /// 이 배율이 baseSpeed × timeSpeedMultiplier 에 추가로 곱해짐.
    /// 1.0 = 기본 속도, 2.0 = 2배 빠르게
    /// </summary>
    public void SetPhaseSpeedMultiplier(float mult) => _phaseSpeedMultiplier = mult;

    protected override void Awake()
    {
        base.Awake();
        EnsureRegistryBuilt();
    }

    void OnDestroy()
    {
        _registry.Remove(_netIndex);
        // 씬의 마지막 ArrowTrap이 사라지면 레지스트리를 비워 다음 씬 로드 시 재구성되게 한다.
        if (_registry.Count == 0) _registryBuilt = false;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        OnPreFireCharge += RelayChargeToClients;
        OnFiring        += RelayFireToClients;
    }

    protected override void OnDisable()
    {
        OnPreFireCharge -= RelayChargeToClients;
        OnFiring        -= RelayFireToClients;
        base.OnDisable();
    }

    // Mouth 연출(Open/Hold)을 Host 발행 신호에서만 파생시키기 위한 릴레이.
    // Client도 이 TrapBase 이벤트를 로컬로 받지만(자기 스케줄 추정), MouthTrapAnimatorAnim /
    // MouthTrapAnimator 둘 다 Client에서는 이 로컬 이벤트를 구독하지 않고 아래 RPC로 도착한
    // 신호만 쓴다 (Mouth↔Arrow 타이밍 수정 — 두 개의 다른 시계가 같은 Animator/BlendShape를
    // 다투던 문제 해소).
    void RelayChargeToClients()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;
        StageNetworkState.Instance?.SyncArrowChargeClientRpc(_netIndex);
    }

    void RelayFireToClients()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;
        StageNetworkState.Instance?.SyncArrowFireClientRpc(_netIndex);
    }

    /// <summary>
    /// StageNetworkState.SyncArrowChargeClientRpc 수신 시 Client에서 호출. Mouth Open 연출만 재생.
    /// Mouth 연출 컴포넌트는 프리팹마다 Animator 기반(MouthTrapAnimatorAnim) 또는 BlendShape
    /// 기반(MouthTrapAnimator) 중 하나만 붙어 있으므로 둘 다 시도해도 중복 재생되지 않는다.
    /// </summary>
    public static void PlayChargeById(int id)
    {
        _registry.TryGetValue(id, out ArrowTrap t);
        if (t == null) return;
        t.GetComponent<MouthTrapAnimatorAnim>()?.PlayOpenFromNetwork();
        t.GetComponent<MouthTrapAnimator>()?.PlayOpenFromNetwork();
    }

    /// <summary>StageNetworkState.SyncArrowFireClientRpc 수신 시 Client에서 호출. Mouth Hold 연출만 재생.</summary>
    public static void PlayFireById(int id)
    {
        _registry.TryGetValue(id, out ArrowTrap t);
        if (t == null) return;
        t.GetComponent<MouthTrapAnimatorAnim>()?.PlayHoldFromNetwork();
        t.GetComponent<MouthTrapAnimator>()?.PlayHoldFromNetwork();
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
        // StageNetworkState.PhaseStartServerTime(Host가 이 Phase 진입 직전에 기록한 절대
        // ServerTime) 을 기준으로 잡는다 — Host/Client가 동일한 절대 시각을 앵커로 쓰게 되어
        // Client의 Activate() 호출이 Phase NV 전파 지연만큼 늦게 와도 스케줄이 밀리지 않는다.
        // StageStartServerTime이 아니라 별도 슬롯인 PhaseStartServerTime을 쓴다 —
        // StageStartServerTime은 StageStartGate가 "이 방 게이트 완료" 1회성 신호로 배타적으로
        // 쓰므로 같이 쓰면 안 된다 (2026-07-21 회귀 버그).
        // [버그 수정 2026-07-21] 예전엔 이 앵커를 자기 로컬 Activate() 시각으로 대체했었는데,
        // Client의 Activate()가 Host보다 항상 늦게(Phase NV 도착 후) 실행되는 걸 반영하지
        // 못해 입 벌림 애니메이션 등 코스메틱 타이밍이 Host와 계속 어긋나는 문제가 있었다.
        // PhaseManager.EnterPhase()가 Phase마다 MarkPhaseStart()를 다시 찍으므로, 앞 Phase가
        // 길어져도 스케줄이 과거로 밀려 한 번도 발사 안 되는 예전 버그는 재발하지 않는다.
        // StageNetworkState가 없는 씬(테스트 등)에서는 로컬 Activate() 시각으로 폴백.
        if (StageNetworkState.Instance != null && StageNetworkState.Instance.PhaseStartServerTime > 0)
        {
            scheduleStartTime = (float)StageNetworkState.Instance.PhaseStartServerTime + initialDelay;
            while (nm != null && (float)nm.ServerTime.Time < scheduleStartTime)
                yield return null;
        }
        else
        {
            if (initialDelay > 0f)
                yield return new WaitForSeconds(initialDelay);
            scheduleStartTime = nm != null ? (float)nm.ServerTime.Time : Time.time;
        }

        float cycleOffset = 0f;

        while (isRunning)
        {
            foreach (float t in fireAtSeconds)
            {
                if (!isRunning) yield break;

                float targetTime = scheduleStartTime + cycleOffset + t;
                if (ScheduleTimeUtil.IsPastEvent(targetTime, nm)) continue;
                float now        = nm != null ? (float)nm.ServerTime.Time : Time.time;
                // preFireChargeTime 만큼 앞당겨 대기 → 충전 시작 → 정확한 targetTime에 발사
                float waitTime   = Mathf.Max(0f, targetTime - now - preFireChargeTime);
                yield return new WaitForSeconds(waitTime);

                if (!isRunning) yield break;

                yield return StartCoroutine(FireWithCharge());
            }

            if (!loopSchedule) break;

            cycleOffset += schedulePeriod;
        }

        isRunning = false;
    }

    // 경과 시간 기준으로 현재 적용할 속도를 계산
    float GetCurrentSpeed()
    {
        // baseSpeed가 0이면 프리팹 기본값을 그대로 사용
        if (baseSpeed <= 0f) return 0f;

        var   nm  = NetworkManager.Singleton;
        float now = nm != null ? (float)nm.ServerTime.Time : Time.time;
        float elapsed = now - scheduleStartTime;
        float mult    = 1f;

        foreach (SpeedPhase phase in speedPhases)
        {
            if (elapsed >= phase.afterSeconds)
                mult = phase.speedMultiplier;
        }

        return baseSpeed * mult * _phaseSpeedMultiplier;
    }

    protected override void OnTrapTrigger()
    {
        if (arrowPrefab == null) return;

        var nm = NetworkManager.Singleton;
        // Host만 화살을 스폰. Client는 Host가 Spawn한 오브젝트를 수신(B안).
        if (nm == null || !nm.IsServer) return;

        Transform spawn   = firePoint != null ? firePoint : transform;
        Vector3   flatFwd = spawn.forward;

        if (fireSfxId != SFXId.None)
            SFXManager.Instance?.Play(fireSfxId, spawn.position);

        GameObject fired = Instantiate(arrowPrefab, spawn.position, spawn.rotation);

        TrapProjectile proj = fired.GetComponent<TrapProjectile>();
        if (proj == null) return;

        proj.moveDirection = flatFwd;

        float speed = GetCurrentSpeed();
        if (speed > 0f)
        {
            Rigidbody firedRb = fired.GetComponent<Rigidbody>();
            if (firedRb != null)
                firedRb.linearVelocity = flatFwd * speed;
        }

        // B안: velocity를 Spawn "전"에 NetworkVariable로 예약해 두면 스폰 메시지 자체에
        // 실려 전파된다. Spawn 후 별도 ClientRpc로 보내던 이전 방식은 CreateObject 메시지와
        // RPC 메시지의 전송 경로가 달라 RPC가 먼저 도착하면 Deferred OnSpawn으로 지연·유실됐다.
        NetworkObject netObj = fired.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            if (speed > 0f)
                proj.PrepareVelocity(flatFwd * speed);
            // destroyWithScene: true → 씬 리로드 시 자동 Despawn (잔존 화살 방지)
            netObj.Spawn(destroyWithScene: true);
        }
    }
}
