using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 좌/우 분기 미니게임 클리어를 스테이지 목표로 등록하는 Objective.
/// SideSplitChallenge.OnAllCleared 시 Complete() → StageManager.OnStageClear → 다음 Phase.
/// Count 표시: 현재 라운드 번호(1-based) / 전체 — 첫 라운드 1/5 … 다섯 번째 5/5.
///
/// [연동 흐름 — OXQuizObjective와 동일 골격]
/// - Begin()                           : SideSplitChallenge 이벤트 구독, 진행 상황 초기화
/// - SideSplitChallenge.OnRoundReady   → OnProgressChanged 발동 (UI 갱신)
/// - SideSplitChallenge.OnAllCleared   → Complete() → StageManager 클리어
///
/// [Inspector 설정]
///  - challenge : 감시할 SideSplitChallenge
///  - objectiveName (StageObjective 공통) : UI 표시 이름
/// </summary>
public class SideSplitObjective : RoundProgressObjective
{
    [Header("좌/우 분기 미니게임")]
    [Tooltip("이 Objective가 감시할 SideSplitChallenge")]
    [SerializeField] SideSplitChallenge challenge;

    // ── 상태 ──────────────────────────────────────────────────────

    int _playedRounds;
    int _totalRounds;
    int _currentRoundIndex = -1;

    /// <summary>현재 라운드 번호(1-based). 시작 전이면 0.</summary>
    public override int PlayedRounds      => _playedRounds;

    /// <summary>이번 판 총 라운드 수.</summary>
    public override int TotalRounds       => _totalRounds;

    /// <summary>현재 진행 중인 라운드 인덱스(0부터). 진행 중 아니면 -1.</summary>
    public override int CurrentRoundIndex => _currentRoundIndex;

    // ── StageObjective 구현 ──────────────────────────────────────

    public override void Begin()
    {
        Unsubscribe();

        if (challenge == null)
        {
            Debug.LogWarning($"[SideSplitObjective] challenge가 연결되지 않았습니다. ({gameObject.name})");
            return;
        }

        _totalRounds = challenge.TotalRounds;

        // StartChallenge()가 Begin()보다 먼저 호출된 경우 이미 진행 중인 라운드 번호로 동기화.
        // 순서가 올바르면(Begin 먼저) 진행 전 상태로 시작.
        if (challenge.IsStarted)
        {
            _currentRoundIndex = challenge.CurrentRoundIndex;
            _playedRounds      = _currentRoundIndex + 1;
        }
        else
        {
            _currentRoundIndex = -1;
            _playedRounds      = 0;
        }

        challenge.OnRoundReady.AddListener(HandleRoundReady);
        challenge.OnAllCleared.AddListener(HandleAllCleared);

        OnProgressChanged?.Invoke();
    }

    public override void Tick() { }

    // ── 내부 핸들러 ───────────────────────────────────────────────

    /// <summary>새 라운드가 뜨면 현재 라운드 번호(1-based)로 Count 갱신.</summary>
    void HandleRoundReady(SideSplitRoundInfo _)
    {
        if (challenge == null) return;
        _currentRoundIndex = challenge.CurrentRoundIndex;
        _playedRounds      = _currentRoundIndex + 1;
        _totalRounds       = challenge.TotalRounds;
        OnProgressChanged?.Invoke();
    }

    void HandleAllCleared()
    {
        _playedRounds      = _totalRounds;
        _currentRoundIndex = -1;
        OnProgressChanged?.Invoke();

        // [축 SSOT: NetworkDesign.md §11A.2] Complete() 확정은 Host 레인에서만.
        // SideSplitChallenge.OnAllCleared는 Host/Client 전 머신에서 공통으로 발동되므로 여기서 가드.
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

        Complete();
    }

    // ── 구독 해제 ─────────────────────────────────────────────────

    void Unsubscribe()
    {
        if (challenge == null) return;
        challenge.OnRoundReady.RemoveListener(HandleRoundReady);
        challenge.OnAllCleared.RemoveListener(HandleAllCleared);
    }

    void OnDestroy()
    {
        Unsubscribe();
    }
}
