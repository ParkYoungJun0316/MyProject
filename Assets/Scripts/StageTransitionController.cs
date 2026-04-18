using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 스테이지/Phase 전환 시 연출 시퀀스를 조율하는 컨트롤러.
///
/// [전환 순서]
/// 1. Player 이동 잠금
/// 2. MouthController 입 닫기 → 암흑 상태
/// 3. onTransitionMidpoint 호출 → MaterialSwapper.Apply / PhaseManager.AdvancePhase 등 연결
/// 4. 입 열기
/// 5. Player 이동 잠금 해제
/// 6. onTransitionComplete 호출
///
/// [사용법]
/// - 씬에 빈 GameObject 생성 → StageTransitionController 컴포넌트 추가
/// - MouthController, Player 연결
/// - StageManager.OnStageClear → TriggerTransition() 연결
/// - onTransitionMidpoint → MaterialSwapper.Apply(n) / PhaseManager.AdvancePhase 등 연결
/// - onTransitionComplete → 필요 시 다음 스테이지 UI, 효과음 등 연결
/// </summary>
public class StageTransitionController : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("입 닫기/열기를 담당하는 MouthController")]
    [SerializeField] private MouthController mouthController;

    [Tooltip("이동 잠금 대상 Player")]
    [SerializeField] private Player player;

    [Header("이벤트")]
    [Tooltip("입이 완전히 닫혀 암흑 상태일 때 호출.\n" +
             "MaterialSwapper.Apply / PhaseManager.AdvancePhase 등 연결")]
    public UnityEvent onTransitionMidpoint;

    [Tooltip("입이 다시 완전히 열리고 플레이어 잠금 해제 후 호출.\n" +
             "다음 스테이지 UI 표시, 효과음 등 연결")]
    public UnityEvent onTransitionComplete;

    /// <summary>현재 전환 시퀀스가 진행 중이면 true.</summary>
    public bool IsTransitioning { get; private set; }

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>
    /// 전환 시퀀스 시작.
    /// StageManager.OnStageClear 또는 PhaseManager.onPhaseComplete 이벤트에 연결해 사용.
    /// 이미 전환 중이면 무시.
    /// </summary>
    public void TriggerTransition()
    {
        if (IsTransitioning) return;

        if (mouthController == null || player == null)
        {
            Debug.LogWarning("[StageTransitionController] MouthController 또는 Player가 연결되지 않았습니다.", this);
            return;
        }

        IsTransitioning = true;
        player.SetLocked(true);

        mouthController.CloseForTransition(
            onClosed: () => onTransitionMidpoint?.Invoke(),
            onOpened: () =>
            {
                player.SetLocked(false);
                IsTransitioning = false;
                onTransitionComplete?.Invoke();
            }
        );
    }

    // ── 에디터 지원 ──────────────────────────────────────────────
#if UNITY_EDITOR
    [ContextMenu("테스트: 전환 시퀀스 실행")]
    void Debug_Trigger() => TriggerTransition();
#endif
}
