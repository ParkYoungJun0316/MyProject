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
/// roomCodeInputField는 두 경로에서 의미가 다르다 — 로컬: 6자리 룸코드, Steam: Lobby Id 전체 숫자.
/// </summary>
public class TitleMenuController : MonoBehaviour
{
    [Header("씬 전환")]
    [Tooltip("로드할 로비 씬 이름.")]
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
    /// 이 프로세스에서 이미 Steam 네트워킹(호스트든 클라이언트든)을 시작한 적이 있으면(=이번이 2번째
    /// 이상 시도, 혹은 호스트로 방을 열었다 종료한 뒤 처음 접속하는 경우) 게임을
    /// <c>+connect_lobby &lt;lobbyId&gt;</c> 인자로 재실행하고 현재 프로세스를 종료한다.
    /// 재시작이 곧 "냉기동" 경로를 그대로 다시 타는 것이므로, SteamNetworkingSockets 릴레이 세션 /
    /// NGO NetworkSceneManager 씬 핸들이 누적되어 발생하는 중복 메시지·Server Scene Handle 충돌을
    /// 근본적으로 회피한다.
    /// 재시작 트리거 시 true 반환(호출부는 곧바로 return해야 함). 재실행 실패 시 false를 반환해
    /// 인프로세스 접속으로 폴백한다.
    /// </summary>
    static bool TryRestartForWarmReconnect(SteamId lobbyId)
    {
        Debug.Log($"[TitleMenuController][DIAG] TryRestartForWarmReconnect 진입 — lobbyId={lobbyId}, " +
                  $"HasStartedSteamNetworkingThisProcess={NetworkManagerSetup.HasStartedSteamNetworkingThisProcess}");

        if (!NetworkManagerSetup.HasStartedSteamNetworkingThisProcess) return false;

        Debug.Log($"[TitleMenuController] 이 프로세스에서 이미 Steam 네트워킹을 시작한 적 있음 — " +
                  $"트랜스포트 중복 메시지/씬 핸들 충돌 버그 회피를 위해 프로세스 재시작 후 lobbyId={lobbyId}로 재접속.");

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
            // in-scene NetworkObject(LobbyNetworkManager)가 OnNetworkSpawn을 받으려면
            // NetworkSceneManager를 통해 씬을 로드해야 한다.
            NetworkManager.Singleton.SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
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

            NetworkManager.Singleton.SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
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
    /// 확인 버튼.
    /// 로컬 경로: 6자리 룸코드 검증 후 LAN Discovery 시작.
    /// Steam 경로: Lobby Id(숫자) 검증 후 SteamLobbyManager.JoinLobbyAsync → StartClientSteam(§4).
    /// joinPanel 내 확인 버튼에 연결.
    /// </summary>
    public void OnClickConfirmJoin()
    {
        string code = roomCodeInputField != null ? roomCodeInputField.text.Trim() : string.Empty;

        if (UseLocalNetworkPath)
        {
            ConfirmJoinLocal(code);
        }
        else
        {
            ConfirmJoinSteam(code);
        }
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

    void ConfirmJoinSteam(string code)
    {
        if (code.Length == 0 || !IsDigitsOnly(code) || !ulong.TryParse(code, out ulong lobbyIdValue))
        {
            SetJoinStatus("Wrong code.");
            return;
        }

        Debug.Log($"[TitleMenuController] ConfirmJoinSteam — lobbyId={lobbyIdValue}");
        SetJoinStatus("Joining...");
        _ = JoinGameSteamAsync(lobbyIdValue, "코드입력");
    }

    /// <param name="source">
    /// 진단용 태그 — 어느 진입 경로에서 호출됐는지 로그로 구분하기 위함
    /// ("온기동초대"/"코드입력"/"냉기동"). 실제 로직 분기에는 쓰이지 않는다.
    /// </param>
    async System.Threading.Tasks.Task JoinGameSteamAsync(SteamId lobbyId, string source = "미상")
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Debug.Log($"[TitleMenuController][DIAG] JoinGameSteamAsync 진입 — source={source}, lobbyId={lobbyId}, " +
                  $"HasStartedSteamNetworkingThisProcess={NetworkManagerSetup.HasStartedSteamNetworkingThisProcess}");

        // "온기동" 트랜스포트 중복 메시지 버그 우회(SteamworksIntegrationDesign.md 트랙5·6):
        // 이 프로세스에서 이미 한 번이라도 Steam 네트워킹(호스트든 클라이언트든)을 시작한 적이
        // 있으면, 인프로세스 재접속은 Server Scene Handle 충돌로 항상 실패하는 것으로 실측 확인됨
        // (호스트 종료 후 첫 Client 접속도 포함 — 트랙6 세션10). 검증된 "냉기동" 경로
        // (+connect_lobby 커맨드라인 재실행)로 우회한다.
        if (TryRestartForWarmReconnect(lobbyId))
        {
            Debug.Log($"[TitleMenuController][DIAG] JoinGameSteamAsync — source={source}, 프로세스 재시작 경로로 위임" +
                      $"({sw.ElapsedMilliseconds}ms 경과, 이 프로세스는 곧 종료됨)");
            return;
        }

        if (SteamLobbyManager.Instance == null || NetworkManagerSetup.Instance == null)
        {
            Debug.LogError($"[TitleMenuController][DIAG] JoinGameSteamAsync — source={source}, " +
                           "SteamLobbyManager/NetworkManagerSetup을 찾을 수 없습니다.");
            SetJoinStatus("Failed to join.");
            return;
        }

        // 정체(먹통) 감지용 워치독 — timeoutSeconds 안에 로비 씬으로 전환 안 되면 전체 상태를 한 번에 덤프.
        // 접속이 성공하면 0.Title 씬이 언로드되며 이 컴포넌트 자체가 파괴돼 코루틴이 조용히 멈춘다(정상, 별도 성공 로그 없음).
        StartCoroutine(JoinWatchdog(source, lobbyId));

        try
        {
            Debug.Log($"[TitleMenuController][DIAG] JoinGameSteamAsync — source={source}, JoinLobbyAsync 호출 시작 " +
                      $"({sw.ElapsedMilliseconds}ms 경과)");
            Steamworks.Data.Lobby? lobby = await SteamLobbyManager.Instance.JoinLobbyAsync(lobbyId);
            Debug.Log($"[TitleMenuController][DIAG] JoinGameSteamAsync — source={source}, JoinLobbyAsync 반환 " +
                      $"({sw.ElapsedMilliseconds}ms 경과) — lobby={(lobby.HasValue ? lobby.Value.Id.ToString() : "null")}");
            if (lobby == null)
            {
                SetJoinStatus("Room not found.");
                return;
            }

            Debug.Log($"[TitleMenuController][DIAG] JoinGameSteamAsync — source={source}, " +
                      $"Lobby 멤버 {lobby.Value.MemberCount}/{lobby.Value.MaxMembers}, Owner={lobby.Value.Owner.Id}");

            if (joinPanel != null) joinPanel.SetActive(false);

            LobbyContext.Mode = LobbyMode.OnlineClient;

            // Host가 SetData("vport", ...)로 공유한 virtual port를 읽어 그대로 접속 — 이슈 D 우회.
            // 값이 없거나(구버전) 파싱 실패 시 0으로 폴백.
            string vportStr = lobby.Value.GetData("vport");
            if (!int.TryParse(vportStr, out int vport)) vport = 0;

            // 로컬 경로와 동일하게 StartClient 후 씬 전환은 NGO SceneManager가 자동 처리 — 수동 LoadScene 금지.
            bool ok = NetworkManagerSetup.Instance.StartClientSteam(lobby.Value.Owner.Id, vport);
            Debug.Log($"[TitleMenuController][DIAG] JoinGameSteamAsync — source={source}, StartClientSteam 반환 " +
                      $"({sw.ElapsedMilliseconds}ms 경과) — ok={ok}, virtualPort={vport}. " +
                      "이후 씬 전환 진행 상황은 [NetworkManagerSetup][DIAG][SceneEvent] 로그로 추적됨.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TitleMenuController][DIAG] JoinGameSteamAsync — source={source}, 예외 " +
                           $"({sw.ElapsedMilliseconds}ms 경과) — {e}");
            SetJoinStatus("Failed to join.");
        }
    }

    /// <summary>
    /// 접속 시도 후 일정 시간 안에 로비 씬(<see cref="lobbySceneName"/>)으로 전환됐는지 확인하는 정체 감지 워치독.
    /// 성공하면 0.Title 씬이 언로드되며 이 컴포넌트가 파괴돼 코루틴이 조용히 중단되므로 별도 성공 로그는 없다.
    /// 실패(정체)했을 때만 전체 네트워크/Steam 상태를 한 번에 덤프해서, 다음 재현에서 로그 한 번으로
    /// 실패 지점을 좁힐 수 있게 한다("초대"/"코드입력"/"냉기동" 중 어느 source에서도 동일하게 동작).
    /// </summary>
    IEnumerator JoinWatchdog(string source, SteamId lobbyId, float timeoutSeconds = 10f)
    {
        yield return new WaitForSeconds(timeoutSeconds);

        var net = NetworkManager.Singleton;
        Debug.LogWarning(
            $"[TitleMenuController][DIAG][WATCHDOG] source={source}, lobbyId={lobbyId} — " +
            $"{timeoutSeconds}s 경과했는데도 '{lobbySceneName}' 씬으로 전환되지 않음(정체 의심). " +
            $"ActiveScene={SceneManager.GetActiveScene().name}, " +
            $"NGO.IsListening={(net != null ? net.IsListening.ToString() : "null")}, " +
            $"NGO.IsConnectedClient={(net != null ? net.IsConnectedClient.ToString() : "null")}, " +
            $"NGO.LocalClientId={(net != null ? net.LocalClientId.ToString() : "null")}, " +
            $"NGO.ConnectedClients.Count={(net != null ? net.ConnectedClients.Count.ToString() : "null")}, " +
            $"SteamLobby.IsInLobby={SteamLobbyManager.Instance?.IsInLobby}, " +
            $"HasStartedSteamNetworkingThisProcess={NetworkManagerSetup.HasStartedSteamNetworkingThisProcess}");
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
