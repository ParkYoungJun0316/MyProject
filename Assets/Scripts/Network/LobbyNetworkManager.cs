using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 로비 씬 네트워크 매니저. NetworkBehaviour.
/// 1.Lobby 씬 안 NetworkObject GameObject에 부착.
///
/// [역할]
/// - NetworkList로 슬롯(색·Ready) 상태를 전원에 동기화
/// - 접속 순으로 슬롯 할당 (Host = Slot0)
/// - Ready·색 변경 ServerRpc
/// - Host: Kick / StartGame (NetworkSceneManager.LoadScene)
/// - GameSession.SetActiveColors() 적용 후 M.Stage1 로드
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
                ClientId   = NetworkManager.LocalClientId,
                ColorIndex = 0,
                IsReady    = false,
            });
        }
        else
        {
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

        if (Instance == this) Instance = null;
    }

    // ── 접속·이탈 콜백 (Host 전용) ────────────────────────────────

    void OnClientJoined(ulong clientId)
    {
        // Host 자신은 OnNetworkSpawn에서 이미 추가됨
        if (clientId == NetworkManager.LocalClientId) return;

        _slots.Add(new LobbyPlayerState
        {
            ClientId   = clientId,
            ColorIndex = GetNextFreeColorIndex(),
            IsReady    = false,
        });
    }

    void OnClientLeft(ulong clientId)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].ClientId != clientId) continue;
            _slots.RemoveAt(i);
            return;
        }
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
    /// GameSession에 활성 색 적용 후 NetworkSceneManager로 M.Stage1 로드.
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

        NetworkManager.SceneManager.LoadScene("M.Stage1", LoadSceneMode.Single);
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

    void HandleSlotsChanged(NetworkListEvent<LobbyPlayerState> _) =>
        OnSlotsChanged?.Invoke();

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
