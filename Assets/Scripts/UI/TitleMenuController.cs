using System;
using System.Collections;
using Steamworks;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// 타이틀 씬 메인 메뉴 컨트롤러.
///
/// [배치 방법]
/// TitleCanvas 또는 별도 빈 GameObject에 부착.
///
/// [Inspector 연결]
/// - lobbySceneName         : Host/Client 접속 후 이동할 씬 이름 (기본 "Tutorial" — 구 로비 역할을 흡수함, NetworkDesign.md §6B)
/// - discordUrl             : Discord 초대 링크
/// - screenFader            : 선택. 씬 전환 전 페이드아웃
/// - settingsPanel          : 설정/옵션 패널 GameObject
/// - joinPanel              : 룸코드 입력 패널 GameObject
/// - roomCodeInputField     : 6자리 숫자 입력 TMP_InputField
/// - joinStatusText         : 상태 메시지 TMP_Text (찾는 중... / 방을 찾을 수 없습니다.)
/// - discoveryTimeoutSeconds: 타임아웃 (기본 5초)
///
/// [버튼 OnClick 연결]
/// 게임 만들기    → OnClickCreateGame()
/// 게임 참여      → OnClickJoinGame()
/// Join 확인      → OnClickConfirmJoin()
/// Join 닫기      → OnClickCloseJoin()
/// 설정           → OnClickSettings()
/// (설정 패널 내부 닫기(X) 버튼은 OptionsMenuController.OnClickClose()에 직결 — 패널 자신을 SetActive(false).
///  OnClickCloseSettings()는 코드에서 강제로 닫아야 할 때 쓰는 보조 API.)
/// 게임 종료      → OnClickQuit()
/// Discord        → OnClickDiscord()
///
/// [로컬 IP vs Steam 경로 분기 — SteamworksIntegrationDesign.md §5 확정]
/// 에디터(①ParrelSync) 또는 Development Build(②)면 기존 LanDiscovery/StartHost/StartClient
/// 로컬 IP 경로를 그대로 사용한다. 그 외(정식 릴리스 빌드, ④)는 Steam Lobby 경로
/// (SteamLobbyManager + StartHostSteam/StartClientSteam)를 사용한다.
/// roomCodeInputField/joinPanel은 **로컬(①②) 경로 전용**이다 — Steam(④)은 룸코드 입력 UI 자체가 없고
/// 오버레이 초대 수락/+connect_lobby로만 조인한다(2026-08-17 확정, NetworkDesign.md §4.2/§5).
/// </summary>
public class TitleMenuController : MonoBehaviour
{
    [Header("씬 전환")]
    [Tooltip("접속 후 이동할 씬 이름 (구 로비 역할을 흡수한 Tutorial 사전 게이트 구간, NetworkDesign.md §6B).")]
    [SerializeField] private string lobbySceneName = "Tutorial";

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

    [Tooltip("joinPanel 안의 상태 메시지 TMP_Text.\n예) Searching... / Room not found.")]
    [SerializeField] private TMP_Text joinStatusText;

    [Tooltip("룸코드 Discovery 타임아웃 (초).")]
    [SerializeField] private float discoveryTimeoutSeconds = 5f;

    [Header("페이드 (선택)")]
    [Tooltip("씬 전환 전 페이드아웃. 비워두면 즉시 전환.")]
    [SerializeField] private ScreenFader screenFader;

    [Tooltip("페이드아웃 시간(초). 0이면 즉시.")]
    [SerializeField] private float fadeOutDuration = 0f;

    private Coroutine _discoveryTimeoutCoroutine;

    /// <summary>
    /// true면 기존 로컬 IP 경로(LanDiscovery/StartHost/StartClient) 사용.
    /// false면 Steam Lobby 경로 사용 — 정식 릴리스 빌드 기준(§5).
    /// 판정 로직 단일 소스는 <see cref="NetworkManagerSetup.UseLocalNetworkPath"/>.
    /// </summary>
    static bool UseLocalNetworkPath => NetworkManagerSetup.UseLocalNetworkPath;

