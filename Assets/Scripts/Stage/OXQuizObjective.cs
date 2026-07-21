using Unity.Netcode;
using UnityEngine;

/// <summary>
/// OX 퀴즈 클리어를 스테이지 목표로 등록하는 Objective.
/// OXQuizManager.OnAllCleared 시 Complete() → StageManager.OnStageClear → 다음 Phase.
/// Count 표시 규칙(ObjectiveUI SSOT)에 맞춰 정산 완료된 문제 수 / 전체 문제 수로 노출한다.
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
public class OXQuizObjective : RoundProgressObjective
{
    [Header("OX 퀴즈")]
    [Tooltip("이 Objective가 감시할 OXQuizManager")]
    [SerializeField] OXQuizManager quizManager;

    // ── 상태 ──────────────────────────────────────────────────────

    int _playedRounds;
    int _totalRounds;
    int _currentRoundIndex = -1;

    /// <summary>정산 완료된(정답 처리된) 문제 수.</summary>
    public override int PlayedRounds      => _playedRounds;

    /// <summary>이번 판 총 출제 문제 수.</summary>
    public override int TotalRounds       => _totalRounds;

    /// <summary>현재 진행 중인 문제 인덱스(0부터). 진행 중 아니면 -1.</summary>
    public override int CurrentRoundIndex => _currentRoundIndex;

    // ── StageObjective 구현 ──────────────────────────────────────

    public override void Begin()
    {
        Unsubscribe();

        if (quizManager == null)
        {
            Debug.LogWarning($"[OXQuizObjective] quizManager가 연결되지 않았습니다. ({gameObject.name})");
            return;
        }

        _totalRounds = quizManager.TotalQuestions;

        // StartQuiz()가 Begin()보다 먼저 호출된 경우 이미 진행 중인 문제 번호로 동기화.
        // 순서가 올바르면(Begin 먼저) 진행 전 상태로 시작.
        _currentRoundIndex = quizManager.IsStarted ? quizManager.CurrentQuestionIndex : -1;
        _playedRounds      = Mathf.Max(0, _currentRoundIndex);

        quizManager.OnQuestionReady.AddListener(HandleQuestionReady);
        quizManager.OnAllCleared.AddListener(HandleAllCleared);

        OnProgressChanged?.Invoke();
    }

    public override void Tick() { }

    // ── 내부 핸들러 ───────────────────────────────────────────────

    /// <summary>새 문제가 뜨면 그 이전 문제까지 정산 완료로 집계.</summary>
    void HandleQuestionReady(string _)
    {
        if (quizManager == null) return;
        _currentRoundIndex = quizManager.CurrentQuestionIndex;
        _playedRounds      = _currentRoundIndex;
        _totalRounds       = quizManager.TotalQuestions;
        OnProgressChanged?.Invoke();
    }

    void HandleAllCleared()
    {
        _playedRounds      = _totalRounds;
        _currentRoundIndex = -1;
        OnProgressChanged?.Invoke();

        // [축 SSOT: NetworkDesign.md §11A.2] Complete() 확정은 Host 레인에서만.
        // OXQuizManager.OnAllCleared는 Host/Client 전 머신에서 공통으로 발동되므로 여기서 가드.
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

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
