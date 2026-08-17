using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;

namespace Netcode.Transports.Facepunch
{
    using SocketConnection = Connection;

    public class FacepunchTransport : NetworkTransport, IConnectionManager, ISocketManager
    {
        private ConnectionManager connectionManager;
        private SocketManager socketManager;
        private Dictionary<ulong, Client> connectedClients;
        private bool m_SteamInitialized;

        [Space]
        [Tooltip("The Steam App ID of your game. Technically you're not allowed to use 480, but Valve doesn't do anything about it so it's fine for testing purposes.")]
        [SerializeField] private uint steamAppId = 480;

        [Tooltip("The Steam ID of the user targeted when joining as a client.")]
        [SerializeField] public ulong targetSteamId;

        // NOTE(project workaround, 2026-08-07): 릴레이 소켓 virtual port. 기본값 0 고정 대신
        // 세션(Host 시작)마다 다른 값을 쓰도록 NetworkManagerSetup이 매번 갱신한다.
        // 이유: SteamNetworkingSockets.CreateRelaySocket(0)을 같은 프로세스에서 두 번째로 호출하면
        // "ArgumentException: Invalid Socket"으로 항상 실패하는 버그가 있음(Steam 릴레이 레이어의
        // 소켓 재사용 제한으로 추정 — SteamworksIntegrationDesign.md 이슈 D). virtual port를 매번
        // 새로 발급하면 이 재사용 자체를 피해서 우회 가능. Client는 접속 대상 Host가 실제로 쓴 값을
        // 알아야 하므로 Steam Lobby 데이터로 동기화한다(SteamLobbyManager).
        public int virtualPort;

        [Header("Info")]
        [ReadOnly]
        [Tooltip("When in play mode, this will display your Steam ID.")]
        [SerializeField] private ulong userSteamId;

        private LogLevel LogLevel => NetworkManager.Singleton.LogLevel;

        private class Client
        {
            public SteamId steamId;
            public SocketConnection connection;
        }

        #region NetworkTransport Overrides

        protected override void OnEarlyUpdate()
        {
            SteamClient.RunCallbacks();

            if (!m_SteamInitialized && SteamClient.IsValid)
            {
                m_SteamInitialized = true;
                SteamNetworkingUtils.InitRelayNetworkAccess();

                if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Initialized access to Steam Relay Network.");

                userSteamId = SteamClient.SteamId;

                if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Fetched user Steam ID.");
            }
        }

        public override ulong ServerClientId => 0;

        public override void DisconnectLocalClient()
        {
            connectionManager?.Connection.Close();

            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Disconnecting local client.");
        }

        public override void DisconnectRemoteClient(ulong clientId)
        {
            if (connectedClients.TryGetValue(clientId, out Client user))
            {
                // Flush any pending messages before closing the connection
                user.connection.Flush();
                user.connection.Close();
                connectedClients.Remove(clientId);

                if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Disconnecting remote client with ID {clientId}.");
            }
            else if (LogLevel <= LogLevel.Normal)
                Debug.LogWarning($"[{nameof(FacepunchTransport)}] - Failed to disconnect remote client with ID {clientId}, client not connected.");
        }

        public override unsafe ulong GetCurrentRtt(ulong clientId)
        {
            return 0;
        }

        public override void Initialize(NetworkManager networkManager = null)
        {
            connectedClients = new Dictionary<ulong, Client>();
            m_ClientConnectedOnce = false; // 진단용 중복 Connect 가드 — 세션마다 리셋

            try
            {
                SteamClient.Init(steamAppId, false);
            }
            catch (Exception e)
            {
                if (LogLevel <= LogLevel.Error)
                    Debug.LogError($"[{nameof(FacepunchTransport)}] - Caught an exeption during initialization of Steam client: {e}");
            }
        }

