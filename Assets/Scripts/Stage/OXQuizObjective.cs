using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// OX 퀴즈 클리어를 스테이지 목표로 등록하는 Objective.
/// OXQuizManager.OnAllCleared 시 Complete() → StageManager.OnStageClear → 다음 Phase.
///
/// [연동 흐름]
/// - Begin()                      : OXQuizManager 이벤트 구독, 진행 상황 초기화
/// - OXQuizManager.OnQuestionReady → OnProgressChanged 발동 (UI 갱신)
/// - OXQuizManager.OnAllCleared   → Complete() → StageManager 클리어
///
/// [Inspector 설정]
///  - quizManager : 감시할 OXQuizManager
///  - objectiveName (StageObjective 공통) : UI 표시 이름
/// </summary>
public class OXQuizObjective : StageObjective
{
    [Header("OX 퀴즈")]
    [Tooltip("이 Objective가 감시할 OXQuizManager")]
    [SerializeField] OXQuizManager quizManager;

    [Header("이벤트 (UI 연결용)")]
    [Tooltip("문제 번호 갱신. (현재 문제 번호, 전체 문제 수) — ObjectiveUI가 자동 연결")]
    public UnityEvent<int, int> OnProgressChanged;

    // ── 상태 ──────────────────────────────────────────────────────

    int _currentQuestion;
    int _totalQuestions;

    public int CurrentQuestion => _currentQuestion;
    public int TotalQuestions  => _totalQuestions;

    // ── StageObjective 구현 ──────────────────────────────────────

    public override void Begin()
    {
        Unsubscribe();

        if (quizManager == null)
        {
            Debug.LogWarning($"[OXQuizObjective] quizManager가 연결되지 않았습니다. ({gameObject.name})");
            return;
        }

        _totalQuestions = quizManager.TotalQuestions;

        // StartQuiz()가 Begin()보다 먼저 호출된 경우 이미 진행 중인 문제 번호로 동기화.
        // 순서가 올바르면(Begin 먼저) 0에서 시작.
        _currentQuestion = quizManager.IsStarted ? quizManager.CurrentQuestionIndex + 1 : 0;

        quizManager.OnQuestionReady.AddListener(HandleQuestionReady);
        quizManager.OnAllCleared.AddListener(HandleAllCleared);

        OnProgressChanged?.Invoke(_currentQuestion, _totalQuestions);
    }

    public override void Tick() { }

    // ── 내부 핸들러 ───────────────────────────────────────────────

    void HandleQuestionReady(string _)
    {
        if (quizManager == null) return;
        _currentQuestion = quizManager.CurrentQuestionIndex + 1;
        _totalQuestions  = quizManager.TotalQuestions;
        OnProgressChanged?.Invoke(_currentQuestion, _totalQuestions);
    }

    void HandleAllCleared()
    {
        _currentQuestion = _totalQuestions;
        OnProgressChanged?.Invoke(_currentQuestion, _totalQuestions);
        Complete();
    }

    // ── 구독 해제 ─────────────────────────────────────────────────

    void Unsubscribe()
    {
        if (quizManager == null) return;
        quizManager.OnQuestionReady.RemoveListener(HandleQuestionReady);
        quizManager.OnAllCleared.RemoveListener(HandleAllCleared);
    }

    void OnDestroy()
    {
        Unsubscribe();
    }
}
