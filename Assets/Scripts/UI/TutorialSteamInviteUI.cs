using Steamworks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Tutorial 상시 HUD의 Steam Invite 버튼 (NetworkDesign.md §6B.5, SteamworksIntegrationDesign.md §3).
///
/// [역할]
/// - Steam 경로(정식 릴리스 빌드): 버튼 클릭 또는 [I] 키로 <see cref="SteamLobbyManager.OpenInviteOverlay"/> 호출.
///   (2026-09-18 [I] 추가 — 인게임은 커서가 잠겨 있어 버튼 클릭이 불편하다는 피드백. 이 GameObject가
///   꺼지면 Update도 멈추므로, 로컬 경로 비활성·게이트 통과 후 숨김이 키 입력에도 그대로 적용된다.)
/// - 로컬 경로(①ParrelSync/②Dev Build): 이 버튼 자체를 비활성화 — 룸코드 표시는
///   <see cref="TutorialRoomCodeDisplay"/>가 별도로 담당한다(§6B.5, 로컬/Steam 상호배타).
/// - 게이트 통과 전 수신하는 초대 수락(§6B.5 "초대 수락 처리" 행)도 이 컴포넌트가 처리한다
///   (2026-08-22 추가 — `1.Lobby` 폐지로 사라진 구 `LobbyMenuController.MoveToInvitedLobby`를
///   이 컴포넌트로 이관). 게이트 통과 후 숨김은 이 컴포넌트가 아니라 부모 HUD 패널의
///   <see cref="TutorialHUDGate"/>가 처리하며, 그 패널이 비활성화되면 아래 OnDisable로
///   구독도 같이 해제돼 "통과 후 수락 무시" 정책이 별도 게이트 상태 참조 없이 성립한다.
///
/// [배치 방법]
/// Tutorial 상시 HUD 패널 안의 Invite 버튼 GameObject(Button 컴포넌트 포함)에 부착.
/// </summary>
[RequireComponent(typeof(Button))]
public class TutorialSteamInviteUI : MonoBehaviour
{
    Button _button;
    bool   _invitePending;

    void Awake()
    {
        _button = GetComponent<Button>();
    }

    void Start()
    {
        if (NetworkManagerSetup.UseLocalNetworkPath)
        {
            gameObject.SetActive(false);
            return;
        }

        _button.onClick.AddListener(OnClickInvite);
    }

    void OnEnable()
    {
        if (SteamLobbyManager.Instance != null)
            SteamLobbyManager.Instance.OnInviteAccepted += OnSteamInviteAccepted;
    }

    void OnDisable()
    {
        if (SteamLobbyManager.Instance != null)
            SteamLobbyManager.Instance.OnInviteAccepted -= OnSteamInviteAccepted;
    }

    void Update()
    {
        // 채팅·치어네임 입력 중 'i' 타이핑, ESC 메뉴 등 커서 UI가 떠 있을 때는 무시한다
        // (TutorialCheerNameSignboard의 E 키와 동일 게이팅).
        if (InGameChatUI.IsChatOpen || TutorialCheerNameUI.IsOpen || CursorUnlockRequestUtil.IsRequested) return;

        if (Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame)
            OnClickInvite();
    }

    void OnClickInvite()
    {
        if (SteamLobbyManager.Instance == null)
        {
            Debug.LogWarning("[TutorialSteamInviteUI] SteamLobbyManager.Instance가 null — 초대 오버레이를 열 수 없습니다.");
            return;
        }

        SteamLobbyManager.Instance.OpenInviteOverlay();
    }

    /// <summary>
    /// 게이트 통과 전 Tutorial 대기 중 초대 수락 시(§6B.5). 이미 이 프로세스에서 Steam
    /// 네트워킹을 시작한 상태(Host든 Client든)이므로 인프로세스 재접속은 시도하지 않고,
    /// 기존 방을 정리한 뒤 곧바로 프로세스를 재시작해 새 lobbyId로 접속한다
    /// (트랙5·6에서 검증된 "웜 리커넥트는 항상 재시작" 원칙과 동일 — TitleMenuController.
    /// TryRestartForWarmReconnect 참고).
    /// </summary>
    void OnSteamInviteAccepted(SteamId lobbyId)
    {
        if (_invitePending) return;
        _invitePending = true;

        Debug.Log($"[TutorialSteamInviteUI] 게이트 전 초대 수락 — 기존 방 정리 후 lobbyId={lobbyId}로 재접속.");
        NetworkManagerSetup.Instance?.Shutdown();
        NetworkManagerSetup.RestartWithConnectLobby(lobbyId);
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 초대 오버레이 열기")]
    void Debug_OpenInvite() => OnClickInvite();
#endif
}
