using UnityEngine;

/// <summary>
/// ColorTileChallenge의 스케줄 결과를 성공 횟수 기준으로 판정하는 Objective.
/// UI에는 성공/실패 구분 없이 진행 라운드 수만 노출한다 (Grid/MemoryRound와 동일한 Count 표시 규칙 — ObjectiveUI SSOT).
///
/// [동작 흐름]
/// - Challenge.activateAtSeconds 길이 = 총 라운드 수 (Inspector 별도 설정 불필요)
/// - Challenge.autoStart = true + 스케줄 그대로 → Challenge가 직접 Activate()
/// - OnSuccess/OnFail 둘 다 "라운드 1회 진행"으로 집계 (성공/실패 여부는 클리어 판정에만 사용, UI에는 노출 안 함)
/// - Objective는 Activate()를 호출하지 않음 (스케줄에 관여 X)
///
/// [클리어 조건]
/// - 성공 횟수 >= requiredSuccesses → Complete() → StageManager.OnStageClear
/// - 실패 누적이 벽으로 이어져 사망 → 기존 사망/리셋 파이프라인 (Fail() 사용 안 함)
///
/// [Inspector 설정]
///  - challenge         : 감시할 ColorTileChallenge
///  - requiredSuccesses : 클리어에 필요한 성공 횟수 (예: 5)
/// </summary>
public class ColorTileRoundObjective : RoundProgressObjective
{
    [Header("컬러 타일 챌린지")]
    [Tooltip("감시할 ColorTileChallenge.\n" +
             "이 Objective는 Activate()를 호출하지 않습니다.\n" +
             "Challenge의 autoStart/스케줄 설정을 그대로 사용하세요.")]
    [SerializeField] ColorTileChallenge challenge;

    [Header("클리어 조건")]
    [Tooltip("스테이지 클리어에 필요한 최소 성공 횟수.\n" +
             "예: 총 7라운드 중 5번 성공하면 클리어.")]
    [SerializeField] int requiredSuccesses = 0;

    // ── 상태 ──────────────────────────────────────────────────────

    int _playedRounds;
    int _successCount;

    /// <summary>정산 완료된 라운드 수(성공/실패 무관).</summary>
    public override int PlayedRounds      => _playedRounds;

    /// <summary>Challenge의 스케줄 개수 = 총 라운드 수.</summary>
    public override int TotalRounds       => challenge != null ? challenge.ScheduledRoundCount : 0;

    /// <summary>현재 진행 중인 라운드 인덱스(0부터). 전부 끝났으면 -1.</summary>
    public override int CurrentRoundIndex => _playedRounds < TotalRounds ? _playedRounds : -1;

    /// <summary>클리어에 필요한 성공 횟수.</summary>
    public int RequiredSuccesses => requiredSuccesses;

    // ── StageObjective 구현 ──────────────────────────────────────

    public override void Begin()
    {
        Unsubscribe();

        _playedRounds = 0;
        _successCount = 0;

        if (challenge == null)
        {
            Debug.LogWarning($"[ColorTileRoundObjective] challenge가 연결되지 않았습니다. ({gameObject.name})");
            return;
        }

        challenge.OnSuccess.AddListener(HandleSuccess);
        challenge.OnFail.AddListener(HandleFail);

        OnProgressChanged?.Invoke();
    }

    public override void Tick() { }

    // ── 라운드 결과 처리 ─────────────────────────────────────────

    void HandleSuccess()
    {
        if (IsCompleted || IsFailed) return;

        _successCount++;
        _playedRounds++;
        OnProgressChanged?.Invoke();

        if (_successCount >= requiredSuccesses)
            Complete();
    }

    void HandleFail()
    {
        if (IsCompleted || IsFailed) return;

        _playedRounds++;
        OnProgressChanged?.Invoke();
    }

    // ── 구독 해제 ─────────────────────────────────────────────────

    void Unsubscribe()
    {
        if (challenge == null) return;
        challenge.OnSuccess.RemoveListener(HandleSuccess);
        challenge.OnFail.RemoveListener(HandleFail);
    }

    void OnDestroy()
    {
        Unsubscribe();
    }
}
