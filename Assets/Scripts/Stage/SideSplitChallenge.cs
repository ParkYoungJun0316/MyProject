using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>분기 방향. 좌/우 2방향(M.Stage2) 또는 좌/우/앞/뒤 4방향(T.Stage4)에서 사용.</summary>
public enum SideSplitDirection { Left, Right, Front, Back }

/// <summary>좌/우/앞/뒤 분기 미니게임 한 라운드의 판정 조건. 시드로부터 재생성되는 순수 데이터.
/// frontCount/backCount는 SideSplitChallenge가 4방향 모드(frontZone/backZone 둘 다 연결)일 때만 의미 있음 —
/// 2방향 모드에서는 항상 0.</summary>
public struct SideSplitRound
{
    public int leftCount;
    public int rightCount;
    public int frontCount;
    public int backCount;
    public bool hasColorRequirement;
    public SideSplitDirection colorDirection; // hasColorRequirement가 true일 때만 의미 있음
    public PlayerColorType requiredColor; // hasColorRequirement가 true일 때만 의미 있음
}

/// <summary>OnRoundReady 이벤트로 UI에 전달되는 라운드 안내 데이터.</summary>
public class SideSplitRoundInfo
{
    public int leftCount;
    public int rightCount;
    public int frontCount;
    public int backCount;
    public bool hasColorRequirement;
    public SideSplitDirection colorDirection;
    public PlayerColorType requiredColor;
}

/// <summary>UnityEvent&lt;SideSplitRoundInfo&gt; 직렬화 래퍼.</summary>
[System.Serializable]
public class SideSplitRoundEvent : UnityEvent<SideSplitRoundInfo> { }

/// <summary>UnityEvent&lt;float&gt; 직렬화 래퍼.</summary>
[System.Serializable]
public class SideSplitFloatEvent : UnityEvent<float> { }

// ── 매니저 ───────────────────────────────────────────────────────

/// <summary>
/// 좌/우(+선택적 앞/뒤) 분기 인원+색상 미니게임 매니저.
///
/// [축 SSOT: NetworkDesign.md §11B — 챌린지 축(C 패턴), OXQuizManager와 동일 골격 복제]
/// [설계 SSOT: MinigameDesign.md §1/§2, 4방향 확장은 §1.7]
/// Trigger→RoundStart(Seed)→Generate→Judge→Resolve. Host 레인만 진행을 확정하고,
/// Client는 StageNetworkState의 NV/RPC를 관찰해 동일한 로컬 코드를 재실행할 뿐
/// 독자적으로 판정·진행을 결정하지 않는다.
///
/// [확정 규칙 — MinigameDesign.md §1.2 / §1.7]
///  - 인원 분배: 활성 플레이어 전원이 반드시 활성 방향으로 다 나뉨 (남는 인원 없음)
///  - 판정: 정확히 일치해야 통과 (초과/부족 전부 실패)
///  - 방향: 좌/우 2분기(frontZone/backZone 미연결 — M.Stage2) 또는 좌/우/앞/뒤 4분기
///    (frontZone/backZone 둘 다 연결 — T.Stage4). 어느 방향이 켜졌는지는 Inspector에
///    연결된 zone 필드로만 결정 — 코드 분기 없음(IsFourDirection 참고)
///  - 색상 조건: 라운드마다 선택적. 초반 라운드는 색 조건 없음, 뒷 라운드일수록 색 조건 등장
///  - 판정 시점: 타이머 종료 시점 스냅샷 (OXQuizManager.JudgeByPosition과 동일 원칙)
///  - 페널티: 라운드 실패 시 전원 데미지 후 다음 라운드로 계속 진행 (재시도 루프 아님)
///
/// [동작 흐름]
///  1. Host만 챌린지 시작 확정(StartChallenge) — 배리어 열림, 시드 생성 후 StageNetworkState로 배포
///  2. 전 머신이 시드로 전체 라운드 계획(_rounds)을 동일하게 재생성 (RegenerateRoundPlan)
///  3. 라운드 안내(OnRoundReady) + 타이머 시작 — 전 머신 동일 시점에 표시
///  4. 타이머 종료 시(ServerTime 기준) Host만 활성 zone 전체를 물리 오버랩으로 판정(Judge)
///     - 각 방향 인원이 지정값과 정확히 일치 + (색 조건 있으면) 지정 색 플레이어가 지정 방향에 존재 → 성공
///     - 그 외 전부 실패 → 전원 wrongDamage 피해 (NetworkDamageUtil, Host만)
///  5. 결과 연출(OnRoundSuccess/OnRoundFailed)은 Host가 직접 재생 + NotifyChallengeOutcomeClientRpc로 Client 동기화
///  6. resolveDelay 후 다음 라운드로 Host가 진행 확정(StageNetworkState.ChallengeStepBegin)
///  7. 모든 라운드가 끝났을 때, 생존자가 1명 이상이면 Host가 클리어 확정
///     - AllCleared → barrierDoor Close + StageNetworkState.ChallengeCleared
/// </summary>
public class SideSplitChallenge : MonoBehaviour
{
    [Header("판정 볼륨 — 좌/우는 필수, 앞/뒤는 선택 (둘 다 연결하면 4방향 모드로 전환됨)")]
    public SideSplitZone leftZone;
    public SideSplitZone rightZone;

