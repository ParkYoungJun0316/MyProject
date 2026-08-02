using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ChallengeStepState가 어느 챌린지 매니저 것인지 식별하는 태그.
/// [버그 수정 2026-07-28] _challengeStep은 씬당 여러 챌린지 종류가 공유하는 슬롯인데(§11B.2),
/// "이 컴포넌트를 꺼라"(_currentPhase NV)와 "이번 라운드 데이터"(_challengeStep NV)가 서로 다른
/// NetworkVariable이라 Client 도착 순서가 NGO에서 보장되지 않는다. 순서가 뒤집히면 아직
/// SetActive(false) 안 된 이전 챌린지가 새 챌린지의 stepIndex를 자기 것으로 오인해 반응했다
/// (A 챌린지를 고치면 활성화 타이밍이 바뀌어 B가 깨지는 회귀의 실제 원인 — PhaseManager.EnterPhase()가
/// onPhaseEnter(챌린지 시작)→SyncPhase(오브젝트 on/off) 순서로 별도 NV 2개를 쓰기 때문).
/// 각 매니저가 이 태그로 "내 것이 아니면 무시"를 판단하면 활성화 타이밍이 완벽히 맞지 않아도 안전하다.
/// </summary>
public enum ChallengeOwnerType
{
    None,
    OX,
    ColorTile,
    GridColor,
    GridBW,
    SequenceRing,
    DirectionalBarrier,
}

/// <summary>
/// 챌린지 라운드 상태(시드·스텝 인덱스·스텝 시작 서버 시간·소유자)를 하나로 묶은 값.
/// [버그 수정 2026-07-20] 세 값을 별도 NetworkVariable로 나눠두면, Client 쪽에 도착하는
/// 순서가 보장되지 않아 "스텝 인덱스는 갱신됐지만 시드는 아직 이전 값"인 순간에
/// OnValueChanged가 발동해 Host와 다른 셔플 결과가 나올 수 있었다(Host/Client 문제 불일치).
/// 하나의 NetworkVariable로 합쳐 항상 원자적으로 같이 도착하게 만든다.
/// [버그 수정 2026-07-28] owner 추가 — 위 ChallengeOwnerType 참고.
/// </summary>
public struct ChallengeStepState : INetworkSerializable, IEquatable<ChallengeStepState>
{
    public int    seed;
    public int    stepIndex;
    public double stepStartServerTime;
    public ChallengeOwnerType owner;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref seed);
        serializer.SerializeValue(ref stepIndex);
        serializer.SerializeValue(ref stepStartServerTime);
        serializer.SerializeValue(ref owner);
    }

    public bool Equals(ChallengeStepState other) =>
        seed == other.seed &&
        stepIndex == other.stepIndex &&
        stepStartServerTime.Equals(other.stepStartServerTime) &&
        owner == other.owner;

    public override bool Equals(object obj) => obj is ChallengeStepState other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(seed, stepIndex, stepStartServerTime, owner);
}

/// <summary>
/// Floor 타일 롤 상태(시드 + 그 순간의 keepBWRatio)를 하나로 묶은 값.
/// [Floor 마이그레이션 — MStageNetworkBoard.md "Floor 마이그레이션 상세 설계"]
/// Floor는 성공/실패 판정이 없는 "무한 반복 Generate"라 stepIndex/시작시간이 불필요하다 —
/// 롤마다 seed 자체가 바뀌므로 OnValueChanged만으로 "새 롤이 왔다"는 신호가 충분하다
/// (중간 롤을 하나 놓쳐도 최종 상태로 스냅되니 무해 — 판정형 챌린지와 달리 스텝을 건너뛰면 안 되는
/// 제약이 없다). keepBWRatio를 같이 실어보내는 이유는 Client가 Phase 진행을 독자 계산하지
/// 않게 하기 위함(SequenceRing 시간 동기화에서 얻은 교훈과 동일).
/// </summary>
public struct FloorRollState : INetworkSerializable, IEquatable<FloorRollState>
{
    public int   seed;
    public float keepBWRatio;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref seed);
        serializer.SerializeValue(ref keepBWRatio);
    }

    public bool Equals(FloorRollState other) =>
        seed == other.seed &&
        keepBWRatio.Equals(other.keepBWRatio);

    public override bool Equals(object obj) => obj is FloorRollState other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(seed, keepBWRatio);
}

/// <summary>
/// 스테이지 시작 게이트 완료 신호(시간 + 게이트 식별자)를 하나로 묶은 값.
/// [버그 수정 2026-08 — 다중 게이트 씬 stale 재점화] serverTime만 단독 NV로 두면, 씬 하나에
/// 게이트가 여러 개(T.Stage2/4/5)일 때 앞 게이트가 찍은 값이 뒤 게이트에도 그대로 남아있어
/// 뒤 게이트가 Arm()되는 즉시 "이미 시작됨"으로 오인해 카운트다운·존 점유 없이 바로 시작해버렸다
/// (Host는 자기 AllZonesOccupied()를 실제로 기다리므로 Host/Client가 다른 타이밍을 봄).
/// gateId를 시간과 원자적으로 같이 실어보내면(ChallengeStepState와 동일 원칙), 각 게이트는
/// "내 gateId가 찍힌 신호"만 자기 것으로 인정하므로 다른 게이트의 낡은 신호를 착각할 수 없다.
/// </summary>
public struct StageStartSignal : INetworkSerializable, IEquatable<StageStartSignal>
{
    public double serverTime;
    public int    gateId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref serverTime);
        serializer.SerializeValue(ref gateId);
    }

    public bool Equals(StageStartSignal other) =>
        serverTime.Equals(other.serverTime) && gateId == other.gateId;

    public override bool Equals(object obj) => obj is StageStartSignal other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(serverTime, gateId);
}

