using Unity.Netcode;
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
///  totalPhases       : 보스 페이즈 총 수 (PhaseManager.phases 수와 동일하게)
///  phaseManager      : BossFlow의 PhaseManager
///  trapCleanupManager: 씬의 StageManager_BossN 중 아무거나 하나 (DestroyAllProjectiles가
///                      FindObjectsByType로 씬 전역 검색이라 어떤 인스턴스를 연결해도 동일하게 동작)
///  OnPhaseCleared    : BossHealthBarUI.OnPhaseCleared 연결
///  OnBossDefeated    : 비워둬도 됨 (PhaseManager.onAllPhasesComplete로 씬 전환 처리)
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

    [Tooltip("발사체 정리용 StageManager 참조. 씬의 StageManager_Boss1~5 중 아무거나 하나 연결.\n" +
             "DestroyAllProjectiles()는 씬 전역 검색이라 어떤 인스턴스를 연결해도 동일하게 동작함.")]
    [SerializeField] StageManager trapCleanupManager;

    [Header("이벤트")]
    [Tooltip("페이즈 1개 클리어 시 (클리어 수, 전체 수) 전달\n→ BossHealthBarUI.OnPhaseCleared 연결")]
    public UnityEvent<int, int> OnPhaseCleared;

    [Tooltip("모든 페이즈 완료 시 호출 (선택)\n→ PhaseManager.onAllPhasesComplete로 씬 전환 처리 권장")]
    public UnityEvent OnBossDefeated;

    int  _phasesCleared;
    bool _isDefeated;
    StageNetworkState _netState;

    public int  PhasesCleared => _phasesCleared;
    public int  TotalPhases   => totalPhases;
    public bool IsDefeated    => _isDefeated;

    // ── Unity ────────────────────────────────────────────────────

    void Start()
    {
        // Unity 전역 Awake→Start 순서로 보장받음 (OXQuizManager/ColorTileChallenge와 동일 전제).
        _netState = StageNetworkState.Instance;
        if (_netState != null)
            _netState.OnBossPhasesClearedChanged += HandleBossPhasesClearedChanged;
    }

    void OnDestroy()
    {
        if (_netState != null)
            _netState.OnBossPhasesClearedChanged -= HandleBossPhasesClearedChanged;
    }

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

        // Host: NV도 0으로 리셋 (Client가 호출해도 SetBossPhasesCleared 내부 가드로 no-op).
        _netState?.SetBossPhasesCleared(0);

        // 체력바 초기 상태 반영 (전 머신 로컬 — 값이 이미 0이라 NV 변경 이벤트가 안 뜰 수 있어 직접 발동)
        OnPhaseCleared?.Invoke(0, totalPhases);
    }

    /// <summary>
    /// 페이즈 1개 클리어 처리. Host 레인에서만 실제로 진행한다 — 이 메서드를 호출하는
    /// 챌린지들(PhaseSurviveChallenge/SequenceRingObjective/GridBWTileChallenge)은 이미
    /// 전부 Host 판정 확정 후에만 호출하도록 가드돼 있지만, 방어적으로 한 번 더 막는다.
    ///
    /// 연결 방법 (Inspector):
    ///   PhaseSurviveChallenge.OnChallengeComplete   → 이 메서드
    ///   SequenceRingObjective.OnCompleted           → 이 메서드
    ///   GridBWTileChallenge.OnChallengeComplete     → 이 메서드
    /// </summary>
    public void NotifyPhaseCleared()
    {
        if (IsClientOnly()) return;
        if (_isDefeated) return;

        _phasesCleared++;

        // Host: NV 복제 → 전 머신 HandleBossPhasesClearedChanged가 OnPhaseCleared 발동
        // (Host 자신도 이 콜백을 통해 발동됨 — 여기서 직접 Invoke하지 않음).
        _netState?.SetBossPhasesCleared(_phasesCleared);

        // [버그 수정 2026-07-29] 이전 페이즈에서 발사된 TrapProjectile(ArrowTrap 화살 등)이
        // Phase GameObject의 자식이 아니라 씬 루트에 생성되므로, PhaseManager의 objectsToDisable
        // (Phase 계층 SetActive(false))로는 정리되지 않고 다음 페이즈까지 그대로 날아다녔다.
        // 일반 스테이지는 StageManager.Update()가 자기 objectives 완료를 감지해 자동으로
        // DeactivateAllTraps+DestroyAllProjectiles를 호출하지만, 보스는 StageManager.objectives가
        // 비어있고 클리어 판정이 이 메서드(BossFightObjective)로 완전히 분리돼 있어 그 자동 청소를
        // 한 번도 안 탔다. 페이즈 전환 직전에 여기서 명시적으로 호출해 동일한 청소를 보장한다.
        trapCleanupManager?.DestroyAllProjectiles();

        // 월드 전환 (다음 아레나 enable/disable + onPhaseEnter 발동 → 다음 챌린지 시작)
        phaseManager?.AdvancePhase();

        if (_phasesCleared >= totalPhases)
        {
            _isDefeated = true;
            OnBossDefeated?.Invoke();
        }
    }

    void HandleBossPhasesClearedChanged(int cleared) => OnPhaseCleared?.Invoke(cleared, totalPhases);

    static bool IsClientOnly()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && !nm.IsServer;
    }

    // ── 에디터 ───────────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: Begin (카운터 초기화)")]
    void Debug_Begin() => Begin();

    [ContextMenu("테스트: 페이즈 1개 강제 클리어")]
    void Debug_ClearPhase() => NotifyPhaseCleared();
#endif
}
