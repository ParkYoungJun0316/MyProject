using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

// ── 데이터 클래스 ────────────────────────────────────────────────

/// <summary>O/X 발판 한 쌍.</summary>
[System.Serializable]
public class OXRow
{
    [Tooltip("O 발판 (isOSide = true 인 OXQuizTile)")]
    public OXQuizTile oTile;
    [Tooltip("X 발판 (isOSide = false 인 OXQuizTile)")]
    public OXQuizTile xTile;

    public void SetState(OXQuizTile.TileState state)
    {
        oTile?.SetState(state);
        xTile?.SetState(state);
    }
}

/// <summary>OX 퀴즈 문제.</summary>
[System.Serializable]
public class OXQuestion
{
    [Tooltip("출제할 문제 텍스트 (OnQuestionReady 이벤트로 UI에 전달됨)")]
    [TextArea(2, 4)]
    public string questionText;

    [Tooltip("정답. true = O, false = X")]
    public bool correctAnswerIsO;

    [Tooltip("판정 후 정답 공개 시 표시할 해설 (OnAnswerRevealed 두 번째 인자)")]
    [TextArea(2, 5)]
    public string explanationText;
}

/// <summary>UnityEvent&lt;string&gt; 직렬화 래퍼.</summary>
[System.Serializable]
public class StringEvent : UnityEvent<string> { }

/// <summary>UnityEvent&lt;float&gt; 직렬화 래퍼.</summary>
[System.Serializable]
public class FloatEvent : UnityEvent<float> { }

/// <summary>UnityEvent&lt;bool, string&gt; 직렬화 래퍼. bool = 정답이 O면 true.</summary>
[System.Serializable]
public class AnswerRevealEvent : UnityEvent<bool, string> { }

// ── 매니저 ───────────────────────────────────────────────────────

/// <summary>
/// 제자리형 OX 퀴즈 매니저. 자체 Trigger를 통해 퀴즈를 시작합니다.
///
/// [축 SSOT: MStageNetworkBoard.md §1/§2 — 축 #4(챌린지) 확정안]
/// Trigger→RoundStart(Seed)→Generate→Judge→Resolve. Host 레인만 진행을 확정하고,
/// Client는 StageNetworkState의 NV/RPC를 관찰해 동일한 로컬 코드를 재실행할 뿐
/// 독자적으로 판정·진행을 결정하지 않는다 (§11A "Host 레인 하나만" 규칙과 동일).
///
/// [동작 흐름]
///  1. 플레이어가 이 오브젝트의 Trigger 안으로 진입 → Host만 퀴즈 시작 확정 (배리어 열림)
///     Host가 라운드 시드를 생성해 StageNetworkState로 배포
///  2. 문제 제시 + 타이머 시작 (OnQuestionReady) — 전 머신이 동일 시드로 셔플을 재생성하므로
///     Host/Client가 항상 같은 문제를 봄
///  3. 타이머 종료 시(ServerTime 기준, 전 머신 동일 시점) 정답 공개는 로컬에서 각자 재생.
///     실제 물리 오버랩 판정(O/X 발판 위치)은 Host만 수행
///     - 정답 위치 플레이어: 생존
///     - 오답 위치 또는 무응답: wrongDamage 피해 (NetworkDamageUtil, Host만)
///  4. 정답 공개 및 해설 (OnAnswerRevealed) — 문제 데이터에서 로컬 도출, RPC 불필요
///  5. correctAnswerDelay 후 다음 문제로 Host가 진행 확정(StageNetworkState.ChallengeStepBegin)
///  6. 모든 문제가 끝났을 때, 생존자가 1명 이상이면 Host가 클리어 확정
///     - AllCleared → barrierDoor Close(StageNetworkState._doorOpenStates로 전파) + StageNetworkState.ChallengeCleared
/// </summary>
public class OXQuizManager : MonoBehaviour
{
    [Header("발판 (O/X 한 쌍)")]
    public OXRow row;

    [Header("문제 목록")]
    public OXQuestion[] questions = new OXQuestion[0];

    [Header("퀴즈 설정")]
    [Tooltip("한 판에 출제할 문제 수. 0이면 questions 배열 전체.\n" +
             "0보다 크면 풀을 셔플한 뒤 그 개수만 랜덤 출제 (풀보다 많으면 풀 크기만큼).")]
    [SerializeField] int questionsPerRun = 0;

    [Tooltip("문제당 답변 제한 시간(초). 0보다 커야 위치 판정이 작동함")]
    public float answerTimeLimit = 0f;

