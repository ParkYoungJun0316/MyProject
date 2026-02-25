using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 목표 1: X초(분) 동안 살아남기.
/// players[] 중 한 명이라도 사망하면 failOnDeath 설정에 따라 실패 처리.
/// targetTime 초가 지나면 완료.
/// </summary>
public class SurviveTimeObjective : StageObjective
{
    [Header("살아남기 설정")]
    [Tooltip("버텨야 하는 시간(초). 예) 300 = 5분")]
    public float targetTime = 300f;

    [Tooltip("플레이어가 사망하면 즉시 실패로 처리할지 여부")]
    public bool failOnDeath = false;

    [Tooltip("추적할 플레이어 목록. 비우면 Start 시 씬에서 자동 수집")]
    public Player[] players;

    [Header("Runtime (확인용)")]
    [SerializeField] float _elapsed;

    public float Elapsed   => _elapsed;
    public float Remaining => Mathf.Max(0f, targetTime - _elapsed);

    public UnityEvent<float> OnTimeChanged; // 매초 남은 시간 전달 (UI 연결용)

    float _nextUITick;

    public override void Begin()
    {
        _elapsed    = 0f;
        _nextUITick = 0f;

        if (players == null || players.Length == 0)
            players = FindObjectsByType<Player>(FindObjectsSortMode.None);

        if (failOnDeath)
            foreach (var p in players)
                if (p != null)
                {
                    var events = p.GetComponent<PlayerEvents>();
                    if (events != null) events.OnDied += OnPlayerDied;
                }
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

    void OnPlayerDied()
    {
        if (failOnDeath) Fail();
    }

    void OnDisable()
    {
        if (players == null) return;
        foreach (var p in players)
            if (p != null)
            {
                var events = p.GetComponent<PlayerEvents>();
                if (events != null) events.OnDied -= OnPlayerDied;
            }
    }
}
