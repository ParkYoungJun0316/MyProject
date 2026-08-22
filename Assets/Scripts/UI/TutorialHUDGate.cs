using UnityEngine;

/// <summary>
/// Tutorial 상시 HUD(룸코드 표시 + Steam Invite 버튼) 패널의 게이트 연동 (NetworkDesign.md §6B.5).
///
/// [역할]
/// <see cref="TutorialNetworkManager.OnGateCountdownComplete"/>가 발동하면(= 전원 게이트 통과,
/// M.Stage1 로드 직전) 이 패널을 숨긴다 — 통과 후엔 더 이상 인원이 늘어날 수 없으므로 HUD가 불필요.
/// 구독은 Start()에서 한다 — Unity는 같은 프레임의 모든 Awake가 끝난 뒤에만 Start를 호출하므로,
/// TutorialNetworkManager.Instance가 자신의 Awake()에서 세팅된 뒤에 안전하게 참조할 수 있다
/// (TitleMenuController의 OnInviteAccepted 구독 타이밍 버그와 동일한 함정 회피, SteamworksIntegrationDesign.md
/// 트랙5 6차 세션 참고).
///
/// [배치 방법]
/// Tutorial 상시 HUD 패널의 루트 GameObject(룸코드 텍스트 + Invite 버튼을 담는 부모)에 부착.
/// </summary>
public class TutorialHUDGate : MonoBehaviour
{
    void Start()
    {
        if (TutorialNetworkManager.Instance != null)
            TutorialNetworkManager.Instance.OnGateCountdownComplete.AddListener(Hide);
    }

    void OnDestroy()
    {
        if (TutorialNetworkManager.Instance != null)
            TutorialNetworkManager.Instance.OnGateCountdownComplete.RemoveListener(Hide);
    }

    public void Hide() => gameObject.SetActive(false);
}
