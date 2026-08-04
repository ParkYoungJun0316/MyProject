using System;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

/// <summary>
/// Steam Private Lobby 생성/조회/입장 래퍼 (SteamworksIntegrationDesign.md §3, §8 확정).
///
/// [설계]
/// - Lobby 타입: Private 고정 (§3). Public/FriendsOnly 분기 없음.
/// - 입장 방식: (a) LobbyId 직접 입력 (b) Invite Overlay — 둘 다 지원 (§3).
/// - §8: Facepunch Lobby의 Owner 자동 이전 콜백은 로그만 남기고 무시한다.
///   방 종료 판단은 기존 DisconnectManager → TitleReturnFlow 경로만 담당 — 여기서 별도 조치하지 않는다.
/// - 룸코드 표시는 마스킹(`7**1` 형태, §3) — <see cref="MaskLobbyId"/> 참고.
///   실제 전체 코드는 <see cref="NetworkManagerSetup.RoomCode"/> → LobbyNetworkManager.SharedRoomCode 경유로
///   그대로 전달되어 기존 "복사" 버튼(LobbyMenuController.OnClickCopy)이 변경 없이 재사용된다.
///
/// [배치 방법]
/// 0.Title 씬 NetworkManager GameObject(또는 SteamManager와 같은 오브젝트)에 부착.
/// SteamManager와 마찬가지로 DontDestroyOnLoad.
/// </summary>
public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager Instance { get; private set; }

    [Header("Lobby 설정 (SteamworksIntegrationDesign.md §3)")]
    [Tooltip("Lobby 최대 인원. 4인 co-op 고정.")]
    [SerializeField] private int maxMembers = 4;

    public Lobby? CurrentLobby { get; private set; }
    public bool IsInLobby => CurrentLobby.HasValue;

    /// <summary>
    /// Invite Overlay 수락 시 발행 (SteamFriends.OnGameLobbyJoinRequested 중계).
    /// 게임이 이미 실행 중인 상태(타이틀 화면)에서만 의미 있음 — 게임 미실행 중 초대 수락(커맨드라인 실행)은 범위 밖.
    /// </summary>
    public event Action<SteamId> OnInviteAccepted;

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        SteamMatchmaking.OnLobbyMemberLeave       += HandleLobbyMemberLeave;
        SteamMatchmaking.OnLobbyMemberDisconnected += HandleLobbyMemberDisconnected;
        SteamFriends.OnGameLobbyJoinRequested      += HandleGameLobbyJoinRequested;
    }

    void OnDisable()
    {
        SteamMatchmaking.OnLobbyMemberLeave       -= HandleLobbyMemberLeave;
        SteamMatchmaking.OnLobbyMemberDisconnected -= HandleLobbyMemberDisconnected;
        SteamFriends.OnGameLobbyJoinRequested      -= HandleGameLobbyJoinRequested;
    }

    // ── 공개 API ──────────────────────────────────────────────────

    /// <summary>Private Lobby 생성. 실패 시 null 반환.</summary>
    public async Task<Lobby?> CreateLobbyAsync()
    {
        if (!EnsureSteamReady()) return null;

        Lobby? lobby = await SteamMatchmaking.CreateLobbyAsync(maxMembers);
        if (lobby == null)
        {
            Debug.LogError("[SteamLobbyManager] Lobby 생성 실패 (CreateLobbyAsync가 null 반환).");
            return null;
        }

        lobby.Value.SetPrivate();   // §3 확정: Private 고정
        lobby.Value.SetJoinable(true);
        CurrentLobby = lobby;

        Debug.Log($"[SteamLobbyManager] Lobby 생성됨 — Id {lobby.Value.Id}, Owner {lobby.Value.Owner.Id}");
        return lobby;
    }

    /// <summary>LobbyId로 참여. 실패 시 null 반환.</summary>
    public async Task<Lobby?> JoinLobbyAsync(SteamId lobbyId)
    {
        if (!EnsureSteamReady()) return null;

        Lobby? lobby = await SteamMatchmaking.JoinLobbyAsync(lobbyId);
        if (lobby == null)
        {
            Debug.LogError("[SteamLobbyManager] Lobby 참여 실패 — LobbyId를 다시 확인하세요.");
            return null;
        }

        CurrentLobby = lobby;
        Debug.Log($"[SteamLobbyManager] Lobby 참여됨 — Owner {lobby.Value.Owner.Id}");
        return lobby;
    }

    /// <summary>현재 Lobby 나가기. NetworkManagerSetup.Shutdown()에서 호출됨.</summary>
    public void LeaveCurrentLobby()
    {
        if (CurrentLobby.HasValue)
        {
            CurrentLobby.Value.Leave();
            Debug.Log("[SteamLobbyManager] Lobby 나감");
        }
        CurrentLobby = null;
    }

    /// <summary>Invite Overlay 열기 (§3 — MVP 초대 메커니즘).</summary>
    public void OpenInviteOverlay()
    {
        if (!CurrentLobby.HasValue)
        {
            Debug.LogWarning("[SteamLobbyManager] 현재 참여 중인 Lobby가 없어 Invite Overlay를 열 수 없습니다.");
            return;
        }
        SteamFriends.OpenGameInviteOverlay(CurrentLobby.Value.Id);
    }

    /// <summary>
    /// LobbyId 마스킹 표시 (§3, `7**1` 형태). 첫 글자 + ** + 마지막 글자.
    /// 실제 전체 코드가 필요하면 <see cref="NetworkManagerSetup.RoomCode"/> 등 원본 문자열을 그대로 사용할 것.
    /// </summary>
    public static string MaskLobbyId(SteamId id) => MaskCode(id.Value.ToString());

    /// <summary>문자열 형태 코드에 대한 범용 마스킹 (룸코드가 이미 문자열인 경우).</summary>
    public static string MaskCode(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length <= 2) return code;
        return $"{code[0]}**{code[^1]}";
    }

    // ── 내부 ──────────────────────────────────────────────────────

    bool EnsureSteamReady()
    {
        if (SteamManager.Instance == null || !SteamManager.Instance.EnsureInitialized())
        {
            Debug.LogError("[SteamLobbyManager] Steam이 초기화되지 않아 Lobby 작업을 진행할 수 없습니다.");
            return false;
        }
        return true;
    }

    /// <summary>
    /// §8 확정: Facepunch Lobby의 Owner 자동 이전 동작을 무시한다.
    /// 방 종료 판단(§12)은 DisconnectManager → TitleReturnFlow 경로에서만 처리 — 여기서는 로그만 남긴다.
    /// </summary>
    void HandleLobbyMemberLeave(Lobby lobby, Friend friend) =>
        Debug.Log($"[SteamLobbyManager] Lobby 멤버 이탈 — {friend.Name} (자동 Owner 이전 무시, §8)");

    void HandleLobbyMemberDisconnected(Lobby lobby, Friend friend) =>
        Debug.Log($"[SteamLobbyManager] Lobby 멤버 연결 끊김 — {friend.Name} (자동 Owner 이전 무시, §8)");

    /// <summary>
    /// Invite Overlay에서 초대를 수락하면 Steam이 발생시키는 콜백(게임이 이미 실행 중인 경우).
    /// 실제 Join 처리는 TitleMenuController가 OnInviteAccepted를 구독해 수행한다
    /// (타이틀 화면에서만 의미 있음 — 인게임 중 수신은 무시해야 함, 구독측 책임).
    /// </summary>
    void HandleGameLobbyJoinRequested(Lobby lobby, SteamId friendId)
    {
        Debug.Log($"[SteamLobbyManager] 초대 수락 감지 — Lobby {lobby.Id}, 초대자 {friendId}");
        OnInviteAccepted?.Invoke(lobby.Id);
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: Lobby 생성")]
    async void Debug_CreateLobby() => await CreateLobbyAsync();

    [ContextMenu("테스트: Invite Overlay 열기")]
    void Debug_OpenInvite() => OpenInviteOverlay();

    [ContextMenu("테스트: Lobby 나가기")]
    void Debug_Leave() => LeaveCurrentLobby();

    [ContextMenu("테스트: 상태 출력")]
    void Debug_Status()
    {
        if (CurrentLobby.HasValue)
        {
            var l = CurrentLobby.Value;
            Debug.Log($"[SteamLobbyManager] LobbyId={l.Id} Masked={MaskLobbyId(l.Id)} " +
                      $"Owner={l.Owner.Id} Members={l.MemberCount}/{l.MaxMembers}");
        }
        else
        {
            Debug.Log("[SteamLobbyManager] 현재 Lobby 없음");
        }
    }
#endif
}
