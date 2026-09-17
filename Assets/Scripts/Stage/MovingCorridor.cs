using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 이동하는 복도 컴포넌트.
///
/// [동작]
///  뒤에서 밀려오는 벽(backWall)과 앞에서 이동하는 벽(frontWall)이
///  같은 방향·속도로 이동하며 두 벽 간 거리를 유지.
///  플레이어는 두 벽 사이를 달려서 빠져나가야 함.
///
///  뒤 벽: Rigidbody로 밀어오므로 플레이어를 실제로 밀어냄 (물리 충돌)
///  앞 벽: 플레이어가 앞으로 도망갈 한계선 역할 (선택)
///
/// [트리거 활성화]
///  activateOnPlayerTrigger = true 시:
///  이 GameObject에 BoxCollider(Is Trigger = true)를 추가하면
///  플레이어 진입 시 자동으로 복도가 시작됨. 트리거 판정은 Host만 한다(Client는 시작 시각 수신으로 따라감).
///
/// [네트워크 — 틱 결정론 시뮬, 2026-09-18 Steam 실기 버그 수정]
///  예전엔 머신마다 Activate() 이후 "현재 위치 + 속도×dt"를 로컬로 누적했고, 속도 변경 시각도
///  "변경을 감지한 시각 + 간격"으로 잡았다. 그래서 ① Client의 늦은 시작, ② 프레임 단위로 늦게 감지된
///  속도 변경이 다음 스케줄에 누적, ③ Maximum Allowed Timestep을 넘는 히치로 버려진 스텝이 전부
///  영구 오차가 돼 막바지에 Host/Client 벽 위치가 수 m 갈라졌다.
///  지금은:
///   · Host가 Activate 순간의 ServerTime을 StageNetworkState.CorridorStartServerTime에 확정.
///   · 전 머신이 그 시각부터 "몇 번째 고정 틱인가"로 같은 시뮬(속도 변경도 틱 번호로 예약)을 돌린다.
///   · 평소엔 FixedUpdate 1회 = 1틱, 서버 시각이 가리키는 틱과 DriftToleranceTicks 넘게 벌어지면
///     따라잡거나(최대 MaxExtraTicksPerStep) 한 스텝 쉰다 → 오차가 쌓이지 않는다.
///   · 위치는 Rigidbody에서 다시 읽지 않고 내부 시뮬 값만 쓴다.
///  남는 오차는 Client ServerTime 추정 오차 + 허용 틱 수만큼(속도 × 수십 ms)이며 시간이 지나도 늘지 않는다.
///
/// [필수 컴포넌트 — 각 벽 오브젝트에]
///  Rigidbody: Is Kinematic = true, Interpolate = Interpolate
///  Collider:  Is Trigger = false
/// </summary>
public class MovingCorridor : MonoBehaviour
{
    [Serializable]
    public class RandomWallSpeedSettings
    {
        [Tooltip("랜덤 속도 전환 사용 여부")]
        public bool enabled = false;

        [Tooltip("속도 변경 최소 간격(초)")]
        public float minInterval = 0f;

        [Tooltip("속도 변경 최대 간격(초)")]
        public float maxInterval = 0f;

        [Tooltip("이 속도(m/s) 값들 중 하나를 매 간격마다 랜덤 선택. 예: 1, 3, 5 입력 시 그 셋 중에서만 뽑힘.\n" +
                 "baseSpeed를 대체함 — 이 배열이 비어 있으면 baseSpeed 그대로 사용.")]
        public float[] discreteSpeeds = new float[0];
    }

    [Header("벽 참조")]
    [Tooltip("뒤에서 플레이어를 쫓아오는 벽 (Rigidbody 필수)")]
    public Rigidbody backWall;

    [Tooltip("앞에서 이동하는 벽 — 없으면 뒤 벽만 이동 (선택)")]
    public Rigidbody frontWall;

    [Header("이동 설정")]
    [Tooltip("복도가 이동하는 방향 (월드 기준). 예: (0,0,1) = 앞쪽")]
    public Vector3 moveDirection = Vector3.zero;

