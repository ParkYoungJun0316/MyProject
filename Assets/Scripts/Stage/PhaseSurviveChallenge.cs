using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 보스 페이즈 1개의 생존 타이머.
/// StageObjective가 아닌 순수 타이머 — 플레이어 UI에 노출되지 않음.
///
/// [역할]
/// - targetTime 초 경과 시 OnChallengeComplete 발동
/// - BossFightObjective가 내부적으로 Begin()을 호출 (직접 호출 불필요)
///
/// [Inspector 연결]
/// - targetTime            : 이 페이즈에서 버텨야 할 초
/// - OnChallengeComplete   : BossFightObjective.NotifyPhaseCleared() 연결
/// </summary>
public class PhaseSurviveChallenge : MonoBehaviour
{
    [Header("생존 설정")]
    [Tooltip("이 페이즈에서 버텨야 하는 시간(초)")]
    public float targetTime = 60f;

    [Header("이벤트")]
    [Tooltip("생존 성공 시 호출\n→ BossFightObjective.NotifyPhaseCleared() 연결")]
    public UnityEvent OnChallengeComplete;

    bool  _running;
    float _elapsed;

    public bool  IsRunning => _running;
    public float Elapsed   => _elapsed;
    public float Remaining => Mathf.Max(0f, targetTime - _elapsed);

    /// <summary>BossFightObjective에서 자동 호출. 타이머 시작.</summary>
    public void Begin()
    {
        _elapsed = 0f;
        _running = true;
    }

    /// <summary>즉시 정지. 씬 리셋 등 강제 중단 시.</summary>
    public void Stop() => _running = false;

    void Update()
    {
        if (!_running) return;

        _elapsed += Time.deltaTime;

        if (_elapsed >= targetTime)
        {
            _running = false;
            OnChallengeComplete?.Invoke();
        }
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: 즉시 완료")]
    void Debug_Complete()
    {
        _running = false;
        OnChallengeComplete?.Invoke();
    }
#endif
}
