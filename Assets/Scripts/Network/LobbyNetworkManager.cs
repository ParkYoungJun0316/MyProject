using System;
using System.Collections;
using System.Collections.Generic;
using Steamworks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 로비 씬 네트워크 매니저. NetworkBehaviour.
/// 1.Lobby 씬 안 NetworkObject GameObject에 부착.
///
/// [역할]
/// - NetworkList로 슬롯(색·Ready) 상태를 전원에 동기화
/// - 접속 순으로 슬롯 할당 (Host = Slot0)
/// - Ready·색 변경 ServerRpc
/// - Host: Kick / StartGame (SceneFlowManager.LoadNextScene 경유)
/// - GameSession.SetActiveColors() 적용 후 SceneFlowManager.sceneSequence의
///   "1.Lobby" 다음 씬으로 이동 — 어떤 씬이 첫 스테이지인지는 이 클래스가 정하지 않음
///
/// [배치]
/// 1.Lobby 씬 빈 GameObject → NetworkObject + LobbyNetworkManager 추가
///
/// [UI 갱신]
/// LobbyMenuController가 OnSlotsChanged 이벤트를 구독해 RefreshAllSlots() 호출.
/// </summary>
public class LobbyNetworkManager : NetworkBehaviour
{
    public static LobbyNetworkManager Instance { get; private set; }

    // 색상 인덱스 → PlayerColorType 매핑 (LobbyPlayerState.ColorIndex 기준)
    public static readonly PlayerColorType[] ColorOrder =
    {
        PlayerColorType.Blue,
        PlayerColorType.Purple,
        PlayerColorType.Green,
        PlayerColorType.Yellow,
    };

    /// <summary>PlayerColorType → ColorOrder 인덱스 변환. 미매칭 시 -1 반환.</summary>
    public static int ColorTypeToIndex(PlayerColorType colorType)
    {
        for (int i = 0; i < ColorOrder.Length; i++)
            if (ColorOrder[i] == colorType) return i;
        return -1;
    }

    // ColorIndex 순 기본 CheerName — GameSession.GetSessionCheerName 등 전 시스템의 단일 소스.
    public static readonly string[] DefaultCheerNames = { "berry", "guma", "sook", "hobak" };

    // 예약어 (Host 검증)
    static readonly string[] ReservedNames =
        { "cheer", "admin", "host", "server", "system", "bot", "null" };

    [Header("DontDestroyOnLoad 시스템 Prefab")]
    [Tooltip("PlayerSpawnCoordinator prefab (NetworkObject 포함).\n" +
             "게임 시작 시 Host가 destroyWithScene:false로 스폰 → 세션 내 씬 간 유지.\n" +
             "NGO의 Network Prefab List에 반드시 등록되어 있어야 함.")]
    [SerializeField] private NetworkObject coordinatorPrefab;

    // NetworkList 는 Awake 전에 초기화해야 함 (필드 초기화 or Awake)
    private readonly NetworkList<LobbyPlayerState> _slots = new();

    // SubmitDisplayNameServerRpc가 OnClientJoined보다 먼저 도착하는 레이스 대비 버퍼(Host 전용).
    // 이 프로젝트에서 Steam 릴레이 트랜스포트가 접속 이벤트를 중복·재정렬 전달하는 문제가
    // 여러 번 확인됐음(SteamworksIntegrationDesign.md 트랙5) — 슬롯이 아직 없을 때 도착한
    // DisplayName을 여기 담아뒀다가 슬롯 생성 시 바로 적용한다.
    private readonly Dictionary<ulong, FixedString64Bytes> _pendingDisplayNames = new();

    // 룸코드: Host가 설정하고 모든 클라이언트에 동기화
    private readonly NetworkVariable<FixedString32Bytes> _sharedRoomCode = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>슬롯 상태가 바뀔 때마다 발행. LobbyMenuController에서 구독해 UI 갱신.</summary>
    public event Action OnSlotsChanged;

