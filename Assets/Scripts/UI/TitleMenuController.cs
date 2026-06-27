using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 타이틀 씬 메인 메뉴 컨트롤러.
///
/// [배치 방법]
/// TitleCanvas 또는 별도 빈 GameObject에 부착.
///
/// [Inspector 연결]
/// - lobbySceneName      : 로비 씬 이름 (기본 "1.Lobby")
/// - discordUrl          : Discord 초대 링크
/// - screenFader         : 선택. 씬 전환 전 페이드아웃
/// - settingsPanel       : 설정/옵션 패널 GameObject
/// - joinGameUnavailablePanel : "게임 참여는 Full Version에서" 안내 패널 (선택)
///
/// [버튼 On Click() 연결]
/// 솔로         → OnClickSolo()
/// 게임 만들기  → OnClickCreateGame()
/// 게임 참여    → OnClickJoinGame()
/// 설정         → OnClickSettings()
/// 게임 종료    → OnClickQuit()
/// Discord      → OnClickDiscord()
/// </summary>
public class TitleMenuController : MonoBehaviour
{
    [Header("씬 전환")]
    [Tooltip("솔로·멀티 공통으로 로드할 로비 씬 이름.")]
    [SerializeField] private string lobbySceneName = "1.Lobby";

    [Header("Discord")]
    [Tooltip("Discord 초대 링크 (예: https://discord.gg/abc123)")]
    [SerializeField] private string discordUrl = "https://discord.gg/";

    [Header("패널 연결")]
    [Tooltip("설정 버튼 클릭 시 열릴 패널. 비워두면 클릭 무시.")]
    [SerializeField] private GameObject settingsPanel;

    [Tooltip("게임 참여 버튼 클릭 시 표시할 안내 패널 (선택). 비워두면 로그만 출력.")]
    [SerializeField] private GameObject joinGameUnavailablePanel;

    [Header("페이드 (선택)")]
    [Tooltip("씬 전환 전 페이드아웃. 비워두면 즉시 전환.")]
    [SerializeField] private ScreenFader screenFader;

    [Tooltip("페이드아웃 시간(초). 0이면 즉시.")]
    [SerializeField] private float fadeOutDuration = 0f;

    // ── 버튼 콜백 ─────────────────────────────────────────────────

    /// <summary>솔로 버튼 — NGO 없이 로비(Offline 모드)로 이동.</summary>
    public void OnClickSolo()
    {
        LobbyContext.Mode = LobbyMode.Offline;
        StartCoroutine(LoadSceneWithFade(lobbySceneName));
    }

    /// <summary>게임 만들기 버튼 — 온라인 Host로 로비 이동.</summary>
    public void OnClickCreateGame()
    {
        LobbyContext.Mode = LobbyMode.OnlineHost;
        StartCoroutine(LoadSceneWithFade(lobbySceneName));
    }

    /// <summary>게임 참여 버튼 — 데모에서는 비활성 안내.</summary>
    public void OnClickJoinGame()
    {
        if (joinGameUnavailablePanel != null)
        {
            joinGameUnavailablePanel.SetActive(true);
        }
        else
        {
            Debug.Log("[TitleMenuController] 게임 참여는 멀티플레이어 버전에서 지원합니다.");
        }
    }

    /// <summary>설정 버튼</summary>
    public void OnClickSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[TitleMenuController] settingsPanel이 연결되지 않았습니다.");
        }
    }

    /// <summary>설정 패널 닫기. 설정 패널 내 닫기 버튼에도 연결.</summary>
    public void OnClickCloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    /// <summary>게임 참여 안내 패널 닫기.</summary>
    public void OnClickCloseJoinUnavailable()
    {
        if (joinGameUnavailablePanel != null)
            joinGameUnavailablePanel.SetActive(false);
    }

    /// <summary>게임 종료 버튼</summary>
    public void OnClickQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>Discord 버튼</summary>
    public void OnClickDiscord()
    {
        if (string.IsNullOrEmpty(discordUrl))
        {
            Debug.LogWarning("[TitleMenuController] discordUrl이 비어 있습니다.");
            return;
        }
        Application.OpenURL(discordUrl);
    }

    // ── 내부 ──────────────────────────────────────────────────────

    IEnumerator LoadSceneWithFade(string sceneName)
    {
        if (screenFader != null && fadeOutDuration > 0f)
        {
            screenFader.FadeOut(fadeOutDuration);
            yield return new WaitForSeconds(fadeOutDuration);
        }

        SceneManager.LoadScene(sceneName);
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 솔로")]
    void Debug_Solo() => OnClickSolo();

    [ContextMenu("테스트: 게임 만들기 (Host)")]
    void Debug_CreateGame() => OnClickCreateGame();

    [ContextMenu("테스트: 설정 패널 열기")]
    void Debug_OpenSettings() => OnClickSettings();

    [ContextMenu("테스트: 게임 참여 안내")]
    void Debug_JoinGame() => OnClickJoinGame();
#endif
}