    [Tooltip("정답 연출 후 다음 문제까지 대기 시간(초)")]
    public float correctAnswerDelay = 0f;

    [Tooltip("오답·무응답 시 플레이어에게 줄 피해량")]
    public int wrongDamage = 1;

    [Header("판정")]
    [Tooltip("발판 Bounds 오버랩 검사에 포함할 레이어. 비어 있으면 실행 시 이름 Player 레이어를 사용합니다.")]
    [SerializeField] LayerMask playerOverlapLayers;

    [Header("배리어 (DoorController)")]
    [Tooltip("벽 역할을 하는 DoorController.\n" +
             "Open() = 벽 솟아오름(퀴즈 시작), Close() = 벽 내려감(퀴즈 종료).\n" +
             "DoorController의 OpenMode는 SlideUp 권장.")]
    public DoorController barrierDoor;

    [Header("UI 이벤트")]
    [Tooltip("새 문제 텍스트 전달 → UI TextMeshPro에 연결")]
    public StringEvent OnQuestionReady;

    [Tooltip("남은 시간(초) 전달 → 타이머 UI에 연결 (0.1초 간격 갱신)")]
    public FloatEvent OnTimerTick;

    [Tooltip("전원 정답 시 발동 → 정답 연출, 효과음 등")]
    public UnityEvent OnCorrectAnswer;

    [Tooltip("한 명 이상 오답/타이머 초과 시 발동 → 오답 연출, 효과음 등")]
    public UnityEvent OnWrongAnswer;

    [Tooltip("판정 후 정답(O/X) 및 해설 전달. bool=true → O, false → X")]
    public AnswerRevealEvent OnAnswerRevealed;

    [Tooltip("이번 판 출제 문제를 모두 진행했을 때 발동 → 문 열기, 스테이지 전환 등")]
    public UnityEvent OnAllCleared;

    int   _questionIndex;
    bool  _quizActive;
    bool  _quizStarted; // 트리거 중복 시작 방지
    int[] _questionOrder;

    Coroutine        _timerCoroutine;
    StageNetworkState _netState; // 구독 해제 시 동일 인스턴스 참조 보장용 캐시
    bool _subscribed;

    int QuestionsToWin
    {
        get
        {
            int pool = questions.Length;
            if (pool == 0) return 0;
            if (questionsPerRun <= 0) return pool;
            return Mathf.Min(questionsPerRun, pool);
        }
    }

    /// <summary>현재 진행 중인 문제 인덱스 (0-based). OXQuizObjective에서 참조.</summary>
    public int CurrentQuestionIndex => _questionIndex;

    /// <summary>이번 판 총 출제 문제 수. OXQuizObjective에서 참조.</summary>
    public int TotalQuestions => QuestionsToWin;

    /// <summary>StartQuiz() 이후 true. OXQuizObjective.Begin()에서 이미 진행 중인지 판별에 사용.</summary>
    public bool IsStarted => _quizStarted;

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
        Inject(row?.oTile, 0);
        Inject(row?.xTile, 0);

