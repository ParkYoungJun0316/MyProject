using UnityEngine;
using UnityEngine.Events;

// ── 데이터 클래스 ────────────────────────────────────────────────

/// <summary>한 행(Row)을 구성하는 O/X 발판 쌍.</summary>
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

/// <summary>한 행에 대응하는 OX 퀴즈 문제.</summary>
[System.Serializable]
public class OXQuestion
{
    [Tooltip("출제할 문제 텍스트 (OnQuestionReady 이벤트로 UI에 전달됨)")]
    [TextArea(2, 4)]
    public string questionText;

    [Tooltip("정답. true = O, false = X")]
    public bool correctAnswerIsO;
}

/// <summary>
/// UnityEvent<string> 직렬화를 위한 래퍼.
/// OnQuestionReady에 연결해 UI 텍스트 컴포넌트에 문제를 표시할 때 사용.
/// </summary>
[System.Serializable]
public class StringEvent : UnityEvent<string> { }

// ── 매니저 ───────────────────────────────────────────────────────

/// <summary>
/// OX 퀴즈 경로 관리자.
/// 2×N 발판 그리드에서 행(Row) 단위로 OX 퀴즈를 진행.
///
/// [설정 방법]
///  1. rows[]: 각 행의 O/X 발판을 순서대로 입력
///      - rows[0~N]: questions[0~N]에 각각 1:1 대응
///  2. questions[]: rows[0]부터 순서대로 문제 입력 (rows 개수와 동일하게)
///  3. 이벤트(OnQuestionReady 등)는 UI 구현 시 연결 — 지금은 비워도 동작
///
/// [초기 상태]
///  rows[0]  → Pending (첫 번째 퀴즈 대상)
///  rows[1~] → Danger (밟으면 즉사)
/// </summary>
public class OXQuizManager : MonoBehaviour
{
    [Header("행 배치 (0번부터 오름차순으로 입력)")]
    public OXRow[] rows = new OXRow[0];

    [Header("문제 목록 (questions[0] = rows[0] 대응, 1:1 매칭)")]
    public OXQuestion[] questions = new OXQuestion[0];

    // ── UI 이벤트 훅 (지금은 비워도 동작 — UI 구현 시 연결) ──────
    [Header("━━ UI 연동 이벤트 — 추후 UI 구현 시 연결 ━━")]

    [Tooltip("새 문제가 활성화될 때 호출. string = 문제 텍스트\n" +
             "→ TextMeshPro UI 텍스트의 SetText() 또는 text 프로퍼티에 연결")]
    public StringEvent OnQuestionReady;

    [Tooltip("정답 시 호출 → 정답 연출, 효과음 등 연결")]
    public UnityEvent OnCorrectAnswer;

    [Tooltip("오답 시 호출 → 오답 연출, 효과음 등 연결")]
    public UnityEvent OnWrongAnswer;

    [Tooltip("모든 행 통과 시 호출 → 문 열기, 다음 스테이지 전환 등 연결")]
    public UnityEvent OnAllCleared;

    [Header("플레이어 (비워두면 자동 탐색)")]
    [Tooltip("비워두면 씬에서 Player를 자동으로 찾음")]
    public Player player;

    // ── Runtime ──────────────────────────────────────────────────
    [Header("Runtime (확인용)")]
    [SerializeField] int  _currentRow;
    [SerializeField] bool _quizActive;

    PlayerEvents _playerEvents;

    void Start()
    {
        if (rows.Length == 0) return;

        // 모든 타일에 manager 참조와 rowIndex 자동 주입 (최초 1회)
        for (int i = 0; i < rows.Length; i++)
        {
            Inject(rows[i].oTile, i);
            Inject(rows[i].xTile, i);
        }

        // 플레이어 이벤트 구독
        if (player == null)
            player = FindFirstObjectByType<Player>();

        if (player != null)
        {
            _playerEvents = player.GetComponent<PlayerEvents>();
            if (_playerEvents != null)
                _playerEvents.OnRespawned += ResetQuiz;
        }

        ResetQuiz();
    }

    void OnDestroy()
    {
        if (_playerEvents != null)
            _playerEvents.OnRespawned -= ResetQuiz;
    }

    /// <summary>
    /// 퀴즈 전체를 처음 상태로 되돌림.
    /// 플레이어 리스폰 시 자동 호출. 외부에서 직접 호출도 가능.
    /// </summary>
    public void ResetQuiz()
    {
        if (rows.Length == 0) return;

        rows[0].SetState(OXQuizTile.TileState.Pending);
        ActivateQuestion(0);

        for (int i = 1; i < rows.Length; i++)
            rows[i].SetState(OXQuizTile.TileState.Danger);
    }

    // ── 공개 API (OXQuizTile.OnCollisionEnter에서 호출) ──────────

    /// <summary>
    /// 플레이어가 Pending 타일을 밟았을 때 호출.
    /// 정답이면 해당 행 Safe + 다음 행 Pending으로 진행.
    /// 오답이면 해당 행 Danger + 플레이어 즉사.
    /// </summary>
    public void OnPlayerAnswer(int rowIndex, bool answeredO, Player player)
    {
        if (!_quizActive || rowIndex != _currentRow) return;

        int qIdx = rowIndex;
        if (qIdx < 0 || qIdx >= questions.Length) return;

        _quizActive = false;

        bool correct = (answeredO == questions[qIdx].correctAnswerIsO);

        if (correct)
        {
            rows[rowIndex].SetState(OXQuizTile.TileState.Safe);
            OnCorrectAnswer?.Invoke();

            int next = rowIndex + 1;
            if (next < rows.Length)
            {
                rows[next].SetState(OXQuizTile.TileState.Pending);
                ActivateQuestion(next);
            }
            else
            {
                OnAllCleared?.Invoke();
            }
        }
        else
        {
            rows[rowIndex].SetState(OXQuizTile.TileState.Danger);
            OnWrongAnswer?.Invoke();
            player.KillInstantly();
        }
    }

    // ── 내부 ─────────────────────────────────────────────────────

    void ActivateQuestion(int rowIndex)
    {
        _currentRow = rowIndex;
        _quizActive = true;

        int qIdx = rowIndex;
        if (qIdx >= 0 && qIdx < questions.Length)
            OnQuestionReady?.Invoke(questions[qIdx].questionText);
    }

    void Inject(OXQuizTile tile, int idx)
    {
        if (tile == null) return;
        tile.quizManager = this;
        tile.rowIndex    = idx;
    }

    // ── 에디터 지원 ──────────────────────────────────────────────

    [ContextMenu("테스트: 전체 Safe")]
    void Debug_AllSafe()
    {
        foreach (var row in rows)
            row.SetState(OXQuizTile.TileState.Safe);
    }

    [ContextMenu("테스트: 전체 Danger")]
    void Debug_AllDanger()
    {
        foreach (var row in rows)
            row.SetState(OXQuizTile.TileState.Danger);
    }

    [ContextMenu("테스트: 초기 상태로 리셋")]
    void Debug_Reset() => ResetQuiz();
}
