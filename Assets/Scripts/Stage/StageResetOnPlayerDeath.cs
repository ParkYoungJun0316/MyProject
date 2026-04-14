using UnityEngine;

/// <summary>
/// 플레이어 사망 시 현재 Phase를 초기화.
///
/// [사용법]
/// 1. [GameFlow] 오브젝트(PhaseManager가 있는 곳)에 이 컴포넌트 추가
/// 2. phaseManager 필드에 PhaseManager 연결
/// 3. 끝 — 씬 내 모든 플레이어의 OnDied를 자동 구독
///
/// [동작]
/// - 플레이어가 죽으면 현재 Phase만 리셋 (앞 Phase로 돌아가지 않음)
/// - 함정은 0초부터 재시작, StageManager Objective도 초기화
/// - 플레이어 리스폰은 Player.cs가 자체 처리 (영향 없음)
/// </summary>
public class StageResetOnPlayerDeath : MonoBehaviour
{
    [SerializeField] PhaseManager phaseManager;

    [Tooltip("리셋까지 대기 시간(초). 0이면 사망 즉시 리셋.\n" +
             "사망 연출이 끝난 뒤 리셋하고 싶으면 Player의 respawnDelay보다 짧게 설정.")]
    [SerializeField] float resetDelay = 0f;

    Player[] _players;

    void Awake()
    {
        if (phaseManager == null)
            phaseManager = FindFirstObjectByType<PhaseManager>();
    }

    void Start()
    {
        SubscribePlayers();
    }

    void SubscribePlayers()
    {
        _players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (Player p in _players)
        {
            PlayerEvents ev = p.GetComponent<PlayerEvents>();
            if (ev != null) ev.OnDied += OnAnyPlayerDied;
        }
    }

    void OnAnyPlayerDied()
    {
        if (phaseManager == null) return;

        if (resetDelay <= 0f)
            phaseManager.RestartCurrentPhase();
        else
            Invoke(nameof(DoReset), resetDelay);
    }

    void DoReset()
    {
        phaseManager?.RestartCurrentPhase();
    }

    void OnDestroy()
    {
        if (_players == null) return;
        foreach (Player p in _players)
        {
            if (p == null) continue;
            PlayerEvents ev = p.GetComponent<PlayerEvents>();
            if (ev != null) ev.OnDied -= OnAnyPlayerDied;
        }
    }
}
