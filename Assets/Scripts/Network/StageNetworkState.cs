using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ChallengeStepState가 어느 챌린지 매니저 것인지 식별하는 태그.
/// [버그 수정 2026-07-28] _challengeStep은 씬당 여러 챌린지 종류가 공유하는 슬롯인데(§11B.2),
/// "이 컴포넌트를 꺼라"(Phase 인덱스 NV)와 "이번 라운드 데이터"(_challengeStep NV)가 서로 다른
/// NetworkVariable이라 Client 도착 순서가 NGO에서 보장되지 않는다. 순서가 뒤집히면 아직
/// SetActive(false) 안 된 이전 챌린지가 새 챌린지의 stepIndex를 자기 것으로 오인해 반응했다
/// (A 챌린지를 고치면 활성화 타이밍이 바뀌어 B가 깨지는 회귀의 실제 원인 — PhaseManager.EnterPhase()가
/// onPhaseEnter(챌린지 시작)→Phase 인덱스 갱신(오브젝트 on/off) 순서로 별도 NV 2개를 쓰기 때문).
/// 각 매니저가 이 태그로 "내 것이 아니면 무시"를 판단하면 활성화 타이밍이 완벽히 맞지 않아도 안전하다.
/// [2026-08 재확인] Phase 인덱스 NV는 이후 PhaseStartSignal(phaseIndex+serverTime)로 교체됐고
/// PhaseManager.EnterPhase()의 쓰기 순서도 onPhaseEnter보다 먼저로 바뀌었지만(WindTrap 타이밍
/// 버그 수정, 위 struct 주석 참고), _challengeStep과는 여전히 별도 NV라 도착 순서는 여전히
/// 보장되지 않는다 — 이 owner 가드가 계속 유효한 안전장치다(구조체 완전 통합은 블라스트 반경
/// 문제로 보류, 아래 NetworkDesign.md §11B.9 참고).
/// </summary>
public enum ChallengeOwnerType
{
    None,
    OX, // [삭제됨] OXQuizManager 제거(MinigameDesign.md §0) — 과거 세션 값과의 숫자 충돌 방지를 위해 자리만 유지, 재사용 금지
    ColorTile,
    GridColor, // [삭제됨] GridColorChallenge → GridChallenge로 통합(CoopStageAudit.M.md §8, 2026-09-11) — 자리만 유지, 재사용 금지
    GridBW,    // [삭제됨] GridBWTileChallenge → GridChallenge로 통합 — 자리만 유지, 재사용 금지
    SequenceRing,
    DirectionalBarrier,
    SideSplit, // 좌/우 분기 인원+색상 미니게임 — MinigameDesign.md §1, SideSplitChallenge
    Grid,      // M.Stage5 혼합판 — 고유색+흑백 통합 챌린지, GridChallenge (CoopStageAudit.M.md §8)
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

    /// <summary>
    /// [버그 수정 2026-08] 같은 owner 타입의 챌린지 매니저가 씬에 여러 인스턴스 존재할 때
    /// (예: T.Boss의 ColorTileChallenge 5개 — 플레이어별 개인 챌린지 4개 + 통합 1개) owner
    /// 타입 하나만으로는 "어느 인스턴스"인지 구분이 안 돼, 한 인스턴스의 이벤트를 나머지
    /// 형제 인스턴스가 자기 것으로 오인해 반응하는 교차 오염이 발생했다(T.Boss AdvancingWall
    /// 페널티가 Host/Client에서 방향 수가 다르게 적용되던 버그의 근본 원인). 0 = 미사용(단일
    /// 인스턴스만 있는 챌린지 타입은 항상 0으로 두면 기존 동작 그대로 유지됨).
    /// </summary>
    public int instanceId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref seed);
        serializer.SerializeValue(ref stepIndex);
        serializer.SerializeValue(ref stepStartServerTime);
        serializer.SerializeValue(ref owner);
        serializer.SerializeValue(ref instanceId);
    }

    public bool Equals(ChallengeStepState other) =>
        seed == other.seed &&
        stepIndex == other.stepIndex &&
        stepStartServerTime.Equals(other.stepStartServerTime) &&
        owner == other.owner &&
        instanceId == other.instanceId;

    public override bool Equals(object obj) => obj is ChallengeStepState other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(seed, stepIndex, stepStartServerTime, owner, instanceId);
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
/// Phase 인덱스 + 그 Phase가 시작된 서버 시각을 하나로 묶은 값.
/// [버그 수정 2026-08 — WindTrap Host/Client 발동 타이밍 1~2초 불일치] phaseIndex(옛 _currentPhase)와
/// serverTime(옛 _phaseStartServerTime)을 별도 NetworkVariable 2개로 나눠뒀을 때, Client에서
/// phaseIndex 변경 콜백(→ PhaseManager.EnterPhaseOnClient() → StageManager.StartStage() →
/// trap.Activate())이 serverTime이 새 값으로 갱신되기 전에 먼저 발동할 수 있었다(NGO가 별도 NV
/// 간 도착 순서를 보장하지 않음 — ChallengeStepState/StageStartSignal과 동일 원인). 그러면
/// PhaseStartServerTime 앵커를 쓰는 WindTrap/ArrowTrap/DropTrap/SpikeTrap/SpikeLaneField가
/// Client에서만 직전 Phase의 낡은 시작 시각을 스케줄 기준으로 잡아버려 Host보다 발동·종료가
/// 어긋났다(실기 다인원 테스트에서 재현 — ParrelSync 근거리 테스트는 지연이 작아 안 드러남).
/// ChallengeStepState/StageStartSignal과 동일한 원칙으로 하나의 NV로 합쳐 원자적으로 전달한다.
/// </summary>
public struct PhaseStartSignal : INetworkSerializable, IEquatable<PhaseStartSignal>
{
    public int    phaseIndex;
    public double serverTime;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref phaseIndex);
        serializer.SerializeValue(ref serverTime);
    }

    public bool Equals(PhaseStartSignal other) =>
        phaseIndex == other.phaseIndex && serverTime.Equals(other.serverTime);

    public override bool Equals(object obj) => obj is PhaseStartSignal other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(phaseIndex, serverTime);
}

/// <summary>
/// T.Boss Sphere 하강 상태(BossSpherePhaseDriver 전용 슬롯).
/// checkpointIndex = 지금 하강 중인 체크포인트 칸,
/// descentStartServerTime = 그 하강이 시작되는 서버 시각.
/// 전 머신이 descentStartServerTime 기준으로 하강 진행도를 결정론적으로 계산한다
/// (로컬 경과 시간 누적 금지 — MovingCorridor와 같은 이유).
/// 칸과 시작 시각을 한 NV로 묶는 이유는 PhaseStartSignal과 동일 — 별도 NV면 도착 순서가 보장되지
/// 않아 Client가 이전 칸의 시작 시각을 새 칸의 것으로 오인할 수 있다.
///
/// [2026-09-21 개명] 구 이름은 BossSphereHitState, 구 필드는 hitSerial이었다 —
/// P3의 "Sphere에 색 맞춰 부딪히기"가 폐기되면서 히트 번호는 사라지고
/// 이 슬롯은 하강 시각 동기화 전용으로만 남았다.
/// </summary>
public struct BossSphereDescentState : INetworkSerializable, IEquatable<BossSphereDescentState>
{
    public int    checkpointIndex;
    public double descentStartServerTime;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref checkpointIndex);
        serializer.SerializeValue(ref descentStartServerTime);
    }

    public bool Equals(BossSphereDescentState other) =>
        checkpointIndex == other.checkpointIndex
        && descentStartServerTime.Equals(other.descentStartServerTime);

    public override bool Equals(object obj) => obj is BossSphereDescentState other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(checkpointIndex, descentStartServerTime);
}

/// <summary>
/// 스테이지 네트워크 상태 중앙 허브. NetworkBehaviour.
/// M.Stage1 / T.Stage1 씬 내 NetworkObject GameObject에 부착.
///
/// [역할]
/// - 스테이지 실패 확정 → Host가 씬 리로드 (NetworkSceneManager)
/// - 팀 공유 목숨 보유 (ReviveSystemDesign.md §4)
/// - Phase 진행 상태 동기화 (CurrentPhase NetworkVariable)
///
/// [배치]
/// 각 스테이지 씬에 빈 GameObject → NetworkObject + StageNetworkState 추가.
///
/// [연결]
/// - StageManager / PlayerReviveState → FailStageFromServer() (Host 직접, ReviveSystemDesign.md §6)
/// - EscMenuController.OnClickReset() → NotifyStageResetServerRpc()
/// - PhaseManager.EnterPhase() → MarkAndSyncPhase(index) (Host에서만 호출 — Phase 인덱스 +
///   시작 서버시간을 PhaseStartSignal로 원자적 전달, 2026-08 버그 수정. 옛 MarkPhaseStart()+
///   SyncPhase(index) 분리 방식은 폐기됨)
/// - StageStartGate.CompleteCountdown() → MarkStageStart(gateId) (PhaseManager와 별개 슬롯)
/// </summary>
public class StageNetworkState : NetworkBehaviour
{
    public static StageNetworkState Instance { get; private set; }