        // 초기 상태: 발판 Danger, 배리어 내려간 상태
        row?.SetState(OXQuizTile.TileState.Danger);

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
        _quizActive = false;
    }

    void OnDestroy()
    {
        Unsubscribe();
    }

    /// <summary>
    /// _netState 바인딩 + 구독을 한 곳에 모은 진입점. OnEnable과 Start 양쪽에서 호출되지만
    /// _subscribed 가드로 중복 구독을 막는다 (GridColorChallenge.TryBindAndSubscribe와 동일 원칙).
    /// [버그 수정 2026-07-28] 기존엔 Start/OnDestroy로만 구독을 걸어서, PhaseManager가 다음 Phase에서
    /// 이 오브젝트를 objectsToDisable로 SetActive(false)하면 OnDestroy가 불리지 않아 구독이 계속
    /// 살아있었다 — 그 뒤 다른 챌린지(ColorTile/GridBW/GridColor/SequenceRing)가 공유 슬롯
    /// (_challengeStep)에 쓸 때마다 비활성 상태인 이 오브젝트의 HandleChallengeStepChanged가 깨어나
    /// StartCoroutine(TimerRoutine())을 호출해 "Coroutine on inactive GameObject" 에러가 날 수 있는
    /// 잠재 버그였다(GridColorChallenge 등과 동일 버그 계열). OnEnable/OnDisable로 옮겨 Phase
    /// 비활성화 시 확실히 구독을 끊는다. 바인딩은 그대로 Start()가 맡아 Awake→Start 순서 보장을 쓴다.
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
    /// 외부에서 강제 시작할 때 사용하거나, 자체 트리거에서 호출.
    /// Host 레인만 실제로 진행 — Client의 로컬 트리거 호출은 무시된다 (축 #4 Q2).
    /// 배리어를 솟아오르게 하고 퀴즈를 시작.
    /// </summary>
    public void StartQuiz()
    {
        if (IsClientOnly()) return;

        barrierDoor?.Open(); // StageNetworkState._doorOpenStates NV로 전 클라이언트에 전파

        int seed = Random.Range(int.MinValue, int.MaxValue);
        _netState?.ChallengeStart(seed, ChallengeOwnerType.OX);

        ResetQuiz();
    }

    /// <summary>퀴즈 라운드 재시작. Host 레인만 확정 (§11 사망 리로드로도 도달 가능하나 그 경로는 씬 전체 재생성이라 여기 도달 안 함).</summary>
    public void ResetQuiz()
    {
        if (IsClientOnly()) return;
        if (_netState == null) return;

        StopTimer();
        _questionIndex = 0;
        _netState.ChallengeStepBegin(0);
    }

    // ── 내부: 문제 진행 (전 머신 공통 — StageNetworkState NV 구독) ─────

    /// <summary>
    /// StageNetworkState.OnChallengeStepChanged 구독 핸들러. Host/Client 동일 코드로 문제를 표시한다.
    /// [버그 수정 2026-07-28] _challengeStep은 공유 슬롯이라 다른 챌린지가 쓴 값에도 이 이벤트가
    /// 발동한다 — owner 태그가 내 것(OX)이 아니면 즉시 무시(ChallengeOwnerType 정의부 참고).
    /// </summary>
    void HandleChallengeStepChanged(int stepIndex)
    {
        if (_netState == null || _netState.ChallengeOwner != ChallengeOwnerType.OX) return;
        if (stepIndex < 0 || row == null || questions.Length == 0) return;
        if (!isActiveAndEnabled) return; // OnDisable에서 구독 해제하지만, 해제 타이밍 레이스 방어용 가드

        _quizStarted   = true; // Host/Client 공통 — IsStarted는 이 신호로만 true가 됨 (§ OXQuizObjective.Begin 참조)
        _questionIndex = stepIndex;
        RegenerateQuestionOrder();
        if (_questionIndex >= _questionOrder.Length) return; // 안전장치

        row.SetState(OXQuizTile.TileState.Pending);
        _quizActive = true;

        string text = questions[_questionOrder[_questionIndex]].questionText;
        OnQuestionReady?.Invoke(text);

        StopTimer();
        if (answerTimeLimit > 0f)
            _timerCoroutine = StartCoroutine(TimerRoutine());
    }

    /// <summary>
    /// ServerTime 기준 공통 타이머. 전 머신이 같은 시점에 타임업을 감지해 정답 공개를 동시 재생하고,
    /// Host만 이어서 실제 물리 판정(JudgeByPosition)을 수행한다.
    /// </summary>
    IEnumerator TimerRoutine()
    {
        var nm = NetworkManager.Singleton;

        while (_quizActive)
        {
            double startTime = _netState != null ? _netState.ChallengeStepStartServerTime : 0.0;
            double elapsed    = (nm != null ? nm.ServerTime.Time : 0.0) - startTime;
            float  remaining  = Mathf.Max(0f, answerTimeLimit - (float)elapsed);

            OnTimerTick?.Invoke(remaining);
            if (remaining <= 0f) break;

            yield return new WaitForSeconds(0.1f);
        }

        if (!_quizActive) yield break;
        _quizActive = false;

        // 정답 공개는 문제 데이터에서 로컬로 도출 가능 — 전 머신 동시 재생 (RPC 불필요)
        OXQuestion current = questions[_questionOrder[_questionIndex]];
        ApplyAnswerRevealColors(current.correctAnswerIsO);
        OnAnswerRevealed?.Invoke(current.correctAnswerIsO, current.explanationText);

        if (IsClientOnly()) yield break; // 실제 판정·데미지·진행 확정은 Host만

        JudgeByPosition(current);
    }

    IEnumerator NextQuestionAfterDelay()
    {
        if (correctAnswerDelay > 0f)
            yield return new WaitForSeconds(correctAnswerDelay);

        _netState?.ChallengeStepBegin(_questionIndex);
    }

    /// <summary>
    /// 마지막 문제 클리어 확정을 correctAnswerDelay만큼 늦춘다 — NextQuestionAfterDelay와 동일한
    /// 이유. 이 대기가 없으면 판정 직후(같은 프레임) ChallengeCleared NV가 곧바로 날아가
    /// 아직 해설을 재생 중인 OnAnswerRevealed 연출(Host/Client 로컬 재생)을 Clear! 화면이
    /// 끊어버려 정답/해설이 표시되다 말거나, Client에서 NV·연출 도착 순서가 뒤바뀌면 정답
    /// 화면이 Clear에 덮이지 못한 채 남는 문제(정답 텍스트 고착)가 있었다.
    /// </summary>
    IEnumerator ClearAfterDelay()
    {
        if (correctAnswerDelay > 0f)
            yield return new WaitForSeconds(correctAnswerDelay);

        barrierDoor?.Close(); // StageNetworkState._doorOpenStates NV로 전파
        // OnAllCleared 발동은 HandleChallengeClearedChanged 하나로만 — Host 자신에게도
        // 즉시 OnValueChanged가 발생하므로 여기서 직접 Invoke하면 Host에서 이중 발동된다.
        _netState?.ChallengeCleared(true);
    }

    // ── 판정 ──────────────────────────────────────────────────────

    /// <summary>
    /// 타이머 종료 시 Host만 호출. O/X 발판 점유 위치로 개별 판정.
    ///  - 정답 위치: 생존
    ///  - 오답 위치 / 무응답: wrongDamage 피해
    /// 전원 정답이면 다음 문제, 한 명이라도 오답이면 오답 처리.
    /// [축 #4 Q4] 이 메서드는 TimerRoutine의 IsClientOnly 가드 뒤에서만 호출된다 — Host 레인 전용.
    /// </summary>
    void JudgeByPosition(OXQuestion current)
    {
        if (row == null) return;

        bool correctIsO = current.correctAnswerIsO;

        List<Player> inO = row.oTile?.GetPlayersInVolume(playerOverlapLayers) ?? new List<Player>();
        List<Player> inX = row.xTile?.GetPlayersInVolume(playerOverlapLayers) ?? new List<Player>();

        var onO = new HashSet<Player>(inO);
        var onX = new HashSet<Player>(inX);

        var correctOccupants = new List<Player>();
        var wrongOccupants   = new List<Player>();
        var nowhereList      = new List<Player>();

        Player[] allPlayers = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (Player p in allPlayers)
        {
            if (p.IsDead) continue;

            bool o = onO.Contains(p);
            bool x = onX.Contains(p);

            if (o && x)
            {
                wrongOccupants.Add(p);
                continue;
            }

            if (correctIsO)
            {
                if (o) correctOccupants.Add(p);
                else if (x) wrongOccupants.Add(p);
                else nowhereList.Add(p);
            }
            else
            {
                if (x) correctOccupants.Add(p);
                else if (o) wrongOccupants.Add(p);
                else nowhereList.Add(p);
            }
        }

        bool anyWrong = wrongOccupants.Count > 0 || nowhereList.Count > 0;
        // 정답 공개 연출(ApplyAnswerRevealColors/OnAnswerRevealed)은 TimerRoutine에서
        // 전 머신이 이미 동시 재생 — 여기서 중복 호출하지 않음 (§11A "이중 계산" 금지와 동일 원칙).

        if (!anyWrong)
        {
            OnCorrectAnswer?.Invoke();
        }
        else
        {
            OnWrongAnswer?.Invoke();

            var damaged = new HashSet<Player>();
            foreach (Player p in wrongOccupants)
            {
                if (damaged.Add(p))
                    NetworkDamageUtil.ApplyDamage(p, wrongDamage);
            }

            foreach (Player p in nowhereList)
            {
                if (damaged.Add(p))
                    NetworkDamageUtil.ApplyDamage(p, wrongDamage);
            }
        }

        // Host는 방금 직접 호출했으니 Client에만 같은 연출을 전파 (RPC 내부에서 IsServer 스킵)
        _netState?.NotifyChallengeOutcomeClientRpc(!anyWrong);

        // 문제 결과와 관계없이 다음 문제로 진행.
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

        _questionIndex++;
        if (_questionIndex >= QuestionsToWin)
        {
            // 중간 문제(NextQuestionAfterDelay)와 동일하게 해설 재생 시간을 확보한 뒤 클리어 확정.
            StartCoroutine(ClearAfterDelay());
            return;
        }

        StartCoroutine(NextQuestionAfterDelay());
    }

    // ── StageNetworkState 구독 핸들러 (Host/Client 공통 단일 경로) ──

    /// <summary>Host는 JudgeByPosition에서 직접 호출하므로 이 핸들러는 Client에서만 의미 있음.</summary>
    void HandleChallengeOutcome(bool success)
    {
        if (_netState == null || _netState.ChallengeOwner != ChallengeOwnerType.OX) return; // owner 가드 — 위 HandleChallengeStepChanged와 동일 이유
        if (success) OnCorrectAnswer?.Invoke();
        else         OnWrongAnswer?.Invoke();
    }

    /// <summary>ChallengeCleared NV 변경 시 Host/Client 공통으로 OnAllCleared를 1회 재생.</summary>
    void HandleChallengeClearedChanged(bool cleared)
    {
        if (_netState == null || _netState.ChallengeOwner != ChallengeOwnerType.OX) return; // owner 가드 — 위 HandleChallengeStepChanged와 동일 이유
        if (cleared) OnAllCleared?.Invoke();
    }

    /// <summary>
    /// §11 사망 문 진입 확정(StageNetworkState.OnDeathReloadStarted) — Host/Client 공통 구독.
    /// 사망은 이 퀴즈의 판정(JudgeByPosition)이 감지할 수 없는 챌린지 축 밖의 사건이라, 여기서 즉시
    /// 타이머 코루틴을 끊어 TimerRoutine이 뒤이어 Despawn된 _netState에 NotifyChallengeOutcomeClientRpc를
    /// 쏘는 것을 원천 차단한다 (SequenceRingMinigame.HandleDeathReloadStarted와 동일 원칙).
    /// </summary>
    void HandleDeathReloadStarted()
    {
        _quizActive = false;
        StopTimer();
    }

    // ── 유틸 ──────────────────────────────────────────────────────

    /// <summary>판정 후 정답 발판만 Safe, 오답 발판만 Danger.</summary>
    void ApplyAnswerRevealColors(bool correctIsO)
    {
        if (row?.oTile != null)
            row.oTile.SetState(correctIsO ? OXQuizTile.TileState.Safe : OXQuizTile.TileState.Danger);
        if (row?.xTile != null)
            row.xTile.SetState(correctIsO ? OXQuizTile.TileState.Danger : OXQuizTile.TileState.Safe);
    }

    void StopTimer()
    {
        if (_timerCoroutine != null)
        {
            StopCoroutine(_timerCoroutine);
            _timerCoroutine = null;
        }
    }

    /// <summary>
    /// StageNetworkState.ChallengeSeed로 questions[]를 셔플해 이번 판 출제 순서(_questionOrder)를
    /// 다시 계산한다. [축 #4 Q3] 전 머신이 같은 시드로 호출하므로 항상 동일한 결과가 나온다 —
    /// 결과 자체를 네트워크로 보내지 않고 "언제든 다시 계산해도 같은 답이 나온다"는 점을 이용.
    /// UnityEngine.Random(전역 상태)을 건드리지 않도록 로컬 System.Random만 사용.
    /// </summary>
    void RegenerateQuestionOrder()
    {
        int seed = _netState != null ? _netState.ChallengeSeed : 0;
        var rng  = new System.Random(seed);

        int poolSize = questions.Length;
        int[] pool   = new int[poolSize];
        for (int i = 0; i < poolSize; i++) pool[i] = i;

        for (int i = poolSize - 1; i > 0; i--)
        {
            int j   = rng.Next(0, i + 1);
            int tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
        }

        int useCount   = QuestionsToWin;
        _questionOrder = new int[useCount];
        for (int i = 0; i < useCount; i++)
            _questionOrder[i] = pool[i];
    }

    void Inject(OXQuizTile tile, int idx)
    {
        if (tile == null) return;
        tile.quizManager = this;
        tile.rowIndex    = idx;
    }

    // ── 에디터 지원 ───────────────────────────────────────────────

    [ContextMenu("테스트: 퀴즈 리셋")]
    void Debug_Reset() => ResetQuiz();

    [ContextMenu("테스트: 전체 Safe")]
    void Debug_Safe() => row?.SetState(OXQuizTile.TileState.Safe);

    [ContextMenu("테스트: 전체 Danger")]
    void Debug_Danger() => row?.SetState(OXQuizTile.TileState.Danger);
}
