using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 팀 응원 키 입력 대체 컴포넌트 (음성이 기본, 이 키는 보조).
///
/// [매핑] [2026-09-14 재배정]
/// `T` = 팀 응원(team). 구 숫자키 `1`(그 전엔 `2`) — 같은 날 이모트 시스템이 숫자키 `1`~`8`을
/// 직접 트리거로 가져가면서 자리가 비어 옮겨졌다(CheerSystemDesign.md §6.3).
/// 개인 버프의 숫자키 `1`(self) 분기는 완전 삭제 — 개인 버프는 이제 Space 키 전용(§6.1).
///
/// [게이팅]
/// 기본 비활성 — GameSettingsManager.DigitCheerEnabled(Options에서 켠다) **또는**
/// CheerSoundLocalState.SessionTKeyAutoOn(이번 세션만: 등록본 없이 게이트 통과·연습 7회 연속 실패, §14.8)
/// **또는** 패널의 "마이크가 없어요"(개인)·"마이크 없음 — 팀 전체"(Host) 토글(10/7).
/// InGameChatUI.IsChatOpen / TutorialCheerNameUI.IsOpen 중엔 무시.
///
/// [서버 검증]
/// SubmitTeamCheerServerRpc(isVoice=false). rate limit은 CheerService.chatRateLimitSeconds.
///
/// [배치]
/// HUD Canvas 아무 곳에나 부착. NetworkObject 불필요.
/// </summary>
public class CheerDigitInput : MonoBehaviour
{
    /// <summary>옵션 ON 또는 세션 자동 ON — 힌트 UI도 같은 기준으로 "T키 가능" 여부를 본다.</summary>
    public static bool IsTKeyEnabled =>
        GameSettingsManager.Instance?.DigitCheerEnabled == true || CheerSoundLocalState.SessionTKeyAutoOn
        || CheerSoundLocalState.PersonalNoMic || CheerSoundLocalState.TeamNoMic;

    void Update()
    {
        if (!IsTKeyEnabled) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (InGameChatUI.IsChatOpen || TutorialCheerNameUI.IsOpen || CostumePanelUI.IsOpen) return;

        var svc = CheerService.Instance;
        if (svc == null) return;

        if (kb.tKey.wasPressedThisFrame)
            svc.SubmitTeamCheerServerRpc(false);
    }
}
