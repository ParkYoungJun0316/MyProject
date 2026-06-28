using System;
using System.Collections.Generic;
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

    // NetworkList 는 Awake 전에 초기화해야 함 (필드 초기화 or Awake)
    private readonly NetworkList<LobbyPlayerState> _slots = new();

    /// <summary>슬롯 상태가 바뀔 때마다 발행. LobbyMenuController에서 구독해 UI 갱신.</summary>
    public event Action OnSlotsChanged;

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

            // Host 자신을 Slot0에 추가
            _slots.Add(new LobbyPlayerState
            {
                ClientId   = NetworkManager.LocalClientId,
                ColorIndex = 0,
                IsReady    = false,
            });
        }

        OnSlotsChanged?.Invoke();
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

        if (IsColorTaken(colorIndex, sender)) return;

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

        // clientId → color 매핑 저장 (PlayerSpawnManager에서 사용)
        NetworkSessionData.ClientColors.Clear();
        var colorList = new PlayerColorType[_slots.Count];
        for (int i = 0; i < _slots.Count; i++)
        {
            NetworkSessionData.ClientColors[_slots[i].ClientId] = ColorOrder[_slots[i].ColorIndex];
            colorList[i] = ColorOrder[_slots[i].ColorIndex];
        }

        // 세션 시드 생성 + 모든 클라이언트에 브로드캐스트
        int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        NetworkSessionData.Seed = seed;
        BroadcastSeedClientRpc(seed);

        // GameSession에 활성 색 적용
        if (GameSession.Instance != null)
            GameSession.Instance.SetActiveColors(colorList);

        NetworkManager.SceneManager.LoadScene("M.Stage1", LoadSceneMode.Single);
    }

    // ── 공개 읽기 API ─────────────────────────────────────────────

    public int SlotCount => _slots?.Count ?? 0;

    public LobbyPlayerState GetSlot(int i) =>
        (_slots == null || i < 0 || i >= _slots.Count) ? LobbyPlayerState.Empty : _slots[i];

    /// <summary>
    /// Start 버튼 활성 조건.
    /// 호스트 슬롯은 Ready 불필요. 나머지 클라이언트 전원 Ready + 색 중복 없음.
    /// 호스트 1인(팀원 없음)이면 즉시 true.
    /// </summary>
    public bool CanStart()
    {
        if (_slots == null || _slots.Count == 0) return false;

        ulong hostId = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.LocalClientId
            : ulong.MaxValue;

        var usedColors = new HashSet<int>();
        foreach (var s in _slots)
        {
            if (!usedColors.Add(s.ColorIndex)) return false; // 색 중복 검사는 호스트 포함

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

    void HandleSlotsChanged(NetworkListEvent<LobbyPlayerState> _) =>
        OnSlotsChanged?.Invoke();

    bool IsColorTaken(int colorIndex, ulong excludeClient)
    {
        foreach (var s in _slots)
            if (s.ClientId != excludeClient && s.ColorIndex == colorIndex)
                return true;
        return false;
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
            Debug.Log($"  [{i}] clientId={s.ClientId} color={ColorOrder[s.ColorIndex]} ready={s.IsReady}");
        }
    }
#endif
}
