using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 스테이지 매니저.
/// objectives[] 의 목표가 전부 완료되면 OnStageClear 발동.
/// 하나라도 실패하면 OnStageFailed 발동.
///
/// [설정]
///  1. 이 오브젝트 또는 자식에 원하는 Objective 스크립트를 붙임
///  2. objectives[] 에 등록 (비우면 자식에서 자동 수집)
///  3. OnStageClear → 다음 씬 전환, 문 열기 등 연결
/// </summary>
public class StageManager : MonoBehaviour
{
    [Header("목표 목록")]
    [Tooltip("비우면 자식 오브젝트에서 자동 수집")]
    public StageObjective[] objectives;

    [Header("이벤트")]
    public UnityEvent OnStageClear;
    public UnityEvent OnStageFailed;

    bool _isCleared;
    bool _isFailed;
    int  _completedCount;

    public bool IsCleared => _isCleared;
    public bool IsFailed  => _isFailed;

    void Awake()
    {
        if (objectives == null || objectives.Length == 0)
            objectives = GetComponentsInChildren<StageObjective>(true);
    }

    void Start()
    {
        foreach (var obj in objectives)
            if (obj != null) obj.Begin();
    }

    void Update()
    {
        if (_isCleared || _isFailed) return;

        _completedCount = 0;
        for (int i = 0; i < objectives.Length; i++)
        {
            if (objectives[i] == null) continue;

            objectives[i].Tick();

            if (objectives[i].IsFailed)
            {
                _isFailed = true;
                OnStageFailed?.Invoke();
                return;
            }

            if (objectives[i].IsCompleted)
                _completedCount++;
        }

        if (_completedCount >= objectives.Length)
        {
            _isCleared = true;
            OnStageClear?.Invoke();
        }
    }

    // ── 외부 호출 ─────────────────────────────────────────────────

    /// <summary>
    /// 스테이지 상태 초기화 후 모든 Objective 재시작.
    /// PhaseManager.RestartCurrentPhase()에서 자동 호출됨.
    /// </summary>
    public void ResetStage()
    {
        _isCleared      = false;
        _isFailed       = false;
        _completedCount = 0;

        foreach (var obj in objectives)
            if (obj != null) obj.ResetObjective();
    }

    // ── 에디터 지원 ──────────────────────────────────────────────
    [ContextMenu("테스트: 스테이지 클리어")]
    void Debug_Clear()
    {
        _isCleared = true;
        OnStageClear?.Invoke();
    }

    [ContextMenu("테스트: 스테이지 실패")]
    void Debug_Fail()
    {
        _isFailed = true;
        OnStageFailed?.Invoke();
    }
}
