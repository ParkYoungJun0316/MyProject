using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// 게임 설정(볼륨 · 화면 · 언어) SSOT. 싱글턴, DontDestroyOnLoad.
///
/// [배치 방법]
/// 0.Title 씬의 NetworkManager GameObject(SteamManager / GameLocalizationBootstrap과 같은 자리)에 부착.
///
/// [볼륨 방식 — AudioMixer 미사용]
/// MasterVolume × BgmVolume / MasterVolume × SfxVolume 을 SFXManager·BGMManager가 각자
/// Instance를 통해 직접 읽어서(pull) 적용함. GameSettingsManager가 값을 밀어주는(push) 방식이 아님 —
/// Awake 실행 순서가 오브젝트마다 비결정적이라(Unity 공식 문서) push 방식은 타이밍 버그 위험이 있음
/// (TitleMenuController/SteamLobbyManager Awake 순서 버그 전례, SteamworksIntegrationDesign.md 트랙5 6차 참고).
/// AudioMixer 자체를 안 쓰는 이유: 지금 필요한 건 슬라이더 3개뿐이라 오버엔지니어링(ReleaseRoadmap
/// "사운드 과투자 금지") — 나중에 덕킹 등 고급 오디오 이펙트가 필요해지면 그때 전환.
///
/// [언어]
/// 사용자가 옵션에서 직접 선택하면 PlayerPrefs에 저장되고, 이후 실행부터
/// GameLocalizationBootstrap의 Steam/systemLanguage 자동 감지보다 우선 적용됨
/// (GameLocalizationBootstrap.ManualLocaleOverrideKey 공유).
/// </summary>
public class GameSettingsManager : MonoBehaviour
{
    public static GameSettingsManager Instance { get; private set; }

    const string KeyMasterVolume = "Settings.MasterVolume";
    const string KeyBgmVolume    = "Settings.BgmVolume";
    const string KeySfxVolume    = "Settings.SfxVolume";
    const string KeyDisplayMode  = "Settings.DisplayMode";
    const string KeyResWidth     = "Settings.ResWidth";
    const string KeyResHeight    = "Settings.ResHeight";

    public float MasterVolume { get; private set; } = 1f;
    public float BgmVolume    { get; private set; } = 1f;
    public float SfxVolume    { get; private set; } = 1f;

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

        MasterVolume = PlayerPrefs.GetFloat(KeyMasterVolume, 1f);
        BgmVolume    = PlayerPrefs.GetFloat(KeyBgmVolume, 1f);
        SfxVolume    = PlayerPrefs.GetFloat(KeySfxVolume, 1f);

        ApplySavedDisplay();
    }

    // ── 볼륨 ──────────────────────────────────────────────────────

    public void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(KeyMasterVolume, MasterVolume);
    }

    public void SetBgmVolume(float value)
    {
        BgmVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(KeyBgmVolume, BgmVolume);
    }

    public void SetSfxVolume(float value)
    {
        SfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(KeySfxVolume, SfxVolume);
    }

    // ── 화면 ──────────────────────────────────────────────────────

    void ApplySavedDisplay()
    {
        // 저장된 적 없으면(최초 실행) Unity/Player Settings 기본값을 그대로 둠 — 불필요한 강제 전환 방지.
        if (!PlayerPrefs.HasKey(KeyResWidth)) return;

        int width  = PlayerPrefs.GetInt(KeyResWidth, Screen.width);
        int height = PlayerPrefs.GetInt(KeyResHeight, Screen.height);
        FullScreenMode mode = (FullScreenMode)PlayerPrefs.GetInt(KeyDisplayMode, (int)Screen.fullScreenMode);

        Screen.SetResolution(width, height, mode);
    }

    /// <summary>옵션 메뉴에서 해상도/화면모드 변경 시 호출. 즉시 적용 + 저장.</summary>
    public void ApplyDisplay(int width, int height, FullScreenMode mode)
    {
        Screen.SetResolution(width, height, mode);
        PlayerPrefs.SetInt(KeyResWidth, width);
        PlayerPrefs.SetInt(KeyResHeight, height);
        PlayerPrefs.SetInt(KeyDisplayMode, (int)mode);
    }

    // ── 언어 ──────────────────────────────────────────────────────

    /// <summary>옵션 메뉴 언어 드롭다운에서 호출. 즉시 적용 + 저장(다음 실행부터 자동 우선 적용).</summary>
    public void SetLocale(Locale locale)
    {
        if (locale == null) return;
        LocalizationSettings.SelectedLocale = locale;
        PlayerPrefs.SetString(GameLocalizationBootstrap.ManualLocaleOverrideKey, locale.Identifier.Code);
    }
}
