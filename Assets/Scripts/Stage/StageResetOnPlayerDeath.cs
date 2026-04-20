using UnityEngine;

/// <summary>
/// 플레이어 사망 시 현재 진행 중인 스테이지를 초기화.
///
/// [동작 모드]
/// ① PhaseManager 연결 시 → RestartCurrentPhase() (PhaseManager 기반 전체 리셋)
/// ② PhaseManager 없음   → IsStarted && !IsCleared 인 StageManager만 직접 리셋
///    (stage2에서 죽으면 stage2만, stage3에서 죽으면 stage3만 리셋됨)
///
/// [리셋 내용]
/// - 등록된 함정 전부 즉시 중단 (StageManager.DeactivateAllTraps)
/// - 씬 내 투사체 전부 파괴     (StageManager.DestroyAllProjectiles)
/// - Objective 초기화
/// - StageResetter 있으면 부서진 바닥 등 원상 복구
/// - SetActive(false→true) 사이클로 TrapBase.OnEnable 재발동
///
/// [사용법]
/// 1. 씬에 빈 GameObject 추가 → 이 컴포넌트 부착
/// 2. (선택) phaseManager 필드에 PhaseManager 연결
///    - PhaseManager 없으면 자동으로 활성 StageManager를 탐색해 직접 리셋
/// 3. resetDelay: 사망 연출 후 리셋까지 대기 시간 (보통 0~1초)
/// </summary>
public class StageResetOnPlayerDeath : MonoBehaviour
{
    [SerializeField] PhaseManager phaseManager;

    [Tooltip("리셋까지 대기 시간(초). 0이면 사망 즉시 리셋.\n" +
             "사망 연출이 끝난 뒤 리셋하고 싶으면 Player의 respawnDelay보다 짧게 설정.")]
    [SerializeField] float resetDelay = 0f;

    Player[] _players;
    bool     _resetPending;

    void Awake()
    {
        if (phaseManager == null)
            phaseManager = FindFirstObjectByType<PhaseManager>();
    }

    void Start()
    {
        SubscribePlayers();
    }

    void SubscribePlayers()
    {
        _players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (Player p in _players)
        {
            PlayerEvents ev = p.GetComponent<PlayerEvents>();
            if (ev != null) ev.OnDied += OnAnyPlayerDied;
        }
    }

    void OnAnyPlayerDied()
    {
        if (_resetPending) return;   // 이미 리셋 예약됨 → 중복 방지
        _resetPending = true;

        if (resetDelay <= 0f)
            DoReset();
        else
            Invoke(nameof(DoReset), resetDelay);
    }

    void DoReset()
    {
        _resetPending = false;

        // ── ① PhaseManager 모드 ──────────────────────────────────────
        if (phaseManager != null)
        {
            phaseManager.RestartCurrentPhase();
            return;
        }

        // ── ② 독립 모드: 현재 진행 중인 StageManager만 직접 리셋 ──────
        //   "stage2에서 죽으면 stage2만, stage3에서 죽으면 stage3만"
        StageManager[] managers = FindObjectsByType<StageManager>(FindObjectsSortMode.None);
        foreach (StageManager sm in managers)
        {
            if (sm == null)        continue;
            if (!sm.IsStarted)     continue;   // 아직 시작 안 된 스테이지 제외
            if (sm.IsCleared)      continue;   // 이미 클리어된 스테이지 제외

            ResetStageDirect(sm);
        }
    }

    /// <summary>
    /// PhaseManager 없이 StageManager를 직접 리셋.
    /// DeactivateAllTraps + DestroyAllProjectiles + SetActive 사이클 포함.
    /// </summary>
    void ResetStageDirect(StageManager sm)
    {
        GameObject root = sm.gameObject;

        // 1. 부서진 오브젝트 복구 (StageResetter가 자식에만 있어도 탐색)
        StageResetter resetter = root.GetComponentInChildren<StageResetter>(true)
                              ?? root.GetComponentInParent<StageResetter>();
        resetter?.RestoreChildStates();

        // 2. 함정 중단 + 투사체 제거 + Objective 초기화
        //    (StageManager.ResetStage 내부에서 DeactivateAllTraps / DestroyAllProjectiles 호출)
        sm.ResetStage();

        // 3. SetActive 사이클 → 모든 TrapBase.OnDisable/OnEnable 발동
        //    (StageManager가 관리하지 않는 독립 함정도 여기서 리셋)
        root.SetActive(false);
        root.SetActive(true);
    }

    void OnDestroy()
    {
        if (_players == null) return;
        foreach (Player p in _players)
        {
            if (p == null) continue;
            PlayerEvents ev = p.GetComponent<PlayerEvents>();
            if (ev != null) ev.OnDied -= OnAnyPlayerDied;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: 강제 리셋")]
    void Debug_ForceReset() => DoReset();
#endif
}
