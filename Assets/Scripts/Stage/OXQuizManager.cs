using System.Collections;
using System.Collections.Generic;
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
/// [동작 흐름]
///  1. 플레이어가 이 오브젝트의 Trigger 안으로 진입 → 퀴즈 시작 (배리어 닫힘)
///  2. 문제 제시 + 타이머 시작 (OnQuestionReady)
///  3. 타이머 종료 시 물리 오버랩으로 O/X 발판 위치 개별 판정
///     - 정답 위치 플레이어: 생존
///     - 오답 위치 또는 무응답: wrongDamage 피해 (인스펙터)
///  4. 정답 공개 및 해설 (OnAnswerRevealed)
///  5. correctAnswerDelay 후 다음 문제로 진행
///     - 오답/무응답 플레이어는 wrongDamage 피해
///     - 사망자는 리스폰 이벤트로 ResetQuiz
///  6. 모든 문제가 끝났을 때, 생존자가 1명 이상이면 클리어
///     - AllCleared → barrierDoor Close
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

    Coroutine    _timerCoroutine;
    PlayerEvents _playerEvents;

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

        Player p = FindFirstObjectByType<Player>();
        if (p != null)
        {
            _playerEvents = p.GetComponent<PlayerEvents>();
            if (_playerEvents != null)
                _playerEvents.OnRespawned += ResetQuiz;
        }

        // 초기 상태: 발판 Danger, 배리어 내려간 상태
        row?.SetState(OXQuizTile.TileState.Danger);
        ShuffleQuestions();
    }

    void OnDestroy()
    {
        if (_playerEvents != null)
            _playerEvents.OnRespawned -= ResetQuiz;
    }

    // ── 공개 API ──────────────────────────────────────────────────

    /// <summary>
    /// 외부에서 강제 시작할 때 사용하거나, 자체 트리거에서 호출.
    /// 배리어를 솟아오르게 하고 퀴즈를 시작.
    /// </summary>
    public void StartQuiz()
    {
        _quizStarted = true;
        barrierDoor?.Open();
        ResetQuiz();
    }

    /// <summary>퀴즈 상태만 리셋. 리스폰 시 자동 호출 (배리어는 건드리지 않음).</summary>
    public void ResetQuiz()
    {
        StopTimer();
        _questionIndex = 0;

        row?.SetState(OXQuizTile.TileState.Danger);

        ShuffleQuestions();
        StartNextQuestion();
    }

    // ── 내부: 문제 진행 ───────────────────────────────────────────

    void StartNextQuestion()
    {
        if (row == null || questions.Length == 0) return;

        row.SetState(OXQuizTile.TileState.Pending);
        _quizActive = true;

        string text = questions[_questionOrder[_questionIndex]].questionText;
        OnQuestionReady?.Invoke(text);

        if (answerTimeLimit > 0f)
            _timerCoroutine = StartCoroutine(TimerRoutine());
    }

    IEnumerator TimerRoutine()
    {
        float remaining = answerTimeLimit;

        while (remaining > 0f && _quizActive)
        {
            OnTimerTick?.Invoke(remaining);
            float wait = Mathf.Min(0.1f, remaining);
            yield return new WaitForSeconds(wait);
            remaining -= wait;
        }

        if (!_quizActive) yield break;

        _quizActive = false;
        JudgeByPosition();
    }

    IEnumerator NextQuestionAfterDelay()
    {
        if (correctAnswerDelay > 0f)
            yield return new WaitForSeconds(correctAnswerDelay);

        StartNextQuestion();
    }

    // ── 판정 ──────────────────────────────────────────────────────

    /// <summary>
    /// 타이머 종료 시 호출. O/X 발판 점유 위치로 개별 판정.
    ///  - 정답 위치: 생존
    ///  - 오답 위치 / 무응답: wrongDamage 피해
    /// 전원 정답이면 다음 문제, 한 명이라도 오답이면 오답 처리.
    /// </summary>
    void JudgeByPosition()
    {
        if (row == null) return;

        bool correctIsO = questions[_questionOrder[_questionIndex]].correctAnswerIsO;

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

        OXQuestion current = questions[_questionOrder[_questionIndex]];

        bool anyWrong = wrongOccupants.Count > 0 || nowhereList.Count > 0;
        ApplyAnswerRevealColors(correctIsO);

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
                    p.TakeDamage(wrongDamage);
            }

            foreach (Player p in nowhereList)
            {
                if (damaged.Add(p))
                    p.TakeDamage(wrongDamage);
            }
        }

        OnAnswerRevealed?.Invoke(current.correctAnswerIsO, current.explanationText);

        // 문제 결과와 관계없이 다음 문제로 진행.
        // 단, 전원 사망이면 리스폰 이벤트를 통해 ResetQuiz가 호출되므로 여기서 추가 진행하지 않음.
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
            barrierDoor?.Close();
            OnAllCleared?.Invoke();
            return;
        }

        StartCoroutine(NextQuestionAfterDelay());
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

    /// <summary>questions[] 셔플 후 이번 판 출제 개수(QuestionsToWin)만 _questionOrder에 담음.</summary>
    void ShuffleQuestions()
    {
        int poolSize = questions.Length;
        int[] pool   = new int[poolSize];
        for (int i = 0; i < poolSize; i++) pool[i] = i;

        for (int i = poolSize - 1; i > 0; i--)
        {
            int j   = Random.Range(0, i + 1);
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
