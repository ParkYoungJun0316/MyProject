using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 숫자키(1~4)로 응원을 제출하는 입력 컴포넌트.
/// 채팅 `/cheer {name}` 폴백을 대체 (CheerAndTutorialDesign.md §6.2 — 2026-08-27 변경).
///
/// [매핑]
/// 1=berry(colorIndex 0) 2=guma(1) 3=sook(2) 4=dan(3) — PlayerColorUtil.ColorOrder와 동일한
/// 고정 순서라 참가 순서와 무관하게 항상 동일하다.
///
/// [서버 검증]
/// 클라이언트는 눌린 숫자를 그대로 targetColorIndex로 보낼 뿐, 자기 응원·버프 중 타겟·
/// 세션 미사용 색 등은 전부 CheerService.ValidateCheer(Host)가 그대로 처리한다
/// (음성/채팅과 동일한 SubmitCheerServerRpc(targetColorIndex, isVoice=false) 경로 재사용 —
/// rate limit도 기존 chatRateLimitSeconds를 그대로 공유).
///
/// [게이팅]
/// InGameChatUI.IsChatOpen / TutorialCheerNameUI.IsOpen 중엔 숫자키가 다른 UI에 쓰이므로
/// 무시한다 (InGameChatUI.Update()·PlayerEmoteMenuUI.Update()와 동일 패턴).
/// 이모트 메뉴(T키)는 클릭 전용으로 바뀌어(2026-08-27) 더 이상 숫자키를 쓰지 않으므로
/// 별도로 확인할 필요가 없다.
///
/// [배치]
/// HUD Canvas(InGameChatUI 등이 붙어있는 오브젝트) 아무 곳에나 부착. NetworkObject 불필요 —
/// CheerService.Instance의 public RPC 메서드를 직접 호출하는 일반 MonoBehaviour.
/// </summary>
public class CheerDigitInput : MonoBehaviour
{
    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (InGameChatUI.IsChatOpen || TutorialCheerNameUI.IsOpen) return;

        int targetIdx = -1;
        if (kb.digit1Key.wasPressedThisFrame) targetIdx = 0;
        else if (kb.digit2Key.wasPressedThisFrame) targetIdx = 1;
        else if (kb.digit3Key.wasPressedThisFrame) targetIdx = 2;
        else if (kb.digit4Key.wasPressedThisFrame) targetIdx = 3;

        if (targetIdx < 0) return;

        CheerService.Instance?.SubmitCheerServerRpc(targetIdx, false);
    }
}
