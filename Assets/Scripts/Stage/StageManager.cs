using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 스테이지 매니저.
/// objectives[] 의 목표가 전부 완료되면 OnStageClear 발동.
/// 하나라도 실패하면 OnStageFailed 발동.
///
/// [축 SSOT: NetworkDesign.md §11A]
/// Clear/Fail 확정(Resolve)은 Host 레인에서만 판정한다 (Update() 하단 IsServer 가드).
/// Fail은 StageNetworkState에 직접 통보한다 — 별도 소프트 리셋도, 전원 즉사도 없다
/// (ReviveSystemDesign.md §6).
///
/// [설정]
///  1. 이 오브젝트 또는 자식에 원하는 Objective 스크립트를 붙임
///  2. objectives[] 에 등록 (비우면 자식에서 자동 수집)
///  3. OnStageClear → 다음 씬 전환, 문 열기 등 연결
/// </summary>
public class StageManager : MonoBehaviour
{
    [Header("목표 목록")]
    [Tooltip("비우면 자식 오브젝트에서 자동 수집")]
    public StageObjective[] objectives;

    [Header("시작 설정")]
    [Tooltip("true: 씬 로드 즉시 자동 시작\n" +
             "false: StartStage() 호출 대기 (StageStartGate 등 외부 연결 필요)")]
    public bool autoStart = false;

    [Header("이벤트")]
    public UnityEvent OnStageClear;
    public UnityEvent OnStageFailed;

    bool _isStarted;
    bool _isCleared;
    bool _isFailed;
    int  _completedCount;

    // TrapBase / FloorManager 가 Awake에서 직접 등록
    readonly List<TrapBase>   _registeredTraps = new List<TrapBase>();
    FloorManager              _registeredFloor;

    public bool IsStarted => _isStarted;
    public bool IsCleared => _isCleared;
    public bool IsFailed  => _isFailed;

    /// <summary>TrapBase.Awake()에서 자동 호출. 계층 위치 무관하게 등록됨.</summary>
    public void RegisterTrap(TrapBase trap)
    {
        if (!_registeredTraps.Contains(trap))
            _registeredTraps.Add(trap);
    }

    /// <summary>FloorManager.Awake()에서 자동 호출.</summary>
    public void RegisterFloor(FloorManager floor) => _registeredFloor = floor;

    void Awake()
    {
        if (objectives == null || objectives.Length == 0)
            objectives = GetComponentsInChildren<StageObjective>(true);
    }

    void Start()
    {
        if (autoStart) StartStage();
    }

    void Update()
    {
        if (!_isStarted || _isCleared || _isFailed) return;

        // Tick()은 전 머신에서 로컬 실행 (진행률 표시 등 Consumer 용도).
        // Complete()/Fail() 자체는 각 Objective가 스스로 Host 가드하는 것과 별개로,
        // 클리어/실패 "확정"은 아래에서 Host 레인 하나로만 판정한다 (§11A.0 Progress/Resolve).
        for (int i = 0; i < objectives.Length; i++)
            if (objectives[i] != null) objectives[i].Tick();

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

        _completedCount = 0;
        for (int i = 0; i < objectives.Length; i++)
        {
            if (objectives[i] == null) continue;

            if (objectives[i].IsFailed)
            {
                _isFailed = true;
                OnStageFailed?.Invoke();
                NotifyStageFailed($"objective 실패 ({objectives[i].GetType().Name})");
                return;
            }

            if (objectives[i].IsCompleted)
                _completedCount++;
        }

        // objectives가 0개면 즉시 클리어되지 않도록 가드
        // 보스 씬에서 StageManager가 함정 전용으로만 쓰일 때 적용
        if (objectives.Length > 0 && _completedCount >= objectives.Length)
        {
            _isCleared = true;
            DeactivateAllTraps();
            DestroyAllProjectiles();
            OnStageClear?.Invoke();
            StageNetworkState.Instance?.NotifyStageCleared(); // Client에도 클리어 배너 브로드캐스트
        }
    }

