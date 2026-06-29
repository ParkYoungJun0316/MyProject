using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// NetworkManager + UnityTransport 초기화 래퍼.
/// NetworkManager와 같은 GameObject에 부착.
/// DontDestroyOnLoad는 NetworkManager 자체가 처리하므로 별도 처리 불필요.
///
/// [배치 방법]
/// 0.Title 씬 > NetworkManager GameObject에 이 컴포넌트 추가.
/// 같은 오브젝트에 NetworkManager + UnityTransport + NetworkManagerSetup 세 컴포넌트가 있어야 함.
///
/// [Inspector 설정]
/// - port          : 7777 (고정값, LAN MVP)
/// - NetworkManager Inspector > Max Connections : 4
/// - NetworkManager Inspector > Dont Destroy On Load : 체크
///
/// [호출 시점]
/// - OnClickCreateGame (TitleMenuController) → StartHost()
/// - OnClickJoinGame 룸코드 확인 후 (Step 2에서 연결) → StartClient(ip)
/// - 타이틀 복귀 시 (Step 8에서 연결)   → Shutdown()
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

    private NetworkManager  _net;
    private UnityTransport  _transport;

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

        _transport.SetConnectionData(address, port);

        bool ok = _net.StartClient();
        if (ok) Debug.Log($"[NetworkManagerSetup] Client 시작됨 — {address}:{port}");
        else    Debug.LogError("[NetworkManagerSetup] StartClient() 실패");
        return ok;
    }

    /// <summary>
    /// 네트워크 종료 + 세션 전체 정리. 타이틀 복귀 시 호출.
    /// LanDiscovery 중단 → NetworkSessionData 초기화 → NGO Shutdown.
    /// </summary>
    public void Shutdown()
    {
        LanDiscovery.Instance?.Stop();
        NetworkSessionData.Clear();
        RoomCode = string.Empty;

        if (_net != null && _net.IsListening)
        {
            _net.Shutdown();
            Debug.Log("[NetworkManagerSetup] Shutdown 완료");
        }
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

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: Host 시작")]
    void Debug_StartHost() => StartHost(LanDiscovery.GenerateRoomCode());

    [ContextMenu("테스트: Shutdown")]
    void Debug_Shutdown() => Shutdown();

    [ContextMenu("테스트: 상태 출력")]
    void Debug_Status() =>
        Debug.Log($"[NetworkManagerSetup] IsHost={IsHost} IsClient={IsClient} IsListening={IsListening} Port={Port}");
#endif
}
