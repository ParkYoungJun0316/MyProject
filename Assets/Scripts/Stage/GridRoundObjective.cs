using Unity.Netcode;
using UnityEngine;

/// <summary>
/// GridChallenge(혼합판) 전용 Objective.
/// [2026-09-11] GridColorChallenge/GridBWTileChallenge 분리 구조 폐기 — 하나의 GridChallenge로 통합
/// (`CoopStageAudit.M.md` §8).
///
/// [클리어 조건]
/// - 연결된 챌린지의 OnChallengeComplete → Complete()
/// - 라운드별 실패는 개인 데미지만 적용 — Objective는 Fail() 호출 안 함
/// - HP 0 사망 → StageResetOnPlayerDeath가 씬 리셋 (기존 파이프라인)
///
/// [StageManager 연동]
/// - StageManager.StartStage() → Begin() → challenge.Activate() 자동 호출
/// - 챌린지의 autoStart = false 필수 (Begin에서 직접 시작하므로 중복 방지)
/// - Gate OnCountdownComplete → Activate() 연결 불필요 (StageStartGate → StartStage만 연결)
///
/// [Inspector 설정]
/// - gridChallenge 연결
/// - StageManager.objectives[]에 이 Objective 등록
/// - StageManager.autoStart = false (StageStartGate가 StartStage 호출)
/// </summary>
public class GridRoundObjective : RoundProgressObjective
{
    [Header("Grid 챌린지")]
    [SerializeField] GridChallenge gridChallenge;

    // ── 상태 ──────────────────────────────────────────────────────

    int _playedRounds;
    int _totalRounds;
    int _currentRoundIndex;

    /// <summary>정산 완료된 라운드 수.</summary>
    public override int PlayedRounds      => _playedRounds;

    /// <summary>챌린지 전체 라운드 수.</summary>
    public override int TotalRounds       => _totalRounds;

    /// <summary>현재 진행 중인 라운드 인덱스(0부터). 진행 중이 아니면 -1.</summary>
    public override int CurrentRoundIndex => _currentRoundIndex;

    // ── StageObjective 구현 ──────────────────────────────────────

    public override void Begin()
    {
        Unsubscribe();

        _playedRounds      = 0;
        _currentRoundIndex = -1;

        if (gridChallenge != null)
        {
            _totalRounds = gridChallenge.TotalRounds;
            gridChallenge.OnRoundStarted.AddListener(HandleRoundStarted);
            gridChallenge.OnRoundSettled.AddListener(HandleRoundSettled);
            gridChallenge.OnChallengeComplete.AddListener(HandleChallengeComplete);
            gridChallenge.OnChallengeCancelled.AddListener(HandleChallengeCancelled);

            // StageManager.StartStage() → Begin() → 챌린지 시작
            gridChallenge.Cancel();    // 이미 실행 중이면 정리 후 재시작
            gridChallenge.Activate();
        }
        else
        {
            Debug.LogWarning($"[GridRoundObjective] gridChallenge가 연결되지 않았습니다. ({gameObject.name})");
        }

        OnProgressChanged?.Invoke();
    }

    public override void Tick() { }

    // ── 이벤트 핸들러 ─────────────────────────────────────────────

    void HandleRoundStarted(int roundIndex)
    {
        _currentRoundIndex = roundIndex;
        OnProgressChanged?.Invoke();
    }

    void HandleRoundSettled(int roundIndex, bool success)
    {
        _playedRounds      = roundIndex + 1;
        _currentRoundIndex = -1;
        OnProgressChanged?.Invoke();
    }

    void HandleChallengeComplete()
    {
        _playedRounds      = _totalRounds;
        _currentRoundIndex = -1;
        OnProgressChanged?.Invoke();

        // [축 SSOT: NetworkDesign.md §11A.2] Complete() 확정은 Host 레인에서만.
        // OnChallengeComplete는 챌린지의 HandleChallengeClearedChanged를 통해 Host/Client 전 머신에서
        // 공통으로 발동되므로 여기서 가드 (ColorTileRoundObjective.HandleSuccess와 동일 패턴).
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

        Complete();
    }

    void HandleChallengeCancelled()
    {
        _currentRoundIndex = -1;
        OnProgressChanged?.Invoke();
    }

    // ── 구독 해제 ─────────────────────────────────────────────────

    void Unsubscribe()
    {
        if (gridChallenge == null) return;

        gridChallenge.OnRoundStarted.RemoveListener(HandleRoundStarted);
        gridChallenge.OnRoundSettled.RemoveListener(HandleRoundSettled);
        gridChallenge.OnChallengeComplete.RemoveListener(HandleChallengeComplete);
        gridChallenge.OnChallengeCancelled.RemoveListener(HandleChallengeCancelled);
    }

    void OnDestroy() => Unsubscribe();
}