    [Tooltip("앞/뒤 볼륨. 둘 다 연결하면 4방향 모드(T.Stage4) — 비워두면 2방향 모드(M.Stage2)와 동일하게 동작.")]
    public SideSplitZone frontZone;
    public SideSplitZone backZone;

    [Header("라운드 설정")]
    [Tooltip("한 판에 진행할 라운드 수")]
    [SerializeField] int totalRounds = 5;

    [Tooltip("색 조건이 포함되는 라운드 수 최소값 (뒤쪽 라운드부터 배정 — 초반은 항상 색 조건 없음)")]
    [SerializeField] int minColorRounds = 3;

    [Tooltip("색 조건이 포함되는 라운드 수 최대값. totalRounds보다 작게 설정해 최소 1라운드는 항상 색 조건 없이 시작하는 것을 권장.")]
    [SerializeField] int maxColorRounds = 4;

    [Tooltip("라운드당 제한시간(초). 0보다 커야 판정이 작동함")]
    public float roundTimeLimit = 0f;

    [Tooltip("결과 연출 후 다음 라운드까지 대기 시간(초)")]
    public float resolveDelay = 0f;

    [Tooltip("라운드 실패 시 전원에게 줄 피해량")]
    public int wrongDamage = 1;

    [Header("판정")]
    [Tooltip("판정 Bounds 오버랩 검사에 포함할 레이어. 비어 있으면 실행 시 Player 레이어를 사용합니다.")]
    [SerializeField] LayerMask playerOverlapLayers;

    [Header("배리어 (DoorController)")]
    [Tooltip("벽 역할을 하는 DoorController.\n" +
             "Open() = 벽 솟아오름(챌린지 시작), Close() = 벽 내려감(챌린지 종료).\n" +
             "DoorController의 OpenMode는 SlideUp 권장.")]
    public DoorController barrierDoor;

    [Header("UI 이벤트")]
    [Tooltip("새 라운드 조건 전달 → UI에 연결")]
    public SideSplitRoundEvent OnRoundReady;

    [Tooltip("남은 시간(초) 전달 → 타이머 UI에 연결 (0.1초 간격 갱신)")]
    public SideSplitFloatEvent OnTimerTick;

    [Tooltip("라운드 판정 성공 시 발동")]
    public UnityEvent OnRoundSuccess;

    [Tooltip("라운드 판정 실패 시 발동")]
    public UnityEvent OnRoundFailed;

    [Tooltip("이번 판 라운드를 모두 진행했을 때 발동 → 문 열기, 스테이지 전환 등")]
    public UnityEvent OnAllCleared;

    int  _roundIndex;
    bool _challengeActive;
    bool _challengeStarted; // 트리거 중복 시작 방지
    SideSplitRound[] _rounds;

    Coroutine         _timerCoroutine;
    StageNetworkState _netState; // 구독 해제 시 동일 인스턴스 참조 보장용 캐시
    bool _subscribed;

