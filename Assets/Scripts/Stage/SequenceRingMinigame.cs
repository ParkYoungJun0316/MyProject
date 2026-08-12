using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

// OXQuizManager 안에 정의된 FloatEvent와 충돌하지 않도록 이 파일에 별도 선언
[Serializable]
public class SequenceFloatEvent : UnityEvent<float> { }

/// <summary>
/// 5×5 링 16칸 순서 협동 미니게임. 입력은 Space 키 하나뿐 — 누른 사람의 "실제 배정색"을
/// PlayerSpawnCoordinator에서 조회해 그대로 판정에 쓴다. 즉 자기 색 스텝일 때만 Space가 통과하고,
/// 다른 색 플레이어가 눌러도 오입력(패널티)으로 처리된다(다인 네트워크 전제).
///
/// [축 SSOT: NetworkDesign.md §11B — 챌린지 축(C 패턴) + §11B.1(Client→Host 입력 제출)]
/// Trigger(StartMinigame, Host만) → RoundStart(Host가 시드 NV 배포) → Generate(전 머신 각자 동일
/// 시드로 스텝 시퀀스 재생성) → Judge(Host 레인만, `TrySubmit`/`TrySubmitAnyKey`) → Resolve(성공은
/// ChallengeCleared, 실패는 outcome ClientRpc로 전파). 포지션 판정형(OX/GridColor/ColorTile)과 달리
/// 키 입력형이라 "누가 눌렀는가"는 Host가 알 수 없으므로 Client는 `StageNetworkState.SubmitStepServerRpc`
/// / `SubmitAnyKeyStepServerRpc`로 요청만 보내고, Host가 `TrySubmit`/`TrySubmitAnyKey`로 실제 판정한다.
/// 남은 시간은 오답 페널티 등 이벤트 기반 변동이 있어 ServerTime 역산이 불가능해 별도
/// `SyncChallengeTimeClientRpc`로 Host가 주기적으로 브로드캐스트한다.
/// </summary>
public class SequenceRingMinigame : MonoBehaviour
{
    public const int RingTileCount = 16;

    public enum MinigameState { Idle, Playing, Success, Failed }

    /// <summary>Lookahead=Phase1(+N), PlusOne=현재+다음, CurrentOnly=현재만, NextOnly=다음만(현재 숨김)</summary>
    public enum PreviewMode
    {
        Lookahead,
        PlusOne,
        CurrentOnly,
        NextOnly,
    }

    public enum StepKind { Normal, Common, Danger }

    [Serializable]
    public struct StepData
    {
        public StepKind kind;
        public PlayerColorType color;
    }

    [Serializable]
    public class ColorDisplayEntry
    {
        public PlayerColorType colorType = PlayerColorType.Blue;
        public Color displayColor = Color.blue;
    }

    [Header("타일 (자식 SequenceRingTile, ringIndex 0~15)")]
    [SerializeField] SequenceRingTile[] ringTiles = new SequenceRingTile[0];

    [Header("목표·시간")]
    [Tooltip("클리어에 필요한 성공 스텝 수 (예: 30)")]
    [SerializeField] int targetStepCount = 0;

    [Tooltip("제한 시간(초). 0 이하면 무제한")]
    [SerializeField] float timeLimit = 0f;

    [Tooltip("틀렸을 때 줄어드는 시간(초)")]
    [SerializeField] float timePenaltyOnWrong = 0f;

    [Header("미리보기 난이도")]
    [SerializeField] PreviewMode previewMode = PreviewMode.Lookahead;

    [Tooltip("Lookahead 모드: 현재 스텝부터 앞으로 몇 칸까지 색 유지 (Phase1 기본 12)")]
    [SerializeField] int previewLookahead = 12;

    [Tooltip("NextOnly: 시작(cur=0) 시 0·1번 칸 색 표시 (이후부터 다음 칸만)")]
    [FormerlySerializedAs("nextOnlyBootstrapFirstStep")]
    [SerializeField] bool nextOnlyBootstrapStartSteps = true;

    [Header("스텝 생성")]
    [Tooltip("Common(흰) 스텝이 나올 확률 0~1")]
    [SerializeField] float commonSpawnChance = 0f;

