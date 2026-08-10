using System.Collections.Generic;
using System.Linq;
using Dissonance;
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
/// - displayModeExclusiveLabel / WindowedLabel / BorderlessLabel : LocalizedString — 화면모드 3항목
///   라벨의 String Table 엔트리 연결(OXQuizManager와 동일 패턴). 미연결 시 한국어 기본값 폴백.
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

    [Header("마이크")]
    [Tooltip("Dissonance IsMuted 토글 — 네트워크 전송만 끊김, 응원 키워드 감지엔 영향 없음.")]
    [SerializeField] private Toggle micMuteToggle;
    [Tooltip("Dissonance.GetMicrophoneDevices() 기반 자동 채움. 첫 항목은 '시스템 기본'.")]
    [SerializeField] private TMP_Dropdown micDeviceDropdown;

    [Header("화면모드 라벨 (Localization)")]
    [Tooltip("String Table 엔트리 연결용 — 문자열 직접 입력 아님(OXQuizManager와 동일 패턴). " +
             "미연결 상태면 한국어 기본값으로 폴백.")]
    [SerializeField] private LocalizedString displayModeExclusiveLabel;
    [SerializeField] private LocalizedString displayModeWindowedLabel;
    [SerializeField] private LocalizedString displayModeBorderlessLabel;

    static readonly FullScreenMode[] DisplayModeValues =
    {
        FullScreenMode.ExclusiveFullScreen,
        FullScreenMode.Windowed,
        FullScreenMode.FullScreenWindow,
    };

    List<Locale> _locales = new List<Locale>();
    List<Resolution> _resolutions = new List<Resolution>();
    List<string> _micDevices = new List<string>();

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
        if (micMuteToggle       != null) micMuteToggle.onValueChanged.AddListener(OnMicMuteChanged);
        if (micDeviceDropdown   != null) micDeviceDropdown.onValueChanged.AddListener(OnMicDeviceChanged);
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
    }

    void OnDisable()
    {
        if (masterVolumeSlider  != null) masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
        if (bgmVolumeSlider     != null) bgmVolumeSlider.onValueChanged.RemoveListener(OnBgmVolumeChanged);
        if (sfxVolumeSlider     != null) sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
        if (languageDropdown    != null) languageDropdown.onValueChanged.RemoveListener(OnLanguageChanged);
        if (displayModeDropdown != null) displayModeDropdown.onValueChanged.RemoveListener(OnDisplayModeChanged);
        if (resolutionDropdown  != null) resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);
        if (micMuteToggle       != null) micMuteToggle.onValueChanged.RemoveListener(OnMicMuteChanged);
        if (micDeviceDropdown   != null) micDeviceDropdown.onValueChanged.RemoveListener(OnMicDeviceChanged);
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
    }

    /// <summary>
    /// 언어 드롭다운에서 즉시 언어를 바꿨을 때, 패널을 닫았다 열지 않아도
    /// 화면모드 드롭다운 옵션 라벨("전체화면" 등, 코드로 채우는 문자열)이 즉시 갱신되도록 함.
    /// </summary>
    void OnSelectedLocaleChanged(Locale locale) => WithRefreshGuard(RefreshDisplayModeDropdown);

    // ── 새로고침 ──────────────────────────────────────────────────

    void RefreshAll() => WithRefreshGuard(() =>
    {
        RefreshVolumeSliders();
        RefreshLanguageDropdown();
        RefreshDisplayModeDropdown();
        RefreshResolutionDropdown();
        RefreshMicRow();
    });

    /// <summary>
    /// UI 값을 코드에서 갱신하는 동안 onValueChanged 콜백이 재귀적으로
    /// GameSettingsManager에 다시 쓰지 않도록 막는 공용 가드.
    /// </summary>
    void WithRefreshGuard(System.Action action)
    {
        _refreshing = true;
        action();
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

    /// <summary>String Table 엔트리가 아직 연결 안 됐으면(IsEmpty) 한국어 기본값으로 폴백.</summary>
    static string LocalizedOrFallback(LocalizedString localized, string fallback) =>
        localized != null && !localized.IsEmpty ? localized.GetLocalizedString() : fallback;

    void RefreshDisplayModeDropdown()
    {
        if (displayModeDropdown == null) return;

        displayModeDropdown.ClearOptions();
        displayModeDropdown.AddOptions(new List<string>
        {
            LocalizedOrFallback(displayModeExclusiveLabel, "전체화면"),
            LocalizedOrFallback(displayModeWindowedLabel, "창모드"),
            LocalizedOrFallback(displayModeBorderlessLabel, "테두리 없는 창모드"),
        });

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

    void RefreshMicRow()
    {
        GameSettingsManager settings = GameSettingsManager.Instance;
        if (settings != null && micMuteToggle != null)
            micMuteToggle.isOn = settings.MicMuted;

        if (micDeviceDropdown == null) return;

        _micDevices.Clear();
        DissonanceComms comms = DissonanceComms.GetSingleton();
        if (comms != null) comms.GetMicrophoneDevices(_micDevices);
        else _micDevices.AddRange(Microphone.devices);

        List<string> labels = new List<string> { "시스템 기본" };
        labels.AddRange(_micDevices);
        micDeviceDropdown.ClearOptions();
        micDeviceDropdown.AddOptions(labels);

        string current = settings != null ? settings.MicDeviceName : "";
        int index = string.IsNullOrEmpty(current) ? 0 : _micDevices.IndexOf(current) + 1;
        micDeviceDropdown.value = Mathf.Max(0, index);
        micDeviceDropdown.RefreshShownValue();
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

    void OnMicMuteChanged(bool value)
    {
        if (_refreshing) return;
        GameSettingsManager.Instance?.SetMicMuted(value);
    }

    void OnMicDeviceChanged(int index)
    {
        if (_refreshing) return;
        // index 0 = "시스템 기본" → 빈 문자열
        string device = index <= 0 || index - 1 >= _micDevices.Count ? "" : _micDevices[index - 1];
        GameSettingsManager.Instance?.SetMicDevice(device);
    }

    /// <summary>"초기화" 버튼(Btn_Reset)의 onClick에 연결. 기본값 적용 후 UI를 새로 반영.</summary>
    public void OnClickReset()
    {
        GameSettingsManager.Instance?.ResetToDefaults();
        RefreshAll();
    }
}
