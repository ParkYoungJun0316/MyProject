using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tutorial 상시 HUD의 Steam Invite 버튼 (NetworkDesign.md §6B.5, SteamworksIntegrationDesign.md §3).
///
/// [역할]
/// - Steam 경로(정식 릴리스 빌드): 버튼 클릭 시 <see cref="SteamLobbyManager.OpenInviteOverlay"/> 호출.
/// - 로컬 경로(①ParrelSync/②Dev Build): 이 버튼 자체를 비활성화 — 룸코드 표시는
///   <see cref="TutorialRoomCodeDisplay"/>가 별도로 담당한다(§6B.5, 로컬/Steam 상호배타).
/// - 게이트 통과 후 숨김은 이 컴포넌트가 아니라 부모 HUD 패널의 <see cref="TutorialHUDGate"/>가 처리한다.
///
/// [배치 방법]
/// Tutorial 상시 HUD 패널 안의 Invite 버튼 GameObject(Button 컴포넌트 포함)에 부착.
/// </summary>
[RequireComponent(typeof(Button))]
public class TutorialSteamInviteUI : MonoBehaviour
{
    Button _button;

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

    void OnClickInvite()
    {
        if (SteamLobbyManager.Instance == null)
        {
            Debug.LogWarning("[TutorialSteamInviteUI] SteamLobbyManager.Instance가 null — 초대 오버레이를 열 수 없습니다.");
            return;
        }

        SteamLobbyManager.Instance.OpenInviteOverlay();
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 초대 오버레이 열기")]
    void Debug_OpenInvite() => OnClickInvite();
#endif
}