    [Tooltip("Danger(검) 스텝이 나올 확률 0~1")]
    [SerializeField] float dangerSpawnChance = 0f;

    [Tooltip("Danger 칸 유지 시간(초). 무입력 시 자동 통과")]
    [SerializeField] float dangerStepDuration = 1f;

    [Header("색상")]
    [Tooltip("미리보기 밖·숨김 타일 Base Color")]
    [SerializeField] Color defaultTileColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    [SerializeField] Color commonDisplayColor = Color.white;
    [SerializeField] Color dangerDisplayColor = Color.black;

    [SerializeField] ColorDisplayEntry[] uniqueColorDisplays = new ColorDisplayEntry[0];

    [Header("시작")]
    [SerializeField] bool startOnAwake = false;

    [Header("이벤트")]
    public UnityEvent OnMinigameStarted;
    public UnityEvent OnMinigameSuccess;
    public UnityEvent OnMinigameFailed;
    [Tooltip("플레이어 입력으로 스텝을 정답 통과할 때마다 발동(자동 통과되는 Danger 타임아웃은 제외). SFX 등에 사용.")]
    public UnityEvent OnCorrectInput;
    public UnityEvent OnWrongInput;
    [FormerlySerializedAs("OnTimerTick")]
    public SequenceFloatEvent OnTimeRemainingChanged;

    const float TimeSyncInterval = 0.1f;

    MinigameState _state = MinigameState.Idle;
    StepData[] _steps = Array.Empty<StepData>();
    SequenceRingTile[] _sortedTiles = Array.Empty<SequenceRingTile>();

    int _currentStepIndex;
    int _successCount;
    float _timeRemaining;
    float _dangerStepTimer;
    float _timeSyncTimer;

    StageNetworkState _netState;

    /// <summary>씬당 1개 전제 — StageNetworkState.SubmitStepServerRpc가 Host에서 참조 (§11B.1).</summary>
    public static SequenceRingMinigame Instance { get; private set; }

    public MinigameState State => _state;
    public int CurrentStepIndex => _currentStepIndex;
    public int SuccessCount => _successCount;
    public float TimeRemaining => _timeRemaining;
    public int TargetStepCount => targetStepCount;
    /// <summary>Inspector에 설정된 제한 시간. 0 이하면 무제한 설정. SequenceRingObjective가 읽음.</summary>
    public float TimeLimit => timeLimit;

    /// <summary>현재 스텝이 위치한 링 칸(0~15)이 바뀔 때 발동. SequenceRingCurrentStepMarker 등
    /// 표시용 컴포넌트가 구독 — Host/Client 전 머신 공통으로 HandleChallengeStepChanged에서 발동되므로
    /// 별도 네트워크 처리 없이 항상 동일한 값을 받는다.</summary>
    public event Action<int> OnCurrentTileRingChanged;

    void Awake()
    {
        // 타일 세팅은 생애 1회만 하면 되므로 Awake에 둔다.
        // Instance 점유/해제는 OnEnable/OnDisable로 옮김 — 씬당 1개가 아니라
        // "Phase 전환으로 지금 활성화된 것 1개"가 진짜 불변식이기 때문 (아래 OnEnable 참고).
        CollectAndSortTiles();
        ApplyDefaultColors();
    }

    void OnEnable()
    {
        // Phase가 objectsToEnable로 이 컨테이너를 켜는 시점 = 이 미니게임이 "현재 활성" Phase가 됨.
        // StageNetworkState는 씬 로드 시 Awake로 이미 떠 있는 영구 싱글턴이라 Phase 중간에
        // OnEnable이 늦게 호출돼도 항상 준비돼 있음 (기존 Start() 전제와 동일하게 안전).
        Instance = this;

        _netState = StageNetworkState.Instance;
        if (_netState != null)
        {
            _netState.OnChallengeStepChanged    += HandleChallengeStepChanged;
            _netState.OnChallengeClearedChanged += HandleChallengeClearedChanged;
            _netState.OnChallengeOutcome        += HandleChallengeOutcome;
            _netState.OnChallengeTimeSync       += HandleChallengeTimeSync;
            _netState.OnDeathReloadStarted      += HandleDeathReloadStarted;

            // [버그 수정 2026-07-25] late-subscribe catch-up: 이 Phase 컨테이너가 활성화되는 시점이
            // Host의 ChallengeStepBegin NV 전파보다 늦으면(구독 전에 이미 지나간 값 변경은 C# 이벤트라
            // 재생되지 않음) 스텝 변경 자체를 영구히 놓친다. 구독 직후 현재 NV 값으로 1회 강제 재실행해
            // "지금 막 구독한 쪽도 항상 최신 상태를 본다"를 보장 — 새 경로가 아니라 같은 핸들러 재사용.
            if (_netState.ChallengeStepIndex >= 0)
                HandleChallengeStepChanged(_netState.ChallengeStepIndex);
        }
    }

