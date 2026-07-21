using System.Collections;
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

    /// <summary>
    /// PhaseManager가 Phase 전환 시 호출.
    /// 이 배율이 baseSpeed × timeSpeedMultiplier 에 추가로 곱해짐.
    /// 1.0 = 기본 속도, 2.0 = 2배 빠르게
    /// </summary>
    public void SetPhaseSpeedMultiplier(float mult) => _phaseSpeedMultiplier = mult;

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

        // B안: Spawn 후 InitializeVelocityClientRpc로 초기 velocity 전파.
        // Client는 NT 위치 동기화 없이 이 velocity로 로컬 비행.
        NetworkObject netObj = fired.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            // destroyWithScene: true → 씬 리로드 시 자동 Despawn (잔존 화살 방지)
            netObj.Spawn(destroyWithScene: true);
            if (speed > 0f)
                proj.InitializeVelocityClientRpc(flatFwd * speed);
        }
    }
}