    /// <summary>
    /// 게임이 꺼져 있다가 Steam Invite 수락으로 새로 실행된 경우, 앱 전체 수명에서 단 한 번만
    /// 커맨드라인 <c>+connect_lobby</c> 처리를 시도하기 위한 플래그.
    /// 타이틀 복귀(TitleReturnFlow)로 이 씬이 다시 로드되어도 재시도하지 않는다 — 그 시점엔
    /// 이미 최초 진입에서 처리(성공/실패)가 끝난 상태이므로 중복 Join 시도를 막는다.
    /// </summary>
    static bool s_launchLobbyArgsHandled;

    // ── Unity 콜백 ────────────────────────────────────────────────

    /// <summary>
    /// OnInviteAccepted 구독은 OnEnable이 아니라 여기 Start() 한 곳에서만 한다 — 1방향.
    /// Unity는 서로 다른 오브젝트 간 Awake/OnEnable 순서를 보장하지 않아 OnEnable()에서
    /// 구독하면 SteamLobbyManager.Awake()보다 먼저 실행될 경우 Instance가 아직 null이라
    /// 구독이 조용히 누락될 수 있었음(트랙5 이슈B 온기동 최초 진입 재현).
    /// Start()는 씬 내 모든 Awake/OnEnable이 끝난 뒤 정확히 1회만 호출되는 것이 보장되므로,
    /// 여기서만 구독하면 재시도·중복방지 플래그 없이도 항상 정확히 1번만 구독된다.
    /// (이 컴포넌트는 타이틀 씬 생명주기 동안 비활성화되지 않으므로 OnEnable 재구독은 불필요.)
    /// </summary>
    void Start()
    {
        if (!UseLocalNetworkPath)
        {
            TryAutoJoinFromLaunchArgs();

            if (SteamLobbyManager.Instance == null)
                Debug.LogError("[TitleMenuController] SteamLobbyManager.Instance가 null — OnInviteAccepted 구독 실패. " +
                               "0.Title NetworkManager GameObject에 SteamLobbyManager가 있는지 확인하세요.");
            else
                SteamLobbyManager.Instance.OnInviteAccepted += OnSteamInviteAccepted;
        }
    }

    void OnDisable()
    {
        if (SteamLobbyManager.Instance != null)
            SteamLobbyManager.Instance.OnInviteAccepted -= OnSteamInviteAccepted;
    }

