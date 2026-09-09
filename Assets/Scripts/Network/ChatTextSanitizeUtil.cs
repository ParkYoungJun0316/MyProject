using System.Text.RegularExpressions;

/// <summary>
/// 플레이어가 입력한 텍스트를 TMP로 그리기 전에 안전한 조각으로 바꾸는 유틸. 이 논리의 SSOT —
/// 같은 처리를 각 UI 스크립트에 다시 쓰지 말고 여기를 부를 것.
///
/// [왜 필요한가]
/// TMP_Text는 richText가 기본 true다(TMP_Text.m_isRichText = true). 그래서 사용자 입력에 든
/// &lt;size=1000%&gt; · &lt;sprite index=0&gt; · &lt;/b&gt;&lt;color=#000000&gt; 같은 태그가 그대로 파싱된다.
/// 채팅은 Host가 받은 문자열을 전원에게 중계하므로, 한 사람이 태그를 치면 그 순간 모든
/// 클라이언트의 채팅 UI가 같이 망가진다(레이아웃 붕괴 · 글자 은폐 · 스프라이트 스팸).
///
/// [왜 '&lt;' 치환이 아니라 noparse 래핑인가]
/// '&lt;'를 다른 문자(＜ 등)로 바꾸는 방식은 그 치환 문자가 Static 폰트에 구워져 있지 않으면
/// 두부(□)가 된다 — Noto 5종은 필요한 문자만 사전 베이킹하기 때문이다(NotoFontStaticBaker 참고).
/// noparse는 새 문자를 도입하지 않고 구간의 태그 해석만 끈다. 유일한 탈출 시퀀스인
/// &lt;/noparse&gt;를 먼저 제거하므로 안쪽에서 빠져나올 방법이 없다.
///
/// [어디서 부르는가]
/// - 채팅: Host의 SendMessageServerRpc에서 중계 전에 1회 — 모든 클라이언트가 같은 문자열을 받는다.
/// - 치어 이름: 표시 시점(CheerService 로컬 조회라 Host를 경유하는 지점이 없다).
/// </summary>
public static class ChatTextSanitizeUtil
{
    /// <summary>
    /// 채팅 한 줄 최대 길이. Host가 이 값으로 자른다 — 클라이언트 InputField의 characterLimit은
    /// 입력 편의일 뿐 신뢰 대상이 아니다(변조된 클라이언트는 얼마든 길게 보낼 수 있다).
    /// 길이를 묶는 건 트랜스포트 보호만이 아니라 폰트 보호이기도 하다: 채팅 Dynamic 폴백은
    /// 처음 보는 문자를 그 순간 메인스레드에서 굽기 때문에, 긴 CJK 문장 하나가 프레임을 튀게 한다.
    /// </summary>
    public const int MaxChatLength = 100;

    /// <summary>noparse 구간을 빠져나가는 유일한 시퀀스. 공백·닫는 괄호 누락 변형까지 같이 잡는다.</summary>
    static readonly Regex NoparseEscape = new(@"<\s*/\s*noparse\s*>?", RegexOptions.IgnoreCase);

    /// <summary>
    /// 길이를 자르고 태그 해석을 끈 "표시용 조각"을 만든다. 결과는 그대로 다른 마크업 안에
    /// 끼워 넣어도 안전하다. 내용이 비면 빈 문자열을 반환하니 호출부에서 중계를 생략할 수 있다.
    /// </summary>
    public static string ToSafeDisplayFragment(string raw, int maxLength = MaxChatLength)
    {
        string neutralized = NeutralizeMarkup(Clamp(raw, maxLength));
        return string.IsNullOrEmpty(neutralized) ? string.Empty : $"<noparse>{neutralized}</noparse>";
    }

    /// <summary>
    /// 이미 길이가 보장된 짧은 텍스트(치어 이름 등)용 — 태그 해석만 끈다.
    /// 반환값은 <see cref="ToSafeDisplayFragment"/>와 달리 래핑되지 않은 알맹이라, 호출부가
    /// 직접 &lt;noparse&gt;로 감싸거나 그대로 쓸 수 있다.
    /// </summary>
    public static string NeutralizeMarkup(string raw)
        => string.IsNullOrEmpty(raw) ? string.Empty : NoparseEscape.Replace(raw, string.Empty);

    /// <summary>
    /// 앞뒤 공백을 떼고 maxLength로 자른다. 자를 위치가 서로게이트 페어 가운데면 한 글자 물러난다 —
    /// 반쪽만 남은 서로게이트는 어느 폰트에도 없는 코드포인트라 두부가 된다.
    /// </summary>
    public static string Clamp(string raw, int maxLength = MaxChatLength)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        string trimmed = raw.Trim();
        if (trimmed.Length <= maxLength) return trimmed;

        int cut = maxLength;
        if (char.IsHighSurrogate(trimmed[cut - 1])) cut--;

        return trimmed.Substring(0, cut);
    }
}
