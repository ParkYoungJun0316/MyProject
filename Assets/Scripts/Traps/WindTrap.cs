using System.Collections;
using System.Collections.Generic;
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
/// 4. windParticle에 파티클 시스템 연결 시 바람 활성화 동안 자동 재생
/// </summary>
[RequireComponent(typeof(Collider))]
public class WindTrap : TrapBase
{
    public enum WindMode { Push, Pull }

    [Header("Wind Trap")]
    [Tooltip("Push = 밀어냄 (바람 방출) / Pull = 당김 (바람 흡입)")]
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
    [Tooltip("바람 활성화 시 재생할 파티클 시스템. 없으면 생략")]
    [SerializeField] private ParticleSystem windParticle = null;

    float _scheduleStartTime;
    float _phaseForceMultiplier = 1f;
    bool _windActive;
    Collider _zone;

    float _windChargeTime = 0f;

    readonly List<Rigidbody> _targetsInZone = new List<Rigidbody>();

    /// <summary>현재 바람 모드 (MouthWindAnimator가 Push/Pull 구분에 사용)</summary>
    public WindMode CurrentWindMode => windMode;

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
    /// PhaseManager가 Phase 전환 시 호출.
    /// 이 배율이 baseForce × timeForceMultiplier 에 추가로 곱해짐.
    /// 1.0 = 기본 힘, 2.0 = 2배 강하게
    /// </summary>
    public void SetPhaseSpeedMultiplier(float mult) => _phaseForceMultiplier = mult;

    protected override void Start()
    {
        _zone = GetComponent<Collider>();
        _zone.isTrigger = true;
        base.Start();
    }

    protected override IEnumerator TrapLoop()
    {
        _scheduleStartTime = Time.time;

        if (fireAtSeconds == null || fireAtSeconds.Length == 0)
        {
            isRunning = false;
            yield break;
        }

        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        float cycleOffset = 0f;

        while (isRunning)
        {
            foreach (float t in fireAtSeconds)
            {
                if (!isRunning) yield break;

                float targetTime = _scheduleStartTime + cycleOffset + t;
                float waitTime   = targetTime - Time.time;

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

        // 사전 충전: MouthWindAnimator가 SetWindChargeTime을 설정했을 때 입 오므림과 동기화
        OnWindCharge?.Invoke();
        if (_windChargeTime > 0f)
            yield return new WaitForSeconds(_windChargeTime);

        if (windParticle != null) windParticle.Play();

        if (windDuration <= 0f)
        {
            ApplyForceToAll(ForceMode.Impulse);
            _windActive = false;
            if (windParticle != null) windParticle.Stop();
            OnWindEnd?.Invoke();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < windDuration)
        {
            ApplyForceToAll(ForceMode.Force);
            elapsed += Time.deltaTime;
            yield return null;
        }

        _windActive = false;
        if (windParticle != null) windParticle.Stop();
        OnWindEnd?.Invoke();
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
        Vector3 dir;

        if (windMode == WindMode.Push)
        {
            dir = transform.forward;
        }
        else
        {
            dir = transform.position - targetPos;
            if (!applyVerticalForce) dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = -transform.forward;
        }

        if (!applyVerticalForce) dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return Vector3.zero;

        return dir.normalized;
    }

    float GetCurrentForce()
    {
        if (baseForce <= 0f) return 0f;

        float elapsed = Time.time - _scheduleStartTime;
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

        if (rb != null && !_targetsInZone.Contains(rb))
            _targetsInZone.Add(rb);
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
        _windActive = false;
        _targetsInZone.Clear();
        if (windParticle != null) windParticle.Stop();
    }
}
