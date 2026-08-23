using Steamworks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tutorial 상시 HUD의 Steam Invite 버튼 (NetworkDesign.md §6B.5, SteamworksIntegrationDesign.md §3).
///
/// [역할]
/// - Steam 경로(정식 릴리스 빌드): 버튼 클릭 시 <see cref="SteamLobbyManager.OpenInviteOverlay"/> 호출.
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
