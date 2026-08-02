using Unity.Netcode;
using UnityEngine;

/// <summary>
/// T.Stage 네트워크 코드 전용 구조화 로그 유틸 (관측성 — `NetworkDesign.md` §9B 참고).
///
/// [목적]
///  M.Stage 라운드에서 "A버그 고치면 B버그, B 고치면 C, C 고치면 다시 A" 회귀가 반복된 핵심 원인은
///  Host/Client 중 어느 쪽이 무엇을 언제 봤는지 로그로 알 수 없었다는 것. 이 유틸은 그 관측성을
///  "공통 포맷 1개"로 채우기 위한 것 — 매 프레임 로그를 늘리는 용도가 아니다.
///
/// [사용 규칙 — 반드시 지킬 것]
///  1. 전환점(state transition)에서만 호출한다: Trigger 감지, RoundStart/시드 배포, Judge/Resolve,
///     Scene Load/Ready, 소유권 가드 실패(교차 오염 감지) 등.
///  2. Update()/FixedUpdate() 등 매 프레임 실행되는 코드 경로 안에서는 절대 호출하지 않는다.
///     (틱마다 찍으면 노이즈가 되어 오히려 관측성을 해침 — §9B 원칙)
///  3. 적용 대상은 T.Stage 신규 코드부터다. 기존 M.Stage 코드에 소급 적용하지 않는다(확정 — §9B).
///
/// [포맷]
///  "[Host] WallMoverSequencer SequenceStart seed=1234 startTime=12.34"
///  "[Client] StageNetworkState ChallengeStepChanged owner=OX step=3"
///  Role은 NetworkManager.Singleton 기준 자동 판정 — 호출부에서 Host/Client를 직접 문자열로 넣지 않는다.
/// </summary>
public static class NetLog
{
    /// <summary>
    /// 전환점 로그 1줄 출력. system=컴포넌트/시스템 이름, evt=전환 이벤트 이름,
    /// details=핵심 필드만(시드·인덱스·시각 등) — 문장형 설명 금지, key=value 나열 권장.
    /// </summary>
    public static void Transition(string system, string evt, string details = null)
    {
        string tag = RoleTag();
        string body = string.IsNullOrEmpty(details) ? evt : $"{evt} {details}";
        Debug.Log($"{tag} {system} {body}");
    }

    static string RoleTag()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return "[Local]";
        return nm.IsServer ? "[Host]" : "[Client]";
    }
}