    [Tooltip("기본 이동 속도 (m/s)")]
    public float baseSpeed = 0f;

    [Header("랜덤 속도 (벽별 개별 적용)")]
    [Tooltip("뒤 벽 랜덤 속도 규칙")]
    public RandomWallSpeedSettings backRandomSpeed = new RandomWallSpeedSettings();

    [Tooltip("앞 벽 랜덤 속도 규칙")]
    public RandomWallSpeedSettings frontRandomSpeed = new RandomWallSpeedSettings();

    [Tooltip("true면 고정 시드를 사용해 재시도 시 동일 패턴을 재현")]
    public bool useFixedRandomSeed = false;

    [Tooltip("고정 시드 값 (useFixedRandomSeed=true 일 때 사용)")]
    public int randomSeed = 0;

    [Header("벽 간격 안전 규칙")]
    [Tooltip("앞/뒤 벽 최소 거리(이동축 기준). 0이면 비활성화")]
    public float minWallDistance = 0f;

    [Tooltip("앞/뒤 벽 최대 거리(이동축 기준). 0이면 비활성화")]
    public float maxWallDistance = 0f;

    [Header("활성화")]
    [Tooltip("씬 시작 시 자동 활성화 여부. activateOnPlayerTrigger와 함께 사용 불가 (둘 중 하나만)")]
    public bool startActive = false;

    [Tooltip("true: 플레이어가 이 GameObject의 Trigger Collider에 진입하면 자동 시작\n" +
             "→ 이 GameObject에 BoxCollider(Is Trigger = true) 추가 필요")]
    public bool activateOnPlayerTrigger = false;

    [Tooltip("true: 한 번만 트리거 허용. false: 플레이어 재진입 시 재활성화")]
    public bool activateOnce = true;

    [Header("이벤트")]
    [Tooltip("복도 활성화 시 호출")]
    public UnityEvent OnActivated;

    [Tooltip("복도 비활성화 시 호출")]
    public UnityEvent OnDeactivated;

    // 서버 시각이 가리키는 틱과 이만큼 넘게 벌어져야 보정한다. 프레임 단위로만 갱신되는 ServerTime 때문에
    // 한 프레임 안의 여러 FixedUpdate가 ±1~2틱 흔들리는 것을 보정으로 오인하지 않기 위한 여유.
    const int DriftToleranceTicks = 2;
    // 한 FixedUpdate에서 추가로 따라잡을 최대 틱 수 — 늦은 시작·히치를 몇 스텝에 나눠 따라잡아 순간이동을 줄인다.
    const int MaxExtraTicksPerStep = 4;
    // Client가 이보다 오래된 시작 시각은 받아들이지 않는다(재활성화 시 낡은 값 재사용 방지).
    const double AnchorMaxAgeSec = 10.0;

    // 다른 파일의 salt: 0x050AD5E7, 0x43484153, 0x5716D000, 0x4D4F5554, 0x5B1DE000, 0x52554E52, 0x434F4C57(ColorWall), 0x574C525A(WallLineRandomizer), 0x53504852(BossSpherePhaseDriver)
    const int SeedSalt = 0x4D43_0001;

    bool _running;
    bool _hasTriggered;

    // ── 결정론 시뮬 상태 (전 머신 동일) ──
    double _anchorServerTime;
    double _consumedAnchor = -1.0;
    float  _tickDt;
    long   _simTick;
    bool   _simInitialized;
    Vector3 _simBack;
    Vector3 _simFront;

    float _backRandomSpeed;
    float _frontRandomSpeed;
    long  _nextBackChangeTick;
    long  _nextFrontChangeTick;
    System.Random _rng;

    void Start()
    {
        if (moveDirection == Vector3.zero)
            moveDirection = Vector3.forward;

        if (startActive) Activate();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!activateOnPlayerTrigger) return;
        if (IsClientOnly()) return; // Client는 Host가 확정한 시작 시각으로 따라간다
        if (activateOnce && _hasTriggered) return;

