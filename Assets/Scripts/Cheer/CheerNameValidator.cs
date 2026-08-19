/// <summary>
/// CheerName 형식·예약어·금칙어 검증 — CheerAndTutorialDesign.md §3.5 "형식(#1~4)"/"세션·시스템(#8)"/
/// "금칙어(#9~12)" 규칙의 단일 소스.
/// 구 LobbyNetworkManager.IsValidCheerNameFormat/ReservedNames를 여기로 추출한 것 — 로직 변경 없음
/// (LobbyNetworkManager 자체는 NetworkDesign.md §6B.7 P8에서 삭제 완료, 2026-08-20).
///
/// 신규 PlayerCheerNameSync(Tutorial, §6B.7 P6)가 이 클래스 하나만 참조한다.
///
/// [금칙어 스코프 — 2026-08-19 확정, "완벽 필터 아님"]
/// 100% 차단이 목표가 아니다(음성 인식 자체도 정확한 발음이 아니면 매칭 안 됨). "대놓고 심한 단어
/// 몇십 개만" 부분 문자열(Contains) 매칭으로 막는다 — 정교한 leetspeak·띄어쓰기 우회·전 세계
/// 방언/은어까지 커버하지 않음(§3.5 #9~12 표 참고). AI 필터 없음, 코드 테이블만 — 플레이테스트에서
/// 걸리는 단어가 나오면 Blocklist에 추가하면 된다.
/// </summary>
public static class CheerNameValidator
{
    /// <summary>예약어 — CheerName으로 쓸 수 없음(CheerAndTutorialDesign.md §3.5 #8).</summary>
    public static readonly string[] ReservedNames =
        { "cheer", "admin", "host", "server", "system", "bot", "null" };

    /// <summary>
    /// 금칙어 최소 목록(§3.5 #9~12) — 부분 문자열 포함 시 차단. 전부 소문자, a-z/0-9만 사용
    /// (IsValidFormat이 이미 그 외 문자를 막으므로 `$` 등 기호 우회형은 여기 없어도 됨).
    /// #9 욕설, #10 성/신체, #11 혐오·차별(대표적인 것 몇 개만), #12 숫자 치환 우회(가장 뻔한 것만).
    /// </summary>
    public static readonly string[] Blocklist =
    {
        // #9 욕설
        "fuck", "shit", "bitch", "ass", "damn", "bastard", "whore", "slut",
        "cunt", "dick", "cock", "prick", "twat", "wanker", "bollocks", "bugger",
        "crap", "piss", "douche", "bullshit",
        // #10 성/신체
        "pussy", "penis", "vagina", "boob", "anal", "porn", "cum", "jizz",
        // #11 혐오·차별 (대표적인 것만, 전체 목록 아님)
        "nigger", "chink", "spic", "kike", "fag", "faggot", "retard", "nazi",
        // #12 숫자 치환 우회 — 가장 뻔한 패턴만
        "fuk", "sh1t", "f4ck", "a55", "b1tch",
    };

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

    /// <summary>금칙어(§3.5 #9~12) 포함 여부 — 부분 문자열 매칭. 호출 전 소문자 변환은 호출부 책임.</summary>
    public static bool ContainsBlockedWord(string lower)
    {
        foreach (string blocked in Blocklist)
            if (lower.Contains(blocked)) return true;
        return false;
    }
}
