using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
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

    [Tooltip("인스턴스화 시 마커 localScale. 낙하체 크기에 맞춰 DropTrap마다 조절")]
    [SerializeField] private Vector3 warnMarkerScale = Vector3.one;

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

    [Header("사운드")]
    [Tooltip("경고 마커 표시 시 재생할 SFX. None이면 무음.")]
    [SerializeField] private SFXId warnSfxId = SFXId.None;

    [Tooltip("낙하체 생성(낙하 시작) 시 재생할 SFX. None이면 무음.")]
    [SerializeField] private SFXId fireSfxId = SFXId.None;

    float _scheduleStartTime;
    float _phaseSpeedMultiplier = 1f;
    int   _targetIndex;

    // warnDuration + 낙하 채움 애니메이션 동안 Deactivate 시 고아 오브젝트가 남지 않도록 추적
    readonly List<GameObject> _pendingObjects = new List<GameObject>();

    // ── 경고 마커 네트워크 동기화 (stable ID 레지스트리, Breakable과 동일 패턴) ──
    // ID는 씬 로드마다 0부터 순서대로 부여. Host/Client 모두 씬 로드 시 동일 순서로
    // Awake가 실행되므로 ID가 일치. StageNetworkState.SyncDropWarnClientRpc에서 사용.
    static readonly Dictionary<int, DropTrap> _registry = new Dictionary<int, DropTrap>();
    static int _nextId = 0;
    int _netIndex = -1;

    /// <summary>
    /// PhaseManager가 Phase 전환 시 호출.
    /// 이 배율이 baseDropSpeed × timeSpeedMultiplier 에 추가로 곱해짐.
    /// 1.0 = 기본 속도, 2.0 = 2배 빠르게
    /// </summary>
    public void SetPhaseSpeedMultiplier(float mult) => _phaseSpeedMultiplier = mult;

    protected override void Awake()
    {
        base.Awake();

        // 씬 리로드 시 첫 DropTrap이 Awake되는 시점에 레지스트리 초기화 (stale 항목 방지)
        if (_registry.Count == 0) _nextId = 0;

        _netIndex = _nextId++;
        _registry[_netIndex] = this;
    }

    void OnDestroy()
    {
        _registry.Remove(_netIndex);
    }

    /// <summary>StageNetworkState.SyncDropWarnClientRpc 수신 시 Client에서 호출. 마커 연출만 재생(낙하체 스폰 없음).</summary>
    public static void PlayWarnById(int id, Vector3 targetPos, float warnDuration, float startY, float speed, Vector3 markerScale)
    {
        if (_registry.TryGetValue(id, out DropTrap t))
            t?.ApplyWarnFromNetwork(targetPos, warnDuration, startY, speed, markerScale);
    }

    void ApplyWarnFromNetwork(Vector3 targetPos, float warnDuration, float startY, float speed, Vector3 markerScale)
    {
        StartCoroutine(WarnMarkerRoutine(targetPos, warnDuration, startY, speed, markerScale));
    }

    protected override System.Collections.IEnumerator TrapLoop()
    {
        var nm = NetworkManager.Singleton;

        // ── 스케줄 기준 시각 결정 ─────────────────────────────────────────
        // ArrowTrap과 동일한 이유로 PhaseStartServerTime(Host가 이 Phase 진입 직전에 기록한
        // 절대 ServerTime) 을 앵커로 사용. StageStartServerTime(StageStartGate 전용 1회성
        // 신호)과는 별개 슬롯. PhaseManager.EnterPhase()가 Phase마다 다시 MarkPhaseStart()를
        // 찍으므로 앞 Phase가 길어져도 과거로 밀리는 문제는 없다.
        // StageNetworkState가 없는 씬에서는 로컬 Activate() 시각으로 폴백.
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
                if (ScheduleTimeUtil.IsPastEvent(targetTime, nm)) continue;
                float now        = nm != null ? (float)nm.ServerTime.Time : Time.time;
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

    float GetCurrentSpeed()
    {
        if (baseDropSpeed <= 0f) return 0f;

        var   nm      = NetworkManager.Singleton;
        float now     = nm != null ? (float)nm.ServerTime.Time : Time.time;
        float elapsed = now - _scheduleStartTime;
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

        // 온라인: Host만 낙하체 스폰. Client는 NetworkObject 수신으로 자동 생성.
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

        StartCoroutine(DropCycle(GetNextTargetPos()));
    }

    /// <summary>보스 등 외부에서 직접 낙하 위치를 지정해 호출</summary>
    public void FireAt(Vector3 targetPos)
    {
        if (dropPrefab == null) return;

        // 온라인: Host만 낙하체 스폰 (OnTrapTrigger와 동일 가드).
        // TrapPlayerTracker.DropLoop()가 Host/Client 양쪽에서 로컬로 돌기 때문에
        // 여기서 막지 않으면 Client가 NetworkObject.Spawn()을 호출해 NotServerException 발생.
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

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
        float speed = GetCurrentSpeed();

        // Fill 기준: 월드 Y=0. 스폰 Y에서 0까지 등속 낙하하는 동안 0→1.
        // (예전 spawnHeight/speed는 targetPos.y≠0이면 착지와 어긋남)
        float startY = spawnPos.y;

        // 바닥 경고 마커 — Host 로컬 표시 + 전 Client에 동일 연출 브로드캐스트.
        if (warnPrefab != null)
        {
            StartCoroutine(WarnMarkerRoutine(targetPos, warnDuration, startY, speed, warnMarkerScale));
            StageNetworkState.Instance?.SyncDropWarnClientRpc(
                _netIndex, targetPos, warnDuration, startY, speed, warnMarkerScale);
        }

        if (warnDuration > 0f)
            yield return new WaitForSeconds(warnDuration);

        if (fireSfxId != SFXId.None)
            SFXManager.Instance?.Play(fireSfxId, spawnPos);

        GameObject drop = Instantiate(dropPrefab, spawnPos, Quaternion.LookRotation(Vector3.down));

        TrapProjectile proj = drop.GetComponent<TrapProjectile>();
        if (proj == null) yield break;

        proj.moveDirection = Vector3.down;
        if (damage > 0) proj.damage = damage;

        Vector3 velocity = speed > 0f ? Vector3.down * speed : Vector3.zero;
        if (speed > 0f)
        {
            Rigidbody dropRb = drop.GetComponent<Rigidbody>();
            if (dropRb != null) dropRb.linearVelocity = velocity;
        }

        // 온라인(B안): Spawn 후 Client에 velocity 전파.
        var nm2 = NetworkManager.Singleton;
        if (nm2 != null && nm2.IsListening)
        {
            NetworkObject netObj = drop.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn(destroyWithScene: true);
                if (speed > 0f)
                    proj.InitializeVelocityClientRpc(velocity);
            }
        }
    }

    /// <summary>
    /// 경고 마커 표시 → warnDuration 유지 → 월드 Y startY→0 채움 → 파괴.
    /// Host 로컬 호출(DropCycle)과 Client RPC 수신(ApplyWarnFromNetwork) 양쪽에서 공용.
    /// </summary>
    IEnumerator WarnMarkerRoutine(Vector3 targetPos, float warnDuration, float startY, float speed, Vector3 markerScale)
    {
        if (warnPrefab == null) yield break;

        // 마커는 Y=1에 깔린다 (Y=0은 바닥과 겹쳐 안 보임).
        Vector3 markerPos = new Vector3(targetPos.x, 1f, targetPos.z);
        GameObject warn = Instantiate(warnPrefab, markerPos, Quaternion.identity);
        warn.transform.localScale = markerScale;
        _pendingObjects.Add(warn);
        if (warnSfxId != SFXId.None)
            SFXManager.Instance?.Play(warnSfxId, markerPos);

        DropWarnMarker marker = warn.GetComponent<DropWarnMarker>();

        if (warnDuration > 0f)
            yield return new WaitForSeconds(warnDuration);

        if (marker != null)
            yield return StartCoroutine(marker.FillUntilWorldY(startY, speed, groundY: 0f));
        else if (speed > 0f && startY > 0f)
            yield return new WaitForSeconds(startY / speed);

        DestroyAndUntrack(warn);
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
    protected override void OnDisable()
    {
        base.OnDisable();
        ClearPendingObjects();
    }
}