        private SendType NetworkDeliveryToSendType(NetworkDelivery delivery)
        {
            return delivery switch
            {
                NetworkDelivery.Reliable => SendType.Reliable,
                NetworkDelivery.ReliableFragmentedSequenced => SendType.Reliable,
                NetworkDelivery.ReliableSequenced => SendType.Reliable,
                NetworkDelivery.Unreliable => SendType.Unreliable,
                NetworkDelivery.UnreliableSequenced => SendType.Unreliable,
                _ => SendType.Reliable
            };
        }

        public override void Shutdown()
        {
            try
            {
                if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Shutting down.");

                connectionManager?.Close();
                socketManager?.Close();
                connectionManager = null;
                socketManager = null;
                // NOTE(project workaround, 2026-08-07): 원래 여기서 SteamClient.Shutdown()을 호출했으나,
                // 이 Transport는 Steam 클라이언트 전체를 소유하지 않는다(SteamManager가 앱 전체 Init/Shutdown을
                // 전담 — SteamworksIntegrationDesign.md §5). 이 줄이 있으면 NGO Shutdown() 한 번마다 전체
                // Steam 세션이 죽어버려서, 이후 StartHost()의 CreateRelaySocket()이 항상
                // "ArgumentException: Invalid Socket"으로 실패하는 버그가 있었음(같은 프로세스에서 재호스트 불가).
                // 소켓/커넥션만 닫고 SteamClient 자체는 살려둔다.
            }
            catch (Exception e)
            {
                if (LogLevel <= LogLevel.Error)
                    Debug.LogError($"[{nameof(FacepunchTransport)}] - Caught an exception while shutting down: {e}");
            }
        }

        public override void Send(ulong clientId, ArraySegment<byte> data, NetworkDelivery delivery)
        {
	        var sendType = NetworkDeliveryToSendType(delivery);

	        if (clientId == ServerClientId)
		        connectionManager.Connection.SendMessage(data.Array, data.Offset, data.Count, sendType);
	        else if (connectedClients.TryGetValue(clientId, out Client user))
		        user.connection.SendMessage(data.Array, data.Offset, data.Count, sendType);
	        else if (LogLevel <= LogLevel.Normal)
		        Debug.LogWarning($"[{nameof(FacepunchTransport)}] - Failed to send packet to remote client with ID {clientId}, client not connected.");
        }

        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            connectionManager?.Receive();
            socketManager?.Receive();

            clientId = 0;
            receiveTime = Time.realtimeSinceStartup;
            payload = default;
            return NetworkEvent.Nothing;
        }

        public override bool StartClient()
        {
            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Starting as client.");

            connectionManager = SteamNetworkingSockets.ConnectRelay<ConnectionManager>(targetSteamId, virtualPort);
            connectionManager.Interface = this;
            return true;
        }

        public override bool StartServer()
        {
            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Starting as server.");

            socketManager = SteamNetworkingSockets.CreateRelaySocket<SocketManager>(virtualPort);
            socketManager.Interface = this;
            return true;
        }

        #endregion

        #region ConnectionManager Implementation

        private byte[] payloadCache = new byte[4096];

        private void EnsurePayloadCapacity(int size)
        {
            if (payloadCache.Length >= size)
                return;

            payloadCache = new byte[Math.Max(payloadCache.Length * 2, size)];
        }

        void IConnectionManager.OnConnecting(ConnectionInfo info)
        {
            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Connecting with Steam user {info.Identity.SteamId}.");
        }

        // NOTE(project workaround, 2026-08-07, 진단 로그+가드): ISocketManager.OnConnected(Host쪽)에는
        // 원래부터 중복 호출 가드(connectedClients.ContainsKey)가 있었는데, 이 IConnectionManager.OnConnected
        // (Client쪽)에는 없었음. 실 Steam Relay 테스트에서 "Client received a transport connection event
        // after already connecting!" 경고가 실제로 재현돼서(SteamworksIntegrationDesign.md 트랙5),
        // 같은 연결에 대해 이 콜백이 두 번 불릴 가능성이 있는 것으로 확인 — 원인(SDK 자체 재발행 vs 다른 경로)은
        // 미확정이라 우선 중복 무시 가드 + 진단 로그만 추가.
        private bool m_ClientConnectedOnce;

