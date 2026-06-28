using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 타이틀 씬 메인 메뉴 컨트롤러.
///
/// [배치 방법]
/// TitleCanvas 또는 별도 빈 GameObject에 부착.
///
/// [Inspector 연결]
/// - lobbySceneName         : 로비 씬 이름 (기본 "1.Lobby")
/// - discordUrl             : Discord 초대 링크
/// - screenFader            : 선택. 씬 전환 전 페이드아웃
/// - settingsPanel          : 설정/옵션 패널 GameObject
/// - joinPanel              : 룸코드 입력 패널 GameObject
/// - roomCodeInputField     : 6자리 숫자 입력 TMP_InputField
/// - joinStatusText         : 상태 메시지 TMP_Text (찾는 중... / 방을 찾을 수 없습니다.)
/// - discoveryTimeoutSeconds: 타임아웃 (기본 5초)
///
/// [버튼 OnClick 연결]
/// 솔로           → OnClickSolo()
/// 게임 만들기    → OnClickCreateGame()
/// 게임 참여      → OnClickJoinGame()
/// Join 확인      → OnClickConfirmJoin()
/// Join 닫기      → OnClickCloseJoin()
/// 설정           → OnClickSettings()
/// 설정 닫기      → OnClickCloseSettings()
/// 게임 종료      → OnClickQuit()
/// Discord        → OnClickDiscord()
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

    [Tooltip("게임 참여 버튼 클릭 시 열릴 룸코드 입력 패널.")]
    [SerializeField] private GameObject joinPanel;

    [Tooltip("joinPanel 안의 TMP_InputField (6자리 숫자).")]
    [SerializeField] private TMP_InputField roomCodeInputField;

    [Tooltip("joinPanel 안의 상태 메시지 TMP_Text.\n예) 찾는 중... / 방을 찾을 수 없습니다.")]
    [SerializeField] private TMP_Text joinStatusText;

    [Tooltip("룸코드 Discovery 타임아웃 (초).")]
    [SerializeField] private float discoveryTimeoutSeconds = 5f;

    [Header("페이드 (선택)")]
    [Tooltip("씬 전환 전 페이드아웃. 비워두면 즉시 전환.")]
    [SerializeField] private ScreenFader screenFader;

    [Tooltip("페이드아웃 시간(초). 0이면 즉시.")]
    [SerializeField] private float fadeOutDuration = 0f;

    private Coroutine _discoveryTimeoutCoroutine;

    // ── 버튼 콜백 ─────────────────────────────────────────────────

    /// <summary>솔로 버튼 — NGO 없이 로비(Offline 모드)로 이동.</summary>
    public void OnClickSolo()
    {
        LobbyContext.Mode = LobbyMode.Offline;
        StartCoroutine(LoadSceneWithFade(lobbySceneName));
    }

    /// <summary>게임 만들기 버튼 — 룸코드 생성 후 NetworkManager.StartHost() → 로비 이동.</summary>
    public void OnClickCreateGame()
    {
        LobbyContext.Mode = LobbyMode.OnlineHost;

        if (NetworkManagerSetup.Instance == null)
        {
            Debug.LogWarning("[TitleMenuController] NetworkManagerSetup을 찾을 수 없습니다.");
            StartCoroutine(LoadSceneWithFade(lobbySceneName));
            return;
        }

        string code = LanDiscovery.GenerateRoomCode();
        bool ok = NetworkManagerSetup.Instance.StartHost(code);

        if (ok)
        {
            // in-scene NetworkObject(LobbyNetworkManager)가 OnNetworkSpawn을 받으려면
            // NetworkSceneManager를 통해 씬을 로드해야 한다.
            NetworkManager.Singleton.SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
        }
        else
        {
            Debug.LogError("[TitleMenuController] StartHost 실패. 로비 이동 중단.");
        }
    }

    /// <summary>게임 참여 버튼 — 룸코드 입력 패널 열기.</summary>
    public void OnClickJoinGame()
    {
        if (joinPanel != null)
        {
            joinPanel.SetActive(true);
            if (roomCodeInputField != null) roomCodeInputField.text = string.Empty;
            SetJoinStatus(string.Empty);
        }
        else
        {
            Debug.LogWarning("[TitleMenuController] joinPanel이 연결되지 않았습니다. Inspector에서 연결하세요.");
        }
    }

    /// <summary>
    /// 룸코드 확인 버튼 — 입력값 검증 후 LAN Discovery 시작.
    /// joinPanel 내 확인 버튼에 연결.
    /// </summary>
    public void OnClickConfirmJoin()
    {
        string code = roomCodeInputField != null ? roomCodeInputField.text.Trim() : string.Empty;

        if (code.Length != 6 || !IsDigitsOnly(code))
        {
            SetJoinStatus("6자리 숫자를 입력해주세요.");
            return;
        }

        if (LanDiscovery.Instance == null)
        {
            Debug.LogWarning("[TitleMenuController] LanDiscovery를 찾을 수 없습니다. " +
                             "0.Title NetworkManager GameObject에 컴포넌트를 추가하세요.");
            return;
        }

        SetJoinStatus("찾는 중...");
        LanDiscovery.Instance.StartDiscovery(code, OnDiscoveryFound);
        _discoveryTimeoutCoroutine = StartCoroutine(DiscoveryTimeout());
    }

    /// <summary>룸코드 입력 패널 닫기 + Discovery 중단.</summary>
    public void OnClickCloseJoin()
    {
        if (_discoveryTimeoutCoroutine != null)
        {
            StopCoroutine(_discoveryTimeoutCoroutine);
            _discoveryTimeoutCoroutine = null;
        }

        LanDiscovery.Instance?.Stop();

        if (joinPanel != null) joinPanel.SetActive(false);
    }

    /// <summary>설정 버튼</summary>
    public void OnClickSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
        else Debug.LogWarning("[TitleMenuController] settingsPanel이 연결되지 않았습니다.");
    }

    /// <summary>설정 패널 닫기. 설정 패널 내 닫기 버튼에도 연결.</summary>
    public void OnClickCloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
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

    // ── Discovery 콜백 ────────────────────────────────────────────

    void OnDiscoveryFound(string hostIp)
    {
        if (_discoveryTimeoutCoroutine != null)
        {
            StopCoroutine(_discoveryTimeoutCoroutine);
            _discoveryTimeoutCoroutine = null;
        }

        if (joinPanel != null) joinPanel.SetActive(false);

        LobbyContext.Mode = LobbyMode.OnlineClient;

        // StartClient 후 씬 전환은 NGO SceneManager가 자동으로 처리.
        // 수동 LoadScene 금지 — Host의 NetworkSceneManager.LoadScene이 Client를 동기화함.
        NetworkManagerSetup.Instance?.StartClient(hostIp);
    }

    IEnumerator DiscoveryTimeout()
    {
        yield return new WaitForSeconds(discoveryTimeoutSeconds);
        LanDiscovery.Instance?.Stop();
        SetJoinStatus("방을 찾을 수 없습니다.");
        _discoveryTimeoutCoroutine = null;
    }

    // ── 내부 ──────────────────────────────────────────────────────

    void SetJoinStatus(string message)
    {
        if (joinStatusText != null) joinStatusText.text = message;
    }

    static bool IsDigitsOnly(string s)
    {
        foreach (char c in s)
            if (!char.IsDigit(c)) return false;
        return true;
    }

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

    [ContextMenu("테스트: 게임 참여 패널 열기")]
    void Debug_JoinPanel() => OnClickJoinGame();

    [ContextMenu("테스트: 설정 패널 열기")]
    void Debug_OpenSettings() => OnClickSettings();
#endif
}
