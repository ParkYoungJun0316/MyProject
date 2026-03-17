using System.Collections;
using UnityEngine;

/// <summary>
/// 공중 낙하 공격 함정.
/// fireAtSeconds에 지정한 초(스케줄 시작 기준)에 낙하체를 생성.
/// loopSchedule=true이면 schedulePeriod마다 패턴을 반복.
/// speedPhases로 시간 경과에 따른 속도 단계 상승을 지원.
/// SetPhaseSpeedMultiplier()로 Phase별 속도 배율을 외부에서 적용.
///
/// [낙하체 프리팹 권장 설정]
/// - TrapProjectile: type=Arrow, destroyOnFloor=true, destroyOnWall=false
/// - Rigidbody: Use Gravity=false (TrapProjectile이 속도 제어)
/// - Collider (Trigger)
/// </summary>
public class DropTrap : TrapBase
{
    [Header("Drop Trap")]
    [Tooltip("낙하할 물체 프리팹 (TrapProjectile 컴포넌트 필수)")]
    [SerializeField] private GameObject dropPrefab = null;

    [Tooltip("낙하 목표 지점 목록. 비어 있으면 이 GameObject 위치를 사용")]
    [SerializeField] private Transform[] targetPoints = new Transform[0];

    [Tooltip("타겟 랜덤 선택 여부. false이면 targetPoints 순서대로 순환")]
    [SerializeField] private bool randomTarget = true;

    [Header("경고")]
    [Tooltip("경고 마커 프리팹. 낙하 위치 바닥에 warnDuration만큼 표시. 없으면 생략")]
    [SerializeField] private GameObject warnPrefab = null;

    [Tooltip("경고 표시 시간 (초). 플레이어가 피할 여유 시간")]
    [SerializeField] private float warnDuration = 0f;

    [Header("낙하")]
    [Tooltip("낙하체가 생성될 높이 (타겟 위치 기준 Y 오프셋, m)")]
    [SerializeField] private float spawnHeight = 0f;

    [Tooltip("낙하체 데미지. 0이면 프리팹 기본값 사용")]
    [SerializeField] private int damage = 0;

    [Header("발사 스케줄 (초 단위)")]
    [Tooltip("낙하할 시각 목록 (스케줄 시작 기준, 초). 예: [0.5, 1.2, 2.0]")]
    [SerializeField] private float[] fireAtSeconds = new float[0];

    [Tooltip("스케줄 반복 여부")]
    [SerializeField] private bool loopSchedule = false;

    [Tooltip("반복 시 한 사이클 길이 (초). loopSchedule=true일 때만 사용")]
    [SerializeField] private float schedulePeriod = 3f;

    [Header("낙하 속도")]
    [Tooltip("기본 낙하 속도 (m/s). 0이면 프리팹 기본값 사용")]
    [SerializeField] private float baseDropSpeed = 0f;

    [Header("난이도 단계 (시간 경과 → 속도 배율 상승)")]
    [Tooltip("afterSeconds 이후 speedMultiplier 배율을 적용. afterSeconds 오름차순 입력")]
    [SerializeField] private SpeedPhase[] speedPhases = new SpeedPhase[0];

    float _scheduleStartTime;
    float _phaseSpeedMultiplier = 1f;
    int   _targetIndex;

    /// <summary>
    /// PhaseManager가 Phase 전환 시 호출.
    /// 이 배율이 baseDropSpeed × timeSpeedMultiplier 에 추가로 곱해짐.
    /// 1.0 = 기본 속도, 2.0 = 2배 빠르게
    /// </summary>
    public void SetPhaseSpeedMultiplier(float mult) => _phaseSpeedMultiplier = mult;

    protected override System.Collections.IEnumerator TrapLoop()
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

    float GetCurrentSpeed()
    {
        if (baseDropSpeed <= 0f) return 0f;

        float elapsed = Time.time - _scheduleStartTime;
        float mult    = 1f;

        foreach (SpeedPhase phase in speedPhases)
        {
            if (elapsed >= phase.afterSeconds)
                mult = phase.speedMultiplier;
        }

        return baseDropSpeed * mult * _phaseSpeedMultiplier;
    }

    protected override void OnTrapTrigger()
    {
        if (dropPrefab == null) return;
        StartCoroutine(DropCycle(GetNextTargetPos()));
    }

    /// <summary>보스 등 외부에서 직접 낙하 위치를 지정해 호출</summary>
    public void FireAt(Vector3 targetPos)
    {
        if (dropPrefab == null) return;
        StartCoroutine(DropCycle(targetPos));
    }

    Vector3 GetNextTargetPos()
    {
        if (targetPoints == null || targetPoints.Length == 0)
            return transform.position;

        if (randomTarget)
            return targetPoints[Random.Range(0, targetPoints.Length)].position;

        Vector3 pos = targetPoints[_targetIndex % targetPoints.Length].position;
        _targetIndex++;
        return pos;
    }

    IEnumerator DropCycle(Vector3 targetPos)
    {
        GameObject warn = null;
        if (warnPrefab != null)
            warn = Instantiate(warnPrefab, targetPos, Quaternion.identity);

        if (warnDuration > 0f)
            yield return new WaitForSeconds(warnDuration);

        if (warn != null)
            Destroy(warn);

        Vector3    spawnPos = targetPos + Vector3.up * spawnHeight;
        GameObject drop     = Instantiate(dropPrefab, spawnPos, Quaternion.LookRotation(Vector3.down));

        TrapProjectile proj = drop.GetComponent<TrapProjectile>();
        if (proj == null) yield break;

        proj.moveDirection = Vector3.down;

        float speed = GetCurrentSpeed();
        if (speed > 0f)  proj.speed  = speed;
        if (damage > 0)  proj.damage = damage;
    }
}