    /// <summary>현재 진행 중인 라운드 인덱스(0-based). SideSplitObjective에서 참조.</summary>
    public int CurrentRoundIndex => _roundIndex;

    /// <summary>이번 판 총 라운드 수. SideSplitObjective에서 참조.</summary>
    public int TotalRounds => totalRounds;

    /// <summary>StartChallenge() 이후 true. SideSplitObjective.Begin()에서 이미 진행 중인지 판별에 사용.</summary>
    public bool IsStarted => _challengeStarted;

    /// <summary>frontZone/backZone이 둘 다 연결돼 있으면 4방향 모드(T.Stage4). SideSplitUI에서 안내 문구 분기용.</summary>
    public bool IsFourDirection => frontZone != null && backZone != null;

    /// <summary>활성 zone 하나(방향 태그 + 참조). 고정 순서(Left→Right→Front→Back)로만 열거 — 결정적 시드 소비 순서 보장.</summary>
    struct ZoneSlot
    {
        public SideSplitDirection direction;
        public SideSplitZone zone;
    }

    /// <summary>Inspector에 연결된 zone만 고정 순서로 모은 목록. null인 슬롯(2방향 모드의 front/back)은 제외.</summary>
    List<ZoneSlot> ActiveZones()
    {
        var list = new List<ZoneSlot>(4);
        if (leftZone  != null) list.Add(new ZoneSlot { direction = SideSplitDirection.Left,  zone = leftZone  });
        if (rightZone != null) list.Add(new ZoneSlot { direction = SideSplitDirection.Right, zone = rightZone });
        if (frontZone != null) list.Add(new ZoneSlot { direction = SideSplitDirection.Front, zone = frontZone });
        if (backZone  != null) list.Add(new ZoneSlot { direction = SideSplitDirection.Back,  zone = backZone  });
        return list;
    }

    static int GetCount(in SideSplitRound round, SideSplitDirection dir) => dir switch
    {
        SideSplitDirection.Left  => round.leftCount,
        SideSplitDirection.Right => round.rightCount,
        SideSplitDirection.Front => round.frontCount,
        SideSplitDirection.Back  => round.backCount,
        _                        => 0,
    };

    static void SetCount(ref SideSplitRound round, SideSplitDirection dir, int count)
    {
        switch (dir)
        {
            case SideSplitDirection.Left:  round.leftCount  = count; break;
            case SideSplitDirection.Right: round.rightCount = count; break;
            case SideSplitDirection.Front: round.frontCount = count; break;
            case SideSplitDirection.Back:  round.backCount  = count; break;
        }
    }

