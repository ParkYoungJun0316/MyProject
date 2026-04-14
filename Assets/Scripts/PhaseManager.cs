using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Phase 전환 시 적용할 함정 속도 배율 항목.
/// ArrowTrap 또는 DropTrap을 지정하면 SetPhaseSpeedMultiplier()를 자동 호출.
/// </summary>
[System.Serializable]
public class TrapSpeedEntry
{
    [Tooltip("속도 배율을 적용할 함정 (ArrowTrap 또는 DropTrap)")]
    public TrapBase trap;

    [Tooltip("이 Phase에서 적용할 속도 배율. 1.0 = 기본 속도, 2.0 = 2배 빠르게")]
    public float speedMultiplier = 1f;
}

/// <summary>
/// Phase 1개의 데이터.
/// PhaseManager.phases[] 에 순서대로 등록.
/// </summary>
[System.Serializable]
public class PhaseData
{
    [Tooltip("Inspector 표시용 이름")]
    public string phaseName = "Phase";

    [Tooltip("이 Phase를 클리어하기 위해 살아남아야 하는 시간(초).\n" +
             "0 이면 즉시 다음 Phase로 넘어감 (연출·전환 전용 Phase에 사용).")]
    public float surviveDuration = 30f;

    [Header("오브젝트 제어")]
    [Tooltip("이 Phase 진입 시 비활성화할 오브젝트 (바닥 제거, 장애물 등)")]
    public GameObject[] objectsToDisable;

    [Tooltip("이 Phase 진입 시 활성화할 오브젝트 (새 바닥, 새 함정 오브젝트 등)")]
    public GameObject[] objectsToEnable;

    [Header("함정 제어")]
    [Tooltip("이 Phase 진입 시 Deactivate() 할 함정 (먼저 끄고, 그 다음에 켬)")]
    public TrapBase[] trapsToDeactivate;

    [Tooltip("이 Phase 진입 시 Activate() 할 함정")]
    public TrapBase[] trapsToActivate;

    [Header("속도 배율")]
    [Tooltip("이 Phase에서 각 함정에 적용할 속도 배율.\n" +
             "여기서 설정한 값이 각 함정의 baseSpeed에 곱해짐.\n" +
             "함정 내부 speedPhases(시간 기반 배율)와 독립적으로 동작.")]
    public TrapSpeedEntry[] speedOverrides;

    [Header("세이브포인트")]
    [Tooltip("이 Phase 진입 시 활성화할 세이브포인트 GameObject.\n" +
             "비어 있으면 세이브포인트 제어 없음.")]
    public GameObject savePointObject;

    [Header("진행 방식")]
    [Tooltip("체크 시: surviveDuration 무시. AdvancePhase() 외부 호출이 있어야만 다음 Phase로 넘어감.\n" +
             "StageManager.OnStageClear / Boss 사망 이벤트 등과 연결해서 사용.\n" +
             "미체크 시: surviveDuration 초 후 자동 진행 (연출·생존 구간에 사용).")]
    public bool manualAdvanceOnly = false;

    [Header("이벤트")]
    [Tooltip("이 Phase가 시작될 때 호출")]
    public UnityEvent onPhaseEnter;

    [Tooltip("이 Phase가 완료될 때 호출 (다음 Phase 진입 직전)")]
    public UnityEvent onPhaseComplete;
}

/// <summary>
/// 맵의 Phase 진행을 관리하는 컨트롤러.
///
/// [사용법]
/// 1. 씬에 빈 GameObject 생성 → PhaseManager 컴포넌트 추가
/// 2. phases[] 에 PhaseData를 순서대로 등록
/// 3. 각 PhaseData 에 바닥/함정/속도/세이브포인트 설정
/// 4. 마지막 Phase 완료 이벤트(onAllPhasesComplete)를 StageManager 등과 연결
///
/// [속도 배율 우선순위]
/// 최종 속도 = baseSpeed × (시간 기반 speedPhases 배율) × (Phase 기반 speedMultiplier)
/// → 두 배율이 독립적으로 곱해짐.
/// </summary>
public class PhaseManager : MonoBehaviour
{
    [Header("Phase 목록 (순서대로 진행)")]
    [SerializeField] private PhaseData[] phases;

    [Header("이벤트")]
    [Tooltip("모든 Phase가 완료되었을 때 호출. StageManager.OnStageClear 등에 연결.")]
    public UnityEvent onAllPhasesComplete;

    private int   _currentPhaseIndex = -1;
    private float _phaseElapsed      = 0f;
    private bool  _allPhasesComplete = false;

    public int   CurrentPhaseIndex  => _currentPhaseIndex;
    public float PhaseElapsed       => _phaseElapsed;
    public bool  AllPhasesComplete  => _allPhasesComplete;

    /// <summary>현재 Phase의 남은 시간(초). surviveDuration이 0이면 0 반환.</summary>
    public float PhaseRemaining
    {
        get
        {
            if (phases == null || _currentPhaseIndex < 0 || _currentPhaseIndex >= phases.Length)
                return 0f;
            float dur = phases[_currentPhaseIndex].surviveDuration;
            return dur <= 0f ? 0f : Mathf.Max(0f, dur - _phaseElapsed);
        }
    }

    void Start()
    {
        if (phases != null && phases.Length > 0)
            EnterPhase(0);
    }