    // 현재 Phase 인덱스 + 그 Phase 시작 서버 시간 (Host가 원자적으로 같이 기록, 전원이 읽음).
    // [버그 수정 2026-08] 예전엔 _currentPhase(int)와 _phaseStartServerTime(double)이 별도 NV라
    // Client 도착 순서가 보장 안 됐다 — WindTrap 등 PhaseStartServerTime 앵커 트랩이 Host보다
    // 1~2초 어긋나는 버그의 원인이었다. PhaseStartSignal 구조체로 합쳐 원자적으로 전달한다
    // (위 struct 주석 참고).
    private readonly NetworkVariable<PhaseStartSignal> _phaseStartSignal = new(
        new PhaseStartSignal { phaseIndex = -1, serverTime = -1.0 },
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

    // ── T.Boss Sphere 하강 동기화 (BossSpherePhaseDriver 전용 슬롯, 2026-09-18 버그 수정) ──
    // [버그] 하강을 각 머신이 로컬 경과 시간으로 누적했다. Client는 이벤트를 받은 시각부터 늦게
    // 출발하고 프레임 히치로 버려진 시간이 100초가 넘는 하강 내내 남았다(MovingCorridor와 같은
    // 버그 클래스). Host가 하강 시작 서버 시각을 이 슬롯에 실어 보내고 전 머신이 그 기준으로 센다.
    private readonly NetworkVariable<BossSphereDescentState> _bossSphereDescent = new(
        new BossSphereDescentState { checkpointIndex = -1, descentStartServerTime = -1.0 },
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ── MovingCorridor 시작 서버 시각 (T.Stage4 전용 슬롯, 2026-09-18 버그 수정) ──
    // [버그] 벽 위치를 머신마다 "로컬 Activate 이후 속도×dt 누적"으로 계산해, 시작 지연·속도 변경
    // 감지 지연·프레임 히치가 영구 오차로 쌓였다(Steam 실기에서 막바지 수 m 차이). Host가 시작 시각을
    // 확정하고, 전 머신이 이 시각 기준 틱 번호로 같은 결정론 시뮬을 돌린다. 씬당 복도 1개 전제.
    private readonly NetworkVariable<double> _corridorStartServerTime = new(
        -1.0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ── 구역 시계 시작 서버 시각 (SegmentTimer 슬롯 — T.Stage3 구역 데드라인) ──
    // index = SegmentTimer.segmentIndex(인스펙터 수동 배정, 자동 정렬 배정 아님 — 구역은 씬에 몇 개
    // 없고 순서가 지형으로 고정돼 있어 이름순 수집 규약이 필요 없다). 값 = 그 구역 시계가 시작한
    // 서버 시각, 0 = 아직 시작 안 함.
    // [왜 NV인가] "이 구역 시계는 언제 시작했나"는 일회성 이벤트가 아니라 지속 상태다(§9 Sync 규칙) —
    // ClientRpc로 보내면 그 순간 수신 준비가 안 된 Client에게 값이 영구 유실돼 카운트다운이 아예 안 돈다.
    // NV면 스폰 시 현재값이 자동 동기화되고, 구역마다 슬롯이 따로라 다음 구역이 시작해도 앞 구역
    // 시각이 덮이지 않는다(뒤처진 사람에게 앞 구역 시계가 계속 살아 있어야 한다).
    // [왜 SegmentTimer가 직접 NetworkObject가 아닌가] 씬 배치 NetworkObject는 OnEnable()이 NGO 스폰
    // 처리보다 먼저 돌아 IsServer 가드에 걸리는 레이스가 있다 — T.Stage3의 BoulderSpawnManager가
    // 실제로 그 사고를 냈다(TrapNetworkBoard.md §7). 상주 릴레이인 여기에 슬롯을 두면 그 레이스가 없다.
    private readonly NetworkList<double> _segmentStartTimes = new();

    // ── 팀 공유 목숨 (ReviveSystemDesign.md §4) ──
    // 스테이지 시작 시 (인원수 − 1)로 초기화, **사망마다** -1, 회복 없음. -1은 "아직 미초기화" 센티널
    // — PlayerSpawnCoordinator.OnPlayersReady에서 실제 인원수로 확정한다(파티 크기가 그 전엔 불안정).
    // [2026-09-19] 소모 시점이 "부활 완료"에서 "사망 순간"으로 바뀌었다 — 자동 부활이라 완료 시점에
    // 소모할 이유가 없고, 동시 부활 완료 경쟁(구 FailAllOtherDowned)이 통째로 사라진다(§4).
    private readonly NetworkVariable<int> _teamLivesRemaining = new(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ── 문(Door) 개폐 동기화 (Door 전용 슬롯 — DoorNetworkSync 폐기, 2026-08) ──
    // [설계: TStageNetworkBoard.md §3.1] 문마다 개별 NetworkObject+NetworkBehaviour를 붙이던
    // DoorNetworkSync를 폐기하고, Floor(§11B.8)와 동일한 "슬롯 재사용" 원칙으로 여기에 통합한다.
    // index는 StagePressurePadSetup이 씬의 DoorController[]를 이름순 정렬해 배정 — Host/Client가
    // 항상 동일한 순서로 수집해야 같은 index가 같은 문을 가리킨다.
    private readonly NetworkList<bool> _doorOpenStates = new();

    // ── Pioneer Path 타일 해금 동기화 (T.Stage2 전용 슬롯, 2026-08 버그 수정) ──
    // [버그] PioneerPathTile.OnCollisionEnter의 Unlock()이 로컬 물리에만 의존했는데,
    // 원격 플레이어는 NetworkPlayerSetup.ApplyPhysicsAuthority()가 Rigidbody를 kinematic으로
    // 설정하고(Owner/Host만 non-kinematic) 타일은 Rigidbody가 없는 정적 Collider라, Unity 물리
    // 규칙상 kinematic ↔ 무-Rigidbody 콜라이더 조합은 OnCollisionEnter 자체가 발생하지 않는다.
    // 그 결과 pioneer 본인·Host 화면에서만 해금 색이 보이고 다른 Client는 항상(간헐적 아님)
    // 해금 색을 못 봤다. Door(§3.1)와 동일한 슬롯 재사용 원칙으로 Host가 해금을 확정 브로드캐스트.
    // index는 PioneerPathManager가 zone 순서 → zone 내 path 타일 순서로 배정(계층 순회라 Host/Client
    // 항상 동일 순서).
    private readonly NetworkList<bool> _pioneerTileUnlocked = new();

    // ── 함정 조준 타겟 동기화 (TrapPlayerTracker 슬롯) ──
    // [왜 NV인가] "지금 이 함정이 누구를 조준 중"은 일회성 이벤트가 아니라 지속 상태다(§9 Sync 규칙).
    // ClientRpc로 보내면 그 순간 수신 준비가 안 된 Client(트래커 레지스트리 미구성, Player 캐시 미확보)에게
    // 값이 영구 유실되고 다음 타겟 변경까지 틀린 표시가 유지된다 — §6B.7 NotifyPlayersReady ClientRpc
    // 유실 사고와 동일 유형. NV면 스폰 시 현재값이 자동 동기화되고 GetTrackerTarget()으로 캐치업도 된다.
    // [왜 동기화하나] 실제 발사 방향은 Host 로컬 회전(spawn.forward) 기준인데 함정 회전은 씬 오브젝트의
    // 평범한 Transform이라 동기화가 없다. Client가 타겟을 독자 계산하면 "포신은 나를 조준하는데 화살은
    // 딴 사람에게 날아가는" 불일치가 생긴다.
    // index는 TrapPlayerTracker가 씬 계층 경로 정렬로 배정(Host/Client 항상 동일 순서).
    // 값 = PlayerColorType의 int 캐스팅, -1 = 타겟 없음.
    private readonly NetworkList<int> _trackerTargets = new();

    // ── MemoryRoundObjective 구역 클리어 동기화 (T.Stage2 전용 슬롯, 2026-08 버그 수정) ──
    // [버그] MemoryRoundObjective가 StageManager.OnStageClear(§11A.0 — Host 레인에서만 유효한
    // 로컬 이벤트)를 네트워크 브릿지 없이 직접 구독해, Client의 진행 카운터가 0에서 고정됐다
    // (ObjectiveUI "0/3" 고착). BossFightObjective._phasesCleared와 동일한 버그 클래스라
    // 같은 해법(Host가 카운트 후 NV로 확정 브로드캐스트) 재사용.
    private readonly NetworkVariable<int> _memorySectionsCleared = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ── 색 게이트 문 동기화 (T.Stage5 미로 전용 슬롯) ──
    // [2026-09-18 확장] 흑/백 2상태(_blackDoorOpen, bool) → "열린 색 1개 또는 없음"(int).
    // `TStage5RunnerRedesign.md` §1.2: 패드 1개를 밟으면 그 색 문만 Open, 나머지 전부 Close.
    // 흑·백도 같은 규칙의 한 색일 뿐이라 bool 2상태로는 표현할 수 없어 색 인덱스로 넓혔다.
    // 값 = PlayerColorType의 int 캐스팅(Blue/Purple/Green/Yellow/Black/White), -1 = 전부 닫힘.
    // 패드 판정은 Host의 게이트 컨트롤러가 담당하고, 전 머신은 이 값만 보고
    // DoorController.Open()/Close()를 재생한다. 사망 리로드 시 씬과 함께 초기값(-1)으로 돌아간다.
    private readonly NetworkVariable<int> _openGateColor = new(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ── T.Stage5 러너 슬롯 (T5 전용) ──
    // 이번 판의 러너 clientId 하나뿐이다. 맵 인덱스는 **여기 없다** — T5는 맵이 한 장이다
    // (TStage5RunnerRedesign.md §1.1). 시드에서 파생되는 값은 각 머신이 직접 계산한다.
    // 러너만 NV인 이유는 Host 전권인 체이서가 "타겟의 진실"을 한 곳에서 읽어야 하기 때문이다.
    private readonly NetworkVariable<ulong> _t5Runner = new(
        NoRunner,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ── 씬 준비 취합 슬롯 (커튼 게이트, 전 씬 공통) ──
    // 비트 i = PlayerSpawnCoordinator 명단을 clientId 순으로 정렬했을 때 i번째 사람이 준비됐는가.
    // 최대 4인이라 int 하나면 충분하고, NV라 도착 보장 + 늦게 봐도 최종값이다(재시도가 멱등해진다).
    private readonly NetworkVariable<int> _sceneReadyMask = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool _resetPending;

    // ── 부활 금지 스테이지 (ReviveSystemDesign.md §7.1) ──
    [Header("부활 예외")]
    [SerializeField, Tooltip("체크하면 이 씬에서는 자동 부활이 일어나지 않고 팀 목숨도 소모되지 않는다. " +
        "누가 죽든 그 판이 끝나는 스테이지 전용(T.Stage5) — 실패 확정은 그 씬의 Objective가 사망을 보고 내린다. " +
        "ReviveSystemDesign.md §7.1")]
    bool disableRevive = false;

    /// <summary>이 씬에서 자동 부활을 쓰지 않는가(§7.1). PlayerReviveState가 사망 처리 분기에서 읽는다.</summary>
    public bool IsReviveDisabled => disableRevive;

    /// <summary>실패·리셋이 이미 확정돼 씬 리로드 대기 중인가. 진행 중 부활 예약을 막는 데 쓴다(§9.4).</summary>
    public bool IsStageFailing => _resetPending;

    // 사망을 유발한 콜스택(예: OXQuizManager 데미지 루프+ClientRpc, StageManager/
    // SequenceRing 전원 즉사 루프, Breakable 데미지+넉백)은 전부 yield 없는 동기 코드라
    // 같은 프레임 안에서 이미 끝난다 — 1프레임만 미뤄도 그 뒤에야 Despawn이 일어나
    // RpcException이 해소됨(이 최소 요구치는 아래 초 단위 딜레이가 그보다 훨씬 크므로
    // 항상 만족됨).
    // [UX 개선 2026-08] 플레이테스트 피드백 — 리로드가 너무 빨라 실패했다는 사실 자체를
    // 인지하지 못함. STAGE FAILED 배너를 볼 여유를 초 단위로 확보.
    [SerializeField, Tooltip("실패 확정부터 씬 리로드까지 대기 시간(초). STAGE FAILED 배너를 인지할 여유.")]
    float deathReloadDelay = 1.75f;

    // Client-side 캐시 — SyncSurvivalRemainingClientRpc 매 틱 Find 방지
    private SurviveTimeObjective _surviveObjective;

    // ── 프로퍼티 ──────────────────────────────────────────────────

    public int    CurrentPhase            => _phaseStartSignal.Value.phaseIndex;
    public bool   IsCountdownActive        => _isCountdownActive.Value;
    public double CountdownStartServerTime => _countdownStartServerTime.Value;
    public double StageStartServerTime     => _stageStartSignal.Value.serverTime;
    public int    StageStartGateId         => _stageStartSignal.Value.gateId;
    public double PhaseStartServerTime     => _phaseStartSignal.Value.serverTime;

    public int    ChallengeSeed               => _challengeStep.Value.seed;
    public int    ChallengeStepIndex           => _challengeStep.Value.stepIndex;
    public double ChallengeStepStartServerTime => _challengeStep.Value.stepStartServerTime;
    public ChallengeOwnerType ChallengeOwner   => _challengeStep.Value.owner;
    /// <summary>현재 _challengeStep 슬롯을 쓰고 있는 인스턴스 ID (같은 owner 타입 내 형제 인스턴스 구분용).</summary>
    public int    ChallengeInstanceId          => _challengeStep.Value.instanceId;
    public bool   IsChallengeCleared           => _challengeCleared.Value;

    /// <summary>
    /// NotifyChallengeOutcomeClientRpc가 실어보낸 instanceId의 Client 로컬 캐시.
    /// RPC 파라미터로 직접 전달되므로 _challengeStep이 그 사이 다른 인스턴스에 의해 덮어써져도
    /// 안전하다(OnChallengeOutcome 구독자가 이 값을 읽는 시점은 항상 RPC 본문 실행 중 — 동기).
    /// </summary>
    public int    LastChallengeOutcomeInstanceId { get; private set; }

    public int    BossPhasesCleared            => _bossPhasesCleared.Value;

    public int  DoorCount => _doorOpenStates.Count;

    /// <summary>늦은 구독 캐치업용 — index의 현재 개폐 상태. 범위 밖이면 false.</summary>
    public bool IsDoorOpen(int index) =>
        index >= 0 && index < _doorOpenStates.Count && _doorOpenStates[index];

    public int  PioneerTileCount => _pioneerTileUnlocked.Count;

    /// <summary>늦은 구독 캐치업용 — index 타일의 현재 해금 상태. 범위 밖이면 false.</summary>
    public bool IsPioneerTileUnlocked(int index) =>
        index >= 0 && index < _pioneerTileUnlocked.Count && _pioneerTileUnlocked[index];

    public int  TrackerTargetCount => _trackerTargets.Count;

    /// <summary>늦은 구독 캐치업용 — index 트래커의 현재 조준 타겟 colorIndex. 범위 밖/미설정이면 -1.</summary>
    public int  GetTrackerTarget(int index) =>
        index >= 0 && index < _trackerTargets.Count ? _trackerTargets[index] : -1;

    public int  MemorySectionsCleared => _memorySectionsCleared.Value;

    /// <summary>
    /// 현재 열려 있는 문 색. 값 = PlayerColorType의 int 캐스팅, -1 = 전부 닫힘.
    /// 한 번에 한 색만 열린다(TStage5RunnerRedesign.md §1.2 배타 색 게이트).
    /// </summary>
    public int OpenGateColor => _openGateColor.Value;

    /// <summary>color 문이 지금 열려 있는지. 게이트 컨트롤러·문 연출의 공통 판정점.</summary>
    public bool IsGateColorOpen(PlayerColorType color) => _openGateColor.Value == (int)color;

    /// <summary>러너 없음(미추첨)을 뜻하는 값.</summary>
    public const ulong NoRunner = ulong.MaxValue;

    /// <summary>이번 판의 러너 clientId. 미추첨이면 <see cref="NoRunner"/>.</summary>
    public ulong T5RunnerClientId => _t5Runner.Value;

    /// <summary>clientId가 이번 판의 러너인지. 미추첨이면 항상 false.</summary>
    public bool IsT5Runner(ulong clientId) =>
        _t5Runner.Value != NoRunner && _t5Runner.Value == clientId;

    /// <summary>이 머신의 로컬 플레이어가 러너인지 — 러너 마커·UI 분기용.</summary>
    public bool IsLocalPlayerT5Runner
    {
        get
        {
            var nm = NetworkManager.Singleton;
            return nm != null && nm.IsListening && IsT5Runner(nm.LocalClientId);
        }
    }

    /// <summary>챌린지 스텝(문제/라운드) 인덱스가 바뀔 때 발동. 전 머신 공통 구독점.</summary>
    public event Action<int> OnChallengeStepChanged;
    /// <summary>챌린지 클리어 확정 상태가 바뀔 때 발동.</summary>
    public event Action<bool> OnChallengeClearedChanged;
    /// <summary>Host 판정 결과(성공/실패) 1회성 연출 신호 — Client 전용(Host는 로컬에서 직접 처리).</summary>
    public event Action<bool> OnChallengeOutcome;
    /// <summary>
    /// 스텝 단위 정답/오답 1회성 연출 신호(SFX 등). NV(ChallengeStepState) 변경과 달리 "오답"은
    /// 스텝 인덱스가 그대로라 NV만으로는 Client에 전달되지 않는다 — SequenceRing이 최초 사용.
    /// Host/Client 전 머신 공통 구독점(NotifyChallengeStepResult 참고 — Host 로컬 즉시 발동 + RPC).
    /// </summary>
    public event Action<bool> OnChallengeStepResult;

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

    /// <summary>T.Boss Sphere 하강 상태가 바뀔 때 발동. 전 머신(Host 포함) 공통 구독점 — BossSpherePhaseDriver 전용.</summary>
    public event Action<BossSphereDescentState> OnBossSphereDescentChanged;

    public BossSphereDescentState BossSphereDescent => _bossSphereDescent.Value;

    /// <summary>MovingCorridor 시작 서버 시각. -1 = 아직 시작 안 함.</summary>
    public double CorridorStartServerTime => _corridorStartServerTime.Value;

    /// <summary>Host: MovingCorridor 시작 시각 확정. MovingCorridor.Activate()에서만 호출.</summary>
    public void MarkCorridorStart(double serverTime)
    {
        if (!IsServer || IsDespawned) return;
        _corridorStartServerTime.Value = serverTime;
    }

    /// <summary>구역 index의 시계 시작 서버 시각. 0 = 아직 시작 안 함.</summary>
    public double GetSegmentStartTime(int index) =>
        index >= 0 && index < _segmentStartTimes.Count ? _segmentStartTimes[index] : 0d;

    /// <summary>
    /// Host: 구역 index의 시계를 지금 서버 시각으로 확정. SegmentTimer.StartTimer()에서만 호출.
    /// 이미 시작한 구역이면 무시한다(트리거에 두 번째 플레이어가 들어와도 시계가 리셋되지 않는다).
    /// 슬롯은 index가 닿는 만큼 그때그때 늘린다 — 구역 개수를 미리 확정하는 Init 호출이 없어도
    /// 되고(문·트래커 슬롯과 다른 점), 구역이 z 순서대로 시작하지 않아도 안전하다.
    /// </summary>
    public void MarkSegmentStart(int index)
    {
        if (!IsServer || IsDespawned || index < 0) return;
        while (_segmentStartTimes.Count <= index)
            _segmentStartTimes.Add(0d);
        if (_segmentStartTimes[index] > 0d) return;
        _segmentStartTimes[index] = NetworkManager.Singleton.ServerTime.Time;
    }

    /// <summary>문 개폐 상태가 바뀔 때 발동(index, isOpen). Client가 구독해 DoorController.Open()/Close() 호출용.</summary>
    public event Action<int, bool> OnDoorStateChanged;

    /// <summary>Pioneer Path 타일이 해금될 때 발동(index). 전 머신(Host 포함) 공통 구독점 — PioneerPathManager가 구독해 해당 타일의 Unlock() 호출.</summary>
    public event Action<int> OnPioneerTileUnlocked;

    /// <summary>함정 조준 타겟이 바뀔 때 발동(트래커 index, colorIndex / -1 = 없음). Client의 TrapPlayerTracker가 구독해 회전·인디케이터에 반영.</summary>
    public event Action<int, int> OnTrackerTargetChanged;

    /// <summary>MemoryRoundObjective 구역 클리어 수가 바뀔 때 발동. 전 머신 공통 구독점.</summary>
    public event Action<int> OnMemorySectionsClearedChanged;

    /// <summary>
    /// 열린 문 색이 바뀔 때 발동(PlayerColorType의 int, -1 = 전부 닫힘). 전 머신 공통 구독점.
    /// 게이트 컨트롤러가 구독해 색별 문 묶음의 Open()/Close()를 재생한다.
    /// </summary>
    public event Action<int> OnOpenGateColorChanged;

    /// <summary>
    /// T.Stage5 러너가 확정될 때 발동. 전 머신 공통 구독점 — 러너 마커·발판 제어가 구독한다.
    /// </summary>
    public event Action<ulong> OnT5RunnerChanged;

    /// <summary>
    /// T.Stage5 남은 시간이 갱신될 때 Client에서 발동 — Host는 자기 Tick으로 직접 갱신한다.
    /// T5RunnerObjective.Tick()이 Host 레인에서만 시간을 진행하므로(§11A "Progress는 Host 레인 하나")
    /// Client는 이 신호로만 타이머 UI를 갱신한다.
    /// </summary>
    public event Action<float> OnT5RemainingSync;

    /// <summary>
    /// §11 실패 문으로 재진입이 확정된 순간(Host 레인, BeginStageResetOnServer 진입 시) 1회 발동.
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

    // ── 쓰기 가드 (프리스폰 vs Despawn 구분) ──────────────────────
    //
    // [!IsSpawned 하나로 뭉뚱그리면 안 되는 이유]
    // · 프리스폰 쓰기는 **정상 경로**다 — 스폰 전에 NV/NetworkList에 넣은 값은 NGO가 스폰
    //   페이로드에 실어 Client에 그대로 전달한다. InitDoorSlots/InitPioneerTiles처럼 초기
    //   셋업에서 슬롯을 채우는 호출이 여기 해당하므로, !IsSpawned로 막으면 그 초기화가 조용히
    //   사라져 Client가 빈 슬롯으로 시작한다.
    // · Despawn 이후 쓰기는 **버그**다 — 값이 로컬에만 적용되고(Host에서 OnValueChanged까지
    //   발동) 전송은 안 돼 Host/Client가 조용히 갈라지며, NGO는 "written to during/after
    //   shutdown" 경고를 남긴다.
    // · Instance는 OnNetworkDespawn에서 null이 되지만 호출부 다수가 `_netState`로 참조를
    //   캐시하므로(FloorManager·PioneerPathManager·챌린지들·StagePressurePadSetup 등)
    //   Instance null 처리만으로는 이 창구가 막히지 않는다 — 그래서 여기서 막는다.
    //
    // NV 쓰기 → IsDespawned로 막고, Rpc 송신 → 스폰이 전제라 !IsSpawned로 막는다.
    bool _wasSpawned;
    bool IsDespawned => _wasSpawned && !IsSpawned;

    public override void OnNetworkSpawn()
    {
        _wasSpawned = true;
        _phaseStartSignal.OnValueChanged += OnPhaseChanged;
        _challengeStep.OnValueChanged    += OnChallengeStepChangedNv;
        _floorRoll.OnValueChanged        += OnFloorRollChangedNv;
        _bossPhasesCleared.OnValueChanged += OnBossPhasesClearedNv;
        _bossSphereDescent.OnValueChanged += OnBossSphereDescentNv;
        _doorOpenStates.OnListChanged    += OnDoorOpenStatesChanged;
        _pioneerTileUnlocked.OnListChanged += OnPioneerTileUnlockedChanged;
        _trackerTargets.OnListChanged    += OnTrackerTargetsChanged;
        _memorySectionsCleared.OnValueChanged += OnMemorySectionsClearedNv;
        _openGateColor.OnValueChanged    += OnOpenGateColorNv;
        _t5Runner.OnValueChanged         += OnT5RunnerNv;
        BeginSceneReadyGate();
        // [버그 수정 2026-07-20] Survive Phase 오브젝트가 이전 Phase에서는 비활성 상태로
        // 시작하는 씬(예: M.Stage2 "Stage2.1" 컨테이너)에서는 기본 검색(비활성 제외)이
        // OnNetworkSpawn 시점에 null을 캐시해버려 Client의 생존 타이머 UI가 갱신되지 않았음.
        // 비활성 포함 검색으로 Phase 활성화 여부와 무관하게 항상 찾도록 수정.
        _surviveObjective = FindFirstObjectByType<SurviveTimeObjective>(FindObjectsInactive.Include);

        if (IsServer)
        {
            PlayerSpawnCoordinator.OnPlayersReady += InitTeamLives;
            if (PlayerSpawnCoordinator.IsReady) InitTeamLives(); // 늦은 스폰 대비(OnPlayersReady 재발행 안 함, §11.4와 동일 이유)
        }
    }

    public override void OnNetworkDespawn()
    {
        _phaseStartSignal.OnValueChanged -= OnPhaseChanged;
        _challengeStep.OnValueChanged    -= OnChallengeStepChangedNv;
        _floorRoll.OnValueChanged        -= OnFloorRollChangedNv;
        _bossPhasesCleared.OnValueChanged -= OnBossPhasesClearedNv;
        _bossSphereDescent.OnValueChanged -= OnBossSphereDescentNv;
        _doorOpenStates.OnListChanged    -= OnDoorOpenStatesChanged;
        _pioneerTileUnlocked.OnListChanged -= OnPioneerTileUnlockedChanged;
        _trackerTargets.OnListChanged    -= OnTrackerTargetsChanged;
        _memorySectionsCleared.OnValueChanged -= OnMemorySectionsClearedNv;
        _openGateColor.OnValueChanged    -= OnOpenGateColorNv;
        _t5Runner.OnValueChanged         -= OnT5RunnerNv;
        PlayerSpawnCoordinator.OnPlayersReady -= InitTeamLives;
        EndSceneReadyGate();
        if (Instance == this) Instance = null;
    }

    // ── 팀 공유 목숨 ──────────────────────────────────────────────

    /// <summary>Host 전용: 파티 인원−1로 1회 확정(ReviveSystemDesign.md §4). 명단이 아직 비어 있으면 보류.</summary>
    void InitTeamLives()
    {
        if (!IsServer || IsDespawned || _teamLivesRemaining.Value >= 0) return;
        int entries = PlayerSpawnCoordinator.EntryCount;
        if (entries <= 0) return;
        _teamLivesRemaining.Value = Mathf.Max(0, Mathf.Min(entries, 4) - 1);
    }

    /// <summary>남은 팀 목숨. -1 = 미초기화. 씬 단위로만 초기화된다(한 씬 안의 서브 스테이지끼리는 이어짐).</summary>
    public int TeamLivesRemaining => _teamLivesRemaining.Value;

    /// <summary>
    /// Host 전용: **사망 순간** 목숨 1개 소모 시도(§4). 남아 있으면 1 깎고 true, 0이면 아무것도 하지
    /// 않고 false — false가 곧 "이 죽음이 스테이지 실패"라는 뜻이다(§2).
    /// 판정과 소모가 한 호출이라 "확인했는데 그 사이에 바뀌는" 창이 없다(구 AreTeamLivesExhausted +
    /// ConsumeTeamLife 2단 구조를 대체).
    /// </summary>
    public bool TryConsumeTeamLife()
    {
        if (!IsServer || IsDespawned) return false;
        InitTeamLives();
        if (_teamLivesRemaining.Value <= 0) return false;
        _teamLivesRemaining.Value--;
        return true;
    }

    // ── 스테이지 실패 / 리셋 처리 ─────────────────────────────────

    /// <summary>
    /// ESC Reset(EscMenuController.OnClickReset) 전용 문 — Host/Client 누구나 여기로 요청한다.
    ///
    /// [개명 2026-09-19 — 구 NotifyPlayerDeathServerRpc] 자동 부활 도입으로 **사망은 더 이상
    /// 리로드를 뜻하지 않는다**(ReviveSystemDesign.md §6). 이 문은 이제 "사망 문"이 아니라
    /// "실패·리셋 문"이며, 실제 실패 판정(목숨 0 사망 / 생존자 0 / objective 실패)은 Host가
    /// FailStageFromServer로 직접 들어온다.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void NotifyStageResetServerRpc() => BeginStageResetOnServer("리셋 요청");

    /// <summary>
    /// Host 전용 실패 진입점(§6·§9.2). objective 실패·팀 목숨 소진·생존자 0이 전부 여기로 모인다.
    /// 구 구조는 "전원 즉사 → 사망 문"이었는데, 자동 부활이면 즉사시킨 전원이 곧바로 살아나고
    /// 스테이지 실패가 조용히 무시된다 — 그래서 실패를 사망에 얹지 않고 직접 통보한다.
    /// </summary>
    public void FailStageFromServer(string reason)
    {
        if (!IsServer) return;
        BeginStageResetOnServer(reason);
    }

    /// <summary>
    /// Host 레인 공통 본문. 씬당 첫 요청만 반영(_resetPending) — 리로드로 이 인스턴스가 새로 생기면 다시 받는다.
    /// </summary>
    void BeginStageResetOnServer(string reason)
    {
        // 클리어 전환이 이미 시작됐으면 실패·ESC Reset 리로드를 무시한다(2026-09-08 리뷰).
        // 안 그러면 "현재 씬 리로드"(여기)와 "다음 씬 로드"(SceneFlowManager.TransitionTo)가 동시에
        // NGO SceneManager.LoadScene을 호출해, 로드 진행 중 에러 또는 방금 클리어한 스테이지로
        // 되돌아가는 사고가 난다. 클리어 후 배너 대기(clearToTransitionDelay) 동안 누가 낙사하거나
        // ESC Reset을 누르는 경로가 실제로 존재한다.
        if (SceneFlowManager.Instance != null && SceneFlowManager.Instance.IsTransitioning) return;

        if (_resetPending) return;
        _resetPending = true;

        Debug.Log($"[StageNetworkState] 스테이지 실패·리셋 확정 — {reason}");

        // §11 실패 문 진입 확정 — 챌린지 Progress 루프를 도는 모든 매니저에 즉시 통지해서
        // 리로드 코루틴(아래)이 실제로 씬을 갈아엎기 전에 각자 자기 루프를 멈추게 한다.
        // 진행 중인 부활 예약도 여기서 막힌다 — PlayerReviveState가 IsStageFailing을 보고
        // 예약을 발동시키지 않는다(§9.4, 배너 2초 안에 부활이 먼저 터지는 것 방지).
        OnDeathReloadStarted?.Invoke();

        // 이 메서드가 실패→리로드의 유일한 진입점이므로 여기 한 곳에서만 STAGE FAILED를 울리면
        // 원인(objective 실패·팀 목숨 소진·생존자 0·ESC Reset)과 무관하게 항상 뜬다(§6/§9.2).
        NotifyStageFailed();

        // 리로드 시 새 시드 생성 + 전체 클라이언트에 배포
        int newSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        NetworkSessionData.Seed = newSeed;
        BroadcastNewSeedClientRpc(newSeed);

        StartCoroutine(ReloadAfterDeathAnim());
    }

    IEnumerator ReloadAfterDeathAnim()
    {
        yield return new WaitForSeconds(deathReloadDelay);

        // 이 코루틴은 Rpc(SendTo.Server)로 진입한 Host 레인에서만 돈다 — Client는 이 코드를 안 타므로
        // 여기서 명시적으로 Client에도 암전 시작을 알려야 한다(안 그러면 Client는 컷처럼 보임).
        BroadcastBeginDeathCoverClientRpc();

        // STAGE FAILED 배너 이후 실제 리로드 순간의 컷을 완화 — 최소 유지시간 + 새 씬 동기화 확정
        // (OnPlayersReady) 이후 자동 페이드인.
        if (LoadingCurtain.Instance != null)
            yield return StartCoroutine(LoadingCurtain.Instance.BeginCoverRoutine(waitForPlayersReady: true));

        string sceneName = SceneManager.GetActiveScene().name;
        Debug.Log($"[StageNetworkState] 사망 감지 — '{sceneName}' 리로드 (새 시드: {NetworkSessionData.Seed})");
        NetworkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    /// <summary>Host는 위에서 로컬로 이미 덮었으므로 자기 자신은 제외하고 Client에게만 적용.</summary>
    [ClientRpc]
    void BroadcastBeginDeathCoverClientRpc()
    {
        if (IsServer) return;
        LoadingCurtain.Instance?.BeginCover(waitForPlayersReady: true);
    }

    [ClientRpc]
    void BroadcastNewSeedClientRpc(int seed)
    {
        NetworkSessionData.Seed = seed;
    }

    // ── 스테이지 클리어 배너 동기화 (연출 전용, §11A.0 Host 레인 브릿지) ──

    /// <summary>
    /// 스테이지(Phase) 내 목표 세트가 클리어될 때마다(중간 Phase 포함) 발동 — 배너 연출 전용 신호.
    /// StageManager.Update()의 클리어 판정은 Host 레인에서만 실행되므로(§11A.0), 이 브릿지 없이
    /// Client UI가 StageManager.OnStageClear를 직접 구독하면 MemoryRoundObjective와 동일한 계열의
    /// 버그(Client에서는 절대 발동하지 않음)가 재발한다. ChallengeCleared와 동일한
    /// "Host 로컬 즉시 + ClientRpc 보장 전달" 패턴.
    /// </summary>
    public event Action OnAnyStageClearedPulse;

    /// <summary>Host: StageManager.Update() 클리어 판정 직후 호출.</summary>
    public void NotifyStageCleared()
    {
        if (!IsServer || !IsSpawned) return;
        OnAnyStageClearedPulse?.Invoke();
        NotifyStageClearedClientRpc();
    }

    [ClientRpc]
    void NotifyStageClearedClientRpc()
    {
        if (IsServer) return;
        OnAnyStageClearedPulse?.Invoke();
    }

    /// <summary>
    /// 스테이지 실패 시 배너 연출 전용 신호. STAGE CLEAR와 대칭(ReviveSystemDesign.md §6).
    /// BeginStageResetOnServer 내부에서만 호출 — 실패→리로드 진입점이 하나이므로 별도
    /// IsServer/IsSpawned 가드 없이 그 호출부의 가드에 편승한다.
    /// </summary>
    public event Action OnAnyStageFailedPulse;

    void NotifyStageFailed()
    {
        OnAnyStageFailedPulse?.Invoke();
        NotifyStageFailedClientRpc();
    }

    [ClientRpc]
    void NotifyStageFailedClientRpc()
    {
        if (IsServer) return;
        OnAnyStageFailedPulse?.Invoke();
    }

    /// <summary>
    /// 씬 전체가 끝나 다음 씬으로 넘어가는 클리어(SceneFlowManager.LoadNextScene() 진입 시)에서만
    /// 호출 — OnAnyStageClearedPulse(중간 Phase 클리어 배너 펄스, 이 방 저 방마다 울림)와는 별개
    /// 신호다. 씬의 모든 함정을 멈추는 신호이므로 중간 방 클리어에 얹으면 아직 진행 중인 다른 방
    /// 함정까지 멈추는 사고가 난다 — 반드시 이 전용 신호로만 호출할 것.
    ///
    /// [전파 전용] Host 자신의 정지는 호출부(SceneFlowManager.LoadNextScene)가 로컬에서 이미
    /// 끝냈다. 여기서 Host 로컬 정지까지 같이 하던 예전 구조는 아래 !IsSpawned 가드에 걸릴 때
    /// Host 정지마저 스킵되는 구멍이 있었다(2026-09-08 리뷰).
    /// </summary>
    public void BroadcastStageClearFreezeToClients()
    {
        if (!IsServer || !IsSpawned) return;
        StageClearFreezeClientRpc();
    }

    [ClientRpc]
    void StageClearFreezeClientRpc()
    {
        if (IsServer) return;
        SceneFlowManager.Instance?.FreezeAllHazardsNow();
    }

    // ── 씬 전체 클리어(모든 Phase 완료) 표시 동기화 ────────────────

    /// <summary>
    /// 씬의 모든 Phase가 완료됐을 때 발동 — **표시 전용, Client 레인에서만** 발동하는 펄스.
    /// PhaseManager.onAllPhasesComplete는 Host 레인에서만 Invoke되고 EnterPhaseOnClient()는
    /// onPhaseEnter만 재생하므로, 그 UnityEvent에 인스펙터로 직결된 표시 전용 UI
    /// (ObjectiveUI.ShowSceneClear)는 Client에서 영원히 발동하지 않았다(2026-09-07 리뷰).
    /// OnAnyStageClearedPulse와 같은 골격이되 Host 로컬 Invoke가 없다 — Host는 이미 UnityEvent로
    /// 자기 몫을 처리하므로 중복 발동을 만들지 않는다. UnityEvent 자체를 Client에서 재Invoke하면
    /// 같은 이벤트에 걸린 SceneFlowRelay.LoadNextScene(커튼·진행도 기록)까지 Client 레인에서
    /// 돌아가므로 그 방식은 쓰지 않는다(§11A Host 레인 단일 진실).
    /// </summary>
    public event Action OnAllPhasesCompleteClientPulse;

    /// <summary>
    /// Host: PhaseManager가 onAllPhasesComplete Invoke 직후 호출.
    /// IsSpawned 가드 — 사망 리로드/씬 언로드 중 Despawn 이후에 Phase 완료가 들어오면
    /// 미스폰 객체에서 Rpc를 보내 에러가 난다(SetTrackerTarget과 동일 방어).
    /// </summary>
    public void NotifyAllPhasesComplete()
    {
        if (!IsServer || !IsSpawned) return;
        NotifyAllPhasesCompleteClientRpc();
    }

    [ClientRpc]
    void NotifyAllPhasesCompleteClientRpc()
    {
        if (IsServer) return;
        OnAllPhasesCompleteClientPulse?.Invoke();
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

    /// <summary>
    /// Host: Breakable이 파괴 지연(breakDelay)에 들어가는 순간 호출 — Client도 같은 순간부터
    /// 경고색(탠저린→진홍) 보간을 시작하게 한다. breakDelay 자체는 씬에 저장된 동일 직렬화
    /// 값이라 지속시간을 실어보낼 필요 없이 "지금 시작" 트리거만 보낸다.
    /// 실제 파괴 확정은 이 RPC와 별개로 SyncBreakClientRpc가 담당한다.
    /// </summary>
    [ClientRpc]
    public void SyncBreakPendingClientRpc(int breakableId)
    {
        if (IsServer) return;
        Breakable.BreakPendingById(breakableId);
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

    /// <summary>
    /// Host: DropCycle이 실제 낙하체를 생성하는 시점(경고 종료 후)에 호출.
    /// Client: 동일 DropTrap 인스턴스의 낙하 시작음(fireSfxId)만 재생 — 낙하체 스폰은 B안으로
    /// 별도 동기화되므로 여기서는 순수 사운드만 다룬다.
    /// </summary>
    [ClientRpc]
    public void SyncDropFireClientRpc(int trapId)
    {
        if (IsServer) return;
        DropTrap.PlayFireSfxById(trapId);
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

    /// <summary>
    /// Host: ArrowTrap.OnChargeCancelled(충전 중 Deactivate/Freeze) 발행 시점에 호출.
    /// Client: 경고 사인 숨김 + Mouth 닫기. 이게 없으면 Fire RPC가 영영 안 와 연출이 켜진 채 남는다.
    /// </summary>
    [ClientRpc]
    public void SyncArrowCancelClientRpc(int trapId)
    {
        if (IsServer) return;
        ArrowTrap.PlayCancelById(trapId);
    }

    // ── 함정 조준 타겟 동기화 (TrapPlayerTracker 슬롯, Door §3.1과 동일 원칙) ──────

    /// <summary>
    /// Host: 씬의 TrapPlayerTracker 개수만큼 슬롯을 초기화(전부 -1 = 타겟 없음).
    /// 트래커가 Player 캐시 갱신 시 멱등 호출 — 개수가 이미 맞으면 아무것도 하지 않는다
    /// (index가 트래커마다 고정 배정이라 배정 후 개수 변경은 index 오염, InitDoorSlots와 동일 원칙).
    /// </summary>
    public void InitTrackerTargetSlots(int count)
    {
        if (!IsServer || !IsSpawned) return;
        if (_trackerTargets.Count == count) return;
        _trackerTargets.Clear();
        for (int i = 0; i < count; i++)
            _trackerTargets.Add(-1);
    }

    /// <summary>
    /// Host: 트래커 index의 조준 타겟 갱신. TrapPlayerTracker.SetCurrentTarget()에서 호출.
    /// colorIndex = -1 → 타겟 없음. 씬 언로드/사망 리로드 중 Despawn 이후 호출될 수 있어 IsSpawned 가드 필수.
    /// </summary>
    public void SetTrackerTarget(int index, int colorIndex)
    {
        if (!IsServer || !IsSpawned) return;
        if (index < 0 || index >= _trackerTargets.Count) return;
        if (_trackerTargets[index] == colorIndex) return;
        _trackerTargets[index] = colorIndex;
    }

    void OnTrackerTargetsChanged(NetworkListEvent<int> change)
    {
        switch (change.Type)
        {
            case NetworkListEvent<int>.EventType.Add:
            case NetworkListEvent<int>.EventType.Insert:
            case NetworkListEvent<int>.EventType.Value:
                OnTrackerTargetChanged?.Invoke(change.Index, change.Value);
                break;
        }
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
        if (!IsServer || IsDespawned) return;
        _countdownStartServerTime.Value = NetworkManager.Singleton.ServerTime.Time;
        _isCountdownActive.Value = true;
    }

    /// <summary>Host: 이탈로 카운트다운 리셋 시 호출.</summary>
    public void MarkCountdownReset()
    {
        if (!IsServer || IsDespawned) return;
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
        if (!IsServer || IsDespawned) return;
        _stageStartSignal.Value  = new StageStartSignal { serverTime = NetworkManager.Singleton.ServerTime.Time, gateId = gateId };
        _isCountdownActive.Value = false;
    }

    // ── Phase 동기화 ──────────────────────────────────────────────

    /// <summary>
    /// Host: PhaseManager.EnterPhase() 진입 시 Phase 인덱스 + 그 시각의 서버 시간을 원자적으로
    /// 같이 기록 + 전파(PhaseStartSignal). ArrowTrap/DropTrap/WindTrap 등이 이 Phase에서
    /// Activate()될 때 스케줄 앵커로 PhaseStartServerTime을 사용.
    /// StageStartServerTime(별도 슬롯)과 무관 — StageStartGate의 1회성 신호를 건드리지 않는다.
    /// [버그 수정 2026-08] 예전엔 MarkPhaseStart()(시간)와 SyncPhase()(인덱스) 두 메서드로 나눠
    /// 별도 NV 2개에 썼는데, Client 도착 순서가 보장 안 돼 WindTrap 등의 트랩이 Client에서만
    /// Host와 다른(직전 Phase의) 시각을 스케줄 앵커로 잡는 버그가 있었다. 반드시
    /// phase.onPhaseEnter?.Invoke() 이전에 호출할 것 — 그 안에서 트랩이 Activate()될 때 이미
    /// 갱신된 앵커를 읽어야 한다(PhaseManager.EnterPhase() 참고).
    /// </summary>
    public void MarkAndSyncPhase(int phaseIndex)
    {
        if (!IsServer || IsDespawned) return;
        _phaseStartSignal.Value = new PhaseStartSignal
        {
            phaseIndex = phaseIndex,
            serverTime = NetworkManager.Singleton.ServerTime.Time
        };
    }

    void OnPhaseChanged(PhaseStartSignal prev, PhaseStartSignal next)
    {
        // 비오너(Client)에서도 Phase 변경을 받을 수 있도록 PhaseManager에 알림
        if (!IsServer && PhaseManager.Instance != null)
            PhaseManager.Instance.EnterPhaseOnClient(next.phaseIndex);

        Debug.Log($"[StageNetworkState] Phase 변경: {prev.phaseIndex} → {next.phaseIndex}");
    }

    // ── 보스 진행 동기화 (D축) ──────────────────────────────────────

    /// <summary>Host: 보스 페이즈 클리어 수 갱신. BossFightObjective.NotifyPhaseCleared()에서 호출.</summary>
    public void SetBossPhasesCleared(int cleared)
    {
        if (!IsServer || IsDespawned) return;
        _bossPhasesCleared.Value = cleared;
    }

    void OnBossPhasesClearedNv(int prev, int next) => OnBossPhasesClearedChanged?.Invoke(next);

    // ── T.Boss Sphere 하강 (BossSpherePhaseDriver 전용) ────────────

    /// <summary>Host: Sphere 하강 상태 확정. BossSpherePhaseDriver만 호출(칸 진입 시).</summary>
    public void SetBossSphereDescent(int checkpointIndex, double descentStartServerTime)
    {
        if (!IsServer || IsDespawned) return;
        _bossSphereDescent.Value = new BossSphereDescentState
        {
            checkpointIndex        = checkpointIndex,
            descentStartServerTime = descentStartServerTime,
        };
    }

    void OnBossSphereDescentNv(BossSphereDescentState prev, BossSphereDescentState next) =>
        OnBossSphereDescentChanged?.Invoke(next);

    // ── 문(Door) 개폐 동기화 (Door 전용 슬롯) ──────────────────────

    /// <summary>
    /// Host: 씬의 문 개수만큼 슬롯을 초기화(전부 닫힘). StagePressurePadSetup이 OnPlayersReady
    /// 이후 index 배정을 마친 뒤 1회 호출 — 이후 Add/RemoveAt으로 개수를 바꾸지 않는다
    /// (index가 문마다 고정 배정이라 배정 후 개수 변경은 index 오염으로 이어짐).
    /// </summary>
    public void InitDoorSlots(int count)
    {
        if (!IsServer || IsDespawned) return;
        _doorOpenStates.Clear();
        for (int i = 0; i < count; i++)
            _doorOpenStates.Add(false);
    }

    /// <summary>Host: 문 index의 개폐 상태 갱신. DoorController.OnOpened/OnClosed에서 호출.</summary>
    public void SetDoorOpen(int index, bool isOpen)
    {
        if (!IsServer || IsDespawned) return;
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

    // ── Pioneer Path 타일 해금 동기화 (T.Stage2 전용 슬롯) ─────────

    /// <summary>
    /// Host: 씬의 path 타일 개수만큼 슬롯을 초기화(전부 미해금). PioneerPathManager가
    /// Start()에서 index 배정을 마친 뒤 1회 호출 — 이후 개수를 바꾸지 않는다(index 오염 방지,
    /// InitDoorSlots와 동일 원칙).
    /// </summary>
    public void InitPioneerTiles(int count)
    {
        if (!IsServer || IsDespawned) return;
        _pioneerTileUnlocked.Clear();
        for (int i = 0; i < count; i++)
            _pioneerTileUnlocked.Add(false);
    }

    /// <summary>Host: index 타일 해금 확정. PioneerPathTile.OnCollisionEnter에서 호출(비-Host 호출은 no-op).</summary>
    public void SetPioneerTileUnlocked(int index)
    {
        if (!IsServer || IsDespawned) return;
        if (index < 0 || index >= _pioneerTileUnlocked.Count) return;
        if (_pioneerTileUnlocked[index]) return;
        _pioneerTileUnlocked[index] = true;
    }

    void OnPioneerTileUnlockedChanged(NetworkListEvent<bool> change)
    {
        switch (change.Type)
        {
            case NetworkListEvent<bool>.EventType.Add:
            case NetworkListEvent<bool>.EventType.Insert:
            case NetworkListEvent<bool>.EventType.Value:
                if (change.Value) OnPioneerTileUnlocked?.Invoke(change.Index);
                break;
        }
    }

    // ── MemoryRoundObjective 구역 클리어 동기화 (T.Stage2 전용 슬롯) ──

    /// <summary>Host: 클리어된 구역 수 갱신. MemoryRoundObjective.HandleClear()에서 호출.</summary>
    public void SetMemorySectionsCleared(int cleared)
    {
        if (!IsServer || IsDespawned) return;
        _memorySectionsCleared.Value = cleared;
    }

    void OnMemorySectionsClearedNv(int prev, int next) => OnMemorySectionsClearedChanged?.Invoke(next);

    // ── T.Stage5 색 게이트 / 러너 (T5 전용 슬롯) ─────────────────

    /// <summary>
    /// Host: 열린 문 색 확정. 색 게이트 컨트롤러에서만 호출.
    /// color = PlayerColorType의 int 캐스팅, -1 = 전부 닫힘(스테이지 시작 상태).
    /// </summary>
    public void SetOpenGateColor(int color)
    {
        if (!IsServer || IsDespawned) return;
        _openGateColor.Value = color;
    }

    /// <summary>Host: 열린 문 색 확정(타입 오버로드).</summary>
    public void SetOpenGateColor(PlayerColorType color) => SetOpenGateColor((int)color);

    /// <summary>Host: 모든 문을 닫힌 상태로 — 스테이지 시작·리셋 시 호출.</summary>
    public void CloseAllGates() => SetOpenGateColor(-1);

    /// <summary>
    /// Host: 이번 판의 러너 확정. T5RunnerDirector가 OnPlayersReady 직후 1회 호출.
    /// 맵 인덱스는 여기로 오지 않는다 — 시드 파생이라 NV가 필요 없다(_t5Runner 주석).
    /// </summary>
    public void SetT5Runner(ulong runnerClientId)
    {
        if (!IsServer || IsDespawned) return;
        _t5Runner.Value = runnerClientId;
    }

    void OnOpenGateColorNv(int prev, int next) => OnOpenGateColorChanged?.Invoke(next);
    void OnT5RunnerNv(ulong prev, ulong next)  => OnT5RunnerChanged?.Invoke(next);

    /// <summary>
    /// Host: T.Stage5 남은 시간을 Client HUD에 브로드캐스트.
    /// SyncSurvivalRemainingClientRpc와 동일한 "Host tick + 주기 Rpc" 패턴이고,
    /// 챌린지 축(ChallengeStepState)과는 무관한 T5 전용 독립 채널이다.
    /// </summary>
    [ClientRpc]
    public void SyncT5RemainingClientRpc(float remaining)
    {
        if (IsServer) return;
        OnT5RemainingSync?.Invoke(remaining);
    }

    // ── 챌린지 라운드 동기화 (축 #4 공통) ─────────────────────────

    /// <summary>
    /// Host: 챌린지 새 라운드 시작(트리거 진입 등). 시드를 배포해 전 머신이
    /// 동일한 로컬 생성 코드(셔플·배치)를 재실행하게 한다 — 결과 자체는 전송하지 않음.
    /// seed·stepIndex·owner를 한 NV 쓰기로 같이 보내 Client에서 항상 원자적으로 도착하게 한다.
    /// owner는 호출한 챌린지 자신의 타입 — 각 매니저의 핸들러가 "내 것이 아니면 무시"를
    /// 판단하는 유일한 근거이므로 반드시 자기 자신의 타입을 넘겨야 한다.
    /// </summary>
    public void ChallengeStart(int seed, ChallengeOwnerType owner, int instanceId = 0)
    {
        if (!IsServer || IsDespawned) return;
        _challengeStep.Value    = new ChallengeStepState { seed = seed, stepIndex = -1, stepStartServerTime = -1.0, owner = owner, instanceId = instanceId };
        _challengeCleared.Value = false;
    }

    /// <summary>
    /// Host: 문제/라운드 스텝 시작. 시드는 유지. 시드까지 바꾸려면 ChallengeStepBegin(stepIndex, seed).
    /// </summary>
    public void ChallengeStepBegin(int stepIndex)
    {
        ChallengeStepBegin(stepIndex, _challengeStep.Value.seed);
    }

    /// <summary>
    /// Host: 스텝과 시드를 한 NV 쓰기로 같이 보낸다. ColorTile 점수제 재스폰(슬롯+스폰 인덱스)에 사용.
    /// 기존 챌린지는 ChallengeStepBegin(stepIndex)만 쓰면 시드는 유지된다.
    /// </summary>
    public void ChallengeStepBegin(int stepIndex, int seed)
    {
        if (!IsServer || IsDespawned) return;
        _challengeStep.Value = new ChallengeStepState
        {
            seed                 = seed,
            stepIndex            = stepIndex,
            stepStartServerTime  = NetworkManager.Singleton.ServerTime.Time,
            owner                = _challengeStep.Value.owner,
            instanceId           = _challengeStep.Value.instanceId,
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
        if (!IsServer || IsDespawned) return;
        _challengeStep.Value = new ChallengeStepState
        {
            seed                 = _challengeStep.Value.seed,
            stepIndex            = -1,
            stepStartServerTime  = -1.0,
            owner                = _challengeStep.Value.owner,
            instanceId           = _challengeStep.Value.instanceId,
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
        if (!IsServer || !IsSpawned) return;
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
    ///
    /// [버그 수정 2026-08] instanceId 파라미터 추가 — 같은 owner 타입의 챌린지가 씬에 여러
    /// 인스턴스 있을 때(T.Boss ColorTileChallenge 5개), 예전엔 이 RPC가 Client에서 실제로
    /// OnChallengeOutcome을 발동시키면 owner 타입만 같으면 형제 인스턴스 전부가 반응해
    /// 각자의 페널티를 중복 적용했다(Host는 ResolveRound가 자기 자신만 직접 호출하므로 이
    /// 이벤트 경로 자체를 안 타 우연히 정상으로 보였음). instanceId를 실어보내 구독자가
    /// "내 인스턴스로 온 결과인지" 직접 걸러낼 수 있게 한다 — 기본값 0은 인스턴스가 하나뿐인
    /// 기존 챌린지 타입(OX/GridColor/GridBW/SequenceRing)의 하위호환용.
    /// </summary>
    [ClientRpc]
    public void NotifyChallengeOutcomeClientRpc(bool success, int instanceId = 0)
    {
        if (IsServer) return;
        LastChallengeOutcomeInstanceId = instanceId;
        OnChallengeOutcome?.Invoke(success);
    }

    /// <summary>
    /// Host: 스텝 단위 정답/오답 1회성 연출 신호(SFX 등). ChallengeCleared와 동일한 이유로
    /// RPC 보장 전달 — "오답"은 스텝 인덱스가 그대로라 NV(ChallengeStepState) 변경만으로는
    /// Client가 알 방법이 없다(2026-09-01, SequenceRing OnCorrectInput/OnWrongInput이 Host
    /// 전용 호출 경로인 TrySubmit/TrySubmitAnyKey 안에서만 발동돼 Client가 전혀 못 듣던 문제 수정).
    /// </summary>
    public void NotifyChallengeStepResult(bool correct)
    {
        if (!IsServer || !IsSpawned) return;
        OnChallengeStepResult?.Invoke(correct); // Host 로컬 즉시 발동
        NotifyChallengeStepResultClientRpc(correct);
    }

    /// <summary>Client 전용 — 위 NotifyChallengeStepResult(true/false)가 보장 전달하는 1회성 신호.</summary>
    [ClientRpc]
    void NotifyChallengeStepResultClientRpc(bool correct)
    {
        if (IsServer) return;
        OnChallengeStepResult?.Invoke(correct);
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


    // ── Floor 타일 롤 동기화 (Floor 전용) ──────────────────────────

    /// <summary>
    /// Host: 새 타일 롤 배포. 시드 하나만 보내 전 머신이 로컬로 동일 결과를 재생성하게 한다
    /// (byte[] 상태 배열 전체를 매번 보내던 기존 SyncTilesClientRpc 방식 폐기).
    /// keepBWRatio를 같이 실어보내 Client가 Phase 진행을 독자 계산할 필요가 없게 한다.
    /// Floor는 성공/실패 판정이 없으므로 Judge/Resolve 단계 없이 Generate만 반복한다.
    /// </summary>
    public void FloorRoll(int seed, float keepBWRatio)
    {
        if (!IsServer || IsDespawned) return;
        _floorRoll.Value = new FloorRollState { seed = seed, keepBWRatio = keepBWRatio };
    }

    void OnFloorRollChangedNv(FloorRollState prev, FloorRollState next) => OnFloorRollChanged?.Invoke(next);

    // ── 챌린지 입력 제출 (Client → Host, §11B.1) ───────────────────

    // [버그 수정 2026-09-01 / 2026-09-05] 챌린지 제출은 비멱등이다 — 판정이 곧 스텝 진행
    // (ChallengeStepBegin)이라, 한 입력이 두 번 판정되면 첫 판정이 올린 스텝을 두 번째가 또
    // 판정해 한 번에 두 칸이 소비된다(실기에서 "칸이 2번 눌리는" 증상). 처음엔 같은 프레임
    // 2번째 수신을 버리는 가드를 뒀지만 중복이 다른 틱에 도착하면 통과해서, 발신 측이 붙이는
    // 단조 증가 번호 기준으로 바꿨다(RpcSubmitDedup 주석 참고). 색/AnyKey는 한 입력에 둘 중
    // 하나만 오므로 같은 번호 계열을 공유한다.
    private readonly RpcSubmitDedup _challengeSubmitDedup = new();

    /// <summary>
    /// Client: 자기 색으로 챌린지 스텝 제출(예: SequenceRing 키 입력). 제출 번호 발급이 이 채널의
    /// 책임이므로 호출부(SequenceRingMinigame)는 이 메서드만 쓰고 RPC를 직접 부르지 않는다 —
    /// 번호를 링마다 따로 세면 Phase 전환으로 링이 바뀔 때 번호가 되돌아가 전부 중복 처리된다.
    /// </summary>
    public void SubmitChallengeStep(PlayerColorType color)
    {
        if (!IsSpawned) return; // Despawn 이후 입력은 제출 번호도 소비하지 않는다(중복 판정 방지)
        SubmitStepServerRpc(color, _challengeSubmitDedup.NextSeq());
    }

    /// <summary>Client: Common/Danger 스텝 등 색 구분 없는 "아무 키" 제출.</summary>
    public void SubmitChallengeAnyKeyStep()
    {
        if (!IsSpawned) return;
        SubmitAnyKeyStepServerRpc(_challengeSubmitDedup.NextSeq());
    }

    /// <summary>
    /// Host만 위치·색 등 실제 상태를 갖고 있는 포지션 판정형과 달리, 키 입력형은 "누가 눌렀는가"
    /// 자체가 Host에 없는 정보라 별도 제출 경로가 필요하다 — Host가
    /// SequenceRingMinigame.TrySubmit()으로 판정한다. 진입점은 SubmitChallengeStep().
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void SubmitStepServerRpc(PlayerColorType color, uint submitSeq, RpcParams rpcParams = default)
    {
        if (_challengeSubmitDedup.IsDuplicate(rpcParams.Receive.SenderClientId, submitSeq)) return;

        SequenceRingMinigame.Instance?.TrySubmit(color);
    }

    /// <summary>진입점은 SubmitChallengeAnyKeyStep().</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void SubmitAnyKeyStepServerRpc(uint submitSeq, RpcParams rpcParams = default)
    {
        if (_challengeSubmitDedup.IsDuplicate(rpcParams.Receive.SenderClientId, submitSeq)) return;

        SequenceRingMinigame.Instance?.TrySubmitAnyKey();
    }

    // ── 함정 발사체 피격/파괴 보고 (Client → Host, 상주 릴레이) ───────────
    // [버그 수정 2026-07-28] TrapProjectile은 짧게 살고 죽는 NetworkObject라, 자기 자신을
    // Rpc 대상으로 쓰면 Despawn 이후 늦게 도착한 중복 보고가 NGO 라우팅 단계에서 못 찾아져
    // "Deferred OnSpawn" 대기 → 10초 후 PurgeTrigger 경고로 이어졌다(Arrow/Drop/Boulder
    // 전부 TrapProjectile 공유라 셋 다 동일 증상). 이 오브젝트는 스테이지 내내 살아있으므로
    // 라우팅 실패가 구조적으로 없다 — "이미 처리됨"은 아래 TryGetValue 가드 하나로 끝낸다.

    /// <summary>Client(전원): 발사체 피격 보고. Host가 발사체를 찾아 데미지+Despawn을 위임.
    /// 보고자는 피격 플레이어의 Owner여야 한다(§9.0.1-d).</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ReportTrapHitServerRpc(ulong projectileNetId, ulong playerNetId, RpcParams rpcParams = default)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(projectileNetId, out var projNetObj))
            return; // 이미 처리(다른 보고로 Despawn)됨 — 조용히 무시

        // 남의 캐릭터 피격은 받지 않는다 — 발사체 깨짐(Breakable)이 머신별 로컬 판정이라, 다른 머신
        // 화면에서만 살아 있던 발사체로 데미지가 들어가던 버그의 Host 쪽 가드.
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(playerNetId, out var playerNetObj))
            return;
        if (playerNetObj.OwnerClientId != rpcParams.Receive.SenderClientId) return;

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

    // ── 씬 준비 취합 + 커튼 게이트 (전 씬 공통, 2026-09-18) ───────────────
    //
    // [무엇을 푸는가]
    //  커튼은 "몇 초 지났으니" 걷는 게 아니라 "전원이 준비됐으니" 걷어야 한다. 로컬 준비 조건은
    //  각 머신만 알기 때문에(맵 활성화·NavMesh·텔레포트 …), 각자 자기 로컬 게이트가 끝나면
    //  Host에 보고하고 Host가 전원분을 모아야 한다.
    //
    // [왜 NV 비트마스크인가 — ClientRpc 왕복이 아니라]
    //  "준비됐다"는 연속 상태이지 일회성 이벤트가 아니다(우리 규약: 연속 상태는 NV).
    //  NV라서 ① 도착이 보장되고 ② 늦게 본 머신도 최종값을 읽으며 ③ 같은 보고가 여러 번 와도
    //  비트 OR이라 결과가 같다(멱등) — 클라가 마음 놓고 재시도할 수 있다.
    //  해제용 ClientRpc도 필요 없다: 전원 비트가 켜지면 각 머신이 로컬로 커튼을 걷는다.
    //
    // [막혔을 때 — 조용한 리로드 1회]
    //  LoadingCurtain이 타임아웃을 알리면 Host가 씬을 **조용히** 다시 로드한다.
    //  사망 문(NotifyStageFailed + 새 시드 배포)을 타면 아무도 안 죽었는데 "STAGE FAILED"가 뜨고
    //  판·맵·러너가 재추첨되므로, 그 경로를 재사용하지 않고 전용 경로를 쓴다.
    //  재시도 횟수는 씬과 함께 죽으면 안 되므로 NetworkSessionData(정적)에 둔다.
    //  상한을 넘으면 포기하고 그냥 진행한다 — 무한 암전보다 낫고, 어긋난 상태라면 대개 낙사 →
    //  §11 사망 문으로 전원 리로드가 걸려 스스로 복구된다.

    /// <summary>준비 실패 시 조용한 리로드를 몇 번까지 시도할지.</summary>
    const int SceneReadyMaxRetries = 1;

    bool _sceneReadyGateActive;
    bool _sceneReadyReported;
    float _nextSceneReadyReportAt;

    void BeginSceneReadyGate()
    {
        LoadingCurtain.OnGatesTimedOut += HandleCurtainGatesTimedOut;

        var curtain = LoadingCurtain.Instance;
        if (curtain == null || !curtain.IsCovered) return;

        _sceneReadyGateActive = true;
        _sceneReadyReported   = false;
        curtain.RegisterGate(LoadingCurtain.AllPlayersReadyGate);
    }

    void EndSceneReadyGate()
    {
        LoadingCurtain.OnGatesTimedOut -= HandleCurtainGatesTimedOut;
        _sceneReadyGateActive = false;
    }

    void Update()
    {
        if (!_sceneReadyGateActive) return;

        var curtain = LoadingCurtain.Instance;
        if (curtain == null) { _sceneReadyGateActive = false; return; }

        // 내 로컬 게이트가 아직 남아 있으면 보고할 때가 아니다.
        if (!_sceneReadyReported && !curtain.HasPendingGatesOtherThan(LoadingCurtain.AllPlayersReadyGate))
        {
            if (Time.unscaledTime >= _nextSceneReadyReportAt)
            {
                _nextSceneReadyReportAt = Time.unscaledTime + 0.5f; // 응답이 없으면 재시도
                int bit = LocalReadyBitIndex();
                if (bit >= 0) ReportSceneReadyServerRpc(bit);
            }

            if (IsLocalBitSet()) _sceneReadyReported = true;
        }

        if (!IsSceneReadyMaskComplete()) return;

        _sceneReadyGateActive = false;
        NetworkSessionData.SceneReadyRetryCount = 0; // 성공했으니 재시도 카운트 초기화
        curtain.MarkGateReady(LoadingCurtain.AllPlayersReadyGate);
    }

    /// <summary>clientId 정렬 순서에서 내 자리. 명단이 아직 비었으면 -1.</summary>
    static int LocalReadyBitIndex()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return -1;

        var ids = new List<ulong>();
        foreach ((ulong clientId, PlayerColorType _) in PlayerSpawnCoordinator.GetAllEntries())
            ids.Add(clientId);
        if (ids.Count == 0) return -1;

        ids.Sort();
        int index = ids.IndexOf(nm.LocalClientId);
        return index >= 0 && index < 32 ? index : -1;
    }

    bool IsLocalBitSet()
    {
        int bit = LocalReadyBitIndex();
        return bit >= 0 && (_sceneReadyMask.Value & (1 << bit)) != 0;
    }

    bool IsSceneReadyMaskComplete()
    {
        int count = PlayerSpawnCoordinator.EntryCount;
        if (count <= 0) return false;        // 명단이 아직 없다 = 기다릴 대상도 확정 전

        int full = (1 << count) - 1;
        return (_sceneReadyMask.Value & full) == full;
    }

    /// <summary>Client(전원): 이 머신의 로컬 준비가 끝났다. 같은 보고가 여러 번 와도 결과가 같다(비트 OR).</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ReportSceneReadyServerRpc(int bitIndex)
    {
        if (!IsServer || IsDespawned) return;
        if (bitIndex < 0 || bitIndex >= 32) return;

        _sceneReadyMask.Value |= 1 << bitIndex;
    }

    /// <summary>LoadingCurtain 타임아웃 → Host만 판단한다. 상한 안이면 조용한 리로드, 넘으면 포기(그냥 진행).</summary>
    void HandleCurtainGatesTimedOut(string[] pending)
    {
        if (!IsServer || IsDespawned) return;

        if (NetworkSessionData.SceneReadyRetryCount >= SceneReadyMaxRetries)
        {
            Debug.LogWarning($"[StageNetworkState] 준비 재시도 {NetworkSessionData.SceneReadyRetryCount}회를 " +
                             "넘겨 그대로 진행합니다 — 미완: " + string.Join(", ", pending));
            NetworkSessionData.SceneReadyRetryCount = 0;
            return; // KeepCoveredForRetry를 부르지 않는다 → 커튼이 걷히고 게임이 진행된다
        }

        NetworkSessionData.SceneReadyRetryCount++;
        LoadingCurtain.Instance?.KeepCoveredForRetry();

        NetLog.Transition("StageNetworkState", "SilentReload",
            $"try={NetworkSessionData.SceneReadyRetryCount} pending={string.Join(",", pending)}");
        ReloadSceneSilently();
    }

    /// <summary>
    /// Host: 실패 배너도 새 시드도 없이 현재 씬만 다시 로드한다.
    /// 사망 리로드(NotifyPlayerDeath…)와 **의도적으로 다른 경로**다 — 아무도 죽지 않았고,
    /// 시드를 바꾸면 판·맵·러너가 재추첨돼 "같은 판을 다시 시도"가 아니게 된다.
    /// </summary>
    void ReloadSceneSilently()
    {
        if (!IsServer || IsDespawned) return;

        string sceneName = SceneManager.GetActiveScene().name;
        Debug.LogWarning($"[StageNetworkState] 준비 미완 — '{sceneName}' 조용히 리로드(재시도).");
        NetworkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    // ── T.Stage4 파괴 타일 보고 (Client → Host → 전 머신, 상주 릴레이) ────
    // BreakTile은 판 하나에 수십~백 개라 NetworkObject로 만들 수 없다. 그래서 위 TrapProjectile과
    // 같은 이유로 이 오브젝트를 릴레이로 쓴다 — 타일은 전 머신 공통 인덱스로 가리킨다
    // (BreakTileDirector가 월드 좌표 정렬로 배정). `TStage4TrapRandomization.md` §4.1.

    /// <summary>Host 레인 전용: 타일이 밟혔다는 보고. BreakTileDirector가 구독해 파괴 시각을 정한다.</summary>
    public event Action<int> OnBreakTileStepReported;

    /// <summary>전 머신: (타일 인덱스, 파괴 서버 시각). 이 값 하나로 전 머신이 같은 순간에 부서진다.</summary>
    public event Action<int, double> OnBreakTileArmed;

    /// <summary>
    /// Client(밟은 당사자의 Owner 머신): 파괴 타일을 밟았다고 보고한다.
    /// 중복·유효성 판정은 Host 레인의 BreakTileDirector가 한다 — 여기서는 순수 릴레이다.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ReportBreakTileSteppedServerRpc(int tileIndex)
    {
        OnBreakTileStepReported?.Invoke(tileIndex);
    }

    /// <summary>Host: 확정한 파괴 서버 시각을 전 머신에 배포. BreakTileDirector에서만 호출.</summary>
    public void BroadcastBreakTileArm(int tileIndex, double collapseServerTime)
    {
        if (!IsServer || IsDespawned) return;
        ArmBreakTileClientRpc(tileIndex, collapseServerTime);
    }

    /// <summary>Host 자신도 클라로서 받는다 — 파괴 경로를 전 머신 하나로 유지한다.</summary>
    [ClientRpc]
    void ArmBreakTileClientRpc(int tileIndex, double collapseServerTime)
    {
        OnBreakTileArmed?.Invoke(tileIndex, collapseServerTime);
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 스테이지 실패")]
    void Debug_Fail() => NotifyStageResetServerRpc();

    [ContextMenu("테스트: Phase 0으로 초기화")]
    void Debug_Phase0() => MarkAndSyncPhase(0);
#endif
}
