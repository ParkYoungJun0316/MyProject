using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ColorTileChallenge 클리어 Objective.
/// 고유+흑+백 할당을 모두 채우면 Complete.
/// targetTime 안에 못 채우면 Fail (StageManager가 전원 즉사 → 방 리셋).
/// Count UI는 점수, 남은 시간은 PhaseStartServerTime 역산 — 새 RPC 없음.
/// </summary>
public class ColorTileRoundObjective : RoundProgressObjective
{
    [Header("컬러 타일 챌린지")]
    [Tooltip("감시할 ColorTileChallenge.\n" +
             "이 Objective는 Activate()를 호출하지 않습니다.\n" +
             "Challenge의 autoStart를 그대로 사용하세요.")]
    [SerializeField] ColorTileChallenge challenge;

    [Header("목표 시간")]
    [Tooltip("스테이지 상한(초). 권장 120–300 (2–5분). M.Stage3 기본 180 (3분).\n" +
             "할당을 이 안에 못 채우면 실패. 0이면 시간 실패 없음.")]
    [SerializeField] float targetTime = 180f;

    float _lastRemaining = -1f;

    public override int PlayedRounds =>
        challenge != null ? challenge.QuotaProgress : 0;

    public override int TotalRounds =>
        challenge != null ? challenge.QuotaRequired : 0;

    public override int CurrentRoundIndex =>
        PlayedRounds < TotalRounds ? PlayedRounds : -1;

    public float TargetTime => Mathf.Max(0f, targetTime);
    public float Remaining => ComputeRemaining();

    public override void Begin()
    {
        Unsubscribe();
        _lastRemaining = -1f;

        if (challenge == null)
        {
            Debug.LogWarning($"[ColorTileRoundObjective] challenge가 연결되지 않았습니다. ({gameObject.name})");
            return;
        }

        challenge.OnSuccess.AddListener(HandleSuccess);
        challenge.OnQuotaChanged.AddListener(HandleQuotaChanged);

        OnProgressChanged?.Invoke();
    }

    public override void Tick()
    {
        if (IsCompleted || IsFailed) return;

        float remaining = Remaining;
        if (_lastRemaining < 0f || Mathf.Abs(remaining - _lastRemaining) >= 0.09f)
        {
            _lastRemaining = remaining;
            OnProgressChanged?.Invoke();
        }

        if (TargetTime <= 0f || remaining > 0f) return;
        if (QuotasFilled()) return;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;
        Fail();
    }

    bool QuotasFilled()
    {
        return challenge != null
            && challenge.QuotaRequired > 0
            && challenge.QuotaProgress >= challenge.QuotaRequired;
    }

    float ComputeRemaining()
    {
        if (TargetTime <= 0f) return 0f;

        var nm = NetworkManager.Singleton;
        var net = StageNetworkState.Instance;
        if (nm == null || !nm.IsListening || net == null || net.PhaseStartServerTime <= 0.0)
            return TargetTime;

        float elapsed = Mathf.Max(0f, (float)(nm.ServerTime.Time - net.PhaseStartServerTime));
        return Mathf.Max(0f, TargetTime - elapsed);
    }

    void HandleQuotaChanged()
    {
        if (IsCompleted || IsFailed) return;
        OnProgressChanged?.Invoke();
    }

    void HandleSuccess()
    {
        if (IsCompleted || IsFailed) return;

        OnProgressChanged?.Invoke();
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;
        Complete();
    }

    void Unsubscribe()
    {
        if (challenge == null) return;
        challenge.OnSuccess.RemoveListener(HandleSuccess);
        challenge.OnQuotaChanged.RemoveListener(HandleQuotaChanged);
    }

    void OnDestroy()
    {
        Unsubscribe();
    }
}
