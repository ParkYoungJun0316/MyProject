using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Stage2의 MemoryPath 3구역 진행을 씬 단위로 관리하는 Objective.
///
/// [동작]
///  sections[i].OnStageClear → PlayedRounds 증가 → UI 갱신
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

    public override int PlayedRounds      => _playedRounds;
    public override int TotalRounds       => sections != null ? sections.Length : 0;
    public override int CurrentRoundIndex => _currentRoundIndex;

    // ── StageObjective 구현 ──────────────────────────────────────

    public override void Begin()
    {
        Unsubscribe();

        _playedRounds      = 0;
        _currentRoundIndex = -1;

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

    void HandleClear(int index)
    {
        _playedRounds++;
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

    void OnDestroy() => Unsubscribe();
}
