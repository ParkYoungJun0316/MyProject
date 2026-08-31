using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 숫자키로 응원을 제출하는 입력 컴포넌트.
///
/// [매핑]
/// 1 = 자기 응원(self), 2 = 팀 응원(team). 3/4 제거 (cross-targeting 폐기).
///
/// [게이팅]
/// 기본 비활성 — GameSettingsManager.DigitCheerEnabled (Options에서 켠다).
/// InGameChatUI.IsChatOpen / TutorialCheerNameUI.IsOpen 중엔 무시.
///
/// [서버 검증]
/// SubmitSelfCheerServerRpc / SubmitTeamCheerServerRpc (isVoice=false).
/// rate limit은 CheerService.chatRateLimitSeconds.
///
/// [배치]
/// HUD Canvas 아무 곳에나 부착. NetworkObject 불필요.
/// </summary>
public class CheerDigitInput : MonoBehaviour
{
    void Update()
    {
        if (GameSettingsManager.Instance?.DigitCheerEnabled != true) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (InGameChatUI.IsChatOpen || TutorialCheerNameUI.IsOpen) return;

        var svc = CheerService.Instance;
        if (svc == null) return;

        if (kb.digit1Key.wasPressedThisFrame)
            svc.SubmitSelfCheerServerRpc(false);
        else if (kb.digit2Key.wasPressedThisFrame)
            svc.SubmitTeamCheerServerRpc(false);
    }
}