    void Start()
    {
        if (startOnAwake && !IsClientOnly())
            StartMinigame();
    }

    void OnDisable()
    {
        // Phase가 objectsToDisable로 이 컨테이너를 끌 때 구독 해제 + Instance 소유권 반납.
        // Destroy가 아니라 비활성화이므로 여기서 반납해야 다음 Phase의 OnEnable이 Instance를
        // 정상적으로 가져갈 수 있다 (2026-07-22 버그: 반납 없이 Awake에서 중복 Destroy하던 문제 수정).
        if (_netState != null)
        {
            _netState.OnChallengeStepChanged    -= HandleChallengeStepChanged;
            _netState.OnChallengeClearedChanged -= HandleChallengeClearedChanged;
            _netState.OnChallengeOutcome        -= HandleChallengeOutcome;
            _netState.OnChallengeTimeSync       -= HandleChallengeTimeSync;
            _netState.OnDeathReloadStarted      -= HandleDeathReloadStarted;
        }
        if (Instance == this) Instance = null;
    }

    /// <summary>Client/Host 공통. Host 레인 여부만 다르게 취급 (OXQuizManager와 동일).</summary>
    static bool IsClientOnly()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && !nm.IsServer;
    }

    void Update()
    {
        if (_state != MinigameState.Playing) return;

        // 판정·타이머 진행은 Host 레인에서만 (§11B ④Judge) — Client는 결과를 관찰만
        if (!IsClientOnly())
        {
            TickTimer(Time.deltaTime);
            TickDangerStep(Time.deltaTime);
        }

        // 로컬 키 입력 감지는 전 머신 공통 — Host는 즉시 판정, Client는 제출만(§11B.1)
        PollSpaceInput();
    }

    // ── 공개 API ─────────────────────────────────────────────────

    /// <summary>
    /// 미니게임 시작. Host 레인만 실제로 진행 — Client의 직접 호출은 무시된다 (§11B ①Trigger).
    /// 시드를 생성해 배포하면, 전 머신이 HandleChallengeStepChanged에서 동일한 시드로 스텝
    /// 시퀀스를 재생성한다 (§11B ②RoundStart, OX의 StartQuiz와 동일 구조).
    /// </summary>
    public void StartMinigame()
    {
        if (IsClientOnly()) return;
        if (_state == MinigameState.Playing) return;
        if (_netState == null) return;

        CollectAndSortTiles();
        if (_sortedTiles.Length == 0)
        {
            Debug.LogWarning("[SequenceRingMinigame] SequenceRingTile이 없습니다.", this);
            return;
        }

        if (targetStepCount <= 0)
        {
            Debug.LogWarning("[SequenceRingMinigame] targetStepCount는 1 이상이어야 합니다.", this);
            return;
        }

        _timeRemaining = timeLimit > 0f ? timeLimit : float.MaxValue;
        _timeSyncTimer = TimeSyncInterval;

        int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        _netState.ChallengeStart(seed, ChallengeOwnerType.SequenceRing);
        _netState.ChallengeStepBegin(0);
    }

    public void StopMinigame()
    {
        _state = MinigameState.Idle;
        ApplyDefaultColors();
    }

    public void ResetMinigame()
    {
        StopMinigame();
        _steps            = Array.Empty<StepData>();
        _currentStepIndex = 0;
        _successCount     = 0;
    }

    // ── 라운드 생성·결과 반영 (전 머신 공통 — StageNetworkState NV/RPC 구독) ────

    /// <summary>
    /// StageNetworkState.OnChallengeStepChanged 구독 핸들러. Host/Client 동일 코드로 스텝을 표시한다.
    /// 시퀀스 자체는 캐싱하지 않고 매 스텝마다 시드로 재생성한다(§11B ③Generate).
    /// </summary>
    void HandleChallengeStepChanged(int stepIndex)
    {
        // [버그 수정 2026-07-28] _challengeStep 공유 슬롯 owner 가드 — 내 것(SequenceRing)이 아니면
        // 무시(ChallengeOwnerType 정의부 참고, A-B-C-A 회귀의 근본 원인).
        if (_netState == null || _netState.ChallengeOwner != ChallengeOwnerType.SequenceRing) return;
        if (stepIndex < 0 || _sortedTiles.Length == 0) return;

        bool firstEntry = _state != MinigameState.Playing;
        _state = MinigameState.Playing;

        _currentStepIndex = stepIndex;
        _successCount     = stepIndex;

        GenerateSteps();
        if (_currentStepIndex >= _steps.Length)
        {
            Debug.LogWarning("[SequenceRingMinigame] 스텝 배열이 targetStepCount보다 짧습니다.", this);
            return;
        }

        OnEnterStep(_currentStepIndex);
        RefreshTileColors();
        OnCurrentTileRingChanged?.Invoke(_currentStepIndex % RingTileCount);

        if (firstEntry)
        {
            OnMinigameStarted?.Invoke();
            BroadcastTime();
        }
    }

    /// <summary>ChallengeCleared NV 변경 시 Host/Client 공통으로 OnMinigameSuccess를 1회 재생 (OX의 OnAllCleared와 동일 패턴).</summary>
    void HandleChallengeClearedChanged(bool cleared)
    {
        if (_netState == null || _netState.ChallengeOwner != ChallengeOwnerType.SequenceRing) return; // owner 가드 — HandleChallengeStepChanged와 동일 이유
        if (!cleared) return;

        _state = MinigameState.Success;
        ApplyDefaultColors();
        OnMinigameSuccess?.Invoke();
    }

    /// <summary>Host는 FailMinigame()에서 직접 호출하므로 이 핸들러는 Client에서만 의미 있음.</summary>
    void HandleChallengeOutcome(bool success)
    {
        if (_netState == null || _netState.ChallengeOwner != ChallengeOwnerType.SequenceRing) return; // owner 가드 — HandleChallengeStepChanged와 동일 이유
        if (!success) HandleFailedOutcome();
    }

    void HandleFailedOutcome()
    {
        _state = MinigameState.Failed;
        ApplyDefaultColors();
        OnMinigameFailed?.Invoke();
    }

    /// <summary>Client 전용 — Host가 주기적으로 브로드캐스트하는 남은 시간을 그대로 표시에 반영.</summary>
    void HandleChallengeTimeSync(float remaining)
    {
        _timeRemaining = remaining;
        BroadcastTime();
    }

    /// <summary>
    /// §11 사망 문 진입 확정(StageNetworkState.OnDeathReloadStarted) — Host/Client 공통 구독.
    /// 사망은 이 미니게임의 판정(TrySubmit/TickTimer)이 절대 감지할 수 없는 챌린지 축 밖의 사건이라,
    /// 여기서 즉시 Idle로 되돌려 Update()의 Playing 가드가 다음 프레임부터 TickTimer/TickDangerStep을
    /// 아예 실행하지 않게 만든다 — 이게 RpcException(Despawn된 _netState에 뒤늦은 RPC)의 근본 차단.
    /// </summary>
    void HandleDeathReloadStarted() => StopMinigame();

    /// <summary>네트워크 연동용: 플레이어 색으로 입력 제출. Host 전용 호출 경로(§11B ④Judge) — Client는
    /// SubmitColorInput()을 통해 ServerRpc로만 도달한다.</summary>
    public void TrySubmit(PlayerColorType color)
    {
        if (_state != MinigameState.Playing) return;
        if (_currentStepIndex < 0 || _currentStepIndex >= _steps.Length) return;

        StepData step = _steps[_currentStepIndex];

        switch (step.kind)
        {
            case StepKind.Common:
                AdvanceStep();
                break;

            case StepKind.Danger:
                ApplyWrongPenalty();
                break;

            case StepKind.Normal:
                if (step.color == color)
                    AdvanceStep();
                else
                    ApplyWrongPenalty();
                break;
        }
    }

    /// <summary>Common/Danger용 아무 키 입력.</summary>
    public void TrySubmitAnyKey()
    {
        if (_state != MinigameState.Playing) return;
        if (_currentStepIndex < 0 || _currentStepIndex >= _steps.Length) return;

        StepData step = _steps[_currentStepIndex];

        switch (step.kind)
        {
            case StepKind.Common:
                AdvanceStep();
                break;
            case StepKind.Danger:
                ApplyWrongPenalty();
                break;
            default:
                ApplyWrongPenalty();
                break;
        }
    }

    // ── 입력 (Space 단일 키, 자기 색 판정 + 네트워크 제출, §11B.1) ──

    /// <summary>
    /// Space 하나로 통일. 현재 스텝이 Common/Danger면 색 구분 없는 any-key로 제출하고,
    /// Normal(색) 스텝이면 PlayerSpawnCoordinator에서 조회한 "내 실제 배정색"으로 제출한다.
    /// 누른 사람의 색이 아니면 TrySubmit()의 기존 색 비교에서 자동으로 오입력(패널티) 처리된다 —
    /// 어느 플레이어든 자기 색 스텝에서만 통과할 수 있다(4인 네트워크 전제).
    /// </summary>
    void PollSpaceInput()
    {
        if (Keyboard.current == null) return;
        if (!Keyboard.current.spaceKey.wasPressedThisFrame) return;
        if (_currentStepIndex < 0 || _currentStepIndex >= _steps.Length) return;

        if (_steps[_currentStepIndex].kind != StepKind.Normal)
        {
            SubmitAnyKeyInput();
            return;
        }

        if (!TryGetLocalPlayerColor(out PlayerColorType myColor)) return;
        SubmitColorInput(myColor);
    }

    /// <summary>clientId→색 SSOT(PlayerSpawnCoordinator)에서 이 머신의 실제 배정색을 조회.</summary>
    static bool TryGetLocalPlayerColor(out PlayerColorType color)
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
            return PlayerSpawnCoordinator.TryGetColor(nm.LocalClientId, out color);

        color = default;
        return false;
    }

    /// <summary>Host는 TrySubmit()으로 즉시 판정, Client는 SubmitStepServerRpc로 요청만 (§11B.1).</summary>
    void SubmitColorInput(PlayerColorType color)
    {
        if (IsClientOnly())
            _netState?.SubmitStepServerRpc(color);
        else
            TrySubmit(color);
    }

    /// <summary>Host는 TrySubmitAnyKey()으로 즉시 판정, Client는 SubmitAnyKeyStepServerRpc로 요청만.</summary>
    void SubmitAnyKeyInput()
    {
        if (IsClientOnly())
            _netState?.SubmitAnyKeyStepServerRpc();
        else
            TrySubmitAnyKey();
    }

    // ── 타이머·Danger ────────────────────────────────────────────

    /// <summary>Host 전용(§11B ④Judge). 남은 시간은 페널티 등 이벤트 기반 변동이 있어 ServerTime
    /// 역산이 불가능 — 직접 tick하며 주기적으로 SyncChallengeTimeClientRpc로 브로드캐스트한다.</summary>
    void TickTimer(float dt)
    {
        if (timeLimit <= 0f) return;

        _timeRemaining -= dt;
        BroadcastTime();

        _timeSyncTimer -= dt;
        if (_timeSyncTimer <= 0f)
        {
            _timeSyncTimer = TimeSyncInterval;
            _netState?.SyncChallengeTimeClientRpc(Mathf.Max(0f, _timeRemaining));
        }

        if (_timeRemaining <= 0f)
            FailMinigame();
    }

    void TickDangerStep(float dt)
    {
        if (_currentStepIndex < 0 || _currentStepIndex >= _steps.Length) return;
        if (_steps[_currentStepIndex].kind != StepKind.Danger) return;
        // dangerStepDuration이 0이면 타이머 자동 통과 비활성 (Inspector에서 설정 필요)
        if (dangerStepDuration <= 0f) return;

        _dangerStepTimer -= dt;
        if (_dangerStepTimer <= 0f)
        {
            _dangerStepTimer = 0f; // 연속 Danger 스텝에서 음수 누적 방지
            AdvanceStep(fromPlayerInput: false);
        }
    }

    void OnEnterStep(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= _steps.Length) return;

        _dangerStepTimer = (_steps[stepIndex].kind == StepKind.Danger && dangerStepDuration > 0f)
            ? dangerStepDuration
            : 0f;
    }

    // ── 진행·판정 (Host 전용 호출 경로 — TrySubmit/TrySubmitAnyKey/TickDangerStep은 항상
    //    Host에서만 실행된다: Client 입력은 ServerRpc로만 들어오고, TickDangerStep은 Update()에서
    //    이미 Host 가드 뒤에 있다. §11B ④Judge) ─────────────────────

    /// <summary>
    /// Host: 다음 스텝으로 진행 확정. 실제 스텝 인덱스 전파는 ChallengeStepBegin(NV)로 하고,
    /// 화면 반영(OnEnterStep/RefreshTileColors)은 전 머신 공통 HandleChallengeStepChanged가 담당한다.
    /// </summary>
    /// <param name="fromPlayerInput">
    /// true면 실제 입력으로 통과한 것 — OnCorrectInput 발동. Danger 타임아웃 자동 통과(TickDangerStep)는
    /// 입력이 아니므로 false로 호출해 OnWrongInput처럼 "플레이어가 실제로 뭘 했을 때만" 울리게 한다.
    /// </param>
    void AdvanceStep(bool fromPlayerInput = true)
    {
        if (fromPlayerInput)
            OnCorrectInput?.Invoke();

        _successCount++;

        if (_successCount >= targetStepCount)
        {
            SucceedMinigame();
            return;
        }

        int nextIndex = _currentStepIndex + 1;
        if (nextIndex >= _steps.Length)
        {
            Debug.LogWarning("[SequenceRingMinigame] 스텝 배열이 targetStepCount보다 짧습니다.", this);
            SucceedMinigame();
            return;
        }

        _netState?.ChallengeStepBegin(nextIndex);
    }

    void ApplyWrongPenalty()
    {
        if (timePenaltyOnWrong > 0f && timeLimit > 0f)
        {
            _timeRemaining -= timePenaltyOnWrong;
            BroadcastTime();
            if (_timeRemaining <= 0f)
            {
                FailMinigame();
                return;
            }
        }

        OnWrongInput?.Invoke();
    }

    /// <summary>Host: 클리어 확정. ChallengeCleared NV가 전 머신 공통으로 HandleChallengeClearedChanged를
    /// 발동시키므로 여기서 직접 OnMinigameSuccess를 Invoke하지 않는다(Host 이중 발동 금지, OX와 동일 원칙).
    /// ChallengeCleared(true) 뒤 곧바로 ResetChallengeStep()으로 stepIndex를 -1로 되돌려, 다음 Phase의
    /// 동일 챌린지 컴포넌트가 OnEnable catch-up에서 이 값을 "이미 진행 중"으로 오인하지 않게 한다
    /// (2026-07-27 버그 수정).</summary>
    void SucceedMinigame()
    {
        _netState?.ChallengeCleared(true);
        _netState?.ResetChallengeStep();
    }

    /// <summary>Host: 실패 확정. 로컬 반영 + Client에는 outcome(false)로 전파 (§11B ⑤Resolve).</summary>
    void FailMinigame()
    {
        HandleFailedOutcome();
        _netState?.NotifyChallengeOutcomeClientRpc(false);
    }

    void BroadcastTime()
    {
        // 무제한(timeLimit <= 0) 일 때는 float.MaxValue가 UI에 노출되지 않도록 브로드캐스트 생략
        if (timeLimit <= 0f) return;
        OnTimeRemainingChanged?.Invoke(Mathf.Max(0f, _timeRemaining));
    }

    // ── 시퀀스 생성 (전 머신 공통, §11B ③Generate) ─────────────────

    /// <summary>
    /// ChallengeSeed 기반 System.Random으로 전체 스텝 시퀀스를 재생성한다. 매 스텝 변경 시 다시
    /// 호출되지만(캐싱 없음) 항상 같은 시드 → 같은 결과이므로 결과 자체를 네트워크로 보내지 않는다
    /// (OX의 RegenerateQuestionOrder와 동일 원칙 — UnityEngine.Random 전역 상태 오염 없음).
    /// </summary>
    void GenerateSteps()
    {
        _steps = new StepData[targetStepCount];
        PlayerColorType[] pool = GetUniqueColorPool();

        int seed = _netState != null ? _netState.ChallengeSeed : 0;
        var rng  = new System.Random(seed);

        for (int i = 0; i < targetStepCount; i++)
        {
            float roll = (float)rng.NextDouble();
            float dChance = Mathf.Clamp01(dangerSpawnChance);
            float cChance = Mathf.Clamp01(commonSpawnChance);

            if (roll < dChance)
            {
                _steps[i] = new StepData { kind = StepKind.Danger, color = PlayerColorType.Danger };
            }
            else if (roll < dChance + cChance)
            {
                _steps[i] = new StepData { kind = StepKind.Common, color = PlayerColorType.Common };
            }
            else
            {
                PlayerColorType c = pool.Length > 0
                    ? pool[rng.Next(pool.Length)]
                    : PlayerColorType.Blue;
                _steps[i] = new StepData { kind = StepKind.Normal, color = c };
            }
        }
    }

    /// <summary>
    /// [버그 수정 2026-08-06] 이번 판 실제 활성 색만 반환 — 이전엔 기본 4색만 봐서 실제 접속
    /// 인원과 무관하게 항상 4색이 나왔다(1인은 존재하지도 않는 3색이 섞여 못 깨고, 3인은 미접속
    /// 1색이 섞여 그 스텝을 아무도 못 눌렀음). ColorTileChallenge/GridColorChallenge 등 다른
    /// 챌린지와 동일한 SSOT 체인으로 통일(architecture.mdc: "Prefer extending existing systems").
    /// </summary>
    PlayerColorType[] GetUniqueColorPool()
    {
        PlayerColorType[] psColors = PlayerSpawnCoordinator.GetActiveColors();
        if (psColors.Length > 0) return psColors;

        if (GameSession.Instance != null)
        {
            IReadOnlyList<PlayerColorType> active = GameSession.Instance.GetActiveColors();
            if (active.Count > 0)
            {
                var arr = new PlayerColorType[active.Count];
                for (int i = 0; i < active.Count; i++) arr[i] = active[i];
                return arr;
            }
        }

        return new[]
        {
            PlayerColorType.Blue,
            PlayerColorType.Green,
            PlayerColorType.Yellow,
            PlayerColorType.Purple,
        };
    }

    // ── 미리보기 가시 스텝 ───────────────────────────────────────

    HashSet<int> BuildVisibleStepSet()
    {
        var set = new HashSet<int>();
        int total = _steps.Length;
        if (total == 0) return set;

        int cur = _currentStepIndex;

        switch (previewMode)
        {
            case PreviewMode.Lookahead:
            {
                int end = Mathf.Min(cur + Mathf.Max(0, previewLookahead), total - 1);
                for (int s = cur; s <= end; s++)
                    set.Add(s);
                break;
            }

            case PreviewMode.PlusOne:
            {
                set.Add(cur);
                if (cur + 1 < total) set.Add(cur + 1);
                break;
            }

            case PreviewMode.CurrentOnly:
                set.Add(cur);
                break;

            case PreviewMode.NextOnly:
                if (cur + 1 < total)
                    set.Add(cur + 1);
                if (cur == 0 && nextOnlyBootstrapStartSteps)
                {
                    set.Add(0);
                    if (total > 1)
                        set.Add(1);
                }
                break;
        }

        return set;
    }

    // ── 외부 조회 (마커 등 표시용) ───────────────────────────────

    /// <summary>ringIndex(0~15) 칸의 Transform. SequenceRingCurrentStepMarker 등이 위치 추적에 사용.
    /// 해당 링 칸 타일을 못 찾으면 null.</summary>
    public Transform GetTileTransform(int ringIndex)
    {
        for (int i = 0; i < _sortedTiles.Length; i++)
            if (_sortedTiles[i] != null && _sortedTiles[i].RingIndex == ringIndex)
                return _sortedTiles[i].transform;
        return null;
    }

    // ── 타일 색 갱신 ─────────────────────────────────────────────

    void RefreshTileColors()
    {
        if (_sortedTiles.Length == 0) return;

        HashSet<int> visible = BuildVisibleStepSet();
        int total = _steps.Length;
        int cur = _currentStepIndex;

        for (int r = 0; r < _sortedTiles.Length; r++)
        {
            SequenceRingTile tile = _sortedTiles[r];
            if (tile == null) continue;

            int ring = tile.RingIndex;
            int displayStep = ResolveDisplayStepForRing(ring, visible, cur, total);
            Color color = displayStep >= 0
                ? GetDisplayColor(_steps[displayStep])
                : defaultTileColor;

            tile.ApplyColor(color);
        }
    }

    /// <summary>링 칸에 표시할 스텝 인덱스. 없으면 -1(기본색).</summary>
    int ResolveDisplayStepForRing(int ring, HashSet<int> visible, int cur, int total)
    {
        if (total == 0 || cur < 0) return -1;

        if (previewMode == PreviewMode.NextOnly)
        {
            bool hideCurrent = !(cur == 0 && nextOnlyBootstrapStartSteps);
            return FindBestStepOnRing(ring, visible, cur, total, hideCurrentStep: hideCurrent);
        }

        int curRing = cur < total ? cur % RingTileCount : -1;
        if (curRing == ring && cur < total)
            return cur;

        return FindBestStepOnRing(ring, visible, cur, total, hideCurrentStep: false);
    }

    static int FindBestStepOnRing(int ring, HashSet<int> visible, int cur, int total, bool hideCurrentStep)
    {
        int bestStep = -1;
        for (int s = 0; s < total; s++)
        {
            if (s % RingTileCount != ring) continue;
            if (!visible.Contains(s)) continue;
            if (hideCurrentStep && s == cur) continue;

            if (bestStep < 0 || s < bestStep)
                bestStep = s;
        }

        return bestStep;
    }

    Color GetDisplayColor(StepData step)
    {
        switch (step.kind)
        {
            case StepKind.Common: return commonDisplayColor;
            case StepKind.Danger: return dangerDisplayColor;
            case StepKind.Normal: return GetUniqueDisplayColor(step.color);
            default: return defaultTileColor;
        }
    }

    Color GetUniqueDisplayColor(PlayerColorType colorType)
    {
        if (uniqueColorDisplays != null)
        {
            for (int i = 0; i < uniqueColorDisplays.Length; i++)
                if (uniqueColorDisplays[i].colorType == colorType)
                    return uniqueColorDisplays[i].displayColor;
        }

        return ColoredMemoryPath.GetDefaultColorFor(colorType);
    }

    void ApplyDefaultColors()
    {
        if (_sortedTiles == null) return;
        for (int i = 0; i < _sortedTiles.Length; i++)
            if (_sortedTiles[i] != null)
                _sortedTiles[i].ApplyColor(defaultTileColor);
    }

    void CollectAndSortTiles()
    {
        if (ringTiles == null || ringTiles.Length == 0)
            ringTiles = GetComponentsInChildren<SequenceRingTile>(true);

        var list = new List<SequenceRingTile>();
        for (int i = 0; i < ringTiles.Length; i++)
            if (ringTiles[i] != null)
                list.Add(ringTiles[i]);

        list.Sort((a, b) => a.RingIndex.CompareTo(b.RingIndex));
        _sortedTiles = list.ToArray();
    }

    // ── 에디터 ───────────────────────────────────────────────────

    [ContextMenu("미니게임 시작")]
    void Debug_Start() => StartMinigame();

    [ContextMenu("미니게임 정지")]
    void Debug_Stop() => StopMinigame();

    void Reset()
    {
        uniqueColorDisplays = new[]
        {
            new ColorDisplayEntry { colorType = PlayerColorType.Blue,   displayColor = Color.blue },
            new ColorDisplayEntry { colorType = PlayerColorType.Green,  displayColor = Color.green },
            new ColorDisplayEntry { colorType = PlayerColorType.Yellow, displayColor = Color.yellow },
            new ColorDisplayEntry { colorType = PlayerColorType.Purple, displayColor = new Color(0.55f, 0.2f, 0.95f) },
        };
    }

    void OnValidate()
    {
        previewLookahead = Mathf.Max(0, previewLookahead);
        targetStepCount  = Mathf.Max(0, targetStepCount);
    }
}
