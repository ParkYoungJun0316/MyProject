using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// X초 동안 살아남기. targetTime 경과 시 완료.
/// 사망·추락 처리는 Player / StageResetOnPlayerDeath·PhaseManager가 담당.
/// </summary>
public class SurviveTimeObjective : StageObjective
{
    [Header("살아남기 설정")]
    [Tooltip("버텨야 하는 시간(초). 예) 300 = 5분")]
    public float targetTime = 300f;

    float _elapsed;

    public float Elapsed   => _elapsed;
    public float Remaining => Mathf.Max(0f, targetTime - _elapsed);

    public UnityEvent<float> OnTimeChanged; // 매초 남은 시간 전달 (UI 연결용)

    float _nextUITick;

    public override void Begin()
    {
        _elapsed    = 0f;
        _nextUITick = 0f;
    }

    public override void Tick()
    {
        if (IsCompleted || IsFailed) return;

        var  nm     = NetworkManager.Singleton;
        bool isHost = nm != null && nm.IsServer;

        // ── Host: 타이머 진행 및 완료 판정 ───────────────────────
        if (isHost)
        {
            _elapsed += Time.deltaTime;

            // 1초마다 UI 갱신 + (온라인 Host만) RPC로 Client에 전파
            if (Time.time >= _nextUITick)
            {
                _nextUITick = Time.time + 1f;
                OnTimeChanged?.Invoke(Remaining);
                // IsListening 확인: NGO가 씬에 있지만 Start 안 됐을 때 RPC 오류 방지
                if (nm != null && nm.IsListening && nm.IsServer)
                    StageNetworkState.Instance?.SyncSurvivalRemainingClientRpc(Remaining);
            }

            if (_elapsed >= targetTime)
                Complete();
        }
        // ── Client: StageNetworkState.SyncSurvivalRemainingClientRpc 수신으로만 UI 갱신 ──
    }

    /// <summary>
    /// StageNetworkState.SyncSurvivalRemainingClientRpc가 수신되면 호출.
    /// OnTimeChanged 이벤트를 통해 TimerUI 등 연결된 UI를 갱신한다.
    /// </summary>
    public void NotifyRemainingTime(float remaining)
    {
        OnTimeChanged?.Invoke(remaining);
    }
}
