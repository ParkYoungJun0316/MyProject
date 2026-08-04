using Netcode.Transports.Facepunch;
using Steamworks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// NetworkManager + Transport(UnityTransport / FacepunchTransport) 초기화 래퍼.
/// NetworkManager와 같은 GameObject에 부착.
/// DontDestroyOnLoad는 NetworkManager 자체가 처리하므로 별도 처리 불필요.
///
/// [배치 방법]
/// 0.Title 씬 > NetworkManager GameObject에 이 컴포넌트 추가.
/// 같은 오브젝트에 NetworkManager + UnityTransport + FacepunchTransport + NetworkManagerSetup
/// 네 컴포넌트가 있어야 함(SteamworksIntegrationDesign.md §2).
///
/// [Inspector 설정]
/// - port          : 7777 (고정값, 로컬 ①②MVP 전용)
/// - steamTransport: 같은 GameObject의 FacepunchTransport 연결 (Steam 경로 전용)
/// - NetworkManager Inspector > Max Connections : 4
/// - NetworkManager Inspector > Dont Destroy On Load : 체크
///
/// [연결 경로 — 둘 다 유지, 이름 충돌 없음 (§4 확정)]
/// - 로컬 IP(①ParrelSync ②Dev Build): StartHost(roomCode) / StartClient(address)
/// - Steam(④ 정식 배포): StartHostSteam() / StartClientSteam(SteamId hostId)
///
/// [호출 시점]
/// - OnClickCreateGame (TitleMenuController) → StartHost() 또는 StartHostSteam()
/// - OnClickJoinGame 룸코드/Lobby 확인 후 → StartClient(ip) 또는 StartClientSteam(hostId)
/// - 타이틀 복귀 시 → Shutdown()
/// </summary>
public class NetworkManagerSetup : MonoBehaviour
{
    public static NetworkManagerSetup Instance { get; private set; }

    [Header("Transport 설정")]
    [Tooltip("LAN MVP 고정 포트. Steam 전환 시 무관.")]
    [SerializeField] private ushort port = 7777;

    [Tooltip("최대 접속 인원 (참고용). NGO 2.x는 NetworkConfig.MaxConnections 미지원.\n" +
             "실제 제한은 로비 슬롯 로직(Connection Approval, Step 3)에서 처리.")]
    [SerializeField] private int maxConnections = 4;

    [Header("Steam Transport (SteamworksIntegrationDesign.md §2·§4)")]
    [Tooltip("같은 GameObject에 FacepunchTransport 컴포넌트를 추가하고 연결하세요.\n" +
             "로컬 IP 경로(StartHost/StartClient)는 이 필드를 쓰지 않음 — Steam 경로 전용.")]
    [SerializeField] private FacepunchTransport steamTransport;

    [Header("Steam 스모크 테스트 (에디터 전용 — Lobby 붙기 전 Transport 단독 검증)")]
    [Tooltip("StartClientSteam 테스트용 대상 SteamId. Host 쪽 콘솔에 찍히는 SteamId를 붙여넣으세요.")]
    [SerializeField] private ulong debugTargetSteamId;

    private NetworkManager  _net;
    private UnityTransport  _transport;

    /// <summary>
    /// true면 기존 로컬 IP 경로(LanDiscovery/StartHost/StartClient) 사용.
    /// false면 Steam Lobby 경로 사용 — 정식 릴리스 빌드 기준(SteamworksIntegrationDesign.md §5).
    /// 로컬 vs 릴리스 경로 판정의 단일 소스 — TitleMenuController / GameLocalizationBootstrap 등에서 재사용.
    /// </summary>
    public static bool UseLocalNetworkPath => Application.isEditor || Debug.isDebugBuild;

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _net       = GetComponent<NetworkManager>();
        _transport = GetComponent<UnityTransport>();

