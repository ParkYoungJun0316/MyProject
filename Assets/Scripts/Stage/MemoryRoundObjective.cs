using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Stage2의 MemoryPath 3구역 진행을 씬 단위로 관리하는 Objective.
///
/// [동작]
///  sections[i].OnStageClear(Host 레인 전용, §11A.0) → Host가 StageNetworkState._memorySectionsCleared
///  NV로 확정 브로드캐스트 → 전 머신(Host 포함) 에코로 PlayedRounds 갱신 → UI 갱신.
///  [버그 수정 2026-08] 이전엔 OnStageClear를 네트워크 브릿지 없이 직접 구독해 Client의
///  PlayedRounds가 0에 고정됐다(ObjectiveUI "0/3" 고착) — BossFightObjective._phasesCleared와
///  동일 버그 클래스, TStageNetworkBoard.md §3.5 Bug 2 참고.
///  전체 구역 완료 시 Complete()
///  실패(즉사)는 씬 리셋으로 처리 — Fail() 호출 없음
///
/// [Inspector 설정]
///  1. 빈 GameObject에 이 컴포넌트 부착
///  2. sections[] : StageManager2.1 / 2.2 / 2.3 순서대로 연결
///  3. StageStartGate2.1.OnCountdownComplete → BeginSection0 연결
///     StageStartGate2.2.OnCountdownComplete → BeginSection1 연결
///     StageStartGate2.3.OnCountdownComplete → BeginSection2 연결
///  4. 씬 meta StageManager의 objectives[]에 이 Objective 등록
///     (meta StageManager의 autoStart = true)
/// </summary>
public class MemoryRoundObjective : RoundProgressObjective
{
    [Header("구역 StageManager (순서대로)")]
    [Tooltip("StageManager2.1 → StageManager2.2 → StageManager2.3 순서대로 3개 연결")]
    [SerializeField] StageManager[] sections;

    int _playedRounds;
    int _currentRoundIndex = -1;

    UnityAction[] _clearHandlers;
    StageNetworkState _netState;

    public override int PlayedRounds      => _playedRounds;
    public override int TotalRounds       => sections != null ? sections.Length : 0;
    public override int CurrentRoundIndex => _currentRoundIndex;

    // ── Unity ────────────────────────────────────────────────────

    void Start()
    {
        _netState = StageNetworkState.Instance;
        if (_netState != null)
            _netState.OnMemorySectionsClearedChanged += HandleMemorySectionsClearedChanged;
    }

    // ── StageObjective 구현 ──────────────────────────────────────

    public override void Begin()
    {
        Unsubscribe();

        _playedRounds      = 0;
        _currentRoundIndex = -1;

        // Host: NV도 0으로 리셋 (Client가 호출해도 내부 IsServer 가드로 no-op) — BossFightObjective.Begin()과 동일 패턴.
        _netState?.SetMemorySectionsCleared(0);

        if (sections == null || sections.Length == 0)
        {
            Debug.LogWarning($"[MemoryRoundObjective] sections가 연결되지 않았습니다. ({gameObject.name})");
            return;
        }

        _clearHandlers = new UnityAction[sections.Length];
        for (int i = 0; i < sections.Length; i++)
        {
            if (sections[i] == null) continue;
            int idx = i;
            _clearHandlers[i] = () => HandleClear(idx);
            sections[i].OnStageClear.AddListener(_clearHandlers[i]);
        }

        OnProgressChanged?.Invoke();
    }

    public override void Tick() { }

    // ── Inspector UnityEvent 연결용 ───────────────────────────────

    /// <summary>StageStartGate2.1.OnCountdownComplete에 연결</summary>
    public void BeginSection0() => SetCurrentRound(0);

    /// <summary>StageStartGate2.2.OnCountdownComplete에 연결</summary>
    public void BeginSection1() => SetCurrentRound(1);

    /// <summary>StageStartGate2.3.OnCountdownComplete에 연결</summary>
    public void BeginSection2() => SetCurrentRound(2);

    // ── 내부 ─────────────────────────────────────────────────────

    void SetCurrentRound(int index)
    {
        if (_currentRoundIndex == index) return;
        _currentRoundIndex = index;
        OnProgressChanged?.Invoke();
    }

    /// <summary>
    /// sections[i].OnStageClear는 StageManager.Update()가 Host 레인에서만 발동하므로(§11A.0)
    /// 이 메서드도 Host에서만 호출된다. _playedRounds/OnProgressChanged/Complete()는 직접
    /// 갱신하지 않고 NV 브로드캐스트 → HandleMemorySectionsClearedChanged 에코로만 처리한다
    /// (Host 자신도 이 콜백을 통해 발동됨 — BossFightObjective.NotifyPhaseCleared와 동일 원칙).
    /// </summary>
    void HandleClear(int index)
    {
        if (IsClientOnly()) return; // 방어적 가드 — OnStageClear 자체가 이미 Host 전용
        _netState?.SetMemorySectionsCleared(_playedRounds + 1);
    }

    void HandleMemorySectionsClearedChanged(int cleared)
    {
        _playedRounds      = cleared;
        _currentRoundIndex = -1;
        OnProgressChanged?.Invoke();

        if (_playedRounds >= TotalRounds)
            Complete();
    }

    void Unsubscribe()
    {
        if (sections == null || _clearHandlers == null) return;
        for (int i = 0; i < sections.Length && i < _clearHandlers.Length; i++)
        {
            if (sections[i] != null && _clearHandlers[i] != null)
                sections[i].OnStageClear.RemoveListener(_clearHandlers[i]);
        }
        _clearHandlers = null;
    }

    static bool IsClientOnly()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && !nm.IsServer;
    }

    void OnDestroy()
    {
        Unsubscribe();
        if (_netState != null)
            _netState.OnMemorySectionsClearedChanged -= HandleMemorySectionsClearedChanged;
    }
}
