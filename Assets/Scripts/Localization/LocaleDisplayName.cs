using System.Collections.Generic;
using System.Globalization;
using UnityEngine.Localization;

/// <summary>
/// 옵션 메뉴 언어 드롭다운 표시명.
/// <see cref="CultureInfo.NativeName"/>은 zh-Hans/zh-Hant와 es/es-419를 같은 이름으로 돌려준다.
/// 코어 12개만 노출 — European Portuguese(<c>pt</c>)는 미사용이라 목록에서 제외.
/// </summary>
public static class LocaleDisplayName
{
    public const string UnusedPortugueseCode = "pt";

    static readonly Dictionary<string, string> Overrides = new()
    {
        { "zh-Hans", "中文 (简体)" },
        { "zh-Hant", "中文 (繁體)" },
        { "es",      "Español (España)" },
        { "es-419",  "Español (Latinoamérica)" },
        { "pt-BR",   "Português (Brasil)" },
    };

    public static bool IsOfferedInSettings(Locale locale) =>
        locale != null && locale.Identifier.Code != UnusedPortugueseCode;

    public static string Of(Locale locale)
    {
        if (locale == null) return string.Empty;
        return Of(locale.Identifier.Code, locale.Identifier.CultureInfo, locale.LocaleName);
    }

    /// <summary>폰트 베이커용 — Locale 에셋 없이 코드만으로 드롭다운에 실제로 뜨는 문자열을 모은다.</summary>
    public static string Of(string localeCode) => Of(localeCode, null, localeCode);

    static string Of(string localeCode, CultureInfo culture, string fallback)
    {
        if (string.IsNullOrEmpty(localeCode)) return fallback ?? string.Empty;
        if (Overrides.TryGetValue(localeCode, out string name)) return name;
        if (culture != null) return culture.NativeName;
        try
        {
            return CultureInfo.GetCultureInfo(localeCode).NativeName;
        }
        catch (CultureNotFoundException)
        {
            return fallback ?? localeCode;
        }
    }
}
