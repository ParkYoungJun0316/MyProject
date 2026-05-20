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

        _elapsed += Time.deltaTime;

        // 1초마다 UI 이벤트 발동
        if (Time.time >= _nextUITick)
        {
            _nextUITick = Time.time + 1f;
            OnTimeChanged?.Invoke(Remaining);
        }

        if (_elapsed >= targetTime)
            Complete();
    }
}
