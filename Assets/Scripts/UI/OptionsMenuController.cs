using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// 옵션(설정) 메뉴 UI 컨트롤러. 값 저장/적용은 전부 GameSettingsManager에 위임하고
/// 이 컴포넌트는 UI 요소 ↔ GameSettingsManager 연결만 담당.
///
/// [배치 방법]
/// 타이틀 씬 설정 패널, 인게임 ESC 메뉴 설정 패널 양쪽에 동일 구성으로 배치 가능
/// (같은 프리팹 공유 권장).
///
/// [Inspector 연결]
/// - masterVolumeSlider / bgmVolumeSlider / sfxVolumeSlider : Slider (0~1)
/// - languageDropdown    : TMP_Dropdown — LocalizationSettings.AvailableLocales 기반 자동 채움
/// - displayModeDropdown : TMP_Dropdown — 전체화면 / 창모드 / 테두리없는 창모드 (고정 3항목, 자동 채움)
/// - resolutionDropdown  : TMP_Dropdown — Screen.resolutions 기반 자동 채움
///
/// 패널이 열릴 때(OnEnable)마다 현재 GameSettingsManager / Screen / LocalizationSettings 값을
/// 읽어 UI에 반영함.
/// </summary>
public class OptionsMenuController : MonoBehaviour
{
    [Header("볼륨")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [Header("언어")]
    [SerializeField] private TMP_Dropdown languageDropdown;

    [Header("화면")]
    [Tooltip("전체화면(독점) / 창모드 / 테두리없는 창모드")]
    [SerializeField] private TMP_Dropdown displayModeDropdown;
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    static readonly FullScreenMode[] DisplayModeValues =
    {
        FullScreenMode.ExclusiveFullScreen,
        FullScreenMode.Windowed,
        FullScreenMode.FullScreenWindow,
    };

    List<Locale> _locales = new List<Locale>();
    List<Resolution> _resolutions = new List<Resolution>();

    bool _refreshing;

    // ── 구독 ──────────────────────────────────────────────────────

    void OnEnable()
    {
        RefreshAll();

        if (masterVolumeSlider  != null) masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        if (bgmVolumeSlider     != null) bgmVolumeSlider.onValueChanged.AddListener(OnBgmVolumeChanged);
        if (sfxVolumeSlider     != null) sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        if (languageDropdown    != null) languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
        if (displayModeDropdown != null) displayModeDropdown.onValueChanged.AddListener(OnDisplayModeChanged);
        if (resolutionDropdown  != null) resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
    }

    void OnDisable()
    {
        if (masterVolumeSlider  != null) masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
        if (bgmVolumeSlider     != null) bgmVolumeSlider.onValueChanged.RemoveListener(OnBgmVolumeChanged);
        if (sfxVolumeSlider     != null) sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
        if (languageDropdown    != null) languageDropdown.onValueChanged.RemoveListener(OnLanguageChanged);
        if (displayModeDropdown != null) displayModeDropdown.onValueChanged.RemoveListener(OnDisplayModeChanged);
        if (resolutionDropdown  != null) resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);
    }

    // ── 새로고침 ──────────────────────────────────────────────────

    void RefreshAll()
    {
        _refreshing = true;

        RefreshVolumeSliders();
        RefreshLanguageDropdown();
        RefreshDisplayModeDropdown();
        RefreshResolutionDropdown();

        _refreshing = false;
    }

    void RefreshVolumeSliders()
    {
        GameSettingsManager settings = GameSettingsManager.Instance;
        if (settings == null) return;

        if (masterVolumeSlider != null) masterVolumeSlider.value = settings.MasterVolume;
        if (bgmVolumeSlider    != null) bgmVolumeSlider.value    = settings.BgmVolume;
        if (sfxVolumeSlider    != null) sfxVolumeSlider.value    = settings.SfxVolume;
    }

    void RefreshLanguageDropdown()
    {
        if (languageDropdown == null) return;

        _locales = LocalizationSettings.AvailableLocales.Locales.ToList();
        languageDropdown.ClearOptions();
        languageDropdown.AddOptions(_locales.Select(DisplayNameOf).ToList());

        Locale current = LocalizationSettings.SelectedLocale;
        int index = _locales.FindIndex(l => l == current);
        languageDropdown.value = Mathf.Max(0, index);
        languageDropdown.RefreshShownValue();
    }

    static string DisplayNameOf(Locale locale) =>
        locale.Identifier.CultureInfo != null ? locale.Identifier.CultureInfo.NativeName : locale.LocaleName;

    void RefreshDisplayModeDropdown()
    {
        if (displayModeDropdown == null) return;

        displayModeDropdown.ClearOptions();
        displayModeDropdown.AddOptions(new List<string> { "전체화면", "창모드", "테두리 없는 창모드" });

        int index = System.Array.IndexOf(DisplayModeValues, Screen.fullScreenMode);
        displayModeDropdown.value = Mathf.Max(0, index);
        displayModeDropdown.RefreshShownValue();

        if (resolutionDropdown != null)
            resolutionDropdown.interactable = Screen.fullScreenMode != FullScreenMode.FullScreenWindow;
    }

    void RefreshResolutionDropdown()
    {
        if (resolutionDropdown == null) return;

        _resolutions = Screen.resolutions
            .Select(r => new Resolution { width = r.width, height = r.height })
            .GroupBy(r => (r.width, r.height))
            .Select(g => g.First())
            .OrderByDescending(r => r.width * r.height)
            .ToList();

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(_resolutions.Select(r => $"{r.width} x {r.height}").ToList());

        int index = _resolutions.FindIndex(r => r.width == Screen.width && r.height == Screen.height);
        resolutionDropdown.value = Mathf.Max(0, index);
        resolutionDropdown.RefreshShownValue();
    }

    // ── 콜백 ──────────────────────────────────────────────────────

    void OnMasterVolumeChanged(float value)
    {
        if (_refreshing) return;
        GameSettingsManager.Instance?.SetMasterVolume(value);
    }

    void OnBgmVolumeChanged(float value)
    {
        if (_refreshing) return;
        GameSettingsManager.Instance?.SetBgmVolume(value);
    }

    void OnSfxVolumeChanged(float value)
    {
        if (_refreshing) return;
        GameSettingsManager.Instance?.SetSfxVolume(value);
    }

    void OnLanguageChanged(int index)
    {
        if (_refreshing) return;
        if (index < 0 || index >= _locales.Count) return;
        GameSettingsManager.Instance?.SetLocale(_locales[index]);
    }

    void OnDisplayModeChanged(int index)
    {
        if (_refreshing) return;
        if (index < 0 || index >= DisplayModeValues.Length) return;

        FullScreenMode mode = DisplayModeValues[index];
        bool hasResSelection = resolutionDropdown != null && _resolutions.Count > 0
            && resolutionDropdown.value < _resolutions.Count;

        int width  = hasResSelection ? _resolutions[resolutionDropdown.value].width  : Screen.width;
        int height = hasResSelection ? _resolutions[resolutionDropdown.value].height : Screen.height;

        GameSettingsManager.Instance?.ApplyDisplay(width, height, mode);

        if (resolutionDropdown != null)
            resolutionDropdown.interactable = mode != FullScreenMode.FullScreenWindow;
    }

    void OnResolutionChanged(int index)
    {
        if (_refreshing) return;
        if (index < 0 || index >= _resolutions.Count) return;

        Resolution res = _resolutions[index];
        GameSettingsManager.Instance?.ApplyDisplay(res.width, res.height, Screen.fullScreenMode);
    }
}
