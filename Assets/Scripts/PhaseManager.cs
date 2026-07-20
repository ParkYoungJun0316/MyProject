using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

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
/// 3. 각 PhaseData 에 오브젝트 활성/비활성 설정
/// 4. 마지막 Phase 완료 이벤트(onAllPhasesComplete)를 SceneFlowManager.LoadNextScene 등에 연결
/// </summary>
public class PhaseManager : MonoBehaviour
{
    public static PhaseManager Instance { get; private set; }

    [Header("Phase 목록 (순서대로 진행)")]
    [SerializeField] private PhaseData[] phases;

    [Header("이벤트")]
    [Tooltip("모든 Phase가 완료되었을 때 호출. SceneFlowManager.LoadNextScene 등에 연결.")]
    public UnityEvent onAllPhasesComplete;

    private int   _currentPhaseIndex = -1;
    private float _phaseElapsed      = 0f;
    private bool  _allPhasesComplete = false;

    public int   CurrentPhaseIndex => _currentPhaseIndex;
    public float PhaseElapsed      => _phaseElapsed;
    public bool  AllPhasesComplete => _allPhasesComplete;

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

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (phases == null || phases.Length == 0) return;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer)
        {
            // Client: EnterPhase() 호출 금지 — Host SyncPhase → EnterPhaseOnClient()만 따름
            var sns = StageNetworkState.Instance;
            if (sns != null && sns.CurrentPhase >= 0)
                EnterPhaseOnClient(sns.CurrentPhase);
            return;
        }

        EnterPhase(0);
    }

    void Update()
    {
        if (_allPhasesComplete) return;
        if (phases == null || _currentPhaseIndex < 0 || _currentPhaseIndex >= phases.Length) return;

        // Client는 타이머를 돌리지 않음 — Phase 진행은 Host가 결정하고
        // StageNetworkState._currentPhase NetworkVariable → EnterPhaseOnClient() 경로로 수신
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

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

        // 이 Phase가 발동하는 함정(ArrowTrap/DropTrap 등)의 스케줄 기준 시각을 여기서 기록.
        // onPhaseEnter가 StageManager.StartStage() → trap.Activate()를 호출하므로 그 직전에 찍어야
        // Host/Client가 같은 절대 시각을 앵커로 쓴다 (StageStartGate.CompleteCountdown()과 동일 순서).
        // Phase마다 다시 찍으므로 앞 Phase가 길어져도 스케줄이 과거로 밀리지 않는다.
        StageNetworkState.Instance?.MarkStageStart();

        phase.onPhaseEnter?.Invoke();

        // 온라인 Host → Phase 변경을 다른 클라이언트에 동기화
        StageNetworkState.Instance?.SyncPhase(index);

        // Client는 여기서 종료 — 완료 판단은 Host만 수행
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

        // manualAdvanceOnly = true 면 AdvancePhase() 호출 대기, 자동 진행 없음
        if (phase.manualAdvanceOnly) return;

        // surviveDuration = 0 이면 즉시 다음 Phase
        if (phase.surviveDuration <= 0f)
            PhaseComplete();
    }

    /// <summary>
    /// 클라이언트 측에서 Phase 변경 수신 시 호출 (연출·오브젝트 반영).
    /// StageNetworkState.OnPhaseChanged → 이 메서드 호출.
    /// 게임 로직(타이머 등)은 Host만 실행하므로 여기서는 시각 효과만.
    /// </summary>
    public void EnterPhaseOnClient(int index)
    {
        if (phases == null || index < 0 || index >= phases.Length) return;
        if (_currentPhaseIndex == index) return;
        _currentPhaseIndex = index;
        _phaseElapsed      = 0f; // PhaseRemaining 계산 오염 방지

        PhaseData phase = phases[index];

        if (phase.objectsToDisable != null)
            foreach (GameObject obj in phase.objectsToDisable)
                if (obj != null) obj.SetActive(false);

        if (phase.objectsToEnable != null)
            foreach (GameObject obj in phase.objectsToEnable)
                if (obj != null) obj.SetActive(true);

        phase.onPhaseEnter?.Invoke();
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

    // ── 외부 호출 ─────────────────────────────────────────────────────

    /// <summary>
    /// 다음 Phase로 수동 진행.
    /// StageManager.OnStageClear 또는 기타 조건 완료 이벤트와 연결해 사용.
    /// surviveDuration 기반 자동 진행과 동시에 써도 안전 (이미 완료된 Phase는 무시).
    /// </summary>
    public void AdvancePhase()
    {
        // Client가 외부 이벤트로 직접 호출하더라도 무시 — Phase 진행은 Host만 결정
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

        if (_allPhasesComplete) return;

        // phases가 없으면 PassThrough 용도 — 즉시 onAllPhasesComplete 발동
        if (phases == null || phases.Length == 0)
        {
            _allPhasesComplete = true;
            onAllPhasesComplete?.Invoke();
            return;
        }

        if (_currentPhaseIndex < 0) return;
        PhaseComplete();
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