/// <summary>
/// 스테이지 네트워크 상태 중앙 허브. NetworkBehaviour.
/// M.Stage1 / T.Stage1 씬 내 NetworkObject GameObject에 부착.
///
/// [역할]
/// - 플레이어 사망 신고 수신 → Host가 씬 리로드 (NetworkSceneManager)
/// - Phase 진행 상태 동기화 (CurrentPhase NetworkVariable)
///
/// [배치]
/// 각 스테이지 씬에 빈 GameObject → NetworkObject + StageNetworkState 추가.
///
/// [연결]
/// - StageResetOnPlayerDeath.DoReset() → NotifyPlayerDeathServerRpc()
/// - PhaseManager.EnterPhase() → MarkPhaseStart() + SyncPhase(index) (Host에서만 호출)
/// - StageStartGate.CompleteCountdown() → MarkStageStart(gateId) (PhaseManager와 별개 슬롯)
/// </summary>
public class StageNetworkState : NetworkBehaviour
{
    public static StageNetworkState Instance { get; private set; }

    // 현재 Phase 인덱스 (Host가 쓰고 전원이 읽음)
    private readonly NetworkVariable<int> _currentPhase = new(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // 카운트다운 시작 서버 시간 (Host 기록 — 카운트다운 UI 동기화)
    private readonly NetworkVariable<double> _countdownStartServerTime = new(
        -1.0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // 카운트다운 활성 여부
    private readonly NetworkVariable<bool> _isCountdownActive = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // 스테이지 시작 신호 (StartStage() 직전 Host 기록. 시간 + 어느 게이트인지를 원자적으로 묶음).
    // StageStartGate / MemoryPathIntroController 전용 — "이 게이트가 완료됐다"는 신호로 쓰인다.
    // 다른 시스템이 여기에 같이 쓰면 그 배타성이 깨지므로(예: 2026-07-21 PhaseManager 오공유 버그)
    // 절대 다른 곳에서 같이 쓰지 말 것. gateId는 씬 하나에 게이트가 여럿인 경우(T.Stage2/4/5)
    // 앞 게이트의 낡은 신호를 뒤 게이트가 자기 것으로 오인하지 않도록 구분하는 용도
    // (2026-08 버그 수정 — 위 StageStartSignal 주석 참고).
    private readonly NetworkVariable<StageStartSignal> _stageStartSignal = new(
        new StageStartSignal { serverTime = -1.0, gateId = -1 },
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Phase 시작 서버 시간 (PhaseManager.EnterPhase() 직전 Host 기록).
    // PhaseManager가 발동하는 함정(ArrowTrap/DropTrap 등)의 스케줄 앵커 전용.
    // StageStartServerTime과 별개 슬롯 — Phase마다 다시 찍혀도 StageStartGate 쪽 로직에
    // 영향 없음.
    private readonly NetworkVariable<double> _phaseStartServerTime = new(
        -1.0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ── 챌린지 라운드 동기화 (축 #4 공통 — OX/ColorTile/GridColor/SequenceRing) ──
    // [축 SSOT: MStageNetworkBoard.md §1] Trigger→RoundStart(Seed)→Generate→Judge→Resolve
    // 씬당 챌린지는 한 번에 하나만 진행되므로 이 필드들을 공유 슬롯으로 재사용한다.

    // 시드·스텝 인덱스·스텝 시작 서버 시간을 한 NV로 묶어 원자적으로 배포 (위 ChallengeStepState 참조).
    private readonly NetworkVariable<ChallengeStepState> _challengeStep = new(
        new ChallengeStepState { seed = 0, stepIndex = -1, stepStartServerTime = -1.0 },
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // 챌린지 클리어 확정 여부 (Host만 true로 전환)
    private readonly NetworkVariable<bool> _challengeCleared = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ── Floor 타일 롤 동기화 (Floor 전용 — 챌린지(_challengeStep)와 슬롯 공유 금지) ──
    // [Floor 마이그레이션] 챌린지와 Floor가 씬에서 동시에 도는 경우는 없음이 확인됐지만,
    // 의미가 다른 시스템이라 슬롯을 공유하면 나중에 오공유 버그가 재발할 수 있다
    // (2026-07-21 PhaseManager 오공유 버그와 동일 이유로 별도 슬롯 유지).
    private readonly NetworkVariable<FloorRollState> _floorRoll = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ── 보스 진행 동기화 (D축 — BossFightObjective 전용 슬롯) ──
    // [티켓 B] BossFightObjective의 _phasesCleared가 로컬 카운터+UnityEvent뿐이라 Host/Client가
    // 각자 다른 값을 들고 있었다(세그먼트 lag/Host 끊김). Host만 쓰는 이 슬롯으로 클리어 수를
    // 복제하고, BossFightObjective는 이 값의 변경 이벤트로 OnPhaseCleared를 발동한다.
    private readonly NetworkVariable<int> _bossPhasesCleared = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ── 문(Door) 개폐 동기화 (Door 전용 슬롯 — DoorNetworkSync 폐기, 2026-08) ──
    // [설계: TStageNetworkBoard.md §3.1] 문마다 개별 NetworkObject+NetworkBehaviour를 붙이던
    // DoorNetworkSync를 폐기하고, Floor(§11B.8)와 동일한 "슬롯 재사용" 원칙으로 여기에 통합한다.
    // index는 StagePressurePadSetup이 씬의 DoorController[]를 이름순 정렬해 배정 — Host/Client가
    // 항상 동일한 순서로 수집해야 같은 index가 같은 문을 가리킨다.
    private readonly NetworkList<bool> _doorOpenStates = new();

    private bool _resetPending;

    // 사망을 유발한 콜스택(예: OXQuizManager 데미지 루프+ClientRpc, StageManager/
    // SequenceRing 전원 즉사 루프, Breakable 데미지+넉백)은 전부 yield 없는 동기 코드라
    // 같은 프레임 안에서 이미 끝난다 — 1프레임만 미뤄도 그 뒤에야 Despawn이 일어나
    // RpcException이 해소됨. 프레임 지연은 fps에 반비례해 체감 시간이 늘어나므로(예:
    // ParrelSync 2인 동시 구동 시 fps 저하) 필요한 최소치만 유지.
    const int DeathReloadDelayFrames = 1;

    // Client-side 캐시 — SyncSurvivalRemainingClientRpc 매 틱 Find 방지
    private SurviveTimeObjective _surviveObjective;

    // ── 프로퍼티 ──────────────────────────────────────────────────

    public int    CurrentPhase            => _currentPhase.Value;
    public bool   IsCountdownActive        => _isCountdownActive.Value;
    public double CountdownStartServerTime => _countdownStartServerTime.Value;
    public double StageStartServerTime     => _stageStartSignal.Value.serverTime;
    public int    StageStartGateId         => _stageStartSignal.Value.gateId;
    public double PhaseStartServerTime     => _phaseStartServerTime.Value;

    public int    ChallengeSeed               => _challengeStep.Value.seed;
    public int    ChallengeStepIndex           => _challengeStep.Value.stepIndex;
    public double ChallengeStepStartServerTime => _challengeStep.Value.stepStartServerTime;
    public ChallengeOwnerType ChallengeOwner   => _challengeStep.Value.owner;
    public bool   IsChallengeCleared           => _challengeCleared.Value;

    public int    BossPhasesCleared            => _bossPhasesCleared.Value;

    public int  DoorCount => _doorOpenStates.Count;

    /// <summary>늦은 구독 캐치업용 — index의 현재 개폐 상태. 범위 밖이면 false.</summary>
    public bool IsDoorOpen(int index) =>
        index >= 0 && index < _doorOpenStates.Count && _doorOpenStates[index];

    /// <summary>챌린지 스텝(문제/라운드) 인덱스가 바뀔 때 발동. 전 머신 공통 구독점.</summary>
    public event Action<int> OnChallengeStepChanged;
    /// <summary>챌린지 클리어 확정 상태가 바뀔 때 발동.</summary>
    public event Action<bool> OnChallengeClearedChanged;
    /// <summary>Host 판정 결과(성공/실패) 1회성 연출 신호 — Client 전용(Host는 로컬에서 직접 처리).</summary>
    public event Action<bool> OnChallengeOutcome;

    /// <summary>
    /// 연속 진행형 챌린지(SequenceRing 등)의 남은 시간 동기화 이벤트 — Client 전용(Host는 로컬 tick으로 직접 갱신).
    /// 오답 페널티 등 이벤트 기반 변동이 있어 ServerTime 역산(OX의 ChallengeStepStartServerTime 방식)이
    /// 불가능한 타이머 전용. Host가 직접 tick하며 주기적으로 브로드캐스트한다.
    /// </summary>
    public event Action<float> OnChallengeTimeSync;

    /// <summary>Floor 타일 롤(시드+keepBWRatio)이 바뀔 때 발동. 전 머신 공통 구독점 — Generate만 반복(Judge/Resolve 없음).</summary>
    public event Action<FloorRollState> OnFloorRollChanged;

    /// <summary>보스 페이즈 클리어 수가 바뀔 때 발동. 전 머신 공통 구독점 — BossFightObjective가 구독해 OnPhaseCleared를 발동.</summary>
    public event Action<int> OnBossPhasesClearedChanged;

    /// <summary>문 개폐 상태가 바뀔 때 발동(index, isOpen). Client가 구독해 DoorController.Open()/Close() 호출용.</summary>
    public event Action<int, bool> OnDoorStateChanged;

    /// <summary>
    /// Stage5 타겟 포획 진행 상황(captured, required)이 바뀔 때 발동 — Client 전용 구독점.
    /// Stage5TargetRunner.OnTriggerEnter가 Host-only 판정(TStageNetworkBoard.md §3.2)이라
    /// Client는 로컬 포획 이벤트가 없다 — 이 Rpc가 Client HUD(ObjectiveUI)의 유일한 갱신 경로다.
    /// </summary>
    public event Action<int, int> OnStage5CaptureSync;

    /// <summary>
    /// Stage5 타겟 잡기 남은 시간이 갱신될 때 발동 — Client 전용 구독점.
    /// Stage5TargetObjective.Tick()이 Host 레인에서만 _elapsed를 진행하므로(§11A "Progress는
    /// Host 레인 하나"), Client는 이 신호로만 타이머 UI를 갱신한다.
    /// </summary>
    public event Action<float> OnStage5RemainingSync;

    /// <summary>
    /// §11 사망 문으로 재진입이 확정된 순간(Host 레인, NotifyPlayerDeathServerRpc 진입 시) 1회 발동.
    /// 각 챌린지 매니저(OXQuizManager/ColorTileChallenge/GridColorChallenge/GridBWTileChallenge/
    /// SequenceRingMinigame)의 Host 레인 Progress 루프(Update Tick 또는 판정 코루틴)가 이 신호를
    /// OnChallengeStepChanged 등과 동일한 방식으로 구독해 즉시 자기 상태를 Idle로 되돌린다.
    /// 사망은 챌린지 축(§11A ③Progress) 밖에서 일어나는 사건이라, 챌린지 자신의 Resolve로는
    /// 절대 감지할 수 없다 — 이 이벤트가 그 경계를 넘어 알려주는 유일한 Writer다. 이게 없으면
    /// Progress 루프가 리로드로 이 NetworkObject가 Despawn될 때까지 한두 프레임 더 돌면서
    /// ClientRpc를 계속 쏘아 RpcException(NetworkBehaviour must be spawned...)이 난다.
    /// </summary>
    public event Action OnDeathReloadStarted;

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        _currentPhase.OnValueChanged += OnPhaseChanged;
        _challengeStep.OnValueChanged    += OnChallengeStepChangedNv;
        _floorRoll.OnValueChanged        += OnFloorRollChangedNv;
        _bossPhasesCleared.OnValueChanged += OnBossPhasesClearedNv;
        _doorOpenStates.OnListChanged    += OnDoorOpenStatesChanged;
        // [버그 수정 2026-07-20] Survive Phase 오브젝트가 이전 Phase에서는 비활성 상태로
        // 시작하는 씬(예: M.Stage2 "Stage2.1" 컨테이너)에서는 기본 검색(비활성 제외)이
        // OnNetworkSpawn 시점에 null을 캐시해버려 Client의 생존 타이머 UI가 갱신되지 않았음.
        // 비활성 포함 검색으로 Phase 활성화 여부와 무관하게 항상 찾도록 수정.
        _surviveObjective = FindFirstObjectByType<SurviveTimeObjective>(FindObjectsInactive.Include);
    }

    public override void OnNetworkDespawn()
    {
        _currentPhase.OnValueChanged -= OnPhaseChanged;
        _challengeStep.OnValueChanged    -= OnChallengeStepChangedNv;
        _floorRoll.OnValueChanged        -= OnFloorRollChangedNv;
        _bossPhasesCleared.OnValueChanged -= OnBossPhasesClearedNv;
        _doorOpenStates.OnListChanged    -= OnDoorOpenStatesChanged;
        if (Instance == this) Instance = null;
    }

    // ── 사망 처리 ─────────────────────────────────────────────────

    /// <summary>
    /// 플레이어 사망 시 어느 클라이언트에서든 호출.
    /// Host가 1명이라도 사망 신호를 받으면 전원 씬 리로드.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void NotifyPlayerDeathServerRpc()
    {
        if (_resetPending) return;
        _resetPending = true;

        // §11 사망 문 진입 확정 — 챌린지 Progress 루프를 도는 모든 매니저에 즉시 통지해서
        // 리로드 코루틴(아래)이 실제로 씬을 갈아엎기 전에 각자 자기 루프를 멈추게 한다.
        OnDeathReloadStarted?.Invoke();

        // 사망 리로드 시 새 시드 생성 + 전체 클라이언트에 배포
        int newSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        NetworkSessionData.Seed = newSeed;
        BroadcastNewSeedClientRpc(newSeed);

        StartCoroutine(ReloadAfterDeathAnim());
    }

    IEnumerator ReloadAfterDeathAnim()
    {
        for (int i = 0; i < DeathReloadDelayFrames; i++)
            yield return null;

        string sceneName = SceneManager.GetActiveScene().name;
        Debug.Log($"[StageNetworkState] 사망 감지 — '{sceneName}' 리로드 (새 시드: {NetworkSessionData.Seed})");
        NetworkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    [ClientRpc]
    void BroadcastNewSeedClientRpc(int seed)
    {
        NetworkSessionData.Seed = seed;
    }

    // ── Breakable 파괴 동기화 ─────────────────────────────────────

    /// <summary>
    /// Host: Breakable 파괴 확정 시 Client에 stable ID 전달.
    /// Client: 정적 레지스트리에서 해당 ID의 Breakable을 찾아 파괴 연출 적용.
    /// </summary>
    [ClientRpc]
    public void SyncBreakClientRpc(int breakableId)
    {
        if (IsServer) return;
        Breakable.BreakById(breakableId);
        Debug.Log($"[StageNetworkState] Breakable(id={breakableId}) 파괴 동기화");
    }

    // ── DropTrap 경고 마커 동기화 ─────────────────────────────────

    /// <summary>
    /// Host: DropTrap이 경고 마커를 표시하는 시점에 호출(trapId = DropTrap stable ID).
    /// Client: 동일 DropTrap 인스턴스를 찾아 로컬로 마커 연출만 재생.
    /// 낙하체 스폰은 TrapProjectile B안(Host Spawn+velocity)으로 별도 동기화되므로
    /// 여기서는 순수 비주얼(경고 원 표시 + 채움 애니메이션)만 다룬다.
    /// </summary>
    [ClientRpc]
    public void SyncDropWarnClientRpc(
        int trapId, Vector3 targetPos, float warnDuration, float startY, float speed, Vector3 markerScale)
    {
        if (IsServer) return;
        DropTrap.PlayWarnById(trapId, targetPos, warnDuration, startY, speed, markerScale);
    }

    // ── ArrowTrap Mouth 연출 동기화 (Open/Hold, DropTrap 경고 마커와 동일 패턴) ──

    /// <summary>
    /// Host: ArrowTrap.OnPreFireCharge 발행 시점에 호출(trapId = ArrowTrap stable ID).
    /// Client: 동일 ArrowTrap 인스턴스의 Mouth 연출(Open)만 재생. Client는 자기 로컬 스케줄
    /// (부정확한 ServerTime 추정)로 이 연출을 트리거하지 않는다 — 이 RPC가 유일한 트리거다
    /// (Mouth↔Arrow 타이밍 수정).
    /// </summary>
    [ClientRpc]
    public void SyncArrowChargeClientRpc(int trapId)
    {
        if (IsServer) return;
        ArrowTrap.PlayChargeById(trapId);
    }

    /// <summary>
    /// Host: ArrowTrap.OnFiring 발행 시점(화살 Spawn 직전)에 호출. Client: Mouth 연출(Hold)만
    /// 재생. 화살 Spawn과 같은 호출 지점에서 나가므로 같은 네트워크 배치로 도착한다.
    /// </summary>
    [ClientRpc]
    public void SyncArrowFireClientRpc(int trapId)
    {
        if (IsServer) return;
        ArrowTrap.PlayFireById(trapId);
    }

    // ── WindTrap Mouth 연출 동기화 (Pull/Push Open/Hold/Close, ArrowTrap Mouth와 동일 패턴) ──

    /// <summary>
    /// Host: WindTrap.OnWindCharge 발행 시점에 호출(trapId = WindTrap stable ID).
    /// Client: 동일 WindTrap 인스턴스의 Mouth 연출(오므림/벌리기 시작)만 재생.
    /// </summary>
    [ClientRpc]
    public void SyncWindChargeClientRpc(int trapId)
    {
        if (IsServer) return;
        WindTrap.PlayChargeById(trapId);
    }

    /// <summary>Host: WindTrap.OnWindEnd 발행 시점에 호출. Client: Mouth 연출(복귀)만 재생.</summary>
    [ClientRpc]
    public void SyncWindEndClientRpc(int trapId)
    {
        if (IsServer) return;
        WindTrap.PlayEndById(trapId);
    }

    // ── 생존 타이머 동기화 ───────────────────────────────────────

    /// <summary>
    /// Host: SurviveTimeObjective UI 틱마다 호출.
    /// Client의 SurviveTimeObjective.NotifyRemainingTime()을 통해 TimerUI를 갱신.
    /// </summary>
    [ClientRpc]
    public void SyncSurvivalRemainingClientRpc(float remaining)
    {
        if (IsServer) return;
        _surviveObjective?.NotifyRemainingTime(remaining);
    }

    // ── 시간 동기화 (NetworkTime 기반) ───────────────────────────

    /// <summary>Host: 전원 점유 → 카운트다운 시작 시각을 ServerTime으로 기록.</summary>
    public void MarkCountdownStart()
    {
        if (!IsServer) return;
        _countdownStartServerTime.Value = NetworkManager.Singleton.ServerTime.Time;
        _isCountdownActive.Value = true;
    }

    /// <summary>Host: 이탈로 카운트다운 리셋 시 호출.</summary>
    public void MarkCountdownReset()
    {
        if (!IsServer) return;
        _isCountdownActive.Value = false;
    }

    /// <summary>
    /// Host: StartStage() 직전 서버 시간 + 완료된 게이트 식별자 기록.
    /// TimerUI가 이 값 기준으로 Host/Client 동일한 경과 시간을 계산.
    /// gateId는 호출한 StageStartGate 자신의 식별자 — 씬에 게이트가 여럿이면(T.Stage2/4/5)
    /// 각 게이트가 자기 gateId가 찍힌 신호만 자기 것으로 인정해 다른 게이트의 낡은 신호를
    /// 재사용하지 않게 한다.
    /// </summary>
    public void MarkStageStart(int gateId)
    {
        if (!IsServer) return;
        _stageStartSignal.Value  = new StageStartSignal { serverTime = NetworkManager.Singleton.ServerTime.Time, gateId = gateId };
        _isCountdownActive.Value = false;
    }

    /// <summary>
    /// Host: PhaseManager.EnterPhase() 진입 직전 서버 시간 기록.
    /// ArrowTrap/DropTrap 등이 이 Phase에서 Activate()될 때 스케줄 앵커로 사용.
    /// StageStartServerTime과 별개 — StageStartGate의 1회성 신호를 건드리지 않는다.
    /// </summary>
    public void MarkPhaseStart()
    {
        if (!IsServer) return;
        _phaseStartServerTime.Value = NetworkManager.Singleton.ServerTime.Time;
    }

    // ── Phase 동기화 ──────────────────────────────────────────────

    /// <summary>
    /// Host에서 Phase가 바뀔 때 호출. NetworkVariable로 전원에 전달.
    /// PhaseManager.EnterPhase() 호출 후 호출.
    /// </summary>
    public void SyncPhase(int phaseIndex)
    {
        if (!IsServer) return;
        _currentPhase.Value = phaseIndex;
    }

    void OnPhaseChanged(int prev, int next)
    {
        // 비오너(Client)에서도 Phase 변경을 받을 수 있도록 PhaseManager에 알림
        if (!IsServer && PhaseManager.Instance != null)
            PhaseManager.Instance.EnterPhaseOnClient(next);

        Debug.Log($"[StageNetworkState] Phase 변경: {prev} → {next}");
    }

    // ── 보스 진행 동기화 (D축) ──────────────────────────────────────

    /// <summary>Host: 보스 페이즈 클리어 수 갱신. BossFightObjective.NotifyPhaseCleared()에서 호출.</summary>
    public void SetBossPhasesCleared(int cleared)
    {
        if (!IsServer) return;
        _bossPhasesCleared.Value = cleared;
    }

    void OnBossPhasesClearedNv(int prev, int next) => OnBossPhasesClearedChanged?.Invoke(next);

    // ── 문(Door) 개폐 동기화 (Door 전용 슬롯) ──────────────────────

    /// <summary>
    /// Host: 씬의 문 개수만큼 슬롯을 초기화(전부 닫힘). StagePressurePadSetup이 OnPlayersReady
    /// 이후 index 배정을 마친 뒤 1회 호출 — 이후 Add/RemoveAt으로 개수를 바꾸지 않는다
    /// (index가 문마다 고정 배정이라 배정 후 개수 변경은 index 오염으로 이어짐).
    /// </summary>
    public void InitDoorSlots(int count)
    {
        if (!IsServer) return;
        _doorOpenStates.Clear();
        for (int i = 0; i < count; i++)
            _doorOpenStates.Add(false);
    }

    /// <summary>Host: 문 index의 개폐 상태 갱신. DoorController.OnOpened/OnClosed에서 호출.</summary>
    public void SetDoorOpen(int index, bool isOpen)
    {
        if (!IsServer) return;
        if (index < 0 || index >= _doorOpenStates.Count) return;
        if (_doorOpenStates[index] == isOpen) return;
        _doorOpenStates[index] = isOpen;
    }

    void OnDoorOpenStatesChanged(NetworkListEvent<bool> change)
    {
        switch (change.Type)
        {
            case NetworkListEvent<bool>.EventType.Add:
            case NetworkListEvent<bool>.EventType.Insert:
            case NetworkListEvent<bool>.EventType.Value:
                OnDoorStateChanged?.Invoke(change.Index, change.Value);
                break;
        }
    }

    // ── 챌린지 라운드 동기화 (축 #4 공통) ─────────────────────────

    /// <summary>
    /// Host: 챌린지 새 라운드 시작(트리거 진입 등). 시드를 배포해 전 머신이
    /// 동일한 로컬 생성 코드(셔플·배치)를 재실행하게 한다 — 결과 자체는 전송하지 않음.
    /// seed·stepIndex·owner를 한 NV 쓰기로 같이 보내 Client에서 항상 원자적으로 도착하게 한다.
    /// owner는 호출한 챌린지 자신의 타입 — 각 매니저의 핸들러가 "내 것이 아니면 무시"를
    /// 판단하는 유일한 근거이므로 반드시 자기 자신의 타입을 넘겨야 한다.
    /// </summary>
    public void ChallengeStart(int seed, ChallengeOwnerType owner)
    {
        if (!IsServer) return;
        _challengeStep.Value    = new ChallengeStepState { seed = seed, stepIndex = -1, stepStartServerTime = -1.0, owner = owner };
        _challengeCleared.Value = false;
    }

    /// <summary>
    /// Host: 문제/라운드 스텝 시작. stepIndex 변경이 전 머신에 전파되어
    /// OnChallengeStepChanged 구독자가 동일한 표시·타이머 로직을 실행한다.
    /// 시드·owner는 그대로 들고 있던 값을 유지 — 이 쓰기에서도 seed+stepIndex+time+owner가
    /// 한 번에 같이 간다.
    /// </summary>
    public void ChallengeStepBegin(int stepIndex)
    {
        if (!IsServer) return;
        _challengeStep.Value = new ChallengeStepState
        {
            seed                 = _challengeStep.Value.seed,
            stepIndex            = stepIndex,
            stepStartServerTime  = NetworkManager.Singleton.ServerTime.Time,
            owner                = _challengeStep.Value.owner,
        };
    }

    /// <summary>
    /// Host: 클리어된 챌린지의 stepIndex를 -1로 되돌린다. _challengeStep 슬롯은 씬당 공유이므로
    /// 클리어 후에도 마지막 stepIndex(≥0)가 그대로 남아있으면, 같은 챌린지 타입이 다음 Phase에서
    /// 다시 활성화될 때 OnEnable의 late-subscribe catch-up이 그 값을 "이미 진행 중"으로 오인해
    /// OnMinigameStarted 등 시작 이벤트를 씬 전환 직후(대화/실제 시작 전)에 잘못 재생시킨다
    /// (2026-07-27 버그 수정 — M.Stage4 Sequence Start 표지판 미표시 원인). _challengeCleared는
    /// 건드리지 않는다 — 클리어 신호는 이 호출 전에 이미 ChallengeCleared(true)로 전파된 뒤다.
    /// </summary>
    public void ResetChallengeStep()
    {
        if (!IsServer) return;
        _challengeStep.Value = new ChallengeStepState
        {
            seed                 = _challengeStep.Value.seed,
            stepIndex            = -1,
            stepStartServerTime  = -1.0,
            owner                = _challengeStep.Value.owner,
        };
    }

    /// <summary>
    /// Host: 챌린지 클리어 확정. Complete() 자체는 각 Objective가 자체 Host 가드로 처리 — 이 신호는 연출용.
    /// [버그 수정 2026-08] 예전엔 _challengeCleared NV의 OnValueChanged만으로 OnChallengeClearedChanged를
    /// 발동시켰는데, 이 신호가 뜨자마자(같은/다음 프레임) 다음 챌린지의 ChallengeStart()가 같은 NV를
    /// 곧바로 false로 되돌린다. NGO NetworkVariable은 매 네트워크 틱마다 "그 시점의 최종값"만 스냅샷으로
    /// 보내므로, true→false가 같은 틱 안에서 겹치면 Client가 보는 값은 false→false — 변화 자체가
    /// 감지되지 않아 OnValueChanged가 Client에서 전혀 발동하지 않았다(Host는 .Value setter 시점에
    /// 로컬로 즉시 동기 콜백이 돌기 때문에 이 레이스를 겪지 않아 항상 정상으로 보였음 — OX 마지막 문제
    /// 정답/해설 텍스트가 Client 화면에 영구히 남는 버그의 원인). NotifyChallengeOutcomeClientRpc와
    /// 동일하게 RPC(메시지)로 보장 전달 — 이후 NV가 어떻게 바뀌든 무관하게 항상 도착한다.
    /// _challengeCleared.Value 자체는 늦은 조회용 상태로만 유지(OnValueChanged 구독은 더 이상 하지 않음).
    /// </summary>
    public void ChallengeCleared(bool cleared)
    {
        if (!IsServer) return;
        _challengeCleared.Value = cleared;
        if (!cleared) return;

        OnChallengeClearedChanged?.Invoke(true); // Host 로컬 즉시 발동
        NotifyChallengeClearedClientRpc();
    }

    /// <summary>Client 전용 — 위 ChallengeCleared(true)가 보장 전달하는 1회성 신호.</summary>
    [ClientRpc]
    void NotifyChallengeClearedClientRpc()
    {
        if (IsServer) return;
        OnChallengeClearedChanged?.Invoke(true);
    }

    /// <summary>
    /// Host: 판정 결과(성공/실패) 1회성 연출 신호. 진행 상태(NV)와 별개로
    /// Client 쪽 UnityEvent(정답/오답 연출 등)만 재생한다 — Host는 로컬에서 직접 처리하므로 스킵.
    /// </summary>
    [ClientRpc]
    public void NotifyChallengeOutcomeClientRpc(bool success)
    {
        if (IsServer) return;
        OnChallengeOutcome?.Invoke(success);
    }

    void OnChallengeStepChangedNv(ChallengeStepState prev, ChallengeStepState next) => OnChallengeStepChanged?.Invoke(next.stepIndex);

    /// <summary>
    /// Host: 연속 진행형 챌린지의 남은 시간을 Client에 브로드캐스트 (§11B ④Judge 부속 — 시간 표시 전용,
    /// 판정 자체는 Host만 수행). SyncSurvivalRemainingClientRpc와 동일한 "Host tick + 주기 RPC" 패턴.
    /// </summary>
    [ClientRpc]
    public void SyncChallengeTimeClientRpc(float remaining)
    {
        if (IsServer) return;
        OnChallengeTimeSync?.Invoke(remaining);
    }

    /// <summary>
    /// Host: Stage5 타겟 포획 카운트를 Client HUD에 브로드캐스트. SyncChallengeTimeClientRpc와
    /// 동일한 "Host 값 확정 + 주기/이벤트성 Rpc" 패턴 — 챌린지 축(ChallengeStepState)과는 무관한
    /// 독립 채널이다 (Stage5TargetObjective는 ChallengeOwnerType 대상이 아님).
    /// </summary>
    [ClientRpc]
    public void SyncStage5CaptureClientRpc(int captured, int required)
    {
        if (IsServer) return;
        OnStage5CaptureSync?.Invoke(captured, required);
    }

    /// <summary>
    /// Host: Stage5 타겟 잡기 남은 시간을 Client HUD에 브로드캐스트. SyncSurvivalRemainingClientRpc와
    /// 동일한 "Host tick + 주기 Rpc" 패턴 — SyncStage5CaptureClientRpc처럼 챌린지 축과는 무관한
    /// Stage5 전용 독립 채널이다.
    /// </summary>
    [ClientRpc]
    public void SyncStage5RemainingClientRpc(float remaining)
    {
        if (IsServer) return;
        OnStage5RemainingSync?.Invoke(remaining);
    }

    // ── Floor 타일 롤 동기화 (Floor 전용) ──────────────────────────

    /// <summary>
    /// Host: 새 타일 롤 배포. 시드 하나만 보내 전 머신이 로컬로 동일 결과를 재생성하게 한다
    /// (byte[] 상태 배열 전체를 매번 보내던 기존 SyncTilesClientRpc 방식 폐기).
    /// keepBWRatio를 같이 실어보내 Client가 Phase 진행을 독자 계산할 필요가 없게 한다.
    /// Floor는 성공/실패 판정이 없으므로 Judge/Resolve 단계 없이 Generate만 반복한다.
    /// </summary>
    public void FloorRoll(int seed, float keepBWRatio)
    {
        if (!IsServer) return;
        _floorRoll.Value = new FloorRollState { seed = seed, keepBWRatio = keepBWRatio };
    }

    void OnFloorRollChangedNv(FloorRollState prev, FloorRollState next) => OnFloorRollChanged?.Invoke(next);

    // ── 챌린지 입력 제출 (Client → Host, §11B.1) ───────────────────

    /// <summary>
    /// Client: 자기 색으로 챌린지 스텝 제출 요청(예: SequenceRing 키 입력). Host만 위치·색 등 실제
    /// 상태를 갖고 있는 포지션 판정형과 달리, 키 입력형은 "누가 눌렀는가" 자체가 Host에 없는 정보라
    /// 별도 제출 경로가 필요하다 — Host가 SequenceRingMinigame.TrySubmit()으로 판정한다.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SubmitStepServerRpc(PlayerColorType color)
    {
        SequenceRingMinigame.Instance?.TrySubmit(color);
    }

    /// <summary>Client: Common/Danger 스텝 등 색 구분 없는 "아무 키" 제출 요청.</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SubmitAnyKeyStepServerRpc()
    {
        SequenceRingMinigame.Instance?.TrySubmitAnyKey();
    }

    // ── 함정 발사체 피격/파괴 보고 (Client → Host, 상주 릴레이) ───────────
    // [버그 수정 2026-07-28] TrapProjectile은 짧게 살고 죽는 NetworkObject라, 자기 자신을
    // Rpc 대상으로 쓰면 Despawn 이후 늦게 도착한 중복 보고가 NGO 라우팅 단계에서 못 찾아져
    // "Deferred OnSpawn" 대기 → 10초 후 PurgeTrigger 경고로 이어졌다(Arrow/Drop/Boulder
    // 전부 TrapProjectile 공유라 셋 다 동일 증상). 이 오브젝트는 스테이지 내내 살아있으므로
    // 라우팅 실패가 구조적으로 없다 — "이미 처리됨"은 아래 TryGetValue 가드 하나로 끝낸다.

    /// <summary>Client(전원): 발사체 피격 보고. Host가 발사체를 찾아 데미지+Despawn을 위임.</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ReportTrapHitServerRpc(ulong projectileNetId, ulong playerNetId)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(projectileNetId, out var projNetObj))
            return; // 이미 처리(다른 보고로 Despawn)됨 — 조용히 무시

        projNetObj.GetComponent<TrapProjectile>()?.ApplyHitFromHost(playerNetId);
    }

    /// <summary>Client(전원): 플레이어 NetworkObject를 못 찾은 예외 케이스의 파괴 요청.</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestTrapDestroyServerRpc(ulong projectileNetId)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(projectileNetId, out var projNetObj))
            return;

        projNetObj.GetComponent<TrapProjectile>()?.ApplyDestroyFromHost();
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 사망 신고")]
    void Debug_Death() => NotifyPlayerDeathServerRpc();

    [ContextMenu("테스트: Phase 0으로 초기화")]
    void Debug_Phase0() => SyncPhase(0);
#endif
}
