using System.Collections;
using UnityEngine;

/// <summary>
/// 발사형 함정 범용 컴포넌트 (화살, 돌굴림 등).
/// fireAtSeconds에 지정한 초(스케줄 시작 기준)에 프리팹을 발사.
/// loopSchedule=true이면 schedulePeriod마다 패턴을 반복.
/// speedPhases로 시간 경과에 따른 속도 단계 상승을 지원.
///
/// [Boulder(돌굴림) 사용 시]
/// arrowPrefab에 SpinRoller 컴포넌트가 있으면 speed를 SpinRoller.initialSpeed에도 자동 적용.
/// TrapProjectile.type=Boulder, SpinRoller 부착 프리팹을 연결하면 BoulderTrap과 동일하게 동작.
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
        scheduleStartTime = Time.time;

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

                float targetTime = scheduleStartTime + cycleOffset + t;
                float waitTime = targetTime - Time.time;

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

    // 경과 시간 기준으로 현재 적용할 속도를 계산
    float GetCurrentSpeed()
    {
        // baseSpeed가 0이면 프리팹 기본값을 그대로 사용
        if (baseSpeed <= 0f) return 0f;

        float elapsed = Time.time - scheduleStartTime;
        float mult = 1f;

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

        Transform spawn   = firePoint != null ? firePoint : transform;
        Vector3   flatFwd = spawn.forward;

        // Boulder 타입은 Y를 제거해 수평 직진
        TrapProjectile sampleProj = arrowPrefab.GetComponent<TrapProjectile>();
        bool isBoulder = sampleProj != null &&
                         sampleProj.type == TrapProjectile.ProjectileType.Boulder;
        if (isBoulder)
        {
            flatFwd.y = 0f;
            if (flatFwd.sqrMagnitude < 0.001f) flatFwd = Vector3.forward;
            flatFwd.Normalize();
        }

        Quaternion spawnRot = isBoulder ? Quaternion.LookRotation(flatFwd) : spawn.rotation;
        GameObject fired    = Instantiate(arrowPrefab, spawn.position, spawnRot);

        TrapProjectile proj = fired.GetComponent<TrapProjectile>();
        if (proj == null) return;

        proj.moveDirection = flatFwd;

        float speed = GetCurrentSpeed();
        if (speed > 0f)
        {
            proj.speed = speed;

            // SpinRoller가 있으면 initialSpeed도 함께 설정 (Boulder 굴림 속도 제어)
            SpinRoller roller = fired.GetComponent<SpinRoller>();
            if (roller != null)
                roller.initialSpeed = speed;
        }
    }
}