        if (_net == null)
            Debug.LogError("[NetworkManagerSetup] NetworkManager 컴포넌트가 없습니다. 같은 GameObject에 추가하세요.");
        if (_transport == null)
            Debug.LogError("[NetworkManagerSetup] UnityTransport 컴포넌트가 없습니다. 같은 GameObject에 추가하세요.");
    }

    // ── 공개 API ──────────────────────────────────────────────────

    /// <summary>
    /// Host 시작. TitleMenuController.OnClickCreateGame()에서 호출.
    /// 이미 실행 중이면 무시.
    /// </summary>
    /// <param name="roomCode">LanDiscovery에 브로드캐스트할 6자리 룸코드.</param>
    public bool StartHost(string roomCode)
    {
        if (_net == null || _transport == null) return false;

        if (_net.IsListening)
        {
            Debug.LogWarning("[NetworkManagerSetup] 이미 실행 중입니다. StartHost 무시.");
            return true;
        }

        _net.NetworkConfig.NetworkTransport = _transport; // Steam 경로에서 전환됐을 수 있으므로 로컬로 복귀
        _transport.SetConnectionData("0.0.0.0", port);

        // Connection Approval은 NetworkManager Inspector에서 체크.
        // 콜백만 코드로 등록.
        _net.ConnectionApprovalCallback = ApproveConnection;

        bool ok = _net.StartHost();
        if (ok)
        {
            RoomCode = roomCode;
            LanDiscovery.Instance?.StartBroadcast(roomCode, port);
            Debug.Log($"[NetworkManagerSetup] Host 시작됨 — 포트 {port}, 룸코드 {roomCode}");
        }
        else
        {
            Debug.LogError("[NetworkManagerSetup] StartHost() 실패");
        }
        return ok;
    }

    /// <summary>
    /// Client 시작. 룸코드 → IP 변환 후 호출 (Step 2에서 연결).
    /// </summary>
    /// <param name="address">호스트 IP 주소</param>
    public bool StartClient(string address)
    {
        if (_net == null || _transport == null) return false;

        if (_net.IsListening)
        {
            Debug.LogWarning("[NetworkManagerSetup] 이미 실행 중입니다. StartClient 무시.");
            return true;
        }

        _net.NetworkConfig.NetworkTransport = _transport; // Steam 경로에서 전환됐을 수 있으므로 로컬로 복귀
        _transport.SetConnectionData(address, port);

        bool ok = _net.StartClient();
        if (ok) Debug.Log($"[NetworkManagerSetup] Client 시작됨 — {address}:{port}");
        else    Debug.LogError("[NetworkManagerSetup] StartClient() 실패");
        return ok;
    }

    /// <summary>
    /// Steam Host 시작 (SteamworksIntegrationDesign.md §4 확정 — 로컬 StartHost와 별도 경로).
    /// FacepunchTransport로 전환 후 StartHost와 동일한 흐름을 재사용한다.
    /// 로컬 경로와 달리 SteamManager.EnsureInitialized()를 호출 — §5 "Steam 경로에서만 초기화" 구현.
    /// </summary>
    /// <param name="roomCode">
    /// 로비 화면에 공유할 룸코드. Steam 경로에서는 Lobby Id 전체 문자열(마스킹 전)을 그대로 넘기면
    /// LobbyNetworkManager가 기존 SharedRoomCode 동기화 경로를 그대로 재사용한다(§3).
    /// 비워두면 RoomCode가 설정되지 않는다(에디터 스모크 테스트 등 Lobby 없이 단독 검증할 때).
    /// </param>
    public bool StartHostSteam(string roomCode = "")
    {
        if (_net == null || steamTransport == null)
        {
            Debug.LogError("[NetworkManagerSetup] steamTransport가 연결되지 않았습니다. 같은 GameObject에 FacepunchTransport를 추가하고 연결하세요.");
            return false;
        }

        if (_net.IsListening)
        {
            Debug.LogWarning("[NetworkManagerSetup] 이미 실행 중입니다. StartHostSteam 무시.");
            return true;
        }

        if (SteamManager.Instance == null || !SteamManager.Instance.EnsureInitialized())
        {
            Debug.LogError("[NetworkManagerSetup] Steam 초기화 실패 — StartHostSteam 중단.");
            return false;
        }

        _net.NetworkConfig.NetworkTransport = steamTransport;
        _net.ConnectionApprovalCallback = ApproveConnection;

        bool ok = _net.StartHost();
        if (ok)
        {
            if (!string.IsNullOrEmpty(roomCode)) RoomCode = roomCode;
            Debug.Log($"[NetworkManagerSetup] Steam Host 시작됨 — SteamId {SteamClient.SteamId}");
        }
        else
        {
            Debug.LogError("[NetworkManagerSetup] StartHostSteam() 실패");
        }
        return ok;
    }

    /// <summary>
    /// Steam Client 시작. hostId = 접속할 Lobby Owner의 SteamId (Lobby Id 아님 — §4 확정).
    /// </summary>
    public bool StartClientSteam(SteamId hostId)
    {
        if (_net == null || steamTransport == null)
        {
            Debug.LogError("[NetworkManagerSetup] steamTransport가 연결되지 않았습니다. 같은 GameObject에 FacepunchTransport를 추가하고 연결하세요.");
            return false;
        }

        if (_net.IsListening)
        {
            Debug.LogWarning("[NetworkManagerSetup] 이미 실행 중입니다. StartClientSteam 무시.");
            return true;
        }

        if (SteamManager.Instance == null || !SteamManager.Instance.EnsureInitialized())
        {
            Debug.LogError("[NetworkManagerSetup] Steam 초기화 실패 — StartClientSteam 중단.");
            return false;
        }

        _net.NetworkConfig.NetworkTransport = steamTransport;
        steamTransport.targetSteamId = hostId;

        bool ok = _net.StartClient();
        if (ok) Debug.Log($"[NetworkManagerSetup] Steam Client 시작됨 — target {hostId}");
        else    Debug.LogError("[NetworkManagerSetup] StartClientSteam() 실패");
        return ok;
    }

    /// <summary>
    /// 네트워크 종료 + 세션 전체 정리. 타이틀 복귀 시 호출.
    /// LanDiscovery 중단 → Steam Lobby 나가기(§8) → NetworkSessionData 초기화 → NGO Shutdown → 트랜스포트 로컬 기본값 복귀.
    /// </summary>
    public void Shutdown()
    {
        LanDiscovery.Instance?.Stop();
        SteamLobbyManager.Instance?.LeaveCurrentLobby(); // §8: Lobby 객체는 재사용 안 함, Host/Client 모두 나감 처리
        NetworkSessionData.Clear();
        RoomCode = string.Empty;

        if (_net != null && _net.IsListening)
        {
            _net.Shutdown();
            Debug.Log("[NetworkManagerSetup] Shutdown 완료");
        }

        if (_net != null && _transport != null)
            _net.NetworkConfig.NetworkTransport = _transport; // 다음 세션은 기본적으로 로컬 경로
    }

    // ── Connection Approval ───────────────────────────────────────

    void ApproveConnection(
        NetworkManager.ConnectionApprovalRequest  req,
        NetworkManager.ConnectionApprovalResponse resp)
    {
        int current = _net.ConnectedClients.Count;
        bool approved = current < maxConnections;

        resp.Approved          = approved;
        resp.CreatePlayerObject = false; // Player Prefab은 스테이지 진입 시 수동 스폰
        resp.Pending           = false;

        if (!approved)
            Debug.Log($"[NetworkManagerSetup] 접속 거부 — 현재 {current}명 / 최대 {maxConnections}명");
    }

    // ── 프로퍼티 ──────────────────────────────────────────────────

    public string RoomCode    { get; private set; } = string.Empty;
    public ushort Port        => port;
    public bool   IsHost      => _net != null && _net.IsHost;
    public bool   IsClient    => _net != null && _net.IsClient && !_net.IsHost;
    public bool   IsListening => _net != null && _net.IsListening;
    public bool   IsSteamPath => _net != null && steamTransport != null
                                 && _net.NetworkConfig.NetworkTransport == steamTransport;

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: Host 시작")]
    void Debug_StartHost() => StartHost(LanDiscovery.GenerateRoomCode());

    [ContextMenu("테스트: Shutdown")]
    void Debug_Shutdown() => Shutdown();

    [ContextMenu("테스트: 상태 출력")]
    void Debug_Status() =>
        Debug.Log($"[NetworkManagerSetup] IsHost={IsHost} IsClient={IsClient} IsListening={IsListening} Port={Port}");

    // ── Steam 스모크 테스트 (Lobby 없이 Transport 단독 검증) ─────────
    // 사용법: 이 PC(또는 빌드)에서 "Steam Host 시작" 실행 → 콘솔에 찍히는 SteamId를
    // 다른 Steam 계정 인스턴스의 debugTargetSteamId에 입력 → "Steam Client 시작" 실행.

    [ContextMenu("테스트: Steam Host 시작")]
    void Debug_StartHostSteam() => StartHostSteam();

    [ContextMenu("테스트: Steam Client 시작 (debugTargetSteamId 사용)")]
    void Debug_StartClientSteam()
    {
        if (debugTargetSteamId == 0)
        {
            Debug.LogWarning("[NetworkManagerSetup] debugTargetSteamId가 0입니다. Host 콘솔에서 SteamId를 복사해 입력하세요.");
            return;
        }
        StartClientSteam(debugTargetSteamId);
    }

    [ContextMenu("테스트: Steam 상태 출력")]
    void Debug_SteamStatus()
    {
        bool init = SteamManager.Instance != null && SteamManager.Instance.IsInitialized;
        string myId = init ? SteamClient.SteamId.ToString() : "미초기화 (Steam Host/Client 시작 전엔 정상)";
        Debug.Log($"[NetworkManagerSetup] IsSteamPath={IsSteamPath} MySteamId={myId} IsListening={IsListening}");
    }
#endif
}
