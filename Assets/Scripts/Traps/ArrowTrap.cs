using System.Collections;
using UnityEngine;

[System.Serializable]
public class SpeedPhase
{
    [Tooltip("스테이지 시작 후 이 초가 지나면 이 배율 적용 (오름차순으로 입력)")]
    public float afterSeconds = 0f;

    [Tooltip("baseSpeed 대비 배율. 예: 1.5 = 50% 빠르게")]
    public float speedMultiplier = 1f;
}

/// <summary>
/// 벽에 부착하는 화살 함정.
/// fireAtSeconds에 지정한 초(스케줄 시작 기준)에 화살을 발사.
/// loopSchedule=true이면 schedulePeriod마다 패턴을 반복.
/// speedPhases로 시간 경과에 따른 속도 단계 상승을 지원.
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

        return baseSpeed * mult;
    }

    protected override void OnTrapTrigger()
    {
        if (arrowPrefab == null) return;

        Transform spawn = firePoint != null ? firePoint : transform;
        GameObject arrow = Instantiate(arrowPrefab, spawn.position, spawn.rotation);

        TrapProjectile proj = arrow.GetComponent<TrapProjectile>();
        if (proj == null) return;

        proj.moveDirection = spawn.forward;

        float speed = GetCurrentSpeed();
        if (speed > 0f)
            proj.speed = speed;
    }
}
