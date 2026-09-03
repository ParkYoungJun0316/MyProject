using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ColorTileChallenge 클리어 Objective.
/// 점수제: 고유+흑+백 할당을 모두 채우면 Complete. Count UI는 QuotaProgress/QuotaRequired.
/// 구 클리어: 스케줄 성공 횟수 &gt;= requiredSuccesses.
/// </summary>
public class ColorTileRoundObjective : RoundProgressObjective
{
    [Header("컬러 타일 챌린지")]
    [Tooltip("감시할 ColorTileChallenge.\n" +
             "이 Objective는 Activate()를 호출하지 않습니다.\n" +
             "Challenge의 autoStart/스케줄 설정을 그대로 사용하세요.")]
    [SerializeField] ColorTileChallenge challenge;

    [Header("클리어 조건 (구 라운드 모드)")]
    [Tooltip("점수제가 아닐 때만 사용. 스테이지 클리어에 필요한 최소 성공 횟수.")]
    [SerializeField] int requiredSuccesses = 0;

    int _playedRounds;
    int _successCount;

    public override int PlayedRounds =>
        challenge != null && challenge.UsesQuotaScoring
            ? challenge.QuotaProgress
            : _playedRounds;

    public override int TotalRounds =>
        challenge != null && challenge.UsesQuotaScoring
            ? challenge.QuotaRequired
            : (challenge != null ? challenge.ScheduledRoundCount : 0);

    public override int CurrentRoundIndex
    {
        get
        {
            if (challenge != null && challenge.UsesQuotaScoring)
                return PlayedRounds < TotalRounds ? PlayedRounds : -1;
            return _playedRounds < TotalRounds ? _playedRounds : -1;
        }
    }

    public int RequiredSuccesses => requiredSuccesses;

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
        challenge.OnQuotaChanged.AddListener(HandleQuotaChanged);

        OnProgressChanged?.Invoke();
    }

    public override void Tick() { }

    void HandleQuotaChanged()
    {
        if (IsCompleted || IsFailed) return;
        OnProgressChanged?.Invoke();
    }

    void HandleSuccess()
    {
        if (IsCompleted || IsFailed) return;

        if (challenge != null && challenge.UsesQuotaScoring)
        {
            OnProgressChanged?.Invoke();
            var nmQuota = NetworkManager.Singleton;
            if (nmQuota != null && nmQuota.IsListening && !nmQuota.IsServer) return;
            Complete();
            return;
        }

        _successCount++;
        _playedRounds++;
        OnProgressChanged?.Invoke();

        if (_successCount < requiredSuccesses) return;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

        Complete();
    }

    void HandleFail()
    {
        if (IsCompleted || IsFailed) return;
        if (challenge != null && challenge.UsesQuotaScoring) return;

        _playedRounds++;
        OnProgressChanged?.Invoke();
    }

    void Unsubscribe()
    {
        if (challenge == null) return;
        challenge.OnSuccess.RemoveListener(HandleSuccess);
        challenge.OnFail.RemoveListener(HandleFail);
        challenge.OnQuotaChanged.RemoveListener(HandleQuotaChanged);
    }

    void OnDestroy()
    {
        Unsubscribe();
    }
}
