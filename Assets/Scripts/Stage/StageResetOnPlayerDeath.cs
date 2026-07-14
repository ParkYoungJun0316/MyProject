using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 사망 시 씬 재로드를 StageNetworkState에 위임하는 오케스트레이터.
/// 사망 → StageNetworkState.NotifyPlayerDeathServerRpc() → Host가 씬 전체 재로드.
///
/// [사용법]
/// 씬에 빈 GameObject 추가 → 이 컴포넌트 부착. 설정 없음.
/// </summary>
public class StageResetOnPlayerDeath : MonoBehaviour
{
    // 이미 구독한 Player Set — 중복 구독 방지
    readonly HashSet<Player> _subscribed = new();
    bool _resetPending;

    // Player[] _players는 하위 호환을 위해 유지 (UnsubscribePlayers에서 사용)
    Player[] _players;

    void Start()
    {
        // 네트워크·오프라인 모두: PlayerSpawnCoordinator 이벤트 기준으로 구독
        PlayerSpawnCoordinator.OnPlayersReady += TrySubscribePlayers;
        if (PlayerSpawnCoordinator.IsReady) TrySubscribePlayers();
    }

    void TrySubscribePlayers()
    {
        // GameSession 활성 플레이어 우선
        if (GameSession.Instance != null)
        {
            var active = GameSession.Instance.GetActivePlayers();
            foreach (Player p in active) Subscribe(p);
        }

        // 씬 내 모든 Player 보조 탐색 (네트워크 스폰 포함)
        var all = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (Player p in all) Subscribe(p);

        // 레거시 배열 갱신 (UnsubscribePlayers에서 사용)
        if (_subscribed.Count > 0)
        {
            _players = new Player[_subscribed.Count];
            _subscribed.CopyTo(_players);
        }
    }

    void Subscribe(Player p)
    {
        if (p == null || _subscribed.Contains(p)) return;
        PlayerEvents ev = p.GetComponent<PlayerEvents>();
        if (ev == null) return;
        ev.OnDied += OnAnyPlayerDied;
        _subscribed.Add(p);
        Debug.Log($"[StageResetOnPlayerDeath] 플레이어 구독: {p.name}");
    }

    void OnAnyPlayerDied()
    {
        if (_resetPending) return;
        _resetPending = true;
        DoReset();
    }

    void DoReset()
    {
        if (StageNetworkState.Instance != null)
            StageNetworkState.Instance.NotifyPlayerDeathServerRpc();
        else
            Debug.LogWarning("[StageResetOnPlayerDeath] StageNetworkState를 찾을 수 없습니다.");
    }

    void OnDisable()
    {
        UnsubscribePlayers();
    }

    void OnDestroy()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= TrySubscribePlayers;
        UnsubscribePlayers();
    }

    void UnsubscribePlayers()
    {
        foreach (Player p in _subscribed)
        {
            if (p == null) continue;
            PlayerEvents ev = p.GetComponent<PlayerEvents>();
            if (ev != null) ev.OnDied -= OnAnyPlayerDied;
        }
        _subscribed.Clear();
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: 강제 씬 재로드")]
    void Debug_ForceReset() => DoReset();
#endif
}
