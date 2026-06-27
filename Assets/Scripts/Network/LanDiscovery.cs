using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// LAN UDP 브로드캐스트 기반 Host 탐색 + 6자리 룸코드 매칭.
/// NetworkManager GameObject에 함께 배치 (DontDestroyOnLoad 자동 처리됨).
///
/// [Host 흐름]
/// NetworkManagerSetup.StartHost(roomCode) → StartBroadcast(roomCode, port)
/// → 1초 간격으로 "NWDISC:{code}:{ip}:{port}" UDP 브로드캐스트 전송.
///
/// [Client 흐름]
/// TitleMenuController.OnClickConfirmJoin() → StartDiscovery(code, callback)
/// → UDP 47777 수신 대기 → 코드 일치 시 callback(ip) 호출 (메인 스레드).
///
/// [Inspector]
/// - discoveryPort  : 47777 (게임 포트 7777과 구분)
/// - broadcastInterval : 1초
/// </summary>
public class LanDiscovery : MonoBehaviour
{
    public static LanDiscovery Instance { get; private set; }

    [Header("Discovery 설정")]
    [Tooltip("브로드캐스트/수신에 사용할 UDP 포트. 게임 포트(7777)와 다른 값 사용.")]
    [SerializeField] private int discoveryPort = 47777;

    [Tooltip("Host가 브로드캐스트를 보내는 간격(초).")]
    [SerializeField] private float broadcastInterval = 1f;

    private const string MsgPrefix = "NWDISC:";

    private UdpClient  _broadcaster;
    private UdpClient  _listener;
    private Thread     _listenThread;
    private Coroutine  _broadcastCoroutine;

    private readonly ConcurrentQueue<string> _discovered = new ConcurrentQueue<string>();
    private string         _listenCode;
    private Action<string> _onFound;

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnDestroy() => Stop();

    // 메인 스레드에서 Discovery 결과를 처리 (스레드 → 큐 → Update 패턴)
    void Update()
    {
        if (_onFound == null) return;
        if (!_discovered.TryDequeue(out string ip)) return;

        Action<string> cb = _onFound;
        StopDiscovery();
        cb.Invoke(ip);
    }

    // ── Host: 브로드캐스트 ────────────────────────────────────────

    /// <summary>
    /// Host가 룸코드를 브로드캐스트하기 시작.
    /// NetworkManagerSetup.StartHost() 성공 후 자동 호출됨.
    /// </summary>
    public void StartBroadcast(string roomCode, ushort gamePort)
    {
        StopBroadcast();

        string localIp = GetLocalIP();
        string message = $"{MsgPrefix}{roomCode}:{localIp}:{gamePort}";
        _broadcastCoroutine = StartCoroutine(BroadcastLoop(message));

        Debug.Log($"[LanDiscovery] 브로드캐스트 시작 — 코드:{roomCode} / {localIp}:{gamePort}");
    }

    void StopBroadcast()
    {
        if (_broadcastCoroutine != null)
        {
            StopCoroutine(_broadcastCoroutine);
            _broadcastCoroutine = null;
        }
        try { _broadcaster?.Close(); } catch { }
        _broadcaster = null;
    }

    IEnumerator BroadcastLoop(string message)
    {
        _broadcaster = new UdpClient { EnableBroadcast = true };
        var endpoint = new IPEndPoint(IPAddress.Broadcast, discoveryPort);
        byte[] data  = Encoding.UTF8.GetBytes(message);

        while (true)
        {
            try
            {
                _broadcaster.Send(data, data.Length, endpoint);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LanDiscovery] 브로드캐스트 전송 오류: {e.Message}");
            }
            yield return new WaitForSeconds(broadcastInterval);
        }
    }

    // ── Client: Discovery ─────────────────────────────────────────

    /// <summary>
    /// Client가 룸코드와 일치하는 Host를 탐색.
    /// 발견 시 onFound(hostIp) 를 메인 스레드에서 호출.
    /// TitleMenuController.OnClickConfirmJoin()에서 호출됨.
    /// </summary>
    public void StartDiscovery(string roomCode, Action<string> onFound)
    {
        StopDiscovery();
        _listenCode = roomCode;
        _onFound    = onFound;

        try
        {
            _listener = new UdpClient(discoveryPort) { EnableBroadcast = true };
        }
        catch (Exception e)
        {
            Debug.LogError($"[LanDiscovery] UDP 소켓 바인드 실패 (포트 {discoveryPort}): {e.Message}");
            return;
        }

        _listenThread = new Thread(ListenLoop)
        {
            IsBackground = true,
            Name         = "LanDiscoveryListen"
        };
        _listenThread.Start();

        Debug.Log($"[LanDiscovery] Discovery 시작 — 코드 대기: {roomCode}");
    }

    void ListenLoop()
    {
        var anyEp = new IPEndPoint(IPAddress.Any, 0);

        while (_listener != null)
        {
            try
            {
                byte[] data = _listener.Receive(ref anyEp);
                string msg  = Encoding.UTF8.GetString(data);

                if (!msg.StartsWith(MsgPrefix)) continue;

                string[] parts = msg[MsgPrefix.Length..].Split(':');
                // parts[0]=code, parts[1]=ip, parts[2]=port
                if (parts.Length != 3) continue;

                if (parts[0] == _listenCode)
                    _discovered.Enqueue(parts[1]);
            }
            catch
            {
                // 소켓 Close() 시 예외 발생 → 루프 종료
                break;
            }
        }
    }

    void StopDiscovery()
    {
        _listenCode = null;
        _onFound    = null;

        try { _listener?.Close(); } catch { }
        _listener     = null;
        _listenThread = null;

        while (_discovered.TryDequeue(out _)) { }
    }

    // ── 공통 종료 ─────────────────────────────────────────────────

    /// <summary>브로드캐스트 + Discovery 모두 중단.</summary>
    public void Stop()
    {
        StopBroadcast();
        StopDiscovery();
    }

    // ── 유틸리티 (static) ─────────────────────────────────────────

    /// <summary>랜덤 6자리 숫자 룸코드 생성.</summary>
    public static string GenerateRoomCode() =>
        UnityEngine.Random.Range(100000, 999999).ToString();

    /// <summary>
    /// 룸코드를 마스킹 표시 형식으로 변환.
    /// "123456" → "12**56" (앞 2자리 + ** + 뒤 2자리)
    /// </summary>
    public static string FormatDisplayCode(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length != 6) return code;
        return code[..2] + "**" + code[4..];
    }

    /// <summary>
    /// 현재 기기의 LAN IP 주소를 반환.
    /// UDP 라우팅 테이블을 이용해 실제 전송 인터페이스 IP를 가져옴.
    /// </summary>
    static string GetLocalIP()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            return ((IPEndPoint)socket.LocalEndPoint).Address.ToString();
        }
        catch
        {
            return "127.0.0.1";
        }
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 상태 출력")]
    void Debug_Status()
    {
        Debug.Log($"[LanDiscovery] " +
                  $"Broadcasting={_broadcastCoroutine != null} " +
                  $"Listening={_listener != null} " +
                  $"Port={discoveryPort}");
    }

    [ContextMenu("테스트: 룸코드 생성 출력")]
    void Debug_GenCode()
    {
        string code = GenerateRoomCode();
        Debug.Log($"[LanDiscovery] 생성된 코드: {code} → 표시: {FormatDisplayCode(code)}");
    }

    [ContextMenu("테스트: Stop")]
    void Debug_Stop() => Stop();
#endif
}
