using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// OX 퀴즈 전용 UI.
///
/// [Inspector 연결]
///   quizManager  : OXQuizManager
///   quizObjective: OXQuizObjective (OnProgressChanged 구독)
///   mainText     : 문제 / 정답 / 해설을 교체해 표시하는 TMP
///   progressText : "3/5" 형식 진행도 TMP
///   timerText    : 답 선택 중에만 표시되는 타이머 TMP
///
/// [표시 흐름]
///   문제 표시
///   → 타이머 카운트다운 (answerTimeLimit 동안)
///   → 타이머 숨김 + TRUE / FALSE (answerDisplayDuration 초)
///   → 해설 (explanationDisplayDuration 초)
///   → 다음 문제 or Clear! → 패널 숨김
///
/// [주의] 리스너를 Awake에서 등록하므로 이 오브젝트가
///        씬 로드 시 비활성이어도 이벤트를 정상 수신합니다.
/// </summary>
public class OXQuizUI : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] OXQuizManager   quizManager;
    [SerializeField] OXQuizObjective quizObjective;

    [Header("메인 텍스트 (문제 / 정답 / 해설 공용)")]
    [SerializeField] TextMeshProUGUI mainText;

    [Header("진행도 (3/5 형식)")]
    [SerializeField] TextMeshProUGUI progressText;

    [Header("타이머")]
    [SerializeField] TextMeshProUGUI timerText;

    [Header("표시 시간(초)")]
    [SerializeField] float answerDisplayDuration      = 1f;
    [SerializeField] float explanationDisplayDuration = 3f;
    [SerializeField] float clearDisplayDuration       = 2f;

    [Header("텍스트 색상")]
    [SerializeField] Color questionColor    = Color.white;
    [SerializeField] Color trueColor        = new Color(0.2f, 0.9f, 0.3f, 1f);
    [SerializeField] Color falseColor       = new Color(0.95f, 0.2f, 0.2f, 1f);
    [SerializeField] Color explanationColor = new Color(1f, 0.9f, 0.4f, 1f);
    [SerializeField] Color clearColor       = Color.white;

    Coroutine _sequence;

    // ── Unity 라이프사이클 ─────────────────────────────────────────

    void Awake()
    {
        // 리스너를 먼저 등록한 뒤 숨김.
        // Start()에 두면 오브젝트 비활성 → Start 지연 → 첫 OnQuestionReady 수신 실패.
        RegisterListeners();
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        UnregisterListeners();
    }

    // ── 이벤트 구독 ───────────────────────────────────────────────

    void RegisterListeners()
    {
        if (quizManager != null)
        {
            quizManager.OnQuestionReady.AddListener(ShowQuestion);
            quizManager.OnTimerTick.AddListener(SetTimer);
            quizManager.OnAnswerRevealed.AddListener(ShowAnswer);
            quizManager.OnAllCleared.AddListener(ShowClear);
        }

        if (quizObjective != null)
            quizObjective.OnProgressChanged.AddListener(SetProgress);
    }

    void UnregisterListeners()
    {
        if (quizManager != null)
        {
            quizManager.OnQuestionReady.RemoveListener(ShowQuestion);
            quizManager.OnTimerTick.RemoveListener(SetTimer);
            quizManager.OnAnswerRevealed.RemoveListener(ShowAnswer);
            quizManager.OnAllCleared.RemoveListener(ShowClear);
        }

        if (quizObjective != null)
            quizObjective.OnProgressChanged.RemoveListener(SetProgress);
    }

    // ── 수신 메서드 ───────────────────────────────────────────────

    /// <summary>OXQuizManager.OnQuestionReady</summary>
    void ShowQuestion(string question)
    {
        StopSequence();
        gameObject.SetActive(true);
        SetTimerVisible(true);
        SetMain(question, questionColor);
    }

    /// <summary>OXQuizManager.OnTimerTick</summary>
    void SetTimer(float remaining)
    {
        if (timerText == null) return;
        int display = Mathf.CeilToInt(remaining);
        timerText.text = display > 0 ? display.ToString() : "";
    }

    /// <summary>OXQuizManager.OnAnswerRevealed (bool isO, string explanation)</summary>
    void ShowAnswer(bool isO, string explanation)
    {
        StopSequence();
        _sequence = StartCoroutine(AnswerSequence(isO, explanation));
    }

    /// <summary>OXQuizManager.OnAllCleared</summary>
    void ShowClear()
    {
        StopSequence();
        _sequence = StartCoroutine(ClearSequence());
    }

    /// <summary>OXQuizObjective(RoundProgressObjective).OnProgressChanged.
    /// ObjectiveUI의 Count 표시와 동일하게 "정산 완료 수/전체"로 통일.</summary>
    void SetProgress()
    {
        if (progressText == null || quizObjective == null) return;
        progressText.text = $"{quizObjective.PlayedRounds}/{quizObjective.TotalRounds}";
    }

    // ── 내부 시퀀스 ───────────────────────────────────────────────

    IEnumerator AnswerSequence(bool isO, string explanation)
    {
        SetTimerVisible(false);

        SetMain(isO ? "TRUE" : "FALSE", isO ? trueColor : falseColor);
        yield return new WaitForSeconds(answerDisplayDuration);

        SetMain(explanation, explanationColor);
        yield return new WaitForSeconds(explanationDisplayDuration);

        _sequence = null;
        // 매니저의 correctAnswerDelay가 끝나면 OnQuestionReady → ShowQuestion 자동 호출
    }

    IEnumerator ClearSequence()
    {
        SetTimerVisible(false);
        SetMain("Clear!", clearColor);
        yield return new WaitForSeconds(clearDisplayDuration);

        gameObject.SetActive(false);
        _sequence = null;
    }

    // ── 내부 유틸 ─────────────────────────────────────────────────

    void SetMain(string text, Color color)
    {
        if (mainText == null) return;
        mainText.text  = text;
        mainText.color = color;
    }

    void SetTimerVisible(bool visible)
    {
        if (timerText == null) return;
        timerText.gameObject.SetActive(visible);
    }

    void StopSequence()
    {
        if (_sequence == null) return;
        StopCoroutine(_sequence);
        _sequence = null;
    }
}
