using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// SequenceRingMinigame 결과를 StageObjective로 연동.
///
/// [동작]
/// - Begin()              : 이벤트 구독 + 카운터 리셋. StartMinigame()은 호출 안 함.
///                          (Gate.OnCountdownComplete 또는 PhaseManager.onPhaseEnter에서 StartMinigame() 연결)
/// - OnMinigameSuccess    : Complete() → StageManager.OnStageClear → PhaseManager.AdvancePhase
/// - OnMinigameFailed     : Fail()    → StageManager.OnStageFailed → 씬 리셋
/// - Tick()               : 매 프레임 시간/스텝 변화 감지 → OnProgressChanged 발동 (UI 갱신)
///
/// [클리어 조건]
/// - 제한 시간 안에 targetStepCount 완료
///
/// [실패 조건]
/// - 시간 초과 (timePenaltyOnWrong으로 시간이 0 이하가 되는 것 포함)
/// - timeLimit <= 0: UI에서 0으로 표시 (무제한 없음)
///
/// [Inspector 설정]
/// - minigame             : 연동할 SequenceRingMinigame
/// - objectiveName        : UI 표시 이름
/// </summary>
public class SequenceRingObjective : StageObjective
{
    [Header("링 미니게임")]
    [Tooltip("감시할 SequenceRingMinigame.\n" +
             "StartMinigame()은 이 Objective가 호출하지 않습니다.\n" +
             "Gate.OnCountdownComplete 또는 PhaseManager.onPhaseEnter에 연결하세요.")]
    [SerializeField] SequenceRingMinigame minigame;

    [Header("이벤트 (UI 연결용)")]
    [Tooltip("시간 또는 스텝 변화 시 호출. ObjectiveUI가 자동 구독.")]
    public UnityEvent OnProgressChanged;

    // ── 내부 상태 (전 프레임 비교용) ──────────────────────────────

    int   _lastSuccessCount   = -1;
    float _lastTimeRemaining  = -1f;

    // ── 프로퍼티 (UI가 읽어감) ────────────────────────────────────

    /// <summary>남은 스텝 수 (= targetStepCount - successCount). 미니게임 미연결 시 0.</summary>
    public int RemainingSteps =>
        minigame != null ? Mathf.Max(0, minigame.TargetStepCount - minigame.SuccessCount) : 0;

    /// <summary>총 스텝 수. 미니게임 미연결 시 0.</summary>
    public int TotalSteps =>
        minigame != null ? minigame.TargetStepCount : 0;

    /// <summary>남은 시간(초). timeLimit <= 0 이면 0 반환.</summary>
    public float TimeRemaining =>
        minigame != null ? Mathf.Max(0f, minigame.TimeRemaining) : 0f;

    /// <summary>제한 시간. 0 이하면 0 반환.</summary>
    public float TimeLimit =>
        minigame != null ? Mathf.Max(0f, minigame.TimeLimit) : 0f;

    // ── StageObjective 구현 ──────────────────────────────────────

    public override void Begin()
    {
        Unsubscribe();

        _lastSuccessCount  = -1;
        _lastTimeRemaining = -1f;

        if (minigame == null)
        {
            Debug.LogWarning($"[SequenceRingObjective] minigame이 연결되지 않았습니다. ({gameObject.name})");
            return;
        }

        minigame.OnMinigameSuccess.AddListener(HandleSuccess);
        minigame.OnMinigameFailed.AddListener(HandleFail);

        OnProgressChanged?.Invoke();
    }

    public override void Tick()
    {
        if (IsCompleted || IsFailed || minigame == null) return;
        if (minigame.State != SequenceRingMinigame.MinigameState.Playing) return;

        int   curStep = minigame.SuccessCount;
        float curTime = minigame.TimeRemaining;

        bool changed = (curStep != _lastSuccessCount)
                    || (Mathf.Abs(curTime - _lastTimeRemaining) >= 0.09f);

        if (!changed) return;

        _lastSuccessCount  = curStep;
        _lastTimeRemaining = curTime;
        OnProgressChanged?.Invoke();
    }

    // ── 핸들러 ────────────────────────────────────────────────────

    void HandleSuccess()
    {
        OnProgressChanged?.Invoke();
        Complete();
    }

    void HandleFail()
    {
        OnProgressChanged?.Invoke();
        Fail();
    }

    // ── 구독 해제 ─────────────────────────────────────────────────

    void Unsubscribe()
    {
        if (minigame == null) return;
        minigame.OnMinigameSuccess.RemoveListener(HandleSuccess);
        minigame.OnMinigameFailed.RemoveListener(HandleFail);
    }

    void OnDestroy()
    {
        Unsubscribe();
    }
}
