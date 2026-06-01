using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스케줄 모드
/// - FixedFireTimes: fireAtSeconds + loopSchedule/schedulePeriod
/// - RandomInterval: (randomIntervalMin ~ randomIntervalMax)초마다 한 번씩 낙하
/// </summary>
public enum DropTrapScheduleMode
{
    FixedFireTimes,
    RandomInterval,
}

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

    [Header("경고 — 바닥 마커")]
    [Tooltip("경고 마커 프리팹. 낙하 위치 바닥에 warnDuration만큼 표시. 없으면 생략")]
    [SerializeField] private GameObject warnPrefab = null;

    [Tooltip("경고 표시 시간 (초). 플레이어가 피할 여유 시간")]
    [SerializeField] private float warnDuration = 0f;

    [Header("경고 — 천장 물방울 형성")]
    [Tooltip("낙하 전 천장(spawnHeight 위치)에 표시할 형성 프리팹.\n" +
             "DropletForming 컴포넌트를 붙여 크기가 서서히 자라게 할 것.")]
    [SerializeField] private GameObject ceilingFormPrefab = null;

    [Header("낙하")]
    [Tooltip("낙하체가 생성될 높이 (타겟 위치 기준 Y 오프셋, m)")]
    [SerializeField] private float spawnHeight = 0f;

    [Tooltip("낙하체 데미지. 0이면 프리팹 기본값 사용")]
    [SerializeField] private int damage = 0;

    [Header("발사 스케줄")]
    [Tooltip("FixedFireTimes: fireAtSeconds 사용. RandomInterval: 매번 랜덤 대기 후 낙하")]
    [SerializeField] private DropTrapScheduleMode scheduleMode = DropTrapScheduleMode.FixedFireTimes;

    [Tooltip("RandomInterval: 낙하 사이 대기 최소(초). max보다 크면 자동 맞춤")]
    [SerializeField] private float randomIntervalMin = 0f;

    [Tooltip("RandomInterval: 낙하 사이 대기 최대(초). min과 같으면 고정 간격")]
    [SerializeField] private float randomIntervalMax = 0f;

    [Header("발사 스케줄 — 고정 시각 (FixedFireTimes)")]
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

    // warnDuration 대기 중 Deactivate 시 고아 오브젝트가 남지 않도록 추적
    readonly List<GameObject> _pendingObjects = new List<GameObject>();

    /// <summary>
    /// PhaseManager가 Phase 전환 시 호출.
    /// 이 배율이 baseDropSpeed × timeSpeedMultiplier 에 추가로 곱해짐.
    /// 1.0 = 기본 속도, 2.0 = 2배 빠르게
    /// </summary>
    public void SetPhaseSpeedMultiplier(float mult) => _phaseSpeedMultiplier = mult;

    protected override System.Collections.IEnumerator TrapLoop()
    {
        _scheduleStartTime = Time.time;

        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        if (scheduleMode == DropTrapScheduleMode.RandomInterval)
        {
            if (dropPrefab == null)
            {
                isRunning = false;
                yield break;
            }

            while (isRunning)
            {
                float lo = Mathf.Min(randomIntervalMin, randomIntervalMax);
                float hi = Mathf.Max(randomIntervalMin, randomIntervalMax);
                float wait = lo < hi ? Random.Range(lo, hi) : lo;
                if (wait > 0f)
                    yield return new WaitForSeconds(wait);

                if (!isRunning) yield break;

                yield return StartCoroutine(FireWithCharge());
            }

            isRunning = false;
            yield break;
        }

        if (fireAtSeconds == null || fireAtSeconds.Length == 0)
        {
            isRunning = false;
            yield break;
        }

        float cycleOffset = 0f;

        while (isRunning)
        {
            foreach (float t in fireAtSeconds)
            {
                if (!isRunning) yield break;

                float targetTime = _scheduleStartTime + cycleOffset + t;
                float waitTime   = Mathf.Max(0f, targetTime - Time.time - preFireChargeTime);
                yield return new WaitForSeconds(waitTime);

                if (!isRunning) yield break;

                yield return StartCoroutine(FireWithCharge());
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
        Vector3 spawnPos = targetPos + Vector3.up * spawnHeight;

        // 천장에 물방울 형성 프리팹 스폰 (아래를 향하도록 회전)
        GameObject ceilingForm = null;
        if (ceilingFormPrefab != null)
        {
            ceilingForm = Instantiate(ceilingFormPrefab, spawnPos, Quaternion.LookRotation(Vector3.down));
            _pendingObjects.Add(ceilingForm);
            DropletForming forming = ceilingForm.GetComponent<DropletForming>();
            if (forming != null)
                forming.Initialize(warnDuration);
        }

        // 바닥 경고 마커
        GameObject warn = null;
        if (warnPrefab != null)
        {
            warn = Instantiate(warnPrefab, targetPos, Quaternion.identity);
            _pendingObjects.Add(warn);
        }

        if (warnDuration > 0f)
            yield return new WaitForSeconds(warnDuration);

        DestroyAndUntrack(warn);
        DestroyAndUntrack(ceilingForm);

        GameObject drop = Instantiate(dropPrefab, spawnPos, Quaternion.LookRotation(Vector3.down));

        TrapProjectile proj = drop.GetComponent<TrapProjectile>();
        if (proj == null) yield break;

        proj.moveDirection = Vector3.down;
        if (damage > 0) proj.damage = damage;

        float speed = GetCurrentSpeed();
        if (speed > 0f)
        {
            Rigidbody dropRb = drop.GetComponent<Rigidbody>();
            if (dropRb != null)
                dropRb.linearVelocity = Vector3.down * speed;
        }
    }

    void DestroyAndUntrack(GameObject obj)
    {
        if (obj == null) return;
        _pendingObjects.Remove(obj);
        Destroy(obj);
    }

    void ClearPendingObjects()
    {
        for (int i = _pendingObjects.Count - 1; i >= 0; i--)
        {
            if (_pendingObjects[i] != null)
                Destroy(_pendingObjects[i]);
        }
        _pendingObjects.Clear();
    }

    // Deactivate() 경로: StopAllCoroutines() → OnDeactivated()
    protected override void OnDeactivated()
    {
        ClearPendingObjects();
        _targetIndex = 0;
    }

    // SetActive(false) 경로: OnDisable()만 불리고 OnDeactivated()는 안 불림
    // → 여기서도 반드시 청소해야 ceilingForm이 씬에 잔존하지 않음
    protected override void OnDisable()
    {
        base.OnDisable();
        ClearPendingObjects();
    }
}
