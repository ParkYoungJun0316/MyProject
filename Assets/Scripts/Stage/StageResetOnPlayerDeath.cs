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
    Player[] _players;
    bool     _resetPending;

    void Start()
    {
        // GameSession이 있으면 활성 플레이어만, 없으면 씬 전체
        if (GameSession.Instance != null)
        {
            var active = GameSession.Instance.GetActivePlayers();
            _players = new Player[active.Count];
            for (int i = 0; i < active.Count; i++) _players[i] = active[i];
        }

        // 활성 플레이어를 못 찾은 경우 (네트워크 모드 타이밍 등) 씬 내 Player 직접 탐색
        if (_players == null || _players.Length == 0)
            _players = FindObjectsByType<Player>(FindObjectsSortMode.None);

        foreach (Player p in _players)
        {
            PlayerEvents ev = p.GetComponent<PlayerEvents>();
            if (ev != null) ev.OnDied += OnAnyPlayerDied;
        }
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
        if (_players == null) return;
        foreach (Player p in _players)
        {
            if (p == null) continue;
            PlayerEvents ev = p.GetComponent<PlayerEvents>();
            if (ev != null) ev.OnDied -= OnAnyPlayerDied;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: 강제 씬 재로드")]
    void Debug_ForceReset() => DoReset();
#endif
}
