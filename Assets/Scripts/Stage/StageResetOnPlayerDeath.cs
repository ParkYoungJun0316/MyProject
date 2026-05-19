using UnityEngine;

/// <summary>
/// 플레이어 사망 시 현재 진행 중인 스테이지를 초기화하는 단일 오케스트레이터.
/// 사망 → 스테이지 즉시 리셋 → 게이트 Disarm + 전원 존 리스폰을 이 컴포넌트 한 곳에서 처리.
///
/// [동작 모드]
/// ① PhaseManager 연결 시 → RestartCurrentPhase() (Phase 통째 리셋)
/// ② PhaseManager 없음   → IsStarted && !IsCleared 인 StageManager만 직접 리셋
///
/// [리셋 내용]
/// - 등록된 함정 전부 즉시 중단 (StageManager.DeactivateAllTraps)
/// - 씬 내 투사체 전부 파괴     (StageManager.DestroyAllProjectiles)
/// - Objective 초기화
/// - StageResetter 있으면 부서진 바닥 등 원상 복구
/// - SetActive(false→true) 사이클로 TrapBase.OnEnable 재발동
/// - StageStartGate.OnStageReset() 호출 → Disarm + 전원 존 리스폰 + armDelay 후 재암
///
/// [사용법]
/// 1. 씬에 빈 GameObject 추가 → 이 컴포넌트 부착
/// 2. (선택) phaseManager 필드에 PhaseManager 연결
///    - 비워두면 자동으로 활성 StageManager를 탐색해 직접 리셋
/// 3. (선택) stageStartGate 필드에 StageStartGate 연결
///    - 비워두면 자동 탐색. 없으면 게이트 리셋 단계 생략
///
/// ⚠ StageStartGate는 OnDied를 직접 구독하지 않음.
///    이 컴포넌트가 DoReset() 안에서 OnStageReset()을 호출하므로 중복 처리 없음.
/// </summary>
public class StageResetOnPlayerDeath : MonoBehaviour
{
    [SerializeField] PhaseManager phaseManager;

    [Tooltip("사망 리셋 후 게이트 Disarm + 전원 존 리스폰을 위임할 StageStartGate.\n" +
             "비워두면 씬에서 자동 탐색. 없으면 이 단계 생략.")]
    [SerializeField] StageStartGate stageStartGate;

    Player[] _players;
    bool     _resetPending;

    void Awake()
    {
        if (phaseManager == null)
            phaseManager = FindFirstObjectByType<PhaseManager>();
        if (stageStartGate == null)
            stageStartGate = FindFirstObjectByType<StageStartGate>();
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
        if (_resetPending) return;   // 같은 프레임 내 다중 사망 → 리셋 1회만 수행
        _resetPending = true;
        DoReset();
    }

    void DoReset()
    {
        _resetPending = false;

        // ── ① 스테이지 리셋 ───────────────────────────────────────────
        if (phaseManager != null)
        {
            // PhaseManager 모드: 현재 Phase 통째 리셋
            phaseManager.RestartCurrentPhase();
        }
        else
        {
            // 독립 모드: 현재 진행 중인 StageManager만 직접 리셋
            StageManager[] managers = FindObjectsByType<StageManager>(FindObjectsSortMode.None);
            foreach (StageManager sm in managers)
            {
                if (sm == null)     continue;
                if (!sm.IsStarted)  continue;   // 아직 시작 안 된 스테이지 제외
                if (sm.IsCleared)   continue;   // 이미 클리어된 스테이지 제외

                ResetStageDirect(sm);
            }
        }

        // ── ② 게이트 Disarm + 전원 존 리스폰 + armDelay 후 재암 ────────
        // StageStartGate.OnDied 구독을 제거하고 오케스트레이터에서 일괄 처리
        stageStartGate?.OnStageReset();
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

    void OnDisable()
    {
        UnsubscribePlayers();
    }

    void OnDestroy()
    {
        UnsubscribePlayers();
    }

    void UnsubscribePlayers()
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
