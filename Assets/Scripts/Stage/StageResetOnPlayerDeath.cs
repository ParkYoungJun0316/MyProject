using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 플레이어 사망 시 씬 재로드를 StageNetworkState에 위임하는 오케스트레이터.
/// 사망 → StageNetworkState.NotifyPlayerDeathServerRpc() → Host가 씬 전체 재로드.
///
/// [Fix A 대응]
/// ForceKillClientRpc 수정 이후 RaiseDied()가 모든 클라이언트에서 발화됨.
/// 죽은 플레이어의 Owner 머신에서만 NotifyPlayerDeathServerRpc()를 전송해
/// 중복 ServerRpc를 방지한다.
/// 구독 핸들러를 플레이어별 람다로 저장 (Dictionary) → 정확한 언구독 보장.
///
/// [사용법]
/// 씬에 빈 GameObject 추가 → 이 컴포넌트 부착. 설정 없음.
/// </summary>
public class StageResetOnPlayerDeath : MonoBehaviour
{
    // Player → 해당 플레이어의 OnDied 핸들러 (람다). Dictionary로 언구독 정확하게 처리.
    readonly Dictionary<Player, System.Action> _subscribed = new();
    bool _resetPending;

    void Start()
    {
        // PlayerSpawnCoordinator 이벤트 기준으로 구독 (멀티·솔로 공통 — NGO Host 1인도 동일 경로)
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
    }

    void Subscribe(Player p)
    {
        if (p == null || _subscribed.ContainsKey(p)) return;
        PlayerEvents ev = p.GetComponent<PlayerEvents>();
        if (ev == null) return;

        // 람다에 플레이어 레퍼런스를 캡처해 Owner 여부를 판단한다.
        // Fix A 이후 비Owner 머신에서도 RaiseDied()가 발화되므로,
        // 죽은 플레이어의 Owner 머신에서만 ServerRpc를 전송해 중복을 방지한다.
        Player captured = p;
        System.Action handler = () =>
        {
            var net = captured.GetComponent<NetworkObject>();
            if (net != null && !net.IsOwner) return;
            OnAnyPlayerDied();
        };

        ev.OnDied += handler;
        _subscribed[p] = handler;
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
        foreach (var kvp in _subscribed)
        {
            if (kvp.Key == null) continue;
            PlayerEvents ev = kvp.Key.GetComponent<PlayerEvents>();
            if (ev != null) ev.OnDied -= kvp.Value;
        }
        _subscribed.Clear();
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: 강제 씬 재로드")]
    void Debug_ForceReset() => DoReset();
#endif
}
