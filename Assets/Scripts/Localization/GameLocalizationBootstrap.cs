using System.Collections;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// 게임 시작 시 사용할 Locale을 결정해 적용하는 부트스트랩
/// (SteamworksIntegrationDesign.md §9~11 확정 — 다국어 파일럿, DialogueUI부터).
///
/// [언어 소스 우선순위]
/// 1. 릴리스 경로(<see cref="NetworkManagerSetup.UseLocalNetworkPath"/> == false) — Steam 클라이언트
///    언어(<c>SteamApps.GameLanguage</c>)를 §10 코어 12개 Locale 코드로 매핑해 적용.
/// 2. 로컬 경로(①ParrelSync ②Dev Build)이거나 위가 실패하면 — <c>Application.systemLanguage</c> 폴백.
/// 3. 둘 다 매핑 실패하면 영어(en) 기본값.
///
/// 이는 §5(로컬 경로는 Steam 초기화 스킵) 확정과 충돌하지 않는다 — 릴리스 경로에서 Steam 초기화를
/// Title 진입 시점에 앞당겨 시도할 뿐이고, <see cref="SteamManager.EnsureInitialized"/>는 이미
/// idempotent라 이후 Create/Join 시 재호출해도 안전하다.
///
/// [배치 방법]
/// 0.Title 씬 > NetworkManager GameObject(<see cref="SteamManager"/>와 같은 오브젝트)에 부착.
///
/// [Editor 사전 준비 — 사용자 작업]
/// Project Settings > Localization에서 §10 코어 12개 언어 Locale을 등록해야 매핑이 실제로 적용됨.
/// (미등록 상태면 폴백까지 전부 실패해 아무 것도 바뀌지 않고 에러 로그만 남음.)
/// </summary>
public class GameLocalizationBootstrap : MonoBehaviour
{
    public static GameLocalizationBootstrap Instance { get; private set; }

    const string FallbackLocaleCode = "en";

    static readonly Dictionary<string, string> SteamLanguageToLocaleCode = new()
    {
        { "english",   "en" },
        { "koreana",   "ko" },
        { "japanese",  "ja" },
        { "schinese",  "zh-Hans" },
        { "tchinese",  "zh-Hant" },
        { "russian",   "ru" },
        { "german",    "de" },
        { "french",    "fr" },
        { "spanish",   "es" },
        { "latam",     "es-419" },
        { "brazilian", "pt-BR" },
        { "polish",    "pl" },
    };

    static readonly Dictionary<SystemLanguage, string> SystemLanguageToLocaleCode = new()
    {
        { SystemLanguage.English,            "en" },
        { SystemLanguage.Korean,             "ko" },
        { SystemLanguage.Japanese,           "ja" },
        { SystemLanguage.ChineseSimplified,  "zh-Hans" },
        { SystemLanguage.ChineseTraditional, "zh-Hant" },
        { SystemLanguage.Russian,            "ru" },
        { SystemLanguage.German,             "de" },
        { SystemLanguage.French,             "fr" },
        { SystemLanguage.Spanish,            "es" },
        { SystemLanguage.Polish,             "pl" },
        { SystemLanguage.Portuguese,         "pt-BR" }, // SystemLanguage엔 BR/PT 구분이 없음 — 코어 목록엔 pt-BR만 있어 그대로 매핑
    };

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start() => StartCoroutine(ApplyStartupLocale());

    // ── Locale 결정 · 적용 ────────────────────────────────────────

    IEnumerator ApplyStartupLocale()
    {
        yield return LocalizationSettings.InitializationOperation;

        string source = "systemLanguage(폴백)";
        string localeCode = null;

        if (!NetworkManagerSetup.UseLocalNetworkPath)
        {
            localeCode = TryResolveSteamLocaleCode();
            if (localeCode != null) source = "Steam";
        }

        localeCode ??= ResolveSystemLanguageLocaleCode();

        Locale locale = FindLocale(localeCode) ?? FindLocale(FallbackLocaleCode);
        if (locale == null)
        {
            Debug.LogError($"[GameLocalizationBootstrap] 사용 가능한 Locale이 없습니다 " +
                           $"(코드 '{localeCode}' 및 폴백 '{FallbackLocaleCode}' 모두 실패). " +
                           "Project Settings > Localization에서 Locale을 등록하세요.");
            yield break;
        }

        LocalizationSettings.SelectedLocale = locale;
        Debug.Log($"[GameLocalizationBootstrap] Locale 적용 — {locale.Identifier.Code} (소스: {source})");
    }

    string TryResolveSteamLocaleCode()
    {
        if (SteamManager.Instance == null || !SteamManager.Instance.EnsureInitialized())
            return null;

        string steamLanguage = SteamApps.GameLanguage;
        if (string.IsNullOrEmpty(steamLanguage)) return null;

        return SteamLanguageToLocaleCode.TryGetValue(steamLanguage, out string code) ? code : null;
    }

    static string ResolveSystemLanguageLocaleCode() =>
        SystemLanguageToLocaleCode.TryGetValue(Application.systemLanguage, out string code)
            ? code
            : FallbackLocaleCode;

    static Locale FindLocale(string localeCode)
    {
        if (string.IsNullOrEmpty(localeCode)) return null;
        return LocalizationSettings.AvailableLocales.Locales.Find(l => l.Identifier.Code == localeCode);
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: Locale 재적용")]
    void Debug_Reapply() => StartCoroutine(ApplyStartupLocale());

    [ContextMenu("테스트: 상태 출력")]
    void Debug_Status() =>
        Debug.Log($"[GameLocalizationBootstrap] 현재 SelectedLocale={LocalizationSettings.SelectedLocale?.Identifier.Code} " +
                  $"systemLanguage={Application.systemLanguage} UseLocalNetworkPath={NetworkManagerSetup.UseLocalNetworkPath}");
#endif
}
