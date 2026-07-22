using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 챌린지 라운드 상태 3종(시드·스텝 인덱스·스텝 시작 서버 시간)을 하나로 묶은 값.
/// [버그 수정 2026-07-20] 세 값을 별도 NetworkVariable로 나눠두면, Client 쪽에 도착하는
/// 순서가 보장되지 않아 "스텝 인덱스는 갱신됐지만 시드는 아직 이전 값"인 순간에
/// OnValueChanged가 발동해 Host와 다른 셔플 결과가 나올 수 있었다(Host/Client 문제 불일치).
/// 하나의 NetworkVariable로 합쳐 항상 원자적으로 같이 도착하게 만든다.
/// </summary>
public struct ChallengeStepState : INetworkSerializable, IEquatable<ChallengeStepState>
{
    public int    seed;
    public int    stepIndex;
    public double stepStartServerTime;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref seed);
        serializer.SerializeValue(ref stepIndex);
        serializer.SerializeValue(ref stepStartServerTime);
    }

    public bool Equals(ChallengeStepState other) =>
        seed == other.seed &&
        stepIndex == other.stepIndex &&
        stepStartServerTime.Equals(other.stepStartServerTime);

    public override bool Equals(object obj) => obj is ChallengeStepState other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(seed, stepIndex, stepStartServerTime);
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
/// - StageStartGate.CompleteCountdown() → MarkStageStart() (PhaseManager와 별개 슬롯)
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

    // 스테이지 시작 서버 시간 (StartStage() 직전 Host 기록).
    // StageStartGate / MemoryPathIntroController 전용 — "이 방의 시작 게이트가 완료됐다"는
    // 1회성 배타적 신호로 쓰인다. 다른 시스템이 여기에 같이 쓰면 그 배타성이 깨지므로
    // (예: 2026-07-21 PhaseManager 오공유 버그) 절대 다른 곳에서 같이 쓰지 말 것.
    private readonly NetworkVariable<double> _stageStartServerTime = new(
        -1.0,
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
    public double StageStartServerTime     => _stageStartServerTime.Value;
    public double PhaseStartServerTime     => _phaseStartServerTime.Value;

    public int    ChallengeSeed               => _challengeStep.Value.seed;
    public int    ChallengeStepIndex           => _challengeStep.Value.stepIndex;
    public double ChallengeStepStartServerTime => _challengeStep.Value.stepStartServerTime;
    public bool   IsChallengeCleared           => _challengeCleared.Value;

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
        _challengeCleared.OnValueChanged += OnChallengeClearedNvChanged;
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
        _challengeCleared.OnValueChanged -= OnChallengeClearedNvChanged;
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
    public void SyncDropWarnClientRpc(int trapId, Vector3 targetPos, float warnDuration, float fallDuration)
    {
        if (IsServer) return;
        DropTrap.PlayWarnById(trapId, targetPos, warnDuration, fallDuration);
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
    /// Host: StartStage() 직전 서버 시간 기록.
    /// TimerUI가 이 값 기준으로 Host/Client 동일한 경과 시간을 계산.
    /// </summary>
    public void MarkStageStart()
    {
        if (!IsServer) return;
        _stageStartServerTime.Value = NetworkManager.Singleton.ServerTime.Time;
        _isCountdownActive.Value    = false;
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

    // ── 챌린지 라운드 동기화 (축 #4 공통) ─────────────────────────

    /// <summary>
    /// Host: 챌린지 새 라운드 시작(트리거 진입 등). 시드를 배포해 전 머신이
    /// 동일한 로컬 생성 코드(셔플·배치)를 재실행하게 한다 — 결과 자체는 전송하지 않음.
    /// seed·stepIndex를 한 NV 쓰기로 같이 보내 Client에서 항상 원자적으로 도착하게 한다.
    /// </summary>
    public void ChallengeStart(int seed)
    {
        if (!IsServer) return;
        _challengeStep.Value    = new ChallengeStepState { seed = seed, stepIndex = -1, stepStartServerTime = -1.0 };
        _challengeCleared.Value = false;
    }

    /// <summary>
    /// Host: 문제/라운드 스텝 시작. stepIndex 변경이 전 머신에 전파되어
    /// OnChallengeStepChanged 구독자가 동일한 표시·타이머 로직을 실행한다.
    /// 시드는 그대로 들고 있던 값을 유지 — 이 쓰기에서도 seed+stepIndex+time이 한 번에 같이 간다.
    /// </summary>
    public void ChallengeStepBegin(int stepIndex)
    {
        if (!IsServer) return;
        _challengeStep.Value = new ChallengeStepState
        {
            seed                 = _challengeStep.Value.seed,
            stepIndex            = stepIndex,
            stepStartServerTime  = NetworkManager.Singleton.ServerTime.Time,
        };
    }

    /// <summary>Host: 챌린지 클리어 확정. Complete() 자체는 각 Objective가 자체 Host 가드로 처리 — 이 신호는 연출용.</summary>
    public void ChallengeCleared(bool cleared)
    {
        if (!IsServer) return;
        _challengeCleared.Value = cleared;
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
    void OnChallengeClearedNvChanged(bool prev, bool next) => OnChallengeClearedChanged?.Invoke(next);

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

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 사망 신고")]
    void Debug_Death() => NotifyPlayerDeathServerRpc();

    [ContextMenu("테스트: Phase 0으로 초기화")]
    void Debug_Phase0() => SyncPhase(0);
#endif
}