        void IConnectionManager.OnConnected(ConnectionInfo info)
        {
            if (m_ClientConnectedOnce)
            {
                if (LogLevel <= LogLevel.Normal)
                    Debug.LogWarning($"[{nameof(FacepunchTransport)}][DIAG] - OnConnected가 이미 연결된 상태에서 다시 호출됨(중복) — Steam user {info.Identity.SteamId}. 무시함.");
                return;
            }
            m_ClientConnectedOnce = true;

            InvokeOnTransportEvent(NetworkEvent.Connect, ServerClientId, default, Time.realtimeSinceStartup);

            if (LogLevel <= LogLevel.Normal)
                Debug.Log($"[{nameof(FacepunchTransport)}][DIAG] - Connected with Steam user {info.Identity.SteamId}.");
        }

        void IConnectionManager.OnDisconnected(ConnectionInfo info)
        {
            InvokeOnTransportEvent(NetworkEvent.Disconnect, ServerClientId, default, Time.realtimeSinceStartup);

            if (LogLevel <= LogLevel.Developer)
                Debug.Log($"[{nameof(FacepunchTransport)}] - Disconnected Steam user {info.Identity.SteamId}.");
        }

        unsafe void IConnectionManager.OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            EnsurePayloadCapacity(size);

            fixed (byte* payload = payloadCache)
            {
                UnsafeUtility.MemCpy(payload, (byte*)data, size);
            }

            InvokeOnTransportEvent(NetworkEvent.Data, ServerClientId, new ArraySegment<byte>(payloadCache, 0, size), Time.realtimeSinceStartup);
        }

        #endregion

        #region SocketManager Implementation

        void ISocketManager.OnConnecting(SocketConnection connection, ConnectionInfo info)
        {
            if (LogLevel <= LogLevel.Normal)
                Debug.Log($"[{nameof(FacepunchTransport)}][DIAG] - Accepting connection from Steam user {info.Identity.SteamId} (connection.Id={connection.Id}).");

            connection.Accept();
        }

        void ISocketManager.OnConnected(SocketConnection connection, ConnectionInfo info)
        {
            if (!connectedClients.ContainsKey(connection.Id))
            {
                connectedClients.Add(connection.Id, new Client()
                {
                    connection = connection,
                    steamId = info.Identity.SteamId
                });

                InvokeOnTransportEvent(NetworkEvent.Connect, connection.Id, default, Time.realtimeSinceStartup);

                if (LogLevel <= LogLevel.Normal)
                    Debug.Log($"[{nameof(FacepunchTransport)}][DIAG] - Connected with Steam user {info.Identity.SteamId} (connection.Id={connection.Id}).");
            }
            else if (LogLevel <= LogLevel.Normal)
                Debug.LogWarning($"[{nameof(FacepunchTransport)}][DIAG] - Failed to connect client with ID {connection.Id}, client already connected(중복 Connect 확인됨).");
        }

        void ISocketManager.OnDisconnected(SocketConnection connection, ConnectionInfo info)
        {
            if (connectedClients.Remove(connection.Id))
	    {
	        InvokeOnTransportEvent(NetworkEvent.Disconnect, connection.Id, default, Time.realtimeSinceStartup);

	       if (LogLevel <= LogLevel.Developer)
                    Debug.Log($"[{nameof(FacepunchTransport)}] - Disconnected Steam user {info.Identity.SteamId}");
	    }
     	    else if (LogLevel <= LogLevel.Normal)
                Debug.LogWarning($"[{nameof(FacepunchTransport)}] - Failed to diconnect client with ID {connection.Id}, client not connected.");
        }

        unsafe void ISocketManager.OnMessage(SocketConnection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            EnsurePayloadCapacity(size);

            fixed (byte* payload = payloadCache)
            {
                UnsafeUtility.MemCpy(payload, (byte*)data, size);
            }

            InvokeOnTransportEvent(NetworkEvent.Data, connection.Id, new ArraySegment<byte>(payloadCache, 0, size), Time.realtimeSinceStartup);
        }

        #endregion
    }
}
