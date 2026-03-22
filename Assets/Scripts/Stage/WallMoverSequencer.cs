using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 벽 순서 이동 시퀀서.
///
/// [동작]
///  플레이어가 트리거 존에 진입(또는 외부에서 Activate() 호출)하면
///  wallEntries 목록의 벽들이 순서대로 이동 시작.
///  각 항목의 delayAfterPrevious로 간격 조정.
///
/// [사용법]
///  1. 빈 GameObject 생성 → 이 컴포넌트 추가
///  2. Collider 추가 → Is Trigger = true (트리거존으로 사용 시)
///  3. wallEntries에 WallMover 오브젝트와 딜레이 설정
///  4. activateOnPlayerTrigger = true이면 플레이어 진입 시 자동 실행
///     false이면 Activate()를 PhaseManager UnityEvent 등에 연결
///
/// [예시 구성]
///  Entry 0: wall_Left,  delayAfterPrevious = 0    → 즉시 시작
///  Entry 1: wall_Right, delayAfterPrevious = 1.5  → 0번 시작 1.5초 후 시작
///  Entry 2: wall_Top,   delayAfterPrevious = 2.0  → 1번 시작 2.0초 후 시작
/// </summary>
public class WallMoverSequencer : MonoBehaviour
{
    [System.Serializable]
    public struct WallEntry
    {
        [Tooltip("이동시킬 WallMover 컴포넌트")]
        public WallMover wall;

        [Tooltip("이전 벽이 이동 시작한 후 이 벽이 시작할 때까지 대기(초). 0 = 이전 벽과 동시")]
        public float delayAfterPrevious;
    }

    [Header("벽 시퀀스")]
    [Tooltip("순서대로 실행할 벽 목록. 위에서부터 순서대로 실행됨")]
    public WallEntry[] wallEntries = new WallEntry[0];

    [Header("트리거 설정")]
    [Tooltip("true: 플레이어가 Collider에 진입하면 자동 실행\n" +
             "false: Activate()를 외부(PhaseManager 등)에서 직접 호출")]
    public bool activateOnPlayerTrigger = true;

    [Tooltip("한 번만 트리거 허용. false이면 플레이어가 재진입 시 재실행")]
    public bool activateOnce = true;

    [Header("이벤트")]
    [Tooltip("시퀀스 시작 시 호출")]
    public UnityEvent OnSequenceStarted;

    [Tooltip("모든 벽 이동이 완료됐을 때 호출")]
    public UnityEvent OnSequenceCompleted;

    [Header("Runtime (확인용)")]
    [SerializeField] bool _isRunning;
    [SerializeField] bool _hasActivated;

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>시퀀스 시작. 이미 실행 중이면 무시.</summary>
    public void Activate()
    {
        if (_isRunning) return;
        if (activateOnce && _hasActivated) return;

        _hasActivated = true;
        StartCoroutine(SequenceRoutine());
    }

    /// <summary>모든 벽을 시작 위치로 리셋하고 재사용 가능 상태로 복귀.</summary>
    public void ResetAll()
    {
        StopAllCoroutines();
        _isRunning = false;

        if (wallEntries == null) return;
        for (int i = 0; i < wallEntries.Length; i++)
            if (wallEntries[i].wall != null)
                wallEntries[i].wall.ResetToStart();

        if (!activateOnce) _hasActivated = false;
    }

    // ── 트리거 감지 ──────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (!activateOnPlayerTrigger) return;

        Player player = other.GetComponentInParent<Player>();
        if (player == null || player.IsDead) return;

        Activate();
    }

    // ── 내부 ────────────────────────────────────────────────────

    IEnumerator SequenceRoutine()
    {
        _isRunning = true;
        OnSequenceStarted?.Invoke();

        if (wallEntries != null)
        {
            for (int i = 0; i < wallEntries.Length; i++)
            {
                float delay = wallEntries[i].delayAfterPrevious;
                if (delay > 0f)
                    yield return new WaitForSeconds(delay);

                wallEntries[i].wall?.Activate();
            }
        }

        // 마지막 벽의 이동 완료까지 대기 후 OnSequenceCompleted 발동
        if (wallEntries != null && wallEntries.Length > 0)
        {
            WallMover lastWall = wallEntries[wallEntries.Length - 1].wall;
            if (lastWall != null)
            {
                // WallMover.OnMoveCompleted 이벤트를 직접 구독하지 않고
                // moveDuration을 참고해 대기 (단순하고 의존성 없음)
                yield return new WaitForSeconds(lastWall.moveDuration);
            }
        }

        _isRunning = false;
        OnSequenceCompleted?.Invoke();
    }

    // ── 에디터 지원 ──────────────────────────────────────────────

    [ContextMenu("테스트: 시퀀스 시작")]
    void Debug_Activate()
    {
        _hasActivated = false;
        Activate();
    }

    [ContextMenu("테스트: 전체 리셋")]
    void Debug_Reset() => ResetAll();
}