    /// <summary>
    /// 스테이지 실패를 StageNetworkState에 직접 통보 (ReviveSystemDesign.md §6).
    /// Update()가 이미 Host 레인으로 가드한 뒤 호출하므로 여기서 다시 가드하지 않음.
    ///
    /// [왜 전원 즉사를 폐기했나 — 2026-09-19] 구 구조는 전원을 즉사시켜 "사망 → 씬 리로드" 결선에
    /// 얹는 방식이었다. 자동 부활이 들어오면 **첫 번째로 죽은 사람이 1초 뒤 즉시 살아난다.** 지금은
    /// 목숨이 정확히 (인원−1)이라 우연히 수렴하지만, 목숨을 늘리는 순간 전원 즉사시켰는데 전원이
    /// 살아나고 스테이지 실패가 조용히 무시된다.
    /// </summary>
    void NotifyStageFailed(string reason)
    {
        var netState = StageNetworkState.Instance;
        if (netState == null)
        {
            Debug.LogWarning($"[StageManager] 실패했지만 StageNetworkState가 없어 리로드하지 못함 — {reason} ({name})", this);
            return;
        }
        netState.FailStageFromServer(reason);
    }

    // ── 외부 호출 ─────────────────────────────────────────────────

    /// <summary>
    /// 플레이어가 트리거를 밟으면 호출. Objective 타이머/목표를 시작.
    /// StageStartGate 등 외부에서 연결.
    /// </summary>
    public void StartStage()
    {
        if (_isStarted) return;
        _isStarted = true;

        // 트랩 스케줄 앵커(PhaseStartServerTime) 재확정.
        // PhaseManager.EnterPhase()의 onPhaseEnter와 이 StartStage() 호출 사이에 대화(PhaseDialogueGate)/
        // Gate 대기 같은 비동기 구간이 끼면, 앵커가 실제 발동 시점보다 먼저 찍혀 있어 트랩
        // fireAtSeconds 스케줄의 앞부분이 "이미 지난 이벤트"로 스킵되는 문제가 있었다
        // (M.Stage1, 2026-08). onPhaseEnter가 StartStage()를 곧바로 호출하는 씬(M.Stage2 등)에서는
        // 거의 동시 시각으로 재기록될 뿐이라 영향 없음. MarkAndSyncPhase 내부에 IsServer 가드가
        // 있어 Client에서 호출돼도 무해.
        if (PhaseManager.Instance != null)
            StageNetworkState.Instance?.MarkAndSyncPhase(PhaseManager.Instance.CurrentPhaseIndex);

        foreach (var obj in objectives)
            if (obj != null) obj.Begin();

        foreach (var trap in _registeredTraps)
            if (trap != null) trap.Activate();

        if (_registeredFloor != null) _registeredFloor.StartFloor();
    }

    /// <summary>
    /// 등록된 모든 함정을 비활성화(발사 중단).
    /// 스테이지 클리어 시 자동 호출. 외부에서도 직접 호출 가능.
    /// </summary>
    public void DeactivateAllTraps()
    {
        foreach (var trap in _registeredTraps)
            if (trap != null) trap.Deactivate();
    }

    /// <summary>
    /// 씬에 날아다니는 TrapProjectile 전부 즉시 파괴.
    /// 스테이지 클리어 시 자동 호출. 외부에서도 직접 호출 가능.
    /// 실제 순회는 TrapProjectile.DespawnAllOnServer()가 단독으로 소유한다(Host 가드도 그쪽) —
    /// SceneFlowManager.FreezeAllHazardsNow()와 같은 코드를 두 벌 들고 있지 않기 위함.
    /// </summary>
    public void DestroyAllProjectiles() => TrapProjectile.DespawnAllOnServer();

    // ── 에디터 지원 ──────────────────────────────────────────────
    [ContextMenu("테스트: 스테이지 시작")]
    void Debug_Start() => StartStage();

    [ContextMenu("테스트: 스테이지 클리어")]
    void Debug_Clear()
    {
        _isStarted = true;
        _isCleared = true;
        DeactivateAllTraps();
        DestroyAllProjectiles();
        OnStageClear?.Invoke();
        StageNetworkState.Instance?.NotifyStageCleared();
    }

    [ContextMenu("테스트: 스테이지 실패")]
    void Debug_Fail()
    {
        _isStarted = true;
        _isFailed  = true;
        OnStageFailed?.Invoke();
        NotifyStageFailed("에디터 테스트");
    }
}
