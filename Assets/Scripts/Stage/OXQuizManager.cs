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

    public void ClearOccupants()
    {
        oTile?.ClearOccupants();
        xTile?.ClearOccupants();
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
}

/// <summary>UnityEvent&lt;string&gt; 직렬화 래퍼.</summary>
[System.Serializable]
public class StringEvent : UnityEvent<string> { }

/// <summary>UnityEvent&lt;float&gt; 직렬화 래퍼.</summary>
[System.Serializable]
public class FloatEvent : UnityEvent<float> { }

// ── 매니저 ───────────────────────────────────────────────────────

/// <summary>
/// 제자리형 OX 퀴즈 매니저.
///
/// [동작 흐름]
///  1. 퀴즈 시작 → barrierRoot 활성화
///  2. 문제 제시 + 타이머 시작
///  3. 타이머 종료 시 O/X 발판 점유 위치로 개별 판정
///     - 정답 위치 플레이어: 생존
///     - 오답 위치 또는 아무 발판에도 없는 플레이어: 즉사
///  4. 전원 정답 → correctCount++ → 다음 문제 (또는 AllCleared)
///     한 명이라도 오답 → OnWrongAnswer → 리스폰 시 ResetQuiz 호출
///  5. AllCleared → barrierRoot 비활성화
///
/// [barrierRoot]
///  씬에 보이지 않는 벽 오브젝트들을 자식으로 묶은 부모 GameObject.
///  퀴즈 중에만 활성화되어 플레이어가 발판 밖으로 나가지 못하게 막음.
/// </summary>
public class OXQuizManager : MonoBehaviour
{
    [Header("발판 (O/X 한 쌍)")]
    public OXRow row;

    [Header("문제 목록")]
    public OXQuestion[] questions = new OXQuestion[0];

    [Header("퀴즈 설정")]
    [Tooltip("문제당 답변 제한 시간(초). 0보다 커야 위치 판정이 작동함")]
    public float answerTimeLimit = 0f;

    [Tooltip("정답 연출 후 다음 문제까지 대기 시간(초)")]
    public float correctAnswerDelay = 0f;

    [Tooltip("오답 / 타이머 초과 시 부여할 데미지")]
    public int wrongDamage = 0;

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

    [Tooltip("questionsToWin개 달성 시 발동 → 문 열기, 스테이지 전환 등")]
    public UnityEvent OnAllCleared;

    int   _questionIndex;
    int   _correctCount;
    bool  _quizActive;
    int[] _questionOrder;

    Coroutine    _timerCoroutine;
    PlayerEvents _playerEvents;

    int QuestionsToWin => questions.Length;

    // ── 초기화 ────────────────────────────────────────────────────

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
        row?.ClearOccupants();
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
    /// 플레이어가 입장 트리거를 통과했을 때 호출.
    /// PlayerTriggerZone.OnPlayerEnter 이벤트에 연결.
    /// 배리어를 솟아오르게 하고 퀴즈를 시작.
    /// </summary>
    public void StartQuiz()
    {
        barrierDoor?.Open();
        ResetQuiz();
    }

    /// <summary>퀴즈 상태만 리셋. 리스폰 시 자동 호출 (배리어는 건드리지 않음).</summary>
    public void ResetQuiz()
    {
        StopTimer();
        _correctCount  = 0;
        _questionIndex = 0;

        row?.ClearOccupants();
        row?.SetState(OXQuizTile.TileState.Danger);

        ShuffleQuestions();
        StartNextQuestion();
    }

    // ── 내부: 문제 진행 ───────────────────────────────────────────

    void StartNextQuestion()
    {
        if (row == null || questions.Length == 0) return;

        row.ClearOccupants();
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
    ///  - 오답 위치 / 아무 데도 없음: 즉사
    /// 전원 정답이면 다음 문제, 한 명이라도 오답이면 오답 처리.
    /// </summary>
    void JudgeByPosition()
    {
        if (row == null) return;

        bool correctIsO = questions[_questionOrder[_questionIndex]].correctAnswerIsO;

        List<Player> correctOccupants = correctIsO
            ? row.oTile?.GetOccupants() ?? new List<Player>()
            : row.xTile?.GetOccupants() ?? new List<Player>();

        List<Player> wrongOccupants = correctIsO
            ? row.xTile?.GetOccupants() ?? new List<Player>()
            : row.oTile?.GetOccupants() ?? new List<Player>();

        // 아무 발판에도 없는 살아있는 플레이어 수집
        Player[] allPlayers = FindObjectsByType<Player>(FindObjectsSortMode.None);
        var nowhereList = new List<Player>();
        foreach (Player p in allPlayers)
        {
            if (p.IsDead) continue;
            if (!correctOccupants.Contains(p) && !wrongOccupants.Contains(p))
                nowhereList.Add(p);
        }

        bool anyWrong = wrongOccupants.Count > 0 || nowhereList.Count > 0;

        Debug.Log($"[OX 판정] correct={correctOccupants.Count} wrong={wrongOccupants.Count} nowhere={nowhereList.Count} anyWrong={anyWrong}");

        if (!anyWrong && correctOccupants.Count > 0)
        {
            // 전원 정답
            row.SetState(OXQuizTile.TileState.Safe);
            OnCorrectAnswer?.Invoke();
            _correctCount++;

            if (_correctCount >= QuestionsToWin)
            {
                Debug.Log($"[OX AllCleared] barrierDoor={barrierDoor?.name ?? "null"} → Close() 호출");
                barrierDoor?.Close();
                OnAllCleared?.Invoke();
            }
            else
            {
                _questionIndex++;
                StartCoroutine(NextQuestionAfterDelay());
            }
        }
        else
        {
            // 오답자 / 무응답자 즉사
            row.SetState(OXQuizTile.TileState.Danger);
            OnWrongAnswer?.Invoke();

            foreach (Player p in wrongOccupants) p.TakeDamage(wrongDamage);
            foreach (Player p in nowhereList)    p.TakeDamage(wrongDamage);
            // 데미지로 사망 시 리스폰 이벤트 → ResetQuiz 자동 호출
        }
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

    /// <summary>questions[] 전체 셔플 후 QuestionsToWin개 추출.</summary>
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
