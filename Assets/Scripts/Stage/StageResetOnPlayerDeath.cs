using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 사망 시 현재 씬을 재로드하는 오케스트레이터.
/// 사망 → SceneFlowManager.ReloadCurrentScene() → 씬 전체 재초기화.
/// SceneFlowManager 가 없는 씬에서는 SceneManager.LoadScene 직접 호출로 폴백.
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
        // 네트워크 모드: PlayerSpawnManager가 LoadEventCompleted 이후 스폰하므로
        // Start() 시점에 플레이어가 없을 수 있음 → 코루틴으로 재시도
        StartCoroutine(FindAndSubscribeRoutine());
    }

    IEnumerator FindAndSubscribeRoutine()
    {
        // 최대 15초(0.3초 × 50) 동안 재시도
        for (int i = 0; i < 50; i++)
        {
            TrySubscribePlayers();
            if (_subscribed.Count > 0) yield break;
            yield return new WaitForSeconds(0.3f);
        }
        Debug.LogWarning("[StageResetOnPlayerDeath] 15초 내 플레이어를 찾지 못했습니다.");
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
        // 온라인: StageNetworkState를 통해 Host가 전원 리로드 처리
        if (LobbyContext.IsOnline)
        {
            if (StageNetworkState.Instance != null)
                StageNetworkState.Instance.NotifyPlayerDeathServerRpc();
            else
                Debug.LogWarning("[StageResetOnPlayerDeath] StageNetworkState를 찾을 수 없습니다.");
            return;
        }

        // 오프라인: 기존 SceneFlowManager/SceneManager 처리
        if (SceneFlowManager.Instance != null)
            SceneFlowManager.Instance.ReloadCurrentScene();
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void OnDisable()
    {
        UnsubscribePlayers();
    }

    void OnDestroy()
    {
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
