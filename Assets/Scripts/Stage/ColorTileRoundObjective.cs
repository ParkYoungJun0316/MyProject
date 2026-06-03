using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ColorTileChallenge의 스케줄 결과를 기록하고,
/// 성공 횟수가 requiredSuccesses에 도달하면 스테이지를 클리어하는 Objective.
///
/// [동작 흐름]
/// - Challenge.activateAtSeconds 길이 = 총 라운드 수 (Inspector 별도 설정 불필요)
/// - Challenge.autoStart = true + 스케줄 그대로 → Challenge가 직접 Activate()
/// - OnSuccess → 성공 +1, requiredSuccesses 도달 시 Complete()
/// - OnFail    → X 기록 (패널티 벽은 Challenge가 처리)
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
public class ColorTileRoundObjective : StageObjective
{
    [Header("컬러 타일 찰린지")]
    [Tooltip("감시할 ColorTileChallenge.\n" +
             "이 Objective는 Activate()를 호출하지 않습니다.\n" +
             "Challenge의 autoStart/스케줄 설정을 그대로 사용하세요.")]
    [SerializeField] ColorTileChallenge challenge;

    [Header("클리어 조건")]
    [Tooltip("스테이지 클리어에 필요한 최소 성공 횟수.\n" +
             "예: 총 7라운드 중 5번 성공하면 클리어.")]
    [SerializeField] int requiredSuccesses = 0;

    [Header("이벤트 (UI 연결용)")]
    [Tooltip("라운드 결과(O/X) 기록 시. History / SuccessCount / FailCount 프로퍼티를 읽어서 UI 갱신.")]
    public UnityEvent OnHistoryUpdated;

    // ── 상태 ──────────────────────────────────────────────────────

    int    _playedRounds;
    int    _successCount;
    int    _failCount;
    bool[] _history;

    /// <summary>지금까지 완료된 라운드 수.</summary>
    public int PlayedRounds  => _playedRounds;

    /// <summary>누적 성공(O) 횟수.</summary>
    public int SuccessCount  => _successCount;

    /// <summary>누적 실패(X) 횟수.</summary>
    public int FailCount     => _failCount;

    /// <summary>Challenge의 스케줄 개수 = 총 라운드 수. UI 표시용.</summary>
    public int TotalRounds   => challenge != null ? challenge.ScheduledRoundCount : 0;

    /// <summary>클리어에 필요한 성공 횟수.</summary>
    public int RequiredSuccesses => requiredSuccesses;

    /// <summary>
    /// 라운드별 결과 배열. true = 성공(O), false = 실패(X).
    /// [0 .. PlayedRounds-1] 까지만 유효.
    /// </summary>
    public bool[] History => _history;

    // ── StageObjective 구현 ──────────────────────────────────────

    public override void Begin()
    {
        Unsubscribe();

        _playedRounds = 0;
        _successCount = 0;
        _failCount    = 0;

        int total = TotalRounds;
        _history = total > 0 ? new bool[total] : new bool[0];

        if (challenge == null)
        {
            Debug.LogWarning($"[ColorTileRoundObjective] challenge가 연결되지 않았습니다. ({gameObject.name})");
            return;
        }

        challenge.OnSuccess.AddListener(HandleSuccess);
        challenge.OnFail.AddListener(HandleFail);

        OnHistoryUpdated?.Invoke();
    }

    public override void Tick() { }

    // ── 라운드 결과 처리 ─────────────────────────────────────────

    void HandleSuccess()
    {
        if (IsCompleted || IsFailed) return;

        RecordResult(true);
        _successCount++;

        if (_successCount >= requiredSuccesses)
            Complete();
    }

    void HandleFail()
    {
        if (IsCompleted || IsFailed) return;

        RecordResult(false);
        _failCount++;
    }

    void RecordResult(bool success)
    {
        if (_history != null && _playedRounds < _history.Length)
            _history[_playedRounds] = success;

        _playedRounds++;
        OnHistoryUpdated?.Invoke();
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