        Player player = other.GetComponent<Player>();
        if (player == null || player.IsDead) return;

        _hasTriggered = true;
        Activate();
    }

    void FixedUpdate()
    {
        if (!_running)
        {
            if (IsClientOnly()) TryStartFromHostAnchor();
            if (!_running) return;
        }

        long targetTick = (long)Math.Floor((NetTime() - _anchorServerTime) / _tickDt);
        long diff = targetTick - _simTick;

        int steps;
        if (diff > DriftToleranceTicks)       steps = 1 + (int)Math.Min(diff - 1, MaxExtraTicksPerStep);
        else if (diff < -DriftToleranceTicks) steps = 0; // 앞서 있음 — 서버 시각이 따라올 때까지 한 스텝 쉼
        else                                  steps = 1;

        for (int i = 0; i < steps; i++)
            StepSimulation();

        if (backWall != null)  backWall.MovePosition(_simBack);
        if (frontWall != null) frontWall.MovePosition(_simFront);
    }

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>
    /// 복도 이동 시작. Host는 지금 서버 시각을 시작 시각으로 확정해 배포하고,
    /// Client는 호출돼도 직접 시작하지 않는다 — Host 시작 시각을 받으면 FixedUpdate에서 시작(늦으면 따라잡음).
    /// </summary>
    public void Activate()
    {
        if (_running) return;
        if (IsClientOnly()) return;

        double anchor = NetTime();
        StageNetworkState.Instance?.MarkCorridorStart(anchor);
        StartSimulation(anchor);
    }

    /// <summary>복도 이동 중단 (현 위치 정지). 전 머신 로컬 호출 — 클리어 시 SceneFlowManager가 부른다.</summary>
    public void Deactivate()
    {
        if (!_running) return;
        _running = false;
        OnDeactivated?.Invoke();
    }

    // ── 내부 ────────────────────────────────────────────────────

    static bool IsClientOnly()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && !nm.IsServer;
    }

    /// <summary>WallMover / WallWaveController와 같은 시간 소스(ServerTime).</summary>
    static double NetTime()
    {
        var nm = NetworkManager.Singleton;
        return nm != null ? nm.ServerTime.Time : Time.timeAsDouble;
    }

    void TryStartFromHostAnchor()
    {
        var sns = StageNetworkState.Instance;
        if (sns == null) return;

        double anchor = sns.CorridorStartServerTime;
        if (anchor <= 0d || anchor.Equals(_consumedAnchor)) return;
        if (NetTime() - anchor > AnchorMaxAgeSec) return;

        StartSimulation(anchor);
    }

    void StartSimulation(double anchor)
    {
        _anchorServerTime = anchor;
        _consumedAnchor   = anchor;
        _tickDt           = Time.fixedDeltaTime;
        _simTick          = 0;

        // 최초 시작만 씬 배치 위치에서 출발. 재활성화는 멈춘 자리에서 이어간다(전 머신 동일).
        // 뒷벽은 시작 직전까지 비활성이라 Rigidbody 대신 Transform 위치를 읽는다.
        if (!_simInitialized)
        {
            _simBack  = backWall  != null ? backWall.transform.position  : Vector3.zero;
            _simFront = frontWall != null ? frontWall.transform.position : Vector3.zero;
            _simInitialized = true;
        }

        InitializeRandomRuntime();
        _running = true;
        OnActivated?.Invoke();
    }

    void InitializeRandomRuntime()
    {
        // Environment.TickCount는 머신마다 값이 달라 Host/Client가 다른 랜덤 시퀀스를 뽑는 원인이었음.
        // StagePressurePadSetup.ApplySeedAndColors()와 동일한 "Seed ^ salt" 관례로 결정론적 시드 사용.
        int seed = useFixedRandomSeed ? randomSeed : (NetworkSessionData.Seed ^ SeedSalt);
        _rng = new System.Random(seed);

        _backRandomSpeed     = 0f;
        _frontRandomSpeed    = 0f;
        _nextBackChangeTick  = 0;
        _nextFrontChangeTick = 0;
    }

    /// <summary>고정 틱 1회 진행. 입력은 틱 번호뿐이라 전 머신이 같은 순서로 같은 값을 얻는다.</summary>
    void StepSimulation()
    {
        UpdateRandomSpeed(ref _backRandomSpeed,  ref _nextBackChangeTick,  backRandomSpeed);
        UpdateRandomSpeed(ref _frontRandomSpeed, ref _nextFrontChangeTick, frontRandomSpeed);

        Vector3 direction = moveDirection.normalized;
        float backSpeed  = HasDiscreteSpeeds(backRandomSpeed)  ? _backRandomSpeed  : baseSpeed;
        float frontSpeed = HasDiscreteSpeeds(frontRandomSpeed) ? _frontRandomSpeed : baseSpeed;

        Vector3 backNext  = backWall  != null ? _simBack  + direction * (backSpeed  * _tickDt) : _simBack;
        Vector3 frontNext = frontWall != null ? _simFront + direction * (frontSpeed * _tickDt) : _simFront;

        EnforceWallDistanceLimits(direction, ref backNext, ref frontNext);

        _simBack  = backNext;
        _simFront = frontNext;
        _simTick++;
    }

    static bool HasDiscreteSpeeds(RandomWallSpeedSettings settings) =>
        settings.enabled && settings.discreteSpeeds != null && settings.discreteSpeeds.Length > 0;

    /// <summary>
    /// 속도 변경을 틱 번호로 예약한다. 다음 변경 틱 = "이번 변경이 예약됐던 틱" + 간격 —
    /// 감지 시각 기준으로 잡으면 감지 지연이 매번 스케줄에 누적된다(수정 전 버그).
    /// </summary>
    void UpdateRandomSpeed(ref float currentSpeed, ref long nextChangeTick, RandomWallSpeedSettings settings)
    {
        if (!HasDiscreteSpeeds(settings)) return;

        float minInterval = Mathf.Max(settings.minInterval, 0f);
        float maxInterval = Mathf.Max(settings.maxInterval, minInterval);
        if (Mathf.Approximately(maxInterval, 0f)) return;

        if (_simTick < nextChangeTick) return;

        currentSpeed = settings.discreteSpeeds[_rng.Next(settings.discreteSpeeds.Length)];
        long intervalTicks = Math.Max(1L, (long)Math.Round(RandomRange(minInterval, maxInterval) / _tickDt));
        nextChangeTick += intervalTicks;
    }

    float RandomRange(float min, float max)
    {
        if (Mathf.Approximately(min, max)) return min;
        return (float)(min + (max - min) * _rng.NextDouble());
    }

    void EnforceWallDistanceLimits(Vector3 direction, ref Vector3 backNextPos, ref Vector3 frontNextPos)
    {
        if (backWall == null || frontWall == null) return;

        float signedDistance = Vector3.Dot(frontNextPos - backNextPos, direction);

        if (minWallDistance > 0f && signedDistance < minWallDistance)
        {
            frontNextPos = backNextPos + direction * minWallDistance;
            return;
        }

        if (maxWallDistance > 0f && signedDistance > maxWallDistance)
            frontNextPos = backNextPos + direction * maxWallDistance;
    }

    // ── 에디터 지원 ──────────────────────────────────────────────

    [ContextMenu("테스트: 활성화")]
    void Debug_Activate() => Activate();

    [ContextMenu("테스트: 비활성화")]
    void Debug_Deactivate() => Deactivate();

    void OnDrawGizmos()
    {
        if (backWall == null) return;

        Vector3 dir = (moveDirection == Vector3.zero ? Vector3.forward : moveDirection).normalized;

        // 이동 방향 화살표
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        Vector3 from = backWall.transform.position;
        Vector3 to   = from + dir * 3f;
        Gizmos.DrawLine(from, to);
        Gizmos.DrawSphere(to, 0.15f);

        // frontWall 연결선
        if (frontWall != null)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
            Gizmos.DrawLine(backWall.transform.position, frontWall.transform.position);
        }
    }
}