    /// <summary>
    /// 게임이 꺼져 있는 상태에서 Steam Lobby Invite를 수락하면, Steam이 게임을 실행하며
    /// <c>+connect_lobby &lt;64bit lobbyId&gt;</c>를 커맨드라인 인자로 넘긴다(Steamworks 공식 문서).
    /// 이미 실행 중인 상태에서 수락하는 경로(<see cref="OnSteamInviteAccepted"/>, GameLobbyJoinRequested_t)와는
    /// 별개의 진입점 — 여기서 잡아서 동일한 JoinGameSteamAsync 경로로 합류시킨다.
    /// </summary>
    void TryAutoJoinFromLaunchArgs()
    {
        if (s_launchLobbyArgsHandled) return;
        s_launchLobbyArgsHandled = true;

        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], "+connect_lobby", StringComparison.OrdinalIgnoreCase))
                continue;

            if (ulong.TryParse(args[i + 1], out ulong lobbyIdValue) && lobbyIdValue > 0)
            {
                Debug.Log($"[TitleMenuController] 커맨드라인 +connect_lobby 감지 — lobbyId={lobbyIdValue}");
                SetJoinStatus("Accepting invite...");
                _ = JoinGameSteamAsync(lobbyIdValue, "냉기동");
            }
            else
            {
                Debug.LogWarning($"[TitleMenuController] +connect_lobby 뒤에 유효한 lobbyId가 없습니다 — 값='{args[i + 1]}'");
            }
            return;
        }
    }

    /// <summary>
    /// 이 프로세스에서 이미 Steam 네트워킹을 시작한 적 있으면 인프로세스 재접속 대신
    /// <c>+connect_lobby &lt;lobbyId&gt;</c>로 프로세스를 재시작한다 — "웜 리커넥트는 항상 재시작"
    /// 정책(SteamworksIntegrationDesign.md 트랙6 11차 세션 확정, 상류 Facepunch 트랜스포트 버그 회피).
    /// 재시작 트리거 시 true(호출부는 곧바로 return). 재실행 실패 시 false — 인프로세스 접속 폴백.
    /// </summary>
    static bool TryRestartForWarmReconnect(SteamId lobbyId)
    {
        if (!NetworkManagerSetup.HasStartedSteamNetworkingThisProcess) return false;

        Debug.Log($"[TitleMenuController] 이 프로세스에서 이미 Steam 네트워킹을 시작한 적 있음 — " +
                  $"프로세스 재시작 후 lobbyId={lobbyId}로 재접속.");

        if (NetworkManagerSetup.RestartWithConnectLobby(lobbyId)) return true;

        Debug.LogWarning("[TitleMenuController] 재시작 실패 — 인프로세스 접속으로 폴백합니다(재현될 수 있음).");
        return false;
    }

    // ── 버튼 콜백 ─────────────────────────────────────────────────

    /// <summary>
    /// 게임 만들기 버튼.
    /// 로컬 경로: 룸코드 생성 후 StartHost() → 로비 이동.
    /// Steam 경로: Private Lobby 생성 후 StartHostSteam() → 로비 이동(§3·§5).
    /// </summary>
    public void OnClickCreateGame()
    {
        Debug.Log($"[TitleMenuController] OnClickCreateGame 클릭 — UseLocalNetworkPath={UseLocalNetworkPath}");
        LobbyContext.Mode = LobbyMode.OnlineHost;

        if (UseLocalNetworkPath)
        {
            CreateGameLocal();
        }
        else
        {
            _ = CreateGameSteamAsync();
        }
    }

    void CreateGameLocal()
    {
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
            // in-scene NetworkObject(TutorialNetworkManager)가 OnNetworkSpawn을 받으려면
            // NetworkSceneManager를 통해 씬을 로드해야 한다.
            StartCoroutine(LoadLobbySceneWithCurtain());
        }
        else
        {
            Debug.LogError("[TitleMenuController] StartHost 실패. 로비 이동 중단.");
        }
    }

    async System.Threading.Tasks.Task CreateGameSteamAsync()
    {
        Debug.Log("[TitleMenuController] CreateGameSteamAsync 진입");

        if (SteamLobbyManager.Instance == null || NetworkManagerSetup.Instance == null)
        {
            Debug.LogError("[TitleMenuController] SteamLobbyManager/NetworkManagerSetup을 찾을 수 없습니다.");
            return;
        }

        try
        {
            Debug.Log("[TitleMenuController] CreateLobbyAsync 호출 시작");
            Steamworks.Data.Lobby? lobby = await SteamLobbyManager.Instance.CreateLobbyAsync();
            Debug.Log($"[TitleMenuController] CreateLobbyAsync 반환 — lobby={(lobby.HasValue ? lobby.Value.Id.ToString() : "null")}");
            if (lobby == null)
            {
                Debug.LogError("[TitleMenuController] Steam Lobby 생성 실패. 로비 이동 중단.");
                return;
            }

            bool ok = NetworkManagerSetup.Instance.StartHostSteam(lobby.Value.Id.Value.ToString());
            Debug.Log($"[TitleMenuController] StartHostSteam 반환 — ok={ok}");
            if (!ok)
            {
                Debug.LogError("[TitleMenuController] StartHostSteam 실패. Lobby 정리 후 중단.");
                SteamLobbyManager.Instance.LeaveCurrentLobby();
                return;
            }

            // 이슈 D 우회용 virtual port를 Lobby 데이터로 공유 — Client가 StartClientSteam에 그대로 전달.
            lobby.Value.SetData("vport", NetworkManagerSetup.Instance.LastHostVirtualPort.ToString());

            StartCoroutine(LoadLobbySceneWithCurtain());
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TitleMenuController] CreateGameSteamAsync 예외 — {e}");
        }
    }

    /// <summary>게임 참여 버튼 — 룸코드/LobbyId 입력 패널 열기.</summary>
    public void OnClickJoinGame()
    {
        if (joinPanel != null)
        {
            joinPanel.SetActive(true);
            if (roomCodeInputField != null) roomCodeInputField.text = string.Empty;
            SetJoinStatus(string.Empty);
            StartCoroutine(FocusRoomCodeInputNextFrame());
        }
        else
        {
            Debug.LogWarning("[TitleMenuController] joinPanel이 연결되지 않았습니다. Inspector에서 연결하세요.");
        }
    }

    IEnumerator FocusRoomCodeInputNextFrame()
    {
        yield return null;
        if (roomCodeInputField == null) yield break;
        roomCodeInputField.ActivateInputField();
        EventSystem.current?.SetSelectedGameObject(roomCodeInputField.gameObject);
    }

    /// <summary>
    /// 확인 버튼. joinPanel 내 확인 버튼에 연결.
    /// 로컬(①②) 경로 전용 — 6자리 룸코드 검증 후 LAN Discovery 시작.
    /// Steam(④) 경로에는 이 버튼/패널 자체가 없다 — 오버레이 초대 수락(OnSteamInviteAccepted) 또는
    /// +connect_lobby 냉기동(TryAutoJoinFromLaunchArgs)으로만 조인한다. 코드 입력으로 Steam Lobby에
    /// 참여하는 경로(구 ConfirmJoinSteam)는 2026-08-17 확정으로 완전히 삭제됨
    /// (NetworkDesign.md §4.2/§5, CheerAndTutorialDesign.md §6B.6).
    /// </summary>
    public void OnClickConfirmJoin()
    {
        if (!UseLocalNetworkPath)
        {
            Debug.LogWarning("[TitleMenuController] Steam 빌드에서는 룸코드 참여 UI를 쓰지 않습니다. " +
                             "오버레이 초대 수락 또는 +connect_lobby로만 조인하세요.");
            return;
        }

        string code = roomCodeInputField != null ? roomCodeInputField.text.Trim() : string.Empty;
        ConfirmJoinLocal(code);
    }

    void ConfirmJoinLocal(string code)
    {
        if (code.Length != 6 || !IsDigitsOnly(code))
        {
            SetJoinStatus("Wrong code.");
            return;
        }

        if (LanDiscovery.Instance == null)
        {
            Debug.LogWarning("[TitleMenuController] LanDiscovery를 찾을 수 없습니다. " +
                             "0.Title NetworkManager GameObject에 컴포넌트를 추가하세요.");
            return;
        }

        SetJoinStatus("Searching...");
        LanDiscovery.Instance.StartDiscovery(code, OnDiscoveryFound);
        _discoveryTimeoutCoroutine = StartCoroutine(DiscoveryTimeout());
    }

    /// <param name="source">로그 구분용 태그("온기동초대"/"냉기동"). 로직 분기에는 쓰이지 않는다.</param>
    async System.Threading.Tasks.Task JoinGameSteamAsync(SteamId lobbyId, string source = "미상")
    {
        Debug.Log($"[TitleMenuController] JoinGameSteamAsync — source={source}, lobbyId={lobbyId}");

        // 웜 리커넥트는 항상 재시작(SteamworksIntegrationDesign.md 트랙6 확정 정책) — 인프로세스
        // 재접속은 상류 Facepunch 트랜스포트 버그로 Server Scene Handle 충돌이 재현됨.
        if (TryRestartForWarmReconnect(lobbyId))
            return;

        if (SteamLobbyManager.Instance == null || NetworkManagerSetup.Instance == null)
        {
            Debug.LogError($"[TitleMenuController] JoinGameSteamAsync — source={source}, " +
                           "SteamLobbyManager/NetworkManagerSetup을 찾을 수 없습니다.");
            SetJoinStatus("Failed to join.");
            return;
        }

        try
        {
            Steamworks.Data.Lobby? lobby = await SteamLobbyManager.Instance.JoinLobbyAsync(lobbyId);
            if (lobby == null)
            {
                SetJoinStatus("Room not found.");
                return;
            }

            if (joinPanel != null) joinPanel.SetActive(false);

            LobbyContext.Mode = LobbyMode.OnlineClient;

            // Host가 SetData("vport", ...)로 공유한 virtual port를 읽어 그대로 접속 — 이슈 D 우회.
            // 값이 없거나(구버전) 파싱 실패 시 0으로 폴백.
            string vportStr = lobby.Value.GetData("vport");
            if (!int.TryParse(vportStr, out int vport)) vport = 0;

            // 로컬 경로와 동일하게 StartClient 후 씬 전환은 NGO SceneManager가 자동 처리 — 수동 LoadScene 금지.
            bool ok = NetworkManagerSetup.Instance.StartClientSteam(lobby.Value.Owner.Id, vport);
            Debug.Log($"[TitleMenuController] JoinGameSteamAsync — source={source}, StartClientSteam ok={ok}, virtualPort={vport}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TitleMenuController] JoinGameSteamAsync — source={source}, 예외 — {e}");
            SetJoinStatus("Failed to join.");
        }
    }

    /// <summary>
    /// Steam Invite Overlay 수락 시(SteamLobbyManager.OnInviteAccepted) 자동 참여.
    /// 게임이 이미 타이틀 화면에서 실행 중인 경우만 의미 있음(이 컴포넌트가 활성 상태일 때만 구독됨).
    /// </summary>
    void OnSteamInviteAccepted(SteamId lobbyId)
    {
        SetJoinStatus("Accepting invite...");
        _ = JoinGameSteamAsync(lobbyId, "온기동초대");
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

    /// <summary>
    /// 설정 패널 닫기 — 코드에서 강제로 닫아야 할 때 쓰는 보조 API.
    /// 패널 내부 닫기(X) 버튼은 OptionsMenuController.OnClickClose()에 직결되어 있어 이 메서드를 거치지 않는다.
    /// </summary>
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
        SetJoinStatus("Room not found.");
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

    /// <summary>
    /// Host가 lobbySceneName(Tutorial)으로 넘어가기 전 LoadingCurtain으로 잠깐 덮어준다.
    /// NGO NetworkSceneManager.LoadScene은 in-scene NetworkObject(TutorialNetworkManager)의
    /// OnNetworkSpawn을 받기 위해 반드시 이 경로로 호출해야 하므로 대체하지 않는다.
    /// </summary>
    IEnumerator LoadLobbySceneWithCurtain()
    {
        if (LoadingCurtain.Instance != null)
            yield return LoadingCurtain.Instance.BeginCoverRoutine(waitForPlayersReady: true);

        NetworkManager.Singleton.SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
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
    [ContextMenu("테스트: 게임 만들기 (Host)")]
    void Debug_CreateGame() => OnClickCreateGame();

    [ContextMenu("테스트: 게임 참여 패널 열기")]
    void Debug_JoinPanel() => OnClickJoinGame();

    [ContextMenu("테스트: 설정 패널 열기")]
    void Debug_OpenSettings() => OnClickSettings();
#endif
}
