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
/// [게이트 탐색 우선순위 (Inspector 연결 불필요)]
/// 1. IsStarted && !IsCleared 인 StageManager의 LinkedGate (Manager-Gate 짝)
/// 2. 현재 Armed 상태인 StageStartGate
/// 3. 씬에 있는 아무 StageStartGate (fallback + 에디터 경고)
///
/// [사용법]
/// 1. 씬에 빈 GameObject 추가 → 이 컴포넌트 부착
/// 2. (선택) phaseManager 필드에 PhaseManager 연결
///    - 비워두면 자동으로 활성 PhaseManager를 탐색해 연결
///
/// ⚠ StageStartGate는 OnDied를 직접 구독하지 않음.
///    이 컴포넌트가 DoReset() 안에서 OnStageReset()을 호출하므로 중복 처리 없음.
/// </summary>
public class StageResetOnPlayerDeath : MonoBehaviour
{
    [SerializeField] PhaseManager phaseManager;

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
        if (_resetPending) return;   // 같은 프레임 내 다중 사망 → 리셋 1회만 수행
        _resetPending = true;

        // 게이트는 리셋 이전 시점에 캡처해야 함
        // (PhaseManager.RestartCurrentPhase 이후엔 IsStarted 상태가 바뀔 수 있음)
        StageStartGate gate = FindActiveGate();
        DoReset(gate);
    }

    /// <summary>
    /// 사망 시점에 리스폰에 사용할 StageStartGate를 결정.
    /// 1순위: IsStarted && !IsCleared 인 StageManager의 LinkedGate
    /// 2순위: 현재 Armed 상태인 게이트 (스테이지 시작 전 대기 중)
    /// 3순위: 씬 전체 fallback
    /// </summary>
    StageStartGate FindActiveGate()
    {
        // 1순위: 진행 중 StageManager의 짝 게이트
        StageManager[] managers = FindObjectsByType<StageManager>(FindObjectsSortMode.None);
        foreach (StageManager sm in managers)
        {
            if (sm == null || !sm.IsStarted || sm.IsCleared) continue;
            if (sm.LinkedGate != null) return sm.LinkedGate;
        }

        // 2순위: Armed 상태 게이트 (게이트 앞에서 죽은 경우 등)
        StageStartGate[] gates = FindObjectsByType<StageStartGate>(FindObjectsSortMode.None);
        foreach (StageStartGate g in gates)
            if (g != null && g.IsArmed) return g;

        // 3순위: 씬에 있는 아무 게이트 (fallback)
        if (gates.Length > 0)
        {
#if UNITY_EDITOR
            Debug.LogWarning("[StageResetOnPlayerDeath] 진행 중인 StageManager나 Armed 게이트를 찾지 못해 fallback 게이트를 사용합니다.");
#endif
            return gates[0];
        }

        return null;
    }

    void DoReset(StageStartGate gate)
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
                if (sm == null)    continue;
                if (!sm.IsStarted) continue;   // 아직 시작 안 된 스테이지 제외
                if (sm.IsCleared)  continue;   // 이미 클리어된 스테이지 제외

                ResetStageDirect(sm);
            }
        }

        // ── ② 게이트 Disarm + 전원 존 리스폰 + armDelay 후 재암 ────────
        gate?.OnStageReset();
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
    void Debug_ForceReset() => DoReset(FindActiveGate());
#endif
}