    void Update()
    {
        if (_allPhasesComplete) return;
        if (phases == null || _currentPhaseIndex < 0 || _currentPhaseIndex >= phases.Length) return;

        PhaseData phase = phases[_currentPhaseIndex];

        // 수동 진행 모드는 Update에서 자동 처리 안 함
        if (phase.manualAdvanceOnly) return;

        if (phase.surviveDuration <= 0f) return;

        _phaseElapsed += Time.deltaTime;

        if (_phaseElapsed >= phase.surviveDuration)
            PhaseComplete();
    }

    // ── Phase 진입 ────────────────────────────────────────────────────

    void EnterPhase(int index)
    {
        _currentPhaseIndex = index;
        _phaseElapsed      = 0f;

        PhaseData phase = phases[index];

        // 오브젝트 제어 (비활성화 먼저)
        if (phase.objectsToDisable != null)
            foreach (GameObject obj in phase.objectsToDisable)
                if (obj != null) obj.SetActive(false);

        if (phase.objectsToEnable != null)
            foreach (GameObject obj in phase.objectsToEnable)
                if (obj != null) obj.SetActive(true);

        // 함정 제어 (끄기 먼저, 그 다음 켜기)
        if (phase.trapsToDeactivate != null)
            foreach (TrapBase trap in phase.trapsToDeactivate)
                if (trap != null) trap.Deactivate();

        if (phase.trapsToActivate != null)
            foreach (TrapBase trap in phase.trapsToActivate)
                if (trap != null) trap.Activate();

        // Phase별 속도 배율 적용
        ApplySpeedOverrides(phase);

        // 세이브포인트 활성화
        if (phase.savePointObject != null)
            phase.savePointObject.SetActive(true);

        phase.onPhaseEnter?.Invoke();

        // manualAdvanceOnly = true 면 AdvancePhase() 호출 대기, 자동 진행 없음
        if (phase.manualAdvanceOnly) return;

        // surviveDuration = 0 이면 즉시 다음 Phase
        if (phase.surviveDuration <= 0f)
            PhaseComplete();
    }

    // ── Phase 완료 ────────────────────────────────────────────────────

    void PhaseComplete()
    {
        if (_allPhasesComplete) return;

        PhaseData phase = phases[_currentPhaseIndex];
        phase.onPhaseComplete?.Invoke();

        int nextIndex = _currentPhaseIndex + 1;

        if (nextIndex < phases.Length)
        {
            EnterPhase(nextIndex);
        }
        else
        {
            _allPhasesComplete = true;
            onAllPhasesComplete?.Invoke();
        }
    }

    // ── 속도 배율 적용 ────────────────────────────────────────────────

    void ApplySpeedOverrides(PhaseData phase)
    {
        if (phase.speedOverrides == null) return;

        foreach (TrapSpeedEntry entry in phase.speedOverrides)
        {
            if (entry.trap == null) continue;

            if (entry.trap is ArrowTrap arrowTrap)
                arrowTrap.SetPhaseSpeedMultiplier(entry.speedMultiplier);
            else if (entry.trap is DropTrap dropTrap)
                dropTrap.SetPhaseSpeedMultiplier(entry.speedMultiplier);
        }
    }

    // ── 외부 호출 ─────────────────────────────────────────────────────

    /// <summary>
    /// 다음 Phase로 수동 진행.
    /// StageManager.OnStageClear 또는 기타 조건 완료 이벤트와 연결해 사용.
    /// surviveDuration 기반 자동 진행과 동시에 써도 안전 (이미 완료된 Phase는 무시).
    /// </summary>
    public void AdvancePhase()
    {
        if (_allPhasesComplete) return;
        if (phases == null || _currentPhaseIndex < 0) return;
        PhaseComplete();
    }

    /// <summary>
    /// 현재 Phase를 완전히 재시작.
    /// - StageResetter: 날아다니는 투사체 제거 + 부서진 바닥 등 초기 상태 복원
    /// - SetActive(false → true) 사이클: 모든 컴포넌트 OnDisable/OnEnable → 트랩 0초부터 재발사
    /// - StageManager.ResetStage(): 타이머/클리어 상태 초기화
    /// </summary>
    public void RestartCurrentPhase()
    {
        if (phases == null || _currentPhaseIndex < 0) return;

        _allPhasesComplete = false;
        _phaseElapsed      = 0f;

        PhaseData current = phases[_currentPhaseIndex];

        if (current.objectsToEnable != null)
        {
            foreach (GameObject obj in current.objectsToEnable)
            {
                if (obj == null) continue;

                // 1. 투사체 제거 + 하위 오브젝트 초기 상태 복원 (부서진 바닥 등)
                StageResetter resetter = obj.GetComponent<StageResetter>();
                if (resetter != null) resetter.RestoreChildStates();

                // 2. SetActive 사이클 → 모든 TrapBase OnDisable/OnEnable 발동 → 0초부터 재발사
                obj.SetActive(false);
                obj.SetActive(true);
            }
        }

        // 3. StageManager 타이머/클리어 상태 초기화
        StageManager[] managers = FindObjectsByType<StageManager>(FindObjectsSortMode.None);
        foreach (StageManager sm in managers)
            if (sm != null) sm.ResetStage();

        EnterPhase(_currentPhaseIndex);
    }

    // ── 에디터 지원 ───────────────────────────────────────────────────
#if UNITY_EDITOR
    [ContextMenu("테스트: 다음 Phase로 강제 진행")]
    void Debug_NextPhase()
    {
        if (phases == null || _allPhasesComplete) return;
        PhaseComplete();
    }

    [ContextMenu("테스트: Phase 0으로 리셋")]
    void Debug_ResetToPhase0()
    {
        if (phases == null || phases.Length == 0) return;
        _allPhasesComplete = false;
        EnterPhase(0);
    }
#endif
}