    /// <summary>Start()/HandleChallengeStepChanged()/Judge()/HandleChallengeOutcome()에서 공통으로 쓰는
    /// 전체 활성 zone 시각 상태 갱신 — 2방향/4방향 모드 구분 없이 연결된 zone만 갱신됨.</summary>
    void SetAllZonesState(SideSplitZone.VisualState state)
    {
        leftZone?.SetState(state);
        rightZone?.SetState(state);
        frontZone?.SetState(state);
        backZone?.SetState(state);
    }

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        if (playerOverlapLayers.value == 0)
        {
            int pl = LayerMask.NameToLayer("Player");
            if (pl >= 0)
                playerOverlapLayers = 1 << pl;
        }
    }

    void Start()
    {
        SetAllZonesState(SideSplitZone.VisualState.Neutral);

        TryBindAndSubscribe();
    }

    void OnEnable()
    {
        TryBindAndSubscribe();
    }

    void OnDisable()
    {
        Unsubscribe();
        StopTimer();
        _challengeActive = false;
    }

    void OnDestroy()
    {
        Unsubscribe();
    }

    /// <summary>
    /// _netState 바인딩 + 구독을 한 곳에 모은 진입점 (OXQuizManager.TryBindAndSubscribe와 동일 원칙 —
    /// PhaseManager가 이 오브젝트를 SetActive(false)할 때 OnDisable로 확실히 구독을 끊기 위해
    /// OnEnable/OnDisable을 쓰고, Awake→Start 순서 보장은 Start()가 최초 바인딩 안전망을 맡는다).
    /// </summary>
    void TryBindAndSubscribe()
    {
        if (_subscribed) return;

        _netState ??= StageNetworkState.Instance;
        if (_netState == null) return;

        _netState.OnChallengeStepChanged    += HandleChallengeStepChanged;
        _netState.OnChallengeClearedChanged += HandleChallengeClearedChanged;
        _netState.OnChallengeOutcome        += HandleChallengeOutcome;
        _netState.OnDeathReloadStarted      += HandleDeathReloadStarted;
        _subscribed = true;
    }

    void Unsubscribe()
    {
        if (_netState != null)
        {
            _netState.OnChallengeStepChanged    -= HandleChallengeStepChanged;
            _netState.OnChallengeClearedChanged -= HandleChallengeClearedChanged;
            _netState.OnChallengeOutcome        -= HandleChallengeOutcome;
            _netState.OnDeathReloadStarted      -= HandleDeathReloadStarted;
        }
        _subscribed = false;
    }

    /// <summary>Client/Host 공통. Host 레인 여부만 다르게 취급.</summary>
    static bool IsClientOnly()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && !nm.IsServer;
    }

    // ── 공개 API ──────────────────────────────────────────────────

    /// <summary>
    /// 외부에서 강제 시작할 때 사용하거나, StageStartGate.OnCountdownComplete에서 호출.
    /// Host 레인만 실제로 진행 — Client의 로컬 호출은 무시된다 (§11B Q2).
    /// 배리어를 솟아오르게 하고 챌린지를 시작.
    /// </summary>
    public void StartChallenge()
    {
        if (IsClientOnly()) return;

        barrierDoor?.Open(); // StageNetworkState._doorOpenStates NV로 전 클라이언트에 전파

        int seed = Random.Range(int.MinValue, int.MaxValue);
        _netState?.ChallengeStart(seed, ChallengeOwnerType.SideSplit);

        ResetChallenge();
    }

    /// <summary>챌린지 라운드 재시작. Host 레인만 확정.</summary>
    public void ResetChallenge()
    {
        if (IsClientOnly()) return;
        if (_netState == null) return;

        StopTimer();
        _roundIndex = 0;
        _netState.ChallengeStepBegin(0);
    }

    // ── 내부: 라운드 진행 (전 머신 공통 — StageNetworkState NV 구독) ─────

    /// <summary>
    /// StageNetworkState.OnChallengeStepChanged 구독 핸들러. Host/Client 동일 코드로 라운드를 표시한다.
    /// _challengeStep은 공유 슬롯이라 다른 챌린지가 쓴 값에도 이 이벤트가 발동한다 — owner 태그가
    /// 내 것(SideSplit)이 아니면 즉시 무시 (ChallengeOwnerType 정의부 참고).
    /// </summary>
    void HandleChallengeStepChanged(int stepIndex)
    {
        if (_netState == null || _netState.ChallengeOwner != ChallengeOwnerType.SideSplit) return;
        if (stepIndex < 0) return; // ChallengeStart()의 초기화 신호 — 무시
        if (!isActiveAndEnabled) return; // OnDisable에서 구독 해제하지만, 해제 타이밍 레이스 방어용 가드

        RegenerateRoundPlan();
        if (stepIndex >= _rounds.Length) return; // 안전장치

        _challengeStarted = true; // Host/Client 공통 — IsStarted는 이 신호로만 true가 됨
        _roundIndex        = stepIndex;

        SetAllZonesState(SideSplitZone.VisualState.Neutral);
        _challengeActive = true;

        SideSplitRound round = _rounds[_roundIndex];
        OnRoundReady?.Invoke(new SideSplitRoundInfo
        {
            leftCount           = round.leftCount,
            rightCount          = round.rightCount,
            frontCount          = round.frontCount,
            backCount           = round.backCount,
            hasColorRequirement = round.hasColorRequirement,
            colorDirection      = round.colorDirection,
            requiredColor       = round.requiredColor,
        });

        StopTimer();
        if (roundTimeLimit > 0f)
            _timerCoroutine = StartCoroutine(TimerRoutine());
    }

    /// <summary>
    /// ServerTime 기준 공통 타이머. 전 머신이 같은 시점에 타임업을 감지하고,
    /// Host만 이어서 실제 물리 판정(Judge)을 수행한다.
    /// </summary>
    IEnumerator TimerRoutine()
    {
        var nm = NetworkManager.Singleton;

        while (_challengeActive)
        {
            double startTime = _netState != null ? _netState.ChallengeStepStartServerTime : 0.0;
            double elapsed    = (nm != null ? nm.ServerTime.Time : 0.0) - startTime;
            float  remaining  = Mathf.Max(0f, roundTimeLimit - (float)elapsed);

            OnTimerTick?.Invoke(remaining);
            if (remaining <= 0f) break;

            yield return new WaitForSeconds(0.1f);
        }

        if (!_challengeActive) yield break;
        _challengeActive = false;

        if (IsClientOnly()) yield break; // 실제 판정·데미지·진행 확정은 Host만

        Judge(_rounds[_roundIndex]);
    }

    IEnumerator NextRoundAfterDelay()
    {
        if (resolveDelay > 0f)
            yield return new WaitForSeconds(resolveDelay);

        _netState?.ChallengeStepBegin(_roundIndex);
    }

    /// <summary>마지막 라운드 클리어 확정을 resolveDelay만큼 늦춘다 — NextRoundAfterDelay와 동일한 이유
    /// (판정 직후 결과 연출이 재생 중일 때 Clear! 화면이 곧바로 덮어버리는 것을 방지, OXQuizManager와 동일 원칙).</summary>
    IEnumerator ClearAfterDelay()
    {
        if (resolveDelay > 0f)
            yield return new WaitForSeconds(resolveDelay);

        barrierDoor?.Close(); // StageNetworkState._doorOpenStates NV로 전파
        _netState?.ChallengeCleared(true);
    }

    // ── 판정 ──────────────────────────────────────────────────────

    /// <summary>
    /// 타이머 종료 시 Host만 호출. 활성 zone(2개 또는 4개) 물리 오버랩으로 인원·색상 조건을 판정.
    /// [확정 규칙] 정확히 일치해야 성공 — 두 zone 이상 동시 점유·어느 zone에도 없는 생존자가 있으면 그 자체로 실패.
    /// [설계 가정] 실패 시 페널티는 "누가 잘못 섰는지"를 개별로 가려내지 않고 전원 동일 데미지 —
    /// 인원 분배가 팀 전체 조건이라 개인 귀책을 나누기 애매함 (MinigameDesign.md §1.2 damage 결정과 일치).
    /// [축 #4 Q4] 이 메서드는 TimerRoutine의 IsClientOnly 가드 뒤에서만 호출된다 — Host 레인 전용.
    /// </summary>
    void Judge(SideSplitRound round)
    {
        List<ZoneSlot> zones = ActiveZones();
        int n = zones.Count;

        var occupantSets = new HashSet<Player>[n];
        for (int i = 0; i < n; i++)
            occupantSets[i] = new HashSet<Player>(zones[i].zone.GetPlayersInVolume(playerOverlapLayers));

        var actualCounts    = new int[n];
        bool misplaced      = false; // 2곳 이상 동시 점유 또는 어느 zone에도 없는 생존자 존재
        bool colorSatisfied = !round.hasColorRequirement;

        Player[] allPlayers = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (Player p in allPlayers)
        {
            if (p.IsDead) continue;

            int occupiedIndex = -1;
            int occupiedCount = 0;
            for (int i = 0; i < n; i++)
            {
                if (occupantSets[i].Contains(p))
                {
                    occupiedCount++;
                    occupiedIndex = i;
                }
            }

            if (occupiedCount != 1) { misplaced = true; continue; }

            actualCounts[occupiedIndex]++;

            if (round.hasColorRequirement && !colorSatisfied &&
                zones[occupiedIndex].direction == round.colorDirection &&
                p.isUniqueColor && p.playerColorType == round.requiredColor)
            {
                colorSatisfied = true;
            }
        }

        bool countMatch = !misplaced;
        for (int i = 0; i < n; i++)
        {
            if (actualCounts[i] != GetCount(round, zones[i].direction))
            {
                countMatch = false;
                break;
            }
        }

        bool success = countMatch && colorSatisfied;

        SetAllZonesState(success ? SideSplitZone.VisualState.Success : SideSplitZone.VisualState.Fail);

        if (success)
        {
            OnRoundSuccess?.Invoke();
        }
        else
        {
            OnRoundFailed?.Invoke();

            foreach (Player p in allPlayers)
            {
                if (!p.IsDead)
                    NetworkDamageUtil.ApplyDamage(p, wrongDamage);
            }
        }

        // Host는 방금 직접 호출했으니 Client에만 같은 연출을 전파 (RPC 내부에서 IsServer 스킵)
        _netState?.NotifyChallengeOutcomeClientRpc(success);

        // 결과와 관계없이 다음 라운드로 진행.
        // 단, 전원 사망이면 §11 사망 문(전원 씬 리로드)으로 넘어가므로 여기서 추가 진행 불필요.
        bool anyAlive = false;
        for (int i = 0; i < allPlayers.Length; i++)
        {
            if (!allPlayers[i].IsDead)
            {
                anyAlive = true;
                break;
            }
        }

        if (!anyAlive) return;

        _roundIndex++;
        if (_roundIndex >= totalRounds)
        {
            StartCoroutine(ClearAfterDelay());
            return;
        }

        StartCoroutine(NextRoundAfterDelay());
    }

    // ── StageNetworkState 구독 핸들러 (Host/Client 공통 단일 경로) ──

    /// <summary>Host는 Judge에서 직접 호출하므로 이 핸들러는 Client에서만 의미 있음.</summary>
    void HandleChallengeOutcome(bool success)
    {
        if (_netState == null || _netState.ChallengeOwner != ChallengeOwnerType.SideSplit) return;

        SetAllZonesState(success ? SideSplitZone.VisualState.Success : SideSplitZone.VisualState.Fail);

        if (success) OnRoundSuccess?.Invoke();
        else         OnRoundFailed?.Invoke();
    }

    /// <summary>ChallengeCleared NV 변경 시 Host/Client 공통으로 OnAllCleared를 1회 재생.</summary>
    void HandleChallengeClearedChanged(bool cleared)
    {
        if (_netState == null || _netState.ChallengeOwner != ChallengeOwnerType.SideSplit) return;
        if (cleared) OnAllCleared?.Invoke();
    }

    /// <summary>
    /// §11 사망 문 진입 확정(StageNetworkState.OnDeathReloadStarted) — Host/Client 공통 구독.
    /// 사망은 이 챌린지의 판정(Judge)이 감지할 수 없는 축 밖의 사건이라, 여기서 즉시 타이머
    /// 코루틴을 끊어 TimerRoutine이 뒤이어 Despawn된 _netState에 NotifyChallengeOutcomeClientRpc를
    /// 쏘는 것을 원천 차단한다 (OXQuizManager.HandleDeathReloadStarted와 동일 원칙).
    /// </summary>
    void HandleDeathReloadStarted()
    {
        _challengeActive = false;
        StopTimer();
    }

    // ── 유틸 ──────────────────────────────────────────────────────

    void StopTimer()
    {
        if (_timerCoroutine != null)
        {
            StopCoroutine(_timerCoroutine);
            _timerCoroutine = null;
        }
    }

    /// <summary>
    /// StageNetworkState.ChallengeSeed로 전체 라운드 계획(_rounds)을 다시 계산한다.
    /// 전 머신이 같은 시드로 호출하므로 항상 동일한 결과가 나온다 (OXQuizManager.RegenerateQuestionOrder와
    /// 동일 원칙 — 결과 자체를 네트워크로 보내지 않고 "언제든 다시 계산해도 같은 답이 나온다"는 점을 이용).
    /// UnityEngine.Random(전역 상태)을 건드리지 않도록 로컬 System.Random만 사용.
    ///
    /// [생성 순서 — 전 머신 동일해야 함]
    ///  1. 색 조건 포함 라운드 수 결정 (min~maxColorRounds)
    ///  2. 뒤쪽 라운드부터 그 개수만큼 색 조건 배정 (초반 라운드는 항상 색 조건 없음 — MinigameDesign.md §1.2)
    ///  3. 라운드 0..totalRounds-1 순서대로: 활성 방향 전원 분배(고정 순서 Left→Right→Front→Back으로 순차 소진)
    ///     → (색 조건 라운드면) 색 배정 방향·색상 결정
    ///
    /// [4방향 확장 — MinigameDesign.md §1.7] 활성 zone 수(2 또는 4)만 다를 뿐 알고리즘은 동일하게 일반화됨.
    /// 2방향 모드에서는 항상 zones=[Left,Right] 2개뿐이라 기존 leftCount=rng.Next(0,total+1) /
    /// rightCount=total-leftCount 식과 완전히 동일한 결과가 나옴(순차 소진 방식이 2개일 때 그 식과 동치).
    ///
    /// totalPlayers는 GameSession.ActivePlayerCount 기준 — Host/Client가 같은 세션 상태를 보므로
    /// 별도 네트워크 전송 없이도 전 머신 동일 (§9.1.4 D축 세션 SSOT와 동일 전제).
    /// </summary>
    void RegenerateRoundPlan()
    {
        int seed = _netState != null ? _netState.ChallengeSeed : 0;
        var rng  = new System.Random(seed);

        int totalPlayers = GameSession.Instance != null
            ? GameSession.Instance.ActivePlayerCount
            : CountAlivePlayers();

        List<ZoneSlot> zones = ActiveZones();
        int zoneCount = zones.Count;

        int colorRoundCount = totalRounds > 0
            ? Mathf.Clamp(rng.Next(minColorRounds, maxColorRounds + 1), 0, Mathf.Max(0, totalRounds - 1))
            : 0;
        int firstColorRoundIndex = totalRounds - colorRoundCount;

        _rounds = new SideSplitRound[totalRounds];
        for (int i = 0; i < totalRounds; i++)
        {
            var round = new SideSplitRound();

            // 활성 방향을 고정 순서로 순회하며 순차 소진 — 마지막 방향은 나머지 전부를 받음
            // (2방향일 때 원래의 leftCount=rng.Next(0,total+1); rightCount=total-leftCount와 동치).
            int remaining = totalPlayers;
            var counts = new int[zoneCount];
            for (int z = 0; z < zoneCount; z++)
            {
                bool isLast = z == zoneCount - 1;
                int count = isLast ? remaining : (remaining > 0 ? rng.Next(0, remaining + 1) : 0);
                counts[z] = count;
                remaining -= count;

                SetCount(ref round, zones[z].direction, count);
            }

            if (i >= firstColorRoundIndex && totalPlayers > 0)
            {
                // 인원이 0이 아닌 활성 방향들 중에서만 색 조건을 배정 (2방향 로직의 직접 확장 —
                // "0명인 쪽엔 색 조건을 걸 수 없다"는 원래 규칙 그대로).
                var nonZero = new List<int>(zoneCount);
                for (int z = 0; z < zoneCount; z++)
                    if (counts[z] > 0) nonZero.Add(z);

                if (nonZero.Count > 0)
                {
                    round.hasColorRequirement = true;
                    int pick = nonZero.Count == 1 ? nonZero[0] : nonZero[rng.Next(0, nonZero.Count)];
                    round.colorDirection = zones[pick].direction;

                    // 활성 색 목록에서 시드 기반으로 1개 선택 — PlayerSpawnCoordinator→GameSession→기본4색
                    // 폴백 우선순위를 그대로 재사용 (GameSessionColorDistribution.Distribute와 동일 원칙,
                    // totalSlots=1로 호출하면 활성 색 중 시드 기반 1개만 count=1로 뽑혀 반환됨).
                    round.requiredColor = GameSessionColorDistribution.Distribute(1, rng)[0];
                }
            }

            _rounds[i] = round;
        }
    }

    static int CountAlivePlayers()
    {
        Player[] all = FindObjectsByType<Player>(FindObjectsSortMode.None);
        int count = 0;
        for (int i = 0; i < all.Length; i++)
            if (!all[i].IsDead) count++;
        return count;
    }

    // ── 에디터 지원 ───────────────────────────────────────────────

    [ContextMenu("테스트: 챌린지 리셋")]
    void Debug_Reset() => ResetChallenge();
}
