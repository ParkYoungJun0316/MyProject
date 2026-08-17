/// <summary>
/// CheerName 형식·예약어 검증 — CheerAndTutorialDesign.md §3.5 "형식(#1~4)"/"세션·시스템(#8)" 규칙의 단일 소스.
/// 구 LobbyNetworkManager.IsValidCheerNameFormat/ReservedNames를 여기로 추출한 것 — 로직 변경 없음.
///
/// LobbyNetworkManager(NetworkDesign.md §6B.7 P8에서 삭제 대상)와 신규 PlayerCheerNameSync(Tutorial,
/// §6B.7 P6)가 이 클래스 하나만 참조하도록 통일한다 — 구 로비 코드를 나중에 지워도 검증 규칙이
/// 함께 사라지지 않게 하기 위함.
///
/// [아직 빠진 것 — 별도 작업 대상]
/// CheerAndTutorialDesign.md §3.5 #9~12(욕설·성/체·혐오·우회표현 blocklist)는 구 로비 구현에도
/// 존재하지 않았다. 형식(#1~4)·예약어(#8)만 여기서 다룬다 — 새로 지어내지 않음.
/// </summary>
public static class CheerNameValidator
{
    /// <summary>예약어 — CheerName으로 쓸 수 없음(CheerAndTutorialDesign.md §3.5 #8).</summary>
    public static readonly string[] ReservedNames =
        { "cheer", "admin", "host", "server", "system", "bot", "null" };

    /// <summary>
    /// 형식 검증(§3.5 #1~4: 길이 2~12, a-z/0-9/_ 만 허용, 예약어 아님).
    /// 호출 전 trim + 소문자 변환은 호출부 책임 — 빈 문자열(커스텀 해제) 판단도 호출부에서
    /// 이 함수를 부르기 전에 처리할 것(빈 문자열은 여기서 그냥 실패 처리됨).
    /// </summary>
    public static bool IsValidFormat(string lower, out string reason)
    {
        reason = "";
        if (lower.Length < 2 || lower.Length > 12) { reason = "format"; return false; }
        foreach (char c in lower)
        {
            if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
            { reason = "format"; return false; }
        }
        foreach (string reserved in ReservedNames)
            if (lower == reserved) { reason = "reserved"; return false; }
        return true;
    }
}
