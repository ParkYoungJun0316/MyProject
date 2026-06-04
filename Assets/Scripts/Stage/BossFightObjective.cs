using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 보스전 전체를 하나로 표현하는 컴포넌트.
/// StageObjective가 아닌 독립 MonoBehaviour — BossFlow 아래에 배치.
///
/// [흐름]
///  1. StageStartGate.OnCountdownComplete → Begin()  (카운터 초기화)
///  2. 각 페이즈의 챌린지가 완료되면 → NotifyPhaseCleared()
///  3. PhaseManager.AdvancePhase() → 다음 아레나 on/off + onPhaseEnter 발동
///  4. onPhaseEnter 에서 다음 챌린지 시작 (PhaseSurviveChallenge.Begin / SequenceRingObjective.Begin / GridBWTileChallenge.Activate)
///  5. 마지막 페이즈 완료 → OnBossDefeated 발동
///
/// [Inspector 연결]
///  totalPhases     : 보스 페이즈 총 수 (PhaseManager.phases 수와 동일하게)
///  phaseManager    : BossFlow의 PhaseManager
///  OnPhaseCleared  : BossHealthBarUI.OnPhaseCleared 연결
///  OnBossDefeated  : 비워둬도 됨 (PhaseManager.onAllPhasesComplete로 씬 전환 처리)
///
/// [각 챌린지 이벤트 연결 — Inspector에서]
///  PhaseSurviveChallenge.OnChallengeComplete    → NotifyPhaseCleared()
///  SequenceRingObjective.OnCompleted            → NotifyPhaseCleared()
///  GridBWTileChallenge.OnChallengeComplete      → NotifyPhaseCleared()
///
/// [챌린지 시작 연결 — PhaseManager.onPhaseEnter 에서]
///  Phase 0 : StageStartGate.OnCountdownComplete → PhaseSurviveChallenge.Begin()
///  Phase 1+: onPhaseEnter → 해당 챌린지 시작 메서드 (Begin / Activate)
/// </summary>
public class BossFightObjective : MonoBehaviour
{
    [Header("페이즈 설정")]
    [Tooltip("보스 페이즈 총 수. PhaseManager의 phases[] 배열 수와 동일하게.")]
    [SerializeField] int totalPhases = 5;

    [Header("연결")]
    [Tooltip("씬의 PhaseManager (BossFlow 아래)")]
    [SerializeField] PhaseManager phaseManager;

    [Header("이벤트")]
    [Tooltip("페이즈 1개 클리어 시 (클리어 수, 전체 수) 전달\n→ BossHealthBarUI.OnPhaseCleared 연결")]
    public UnityEvent<int, int> OnPhaseCleared;

    [Tooltip("모든 페이즈 완료 시 호출 (선택)\n→ PhaseManager.onAllPhasesComplete로 씬 전환 처리 권장")]
    public UnityEvent OnBossDefeated;

    int  _phasesCleared;
    bool _isDefeated;

    public int  PhasesCleared => _phasesCleared;
    public int  TotalPhases   => totalPhases;
    public bool IsDefeated    => _isDefeated;

    // ── 외부 호출 ─────────────────────────────────────────────────

    /// <summary>
    /// 보스전 시작. 카운터만 초기화.
    /// StageStartGate.OnCountdownComplete 에 연결.
    /// Phase 0 챌린지 시작은 별도로 직접 연결할 것.
    /// </summary>
    public void Begin()
    {
        _phasesCleared = 0;
        _isDefeated    = false;

        // 체력바 초기 상태 반영
        OnPhaseCleared?.Invoke(0, totalPhases);
    }

    /// <summary>
    /// 페이즈 1개 클리어 처리.
    ///
    /// 연결 방법 (Inspector):
    ///   PhaseSurviveChallenge.OnChallengeComplete   → 이 메서드
    ///   SequenceRingObjective.OnCompleted           → 이 메서드
    ///   GridBWTileChallenge.OnChallengeComplete     → 이 메서드
    /// </summary>
    public void NotifyPhaseCleared()
    {
        if (_isDefeated) return;

        _phasesCleared++;
        OnPhaseCleared?.Invoke(_phasesCleared, totalPhases);

        // 월드 전환 (다음 아레나 enable/disable + onPhaseEnter 발동 → 다음 챌린지 시작)
        phaseManager?.AdvancePhase();

        if (_phasesCleared >= totalPhases)
        {
            _isDefeated = true;
            OnBossDefeated?.Invoke();
        }
    }

    // ── 에디터 ───────────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: Begin (카운터 초기화)")]
    void Debug_Begin() => Begin();

    [ContextMenu("테스트: 페이즈 1개 강제 클리어")]
    void Debug_ClearPhase() => NotifyPhaseCleared();
#endif
}
