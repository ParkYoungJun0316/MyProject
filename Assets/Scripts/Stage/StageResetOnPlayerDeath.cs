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
