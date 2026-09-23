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
/// - micVolumeSlider     : Slider (0~2 = 0~200%, 미연결이면 Row_MicVolume 자동 탐색)
/// - languageDropdown    : TMP_Dropdown — LocalizationSettings.AvailableLocales 기반 자동 채움
/// - displayModeDropdown : TMP_Dropdown — 전체화면 / 창모드 / 테두리없는 창모드 (고정 3항목, 자동 채움)
/// - resolutionDropdown  : TMP_Dropdown — GameSettingsManager.GetSelectableResolutions 기반 자동 채움
///   (화면모드에 따라 목록이 달라짐 — 테두리없는 창모드에선 네이티브 한 줄 + 비활성)
/// - displayModeExclusiveLabel / WindowedLabel / BorderlessLabel : LocalizedString — 화면모드 3항목
///   라벨의 String Table 엔트리 연결(OXQuizManager와 동일 패턴). 미연결 시 한국어 기본값 폴백.
/// - chatFontSizeSlider : Slider — min/max는 GameSettingsManager.Min/MaxChatFontSize와 일치시킬 것
///   (Inspector에서 Slider의 minValue/maxValue를 10~24로 설정).
/// - digitCheerToggle   : Toggle — "T키로 응원하기". 미연결이면 설정 UI만 없음(기본 OFF 유지).
/// - tipToggle          : Toggle — "Tip 항상 표시"(ON = 본문 항상 표시, OFF = Tab 홀드 중에만). 미연결이면 설정 UI만 없음(기본 OFF 유지).
/// - mouseSensitivitySlider : Slider — 마우스(카메라 회전) 감도 배율. min/max는
///   GameSettingsManager.Min/MaxMouseSensitivity(0.1~2)와 일치시킬 것. ThirdPersonCamera가
///   pull 방식으로 매 프레임 반영(별도 push 이벤트 불필요).
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
    [Tooltip("송신 게인 0~2(200%). Inspector 미연결이면 Row_MicVolume/Slider 를 런타임에 찾음.")]
    [SerializeField] private Slider micVolumeSlider;
    [Tooltip("Dissonance.GetMicrophoneDevices() 기반 자동 채움. 첫 항목은 '시스템 기본'.")]
    [SerializeField] private TMP_Dropdown micDeviceDropdown;

    [Header("응원")]
    [Tooltip("T키 = 팀 응원 대체 입력. 기본 OFF. 체크박스 오브젝트는 씬에서 연결.")]
    [SerializeField] private Toggle digitCheerToggle;

    [Header("Tip")]
    [Tooltip("인게임 Tip 본문 항상 표시. 기본 OFF (OFF면 Tab 홀드 중에만 본문 표시).")]
    [SerializeField] private Toggle tipToggle;

    [Header("채팅")]
    [Tooltip("인게임 채팅 글자 크기. Slider의 minValue/maxValue를 " +
             "GameSettingsManager.MinChatFontSize~MaxChatFontSize(10~24)로 맞춰서 배치할 것.")]
    [SerializeField] private Slider chatFontSizeSlider;

    [Header("카메라")]
    [Tooltip("마우스(카메라 회전) 감도 배율. Slider의 minValue/maxValue를 " +
             "GameSettingsManager.MinMouseSensitivity~MaxMouseSensitivity(0.1~2)로 맞춰서 배치할 것.")]
    [SerializeField] private Slider mouseSensitivitySlider;

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

    /// <summary>
    /// 화면모드/해상도의 실제 상태. `Screen.fullScreenMode`를 직접 읽지 않는 이유는
    /// GameSettingsManager.CurrentDisplayMode 주석 참고(SetResolution이 프레임 끝 지연 반영이라
    /// 방금 적용한 값이 아직 Screen에 안 올라와 있음).
    /// </summary>
    static FullScreenMode CurrentMode =>
        GameSettingsManager.Instance != null
            ? GameSettingsManager.Instance.CurrentDisplayMode
            : Screen.fullScreenMode;

    static Resolution CurrentRes =>
        GameSettingsManager.Instance != null
            ? GameSettingsManager.Instance.CurrentResolution
            : new Resolution { width = Screen.width, height = Screen.height };

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
        if (ResolveMicVolumeSlider() != null) micVolumeSlider.onValueChanged.AddListener(OnMicVolumeChanged);
        if (micDeviceDropdown   != null) micDeviceDropdown.onValueChanged.AddListener(OnMicDeviceChanged);
        if (digitCheerToggle    != null) digitCheerToggle.onValueChanged.AddListener(OnDigitCheerChanged);
        if (tipToggle           != null) tipToggle.onValueChanged.AddListener(OnTipChanged);
        if (chatFontSizeSlider  != null) chatFontSizeSlider.onValueChanged.AddListener(OnChatFontSizeChanged);
        if (mouseSensitivitySlider != null) mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;

        if (GameSettingsManager.Instance != null)
            GameSettingsManager.Instance.MicMutedChanged += OnMicMutedChangedExternal;
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
        if (micVolumeSlider     != null) micVolumeSlider.onValueChanged.RemoveListener(OnMicVolumeChanged);
        if (micDeviceDropdown   != null) micDeviceDropdown.onValueChanged.RemoveListener(OnMicDeviceChanged);
        if (digitCheerToggle    != null) digitCheerToggle.onValueChanged.RemoveListener(OnDigitCheerChanged);
        if (tipToggle           != null) tipToggle.onValueChanged.RemoveListener(OnTipChanged);
        if (chatFontSizeSlider  != null) chatFontSizeSlider.onValueChanged.RemoveListener(OnChatFontSizeChanged);
        if (mouseSensitivitySlider != null) mouseSensitivitySlider.onValueChanged.RemoveListener(OnMouseSensitivityChanged);
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;

        if (GameSettingsManager.Instance != null)
            GameSettingsManager.Instance.MicMutedChanged -= OnMicMutedChangedExternal;
    }

    /// <summary>
    /// 인게임 M키 단축키(MicMuteHotkeyUI) 등 이 패널 밖에서 mute 상태가 바뀌었을 때, 패널이
    /// 열려 있는 채로도 토글 체크박스가 즉시 따라 움직이도록 함. WithRefreshGuard로 감싸서
    /// 이 갱신이 OnMicMuteChanged를 다시 호출해 GameSettingsManager에 되먹임하지 않게 막는다.
    /// </summary>
    void OnMicMutedChangedExternal(bool muted) =>
        WithRefreshGuard(() => { if (micMuteToggle != null) micMuteToggle.isOn = muted; });

    /// <summary>
    /// 언어 드롭다운에서 즉시 언어를 바꿨을 때, 패널을 닫았다 열지 않아도
    /// 화면모드 드롭다운 옵션 라벨("Fullscreen" 등, 코드로 채우는 문자열)이 즉시 갱신되도록 함.
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
        RefreshDigitCheerToggle();
        RefreshTipToggle();
        RefreshChatFontSizeSlider();
        RefreshMouseSensitivitySlider();
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

        _locales = LocalizationSettings.AvailableLocales.Locales
            .Where(LocaleDisplayName.IsOfferedInSettings)
            .ToList();
        languageDropdown.ClearOptions();
        languageDropdown.AddOptions(_locales.Select(LocaleDisplayName.Of).ToList());

        Locale current = LocalizationSettings.SelectedLocale;
        int index = _locales.FindIndex(l => l == current);
        languageDropdown.value = Mathf.Max(0, index);
        languageDropdown.RefreshShownValue();
    }

    /// <summary>
    /// String Table 엔트리가 아직 연결 안 됐으면(IsEmpty) 한국어 기본값으로 폴백.
    /// 키가 연결돼 있어도(IsEmpty=false) LocalizationSettings 테이블 로드가 이 호출 시점에
    /// 아직 안 끝났으면 GetLocalizedString()이 빈 문자열을 그대로 반환하는 레이스가 있음
    /// (에디터 Edit 모드 실측 확인 — SelectedLocaleAsync.Result가 null인 상태에서 항상 "" 반환).
    /// 이 경우도 폴백해야 드롭다운 항목이 빈 텍스트로 보이지 않음.
    /// </summary>
    static string LocalizedOrFallback(LocalizedString localized, string fallback)
    {
        if (localized == null || localized.IsEmpty) return fallback;
        string value = localized.GetLocalizedString();
        return string.IsNullOrEmpty(value) ? fallback : value;
    }

    void RefreshDisplayModeDropdown()
    {
        if (displayModeDropdown == null) return;

        displayModeDropdown.ClearOptions();
        displayModeDropdown.AddOptions(new List<string>
        {
            LocalizedOrFallback(displayModeExclusiveLabel, "Fullscreen"),
            LocalizedOrFallback(displayModeWindowedLabel, "Windowed"),
            LocalizedOrFallback(displayModeBorderlessLabel, "Borderless Window"),
        });

        int index = System.Array.IndexOf(DisplayModeValues, CurrentMode);
        displayModeDropdown.value = Mathf.Max(0, index);
        displayModeDropdown.RefreshShownValue();

        if (resolutionDropdown != null)
            resolutionDropdown.interactable = CurrentMode != FullScreenMode.FullScreenWindow;
    }

    /// <summary>
    /// 순수 읽기 — 목록/선택값만 UI에 반영하고 실제 화면 상태(Screen.SetResolution 등)는
    /// 절대 건드리지 않음(Refresh 함수가 부작용을 가지면 패널을 여는 것만으로 화면이
    /// 바뀌는 사고가 남 — 과거 실측 버그 이력).
    ///
    /// 목록 자체는 GameSettingsManager.GetSelectableResolutions가 SSOT — 화면모드에 따라 내용이
    /// 달라진다(창모드는 네이티브 제외, 테두리없는 창모드는 네이티브 한 줄). 여기서는 그 결과를
    /// 라벨로 바꿔 꽂기만 함.
    /// </summary>
    void RefreshResolutionDropdown()
    {
        if (resolutionDropdown == null) return;

        Resolution current = CurrentRes;
        _resolutions = GameSettingsManager.Instance != null
            ? GameSettingsManager.Instance.GetSelectableResolutions(CurrentMode)
            : new List<Resolution> { current };

        // 실제 적용된 해상도가 목록에 없으면(옛 저장값, 외부 요인으로 바뀐 경우 등) 그 값을 끼워넣는다.
        // 예전엔 index -1을 Mathf.Max(0, ...)로 0번(목록 최고 해상도)에 붙여서, 드롭다운이 실제 화면과
        // 다른 값을 가리키는 채로 멀쩡해 보이는 버그가 있었음.
        int index = _resolutions.FindIndex(r => r.width == current.width && r.height == current.height);
        if (index < 0)
        {
            _resolutions.Add(current);
            _resolutions.Sort((a, b) => (b.width * b.height).CompareTo(a.width * a.height));
            index = _resolutions.FindIndex(r => r.width == current.width && r.height == current.height);
        }

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(_resolutions.Select(r => $"{r.width} x {r.height}").ToList());
        resolutionDropdown.value = index;
        resolutionDropdown.RefreshShownValue();
    }

    void RefreshMicRow()
    {
        GameSettingsManager settings = GameSettingsManager.Instance;
        if (settings != null && micMuteToggle != null)
            micMuteToggle.isOn = settings.MicMuted;

        Slider volumeSlider = ResolveMicVolumeSlider();
        if (volumeSlider != null)
        {
            volumeSlider.interactable = true;
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 2f;
            if (settings != null) volumeSlider.value = settings.MicVolume;
        }

        if (micDeviceDropdown == null) return;

        _micDevices.Clear();
        DissonanceComms comms = DissonanceComms.GetSingleton();
        if (comms != null) comms.GetMicrophoneDevices(_micDevices);
        else _micDevices.AddRange(Microphone.devices);

        List<string> labels = new List<string> { "System Default" };
        labels.AddRange(_micDevices);
        micDeviceDropdown.ClearOptions();
        micDeviceDropdown.AddOptions(labels);

        string current = settings != null ? settings.MicDeviceName : "";
        int index = string.IsNullOrEmpty(current) ? 0 : _micDevices.IndexOf(current) + 1;
        micDeviceDropdown.value = Mathf.Max(0, index);
        micDeviceDropdown.RefreshShownValue();
    }

    void RefreshDigitCheerToggle()
    {
        GameSettingsManager settings = GameSettingsManager.Instance;
        if (settings != null && digitCheerToggle != null)
            digitCheerToggle.isOn = settings.DigitCheerEnabled;
    }

    void RefreshTipToggle()
    {
        GameSettingsManager settings = GameSettingsManager.Instance;
        if (settings != null && tipToggle != null)
            tipToggle.isOn = settings.TipAlwaysShow;
    }

    void RefreshChatFontSizeSlider()
    {
        if (chatFontSizeSlider == null) return;

        chatFontSizeSlider.minValue = GameSettingsManager.MinChatFontSize;
        chatFontSizeSlider.maxValue = GameSettingsManager.MaxChatFontSize;

        GameSettingsManager settings = GameSettingsManager.Instance;
        if (settings != null) chatFontSizeSlider.value = settings.ChatFontSize;
    }

    void RefreshMouseSensitivitySlider()
    {
        if (mouseSensitivitySlider == null) return;

        mouseSensitivitySlider.minValue = GameSettingsManager.MinMouseSensitivity;
        mouseSensitivitySlider.maxValue = GameSettingsManager.MaxMouseSensitivity;

        GameSettingsManager settings = GameSettingsManager.Instance;
        if (settings != null) mouseSensitivitySlider.value = settings.MouseSensitivity;
    }

    /// <summary>
    /// Inspector 미연결이어도 기존 Setting_Panel 프리팹의 Row_MicVolume을 찾아 쓴다
    /// (placeholder로 생성된 행이라 serialized 필드가 비어 있음).
    /// </summary>
    Slider ResolveMicVolumeSlider()
    {
        if (micVolumeSlider != null) return micVolumeSlider;

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name != "Row_MicVolume") continue;
            micVolumeSlider = children[i].GetComponentInChildren<Slider>(true);
            break;
        }
        return micVolumeSlider;
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

    /// <summary>
    /// 화면모드 전환. 해상도는 "사용자가 직접 고른 값"(PreferredResolution)을 다시 넘기고,
    /// 모드가 허용하지 않는 값이면 GameSettingsManager가 알아서 내려준다(창모드 → 네이티브 미만,
    /// 테두리없는 창모드 → 네이티브 고정). `rememberResolution: false`인 이유는 이 보정값이
    /// 사용자의 선택으로 기록되면 전체화면 복귀 후에도 낮은 해상도가 눌러앉기 때문.
    /// </summary>
    void OnDisplayModeChanged(int index)
    {
        if (_refreshing) return;
        if (index < 0 || index >= DisplayModeValues.Length) return;

        GameSettingsManager settings = GameSettingsManager.Instance;
        if (settings == null) return;

        FullScreenMode mode = DisplayModeValues[index];
        Resolution preferred = settings.PreferredResolution;
        settings.ApplyDisplay(preferred.width, preferred.height, mode, rememberResolution: false);

        // 모드에 따라 목록 내용과 선택값이 통째로 달라지므로 재구성.
        WithRefreshGuard(() =>
        {
            RefreshResolutionDropdown();
            if (resolutionDropdown != null)
                resolutionDropdown.interactable = mode != FullScreenMode.FullScreenWindow;
        });
    }

    void OnResolutionChanged(int index)
    {
        if (_refreshing) return;
        if (index < 0 || index >= _resolutions.Count) return;

        Resolution res = _resolutions[index];
        GameSettingsManager.Instance?.ApplyDisplay(res.width, res.height, CurrentMode);
    }

    void OnMicMuteChanged(bool value)
    {
        if (_refreshing) return;
        GameSettingsManager.Instance?.SetMicMuted(value);
    }

    void OnDigitCheerChanged(bool value)
    {
        if (_refreshing) return;
        GameSettingsManager.Instance?.SetDigitCheerEnabled(value);
    }

    void OnTipChanged(bool value)
    {
        if (_refreshing) return;
        GameSettingsManager.Instance?.SetTipAlwaysShow(value);
    }

    void OnMicVolumeChanged(float value)
    {
        if (_refreshing) return;
        GameSettingsManager.Instance?.SetMicVolume(value);
    }

    void OnMicDeviceChanged(int index)
    {
        if (_refreshing) return;
        // index 0 = "시스템 기본" → 빈 문자열
        string device = index <= 0 || index - 1 >= _micDevices.Count ? "" : _micDevices[index - 1];
        GameSettingsManager.Instance?.SetMicDevice(device);
    }

    void OnChatFontSizeChanged(float value)
    {
        if (_refreshing) return;
        GameSettingsManager.Instance?.SetChatFontSize(value);
    }

    void OnMouseSensitivityChanged(float value)
    {
        if (_refreshing) return;
        GameSettingsManager.Instance?.SetMouseSensitivity(value);
    }

    /// <summary>"기본값" 버튼(Btn_Reset)의 onClick에 연결. 기본값 적용 후 UI를 새로 반영.</summary>
    public void OnClickReset()
    {
        GameSettingsManager.Instance?.ResetToDefaults();
        RefreshAll();
    }

    /// <summary>
    /// 닫기(X) 버튼 OnClick에 연결. 패널 자신을 끔 —
    /// Title/Lobby/ESC 어디서든 같은 Prefab 인스턴스로 재사용 가능(컨트롤러별 닫기 메서드 불필요).
    /// </summary>
    public void OnClickClose()
    {
        gameObject.SetActive(false);
    }
}