    /// <summary>
    /// SetCheerNameServerRpc 결과 통보 (요청 Client 전용).
    /// (success, errorKey). errorKey: "" / "ready" / "format" / "taken" / "reserved"
    /// </summary>
    public event Action<bool, string> OnCheerNameResult;

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[LobbyNetworkManager][DIAG] OnNetworkSpawn — IsHost={IsHost} LocalClientId={NetworkManager.LocalClientId} " +
                  $"기존 슬롯수={_slots.Count} ConnectedClients={NetworkManager.ConnectedClients.Count}");

        _slots.OnListChanged += HandleSlotsChanged;

        if (IsHost)
        {
            NetworkManager.OnClientConnectedCallback  += OnClientJoined;
            NetworkManager.OnClientDisconnectCallback += OnClientLeft;

            // 룸코드를 NetworkVariable에 저장 → 클라이언트에 자동 동기화
            if (NetworkManagerSetup.Instance != null)
                _sharedRoomCode.Value = NetworkManagerSetup.Instance.RoomCode;

            // Host 자신을 Slot0에 추가 → OnListChanged 발행 → OnSlotsChanged 1회 발행
            _slots.Add(new LobbyPlayerState
            {
                ClientId    = NetworkManager.LocalClientId,
                ColorIndex  = 0,
                IsReady     = false,
                DisplayName = new FixedString64Bytes(GetLocalDisplayName(NetworkManager.LocalClientId)),
            });

            // [DIAG] 이 스폰 시점 이전에 이미 연결된(=구독을 놓쳤을 수 있는) Client가 있는지 확인.
            // NGO EnableSceneManagement=1이면 보통 이럴 일이 없어야 하나, 재호스트/씬 재로드 타이밍에
            // 발생 가능성 있는지 실측 확인용.
            foreach (var kv in NetworkManager.ConnectedClients)
            {
                if (kv.Key == NetworkManager.LocalClientId) continue;
                Debug.LogWarning($"[LobbyNetworkManager][DIAG] OnNetworkSpawn 시점에 이미 연결돼 있던 Client 발견 — " +
                                  $"clientId={kv.Key} (OnClientJoined 구독 전에 연결된 것으로 추정, 슬롯 누락 가능)");
            }
        }
        else
        {
            // Client: 내 연결이 끊기면(Host 이탈 등) 타이틀 복귀 — DisconnectManager(인게임)와 동일 패턴.
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnectedSelf;

            // 내 Steam 표시 이름을 Host에 보고 — 슬롯 자체는 OnClientJoined(Host)에서 이미 생성됨.
            // 단, Host의 OnClientJoined(슬롯 생성)가 이 RPC보다 늦게 처리되는 레이스가 있으면
            // (Steam 릴레이 트랜스포트 메시지 순서 비보장 — SteamworksIntegrationDesign.md 트랙5의
            // 여러 순서/중복 이슈와 같은 계열) 슬롯이 아직 없어 조용히 씹힌다 — 재시도로 보정.
            StartCoroutine(SubmitDisplayNameWithRetry());

            // Client: 서버에서 리스트가 이미 동기화됐을 수 있으므로 초기 UI 갱신
            OnSlotsChanged?.Invoke();
        }
    }

    public override void OnNetworkDespawn()
    {
        _slots.OnListChanged -= HandleSlotsChanged;

        if (IsHost)
        {
            NetworkManager.OnClientConnectedCallback  -= OnClientJoined;
            NetworkManager.OnClientDisconnectCallback -= OnClientLeft;
        }
        else
        {
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnectedSelf;
        }

        if (Instance == this) Instance = null;
    }

    // ── 접속·이탈 콜백 (Host 전용) ────────────────────────────────

    void OnClientJoined(ulong clientId)
    {
        Debug.Log($"[LobbyNetworkManager][DIAG] OnClientJoined 호출됨 — clientId={clientId}, " +
                  $"현재 슬롯수={_slots.Count}, ConnectedClients={NetworkManager.ConnectedClients.Count}");

        // Host 자신은 OnNetworkSpawn에서 이미 추가됨
        if (clientId == NetworkManager.LocalClientId)
        {
            Debug.Log("[LobbyNetworkManager][DIAG] OnClientJoined — 자기 자신(Host)이라 스킵");
            return;
        }

        // NGO가 동일 클라이언트에 대해 OnClientConnectedCallback을 중복 호출하는 경우가 있음
        // (Scene 재동기화/재승인 등 실측 확인됨) — 이미 슬롯이 있으면 중복 추가하지 않음.
        // 이 가드가 없으면 슬롯이 무한정 늘어나며 NetworkList 폭주 → 씬 동기화 충돌
        // (Server Scene Handle already exist)까지 유발하는 것으로 확인됨.
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].ClientId != clientId) continue;
            Debug.LogWarning($"[LobbyNetworkManager][DIAG] OnClientJoined — clientId={clientId}는 이미 슬롯[{i}]에 존재. 중복 호출로 판단해 무시.");
            return;
        }

        var newSlot = new LobbyPlayerState
        {
            ClientId   = clientId,
            ColorIndex = GetNextFreeColorIndex(),
            IsReady    = false,
        };

        // SubmitDisplayNameServerRpc가 이 슬롯 생성보다 먼저 도착해 버퍼링돼 있었다면 바로 적용.
        if (_pendingDisplayNames.TryGetValue(clientId, out var pendingName))
        {
            newSlot.DisplayName = pendingName;
            _pendingDisplayNames.Remove(clientId);
            Debug.Log($"[LobbyNetworkManager][DIAG] OnClientJoined — 버퍼링된 DisplayName 즉시 적용 clientId={clientId}");
        }

        _slots.Add(newSlot);

        Debug.Log($"[LobbyNetworkManager][DIAG] OnClientJoined — 슬롯 추가 완료. 새 슬롯수={_slots.Count}");
    }

    void OnClientLeft(ulong clientId)
    {
        Debug.Log($"[LobbyNetworkManager][DIAG] OnClientLeft 호출됨 — clientId={clientId}, 현재 슬롯수={_slots.Count}");

        // 과거 중복 추가분(위 가드 적용 전 세션 등)이 남아있을 가능성까지 고려해
        // 해당 clientId 슬롯을 전부 제거(첫 매치만 지우면 유령 슬롯이 남을 수 있음).
        bool removedAny = false;
        for (int i = _slots.Count - 1; i >= 0; i--)
        {
            if (_slots[i].ClientId != clientId) continue;
            _slots.RemoveAt(i);
            removedAny = true;
            Debug.Log($"[LobbyNetworkManager][DIAG] OnClientLeft — 슬롯[{i}] 제거 완료. 새 슬롯수={_slots.Count}");
        }
        _pendingDisplayNames.Remove(clientId);

        if (!removedAny)
            Debug.LogWarning($"[LobbyNetworkManager][DIAG] OnClientLeft — clientId={clientId}에 해당하는 슬롯을 못 찾음(이미 없음)");
    }

    // ── 이탈 감지 (Client 전용) ───────────────────────────────────

    /// <summary>
    /// Client: 내 연결이 끊기면 타이틀 복귀. Host가 로비를 나가면(=서버 종료) 발생.
    /// DisconnectManager.OnClientLeft(인게임 버전)와 동일한 isSelf 판정 패턴.
    /// NotifyHostQuitClientRpc가 먼저 도착해 TitleReturnFlow가 이미 처리 중이면
    /// Request()의 _isReturning 가드가 중복 호출을 무시한다.
    /// </summary>
    void OnClientDisconnectedSelf(ulong clientId)
    {
        bool isSelf = clientId == NetworkManager.LocalClientId || !NetworkManager.IsListening;
        Debug.Log($"[LobbyNetworkManager][DIAG] OnClientDisconnectedSelf 호출됨 — clientId={clientId}, " +
                  $"LocalClientId={NetworkManager.LocalClientId}, IsListening={NetworkManager.IsListening}, isSelf={isSelf}");
        if (!isSelf) return;

        Debug.Log("[LobbyNetworkManager] 연결 끊김 — 타이틀 복귀");
        TitleReturnFlow.Instance?.Request(new TitleReturnOptions
        {
            Reason = TitleReturnReason.ClientDisconnected,
            Scope  = TitleReturnScope.SessionOnly,
        });
    }

    /// <summary>
    /// Host가 로비에서 "나가기" 버튼을 눌렀을 때 LobbyMenuController.OnClickQuit()에서 호출.
    /// 남은 Client 전원에게 즉시 타이틀 복귀를 알린다 — DisconnectManager(인게임)의
    /// NotifyAllReturnClientRpc와 동일 패턴. Shutdown()으로 연결이 끊기기 전에 먼저 보내야 한다.
    /// </summary>
    public void NotifyHostQuit()
    {
        Debug.Log($"[LobbyNetworkManager][DIAG] NotifyHostQuit 호출됨 — IsHost={IsHost}, ConnectedClients={NetworkManager.ConnectedClients.Count}");
        if (!IsHost) return;
        NotifyHostQuitClientRpc();
    }

    [ClientRpc]
    void NotifyHostQuitClientRpc()
    {
        Debug.Log($"[LobbyNetworkManager][DIAG] NotifyHostQuitClientRpc 수신됨 — IsHost={IsHost}");
        if (IsHost) return;
        TitleReturnFlow.Instance?.Request(new TitleReturnOptions
        {
            Reason = TitleReturnReason.HostQuitRoom,
            Scope  = TitleReturnScope.SessionOnly,
        });
    }

    // ── ServerRpc ─────────────────────────────────────────────────

    /// <summary>Ready 상태 설정. 모든 클라이언트에서 호출 가능.</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetReadyServerRpc(bool isReady, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].ClientId != sender) continue;
            var s = _slots[i];
            s.IsReady = isReady;
            _slots[i] = s;
            return;
        }
    }

    /// <summary>
    /// 색 선택. Ready 상태거나 타인이 이미 선택한 색이면 무시 (서버 측 강제).
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetColorServerRpc(int colorIndex, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        // 중복 색 선택 허용 — CanStart()에서 중복 체크로 Start만 비활성화

        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].ClientId != sender) continue;
            var s = _slots[i];
            if (s.IsReady) return; // Ready 후 변경 불가
            s.ColorIndex = colorIndex;
            _slots[i] = s;
            return;
        }
    }

    /// <summary>
    /// CheerName 설정 요청.
    /// - 빈 문자열 → 커스텀 해제 (기본값 취급).
    /// - 비어 있지 않으면 Host가 형식·예약어·중복을 검증 후 반영 또는 거절.
    /// - Ready 중에는 변경 불가.
    /// 결과는 SetCheerNameResultClientRpc 로 요청 Client에만 통보.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetCheerNameServerRpc(FixedString32Bytes name, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;

        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].ClientId != sender) continue;
            var s = _slots[i];

            if (s.IsReady) { SendCheerNameResult(sender, false, "ready"); return; }

            string lower = name.ToString().Trim().ToLower();

            // 빈 문자열 = 커스텀 해제
            if (lower.Length == 0)
            {
                s.CheerName = default;
                _slots[i] = s;
                SendCheerNameResult(sender, true, "");
                return;
            }

            // 형식 검사
            if (!IsValidCheerNameFormat(lower, out string reason))
            {
                SendCheerNameResult(sender, false, reason);
                return;
            }

            // 중복 검사 — 해석 후 유일 (빈 슬롯의 유효 이름 포함)
            for (int j = 0; j < _slots.Count; j++)
            {
                if (j == i) continue;
                if (GetEffectiveCheerName(_slots[j]) == lower)
                {
                    SendCheerNameResult(sender, false, "taken");
                    return;
                }
            }

            s.CheerName = new FixedString32Bytes(lower);
            _slots[i] = s;
            SendCheerNameResult(sender, true, "");
            return;
        }
    }

    /// <summary>
    /// 자기 Steam 표시 이름을 Host에 보고. Client가 OnNetworkSpawn에서 자동 호출.
    /// Host 자신은 슬롯 생성 시 직접 설정하므로 호출하지 않음.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SubmitDisplayNameServerRpc(FixedString64Bytes displayName, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].ClientId != sender) continue;
            var s = _slots[i];
            s.DisplayName = displayName;
            _slots[i] = s;
            return;
        }

        // 슬롯이 아직 없음(OnClientJoined보다 먼저 도착한 레이스) — 버퍼링해두면
        // OnClientJoined가 슬롯을 만들 때 바로 적용됨.
        Debug.LogWarning($"[LobbyNetworkManager][DIAG] SubmitDisplayNameServerRpc — clientId={sender} 슬롯 아직 없음. 버퍼링.");
        _pendingDisplayNames[sender] = displayName;
    }

    /// <summary>
    /// 로컬 Steam 표시 이름. Steam 초기화 안 됐으면(로컬 개발 경로) "PlayerN" 폴백.
    /// </summary>
    static string GetLocalDisplayName(ulong clientId)
    {
        if (SteamManager.Instance != null && SteamManager.Instance.IsInitialized)
            return SteamClient.Name;
        return $"Player{clientId}";
    }

    /// <summary>
    /// Client 전용. SubmitDisplayNameServerRpc가 Host의 슬롯 생성(OnClientJoined)보다
    /// 먼저 도착하면 대상 슬롯이 없어 조용히 무시되던 문제 — 최대 5회(1초 간격) 재전송하며,
    /// 내 슬롯의 DisplayName이 채워진 게 확인되면 즉시 중단한다.
    /// 1차 방어(이 재시도)가 5회 안에 확인 못 해도, 2차 방어로 Host의 _pendingDisplayNames
    /// 버퍼가 시간 제한 없이 슬롯 생성 시점에 적용해주므로 실질적으로 유실되지 않는다.
    /// </summary>
    IEnumerator SubmitDisplayNameWithRetry()
    {
        var myName = new FixedString64Bytes(GetLocalDisplayName(NetworkManager.LocalClientId));

        for (int attempt = 0; attempt < 5; attempt++)
        {
            SubmitDisplayNameServerRpc(myName);
            yield return new WaitForSeconds(1f);

            if (IsLocalDisplayNameConfirmed())
            {
                Debug.Log($"[LobbyNetworkManager] DisplayName 반영 확인됨 (시도 {attempt + 1}회).");
                yield break;
            }
        }

        Debug.LogWarning("[LobbyNetworkManager] SubmitDisplayNameServerRpc 5회 재시도 후에도 반영 확인 안 됨 — 슬롯 자체가 없는 상태일 수 있음.");
    }

    bool IsLocalDisplayNameConfirmed()
    {
        ulong localId = NetworkManager.LocalClientId;
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].ClientId != localId) continue;
            return _slots[i].DisplayName.Length > 0;
        }
        return false;
    }

    void SendCheerNameResult(ulong targetClientId, bool success, string errorKey)
    {
        SetCheerNameResultClientRpc(
            success,
            new FixedString32Bytes(errorKey),
            new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { targetClientId } }
            });
    }

    /// <summary>CheerName 설정 결과를 요청 Client에만 전송.</summary>
    [ClientRpc]
    void SetCheerNameResultClientRpc(bool success, FixedString32Bytes errorKey,
                                     ClientRpcParams rpcParams = default)
    {
        OnCheerNameResult?.Invoke(success, errorKey.ToString());
    }

    // ── CheerName 유틸 ────────────────────────────────────────────

    /// <summary>
    /// 슬롯의 유효 CheerName 반환.
    /// CheerName 이 빈 문자열이면 현재 ColorIndex 기본값 반환.
    /// </summary>
    public static string GetEffectiveCheerName(LobbyPlayerState s)
    {
        string custom = s.CheerName.ToString();
        if (string.IsNullOrEmpty(custom))
        {
            int ci = Mathf.Clamp(s.ColorIndex, 0, DefaultCheerNames.Length - 1);
            return DefaultCheerNames[ci];
        }
        return custom;
    }

    static bool IsValidCheerNameFormat(string lower, out string reason)
    {
        reason = "";
        if (lower.Length < 2 || lower.Length > 12) { reason = "format"; return false; }
        foreach (char c in lower)
        {
            if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
            { reason = "format"; return false; }
        }
        foreach (string reserved in ReservedNames)
            if (lower == reserved) { reason = "reserved"; return false; }
        return true;
    }

    /// <summary>Kick. 호스트만 호출. 즉시 슬롯 비움.</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void KickPlayerServerRpc(ulong targetClientId, RpcParams rpcParams = default)
    {
        // 보안: 실제 Host client Id만 허용
        if (rpcParams.Receive.SenderClientId != NetworkManager.LocalClientId) return;
        NetworkManager.DisconnectClient(targetClientId);
    }

    /// <summary>
    /// 게임 시작. Host만 호출.
    /// 조건: 전원 Ready + 색 중복 없음.
    /// GameSession에 활성 색 적용 후 SceneFlowManager.LoadNextScene()으로 전환.
    /// 어떤 씬이 로드되는지는 SceneFlowManager.sceneSequence(Inspector, 0.Title에 배치) 순서를 따른다 —
    /// 여기서 씬 이름을 직접 정하지 않는다 (단일 SSOT: SceneFlowManager).
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void StartGameServerRpc(RpcParams rpcParams = default)
    {
        if (!IsHost) return;
        if (!CanStart())
        {
            Debug.Log($"[LobbyNetworkManager] StartGame 거부 — 슬롯 수:{_slots.Count} / " +
                      "전원 Ready 또는 색 중복 조건 미충족");
            return;
        }

        // ── clientId → color 매핑 확정 ──────────────────────────────────
        var clientColorDict = new Dictionary<ulong, PlayerColorType>(_slots.Count);
        var colorList       = new PlayerColorType[_slots.Count];
        for (int i = 0; i < _slots.Count; i++)
        {
            ulong           id    = _slots[i].ClientId;
            PlayerColorType color = ColorOrder[_slots[i].ColorIndex];
            clientColorDict[id] = color;
            colorList[i]        = color;
        }

        // ── PlayerSpawnCoordinator 스폰 (DontDestroyOnLoad NetworkObject) ──
        // PrepareColors → OnNetworkSpawn에서 NetworkList 채움 → 전 클라이언트 동기화
        if (coordinatorPrefab != null)
        {
            var coordGo     = Instantiate(coordinatorPrefab.gameObject);
            var coordinator = coordGo.GetComponent<PlayerSpawnCoordinator>();
            coordinator.PrepareColors(clientColorDict);
            coordGo.GetComponent<NetworkObject>().Spawn(destroyWithScene: false);
        }
        else
        {
            Debug.LogError("[LobbyNetworkManager] coordinatorPrefab 미설정 — Inspector에서 연결 필요");
        }

        // ── PlayerSpawnManager 엔트리 1회 확정 ──────────────────────────
        // 이후 씬 전환·사망 리로드에서 PSM은 외부 조회 없이 _entries만 사용
        if (PlayerSpawnManager.Instance == null)
        {
            Debug.LogError("[LobbyNetworkManager] PlayerSpawnManager.Instance null — " +
                           "0.Title에 PlayerSpawnManager가 없거나 DontDestroyOnLoad 실패. 게임 시작 중단.");
            return;
        }
        PlayerSpawnManager.Instance.InitializeOnline(clientColorDict);

        // ── 세션 시드 ────────────────────────────────────────────────────
        int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        NetworkSessionData.Seed = seed;
        BroadcastSeedClientRpc(seed);

        // ── 세션 시작 서버 시각 (TimerUI가 Host/Client 공통 경과 시간 계산에 사용) ──────
        double sessionStart = NetworkManager.ServerTime.Time;
        NetworkSessionData.SessionStartServerTime = sessionStart;
        BroadcastSessionStartClientRpc(sessionStart);

        // ── GameSession 활성 색 적용 (Host 로컬) ─────────────────────────
        // Client는 OnPlayersReady 이후 PlayerSpawnCoordinator에서 확정값을 읽음
        GameSession.Instance?.SetActiveColors(colorList);

        // ── 세션 CheerName 확정 후 전원에 배포 ──────────────────────────
        var sessionNames = new FixedString32Bytes[4];
        for (int i = 0; i < 4; i++) sessionNames[i] = new FixedString32Bytes(DefaultCheerNames[i]);
        for (int i = 0; i < _slots.Count; i++)
        {
            int ci = _slots[i].ColorIndex;
            if (ci >= 0 && ci < 4)
                sessionNames[ci] = new FixedString32Bytes(GetEffectiveCheerName(_slots[i]));
        }
        // Host 로컬 적용
        var namesForSession = new string[4];
        for (int i = 0; i < 4; i++) namesForSession[i] = sessionNames[i].ToString();
        GameSession.Instance?.SetSessionCheerNames(namesForSession);
        // Client에 배포
        SyncCheerNamesClientRpc(sessionNames[0], sessionNames[1], sessionNames[2], sessionNames[3]);

        // ── 세션 Steam 표시 이름 확정 후 전원에 배포 ──────────────────────
        // (캐릭터 머리 위 이름표는 CheerName만 쓰지만, TeamStatusUI 코너 패널은 이 값을 씀)
        var sessionDisplayNames = new FixedString64Bytes[4];
        for (int i = 0; i < 4; i++) sessionDisplayNames[i] = new FixedString64Bytes($"Player{i}");
        for (int i = 0; i < _slots.Count; i++)
        {
            int ci = _slots[i].ColorIndex;
            if (ci >= 0 && ci < 4)
                sessionDisplayNames[ci] = _slots[i].DisplayName;
        }
        // Host 로컬 적용
        var displayNamesForSession = new string[4];
        for (int i = 0; i < 4; i++) displayNamesForSession[i] = sessionDisplayNames[i].ToString();
        GameSession.Instance?.SetSessionDisplayNames(displayNamesForSession);
        // Client에 배포
        SyncDisplayNamesClientRpc(sessionDisplayNames[0], sessionDisplayNames[1], sessionDisplayNames[2], sessionDisplayNames[3]);

        // 씬 전환은 SceneFlowManager 단일 SSOT로 위임 — sceneSequence 상 "1.Lobby" 다음 항목이 로드된다.
        if (SceneFlowManager.Instance == null)
        {
            Debug.LogError("[LobbyNetworkManager] SceneFlowManager.Instance null — " +
                           "0.Title에 SceneFlowManager가 없거나 DontDestroyOnLoad 실패. 게임 시작 중단.");
            return;
        }
        SceneFlowManager.Instance.LoadNextScene();
    }

    // ── 공개 읽기 API ─────────────────────────────────────────────

    public int    SlotCount    => _slots?.Count ?? 0;

    /// <summary>모든 클라이언트에서 동일한 룸코드 (NetworkVariable 동기화).</summary>
    public string SharedRoomCode => _sharedRoomCode.Value.ToString();

    /// <summary>호스트 clientId. _slots[0]이 항상 호스트.</summary>
    public ulong  HostClientId  => _slots != null && _slots.Count > 0
        ? _slots[0].ClientId
        : ulong.MaxValue;

    public LobbyPlayerState GetSlot(int i) =>
        (_slots == null || i < 0 || i >= _slots.Count) ? LobbyPlayerState.Empty : _slots[i];

    /// <summary>현재 슬롯 중 중복 색이 하나라도 있으면 true.</summary>
    public bool HasDuplicateColors()
    {
        if (_slots == null) return false;
        var used = new HashSet<int>();
        foreach (var s in _slots)
            if (!used.Add(s.ColorIndex)) return true;
        return false;
    }

    /// <summary>
    /// Start 버튼 활성 조건.
    /// 호스트 슬롯은 Ready 불필요. 나머지 클라이언트 전원 Ready + 색 중복 없음.
    /// 호스트 1인(팀원 없음)이면 즉시 true.
    /// </summary>
    public bool CanStart()
    {
        if (_slots == null || _slots.Count == 0) return false;

        ulong hostId = HostClientId;

        var usedColors = new HashSet<int>();
        var usedNames  = new HashSet<string>();
        foreach (var s in _slots)
        {
            if (!usedColors.Add(s.ColorIndex)) return false; // 색 중복 (호스트 포함)
            if (!usedNames.Add(GetEffectiveCheerName(s))) return false; // 해석 후 이름 유일

            if (s.ClientId == hostId) continue; // 호스트는 Ready 체크 제외
            if (!s.IsReady) return false;
        }
        return true;
    }

    // ── 내부 ──────────────────────────────────────────────────────

    /// <summary>시드를 모든 클라이언트에 배포. StartGameServerRpc 에서 호출.</summary>
    [ClientRpc]
    void BroadcastSeedClientRpc(int seed)
    {
        NetworkSessionData.Seed = seed;
        Debug.Log($"[LobbyNetworkManager] 세션 시드 수신: {seed}");
    }

    /// <summary>세션 시작 서버 시각을 모든 클라이언트에 배포. StartGameServerRpc 에서 호출.</summary>
    [ClientRpc]
    void BroadcastSessionStartClientRpc(double serverTime)
    {
        NetworkSessionData.SessionStartServerTime = serverTime;
    }

    /// <summary>
    /// 세션 CheerName을 Client에 동기화.
    /// 4색(Blue/Purple/Green/Yellow) 순서 고정 — colorIndex 0~3.
    /// </summary>
    [ClientRpc]
    void SyncCheerNamesClientRpc(
        FixedString32Bytes n0, FixedString32Bytes n1,
        FixedString32Bytes n2, FixedString32Bytes n3)
    {
        if (IsHost) return; // Host는 이미 적용됨
        var names = new[] { n0.ToString(), n1.ToString(), n2.ToString(), n3.ToString() };
        GameSession.Instance?.SetSessionCheerNames(names);
        Debug.Log($"[LobbyNetworkManager] Client 세션 CheerName 수신: {string.Join(", ", names)}");
    }

    /// <summary>
    /// 세션 Steam 표시 이름을 Client에 동기화.
    /// 4색(Blue/Purple/Green/Yellow) 순서 고정 — colorIndex 0~3.
    /// </summary>
    [ClientRpc]
    void SyncDisplayNamesClientRpc(
        FixedString64Bytes n0, FixedString64Bytes n1,
        FixedString64Bytes n2, FixedString64Bytes n3)
    {
        if (IsHost) return; // Host는 이미 적용됨
        var names = new[] { n0.ToString(), n1.ToString(), n2.ToString(), n3.ToString() };
        GameSession.Instance?.SetSessionDisplayNames(names);
        Debug.Log($"[LobbyNetworkManager] Client 세션 표시 이름 수신: {string.Join(", ", names)}");
    }

    // ── 로비 Heard 공유 ────────────────────────────────────────────

    /// <summary>로비 Heard 이벤트를 전원에게 브로드캐스트할 때 발행.</summary>
    public event Action<int, int> OnLobbyHeardBroadcast; // (targetColorIndex, speakerColorIndex)

    /// <summary>
    /// 로비 Vosk 감지 → Host에 보고.
    /// Host: speaker 슬롯·target 슬롯 확인 후 전원에 BroadcastLobbyHeardClientRpc.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ReportLobbyHeardServerRpc(int targetColorIndex, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        int speakerColor = -1;
        bool targetOccupied = false;

        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].ClientId == sender)        speakerColor   = _slots[i].ColorIndex;
            if (_slots[i].ColorIndex == targetColorIndex && _slots[i].IsOccupied) targetOccupied = true;
        }

        if (speakerColor < 0 || !targetOccupied) return;
        BroadcastLobbyHeardClientRpc(targetColorIndex, speakerColor);
    }

    [ClientRpc]
    void BroadcastLobbyHeardClientRpc(int targetColorIndex, int speakerColorIndex)
    {
        OnLobbyHeardBroadcast?.Invoke(targetColorIndex, speakerColorIndex);
    }

    void HandleSlotsChanged(NetworkListEvent<LobbyPlayerState> ev)
    {
        Debug.Log($"[LobbyNetworkManager][DIAG] HandleSlotsChanged — type={ev.Type}, 총 슬롯수={_slots.Count}, " +
                  $"구독자수(OnSlotsChanged)={OnSlotsChanged?.GetInvocationList().Length ?? 0}");
        OnSlotsChanged?.Invoke();
    }

    int GetNextFreeColorIndex()
    {
        var used = new HashSet<int>();
        foreach (var s in _slots) used.Add(s.ColorIndex);
        for (int i = 0; i < 4; i++)
            if (!used.Contains(i)) return i;
        return 0;
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 슬롯 상태 출력")]
    void Debug_PrintSlots()
    {
        if (_slots == null) { Debug.Log("[LobbyNetworkManager] _slots=null"); return; }
        Debug.Log($"[LobbyNetworkManager] 슬롯 수: {_slots.Count} / CanStart: {CanStart()}");
        for (int i = 0; i < _slots.Count; i++)
        {
            var s = _slots[i];
            Debug.Log($"  [{i}] clientId={s.ClientId} color={ColorOrder[s.ColorIndex]} " +
                      $"name={GetEffectiveCheerName(s)} (custom={s.CheerName}) ready={s.IsReady}");
        }
    }
#endif
}
