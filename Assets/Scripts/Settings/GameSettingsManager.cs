using System;
using System.Collections;
using Dissonance;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// 게임 설정(볼륨 · 화면 · 언어 · 채팅) SSOT. 싱글턴, DontDestroyOnLoad.
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
    const string KeyMicMuted     = "Settings.MicMuted";
    const string KeyMicDevice    = "Settings.MicDevice";
    const string KeyChatFontSize = "Settings.ChatFontSize";

    /// <summary>채팅 글자 크기 슬라이더 min/max — OptionsMenuController Slider Inspector 값과 맞춰야 함.</summary>
    public const float MinChatFontSize = 10f;
    public const float MaxChatFontSize = 24f;

    [Header("기본값(Reset) 값")]
    [Tooltip("옵션 메뉴 '기본값' 버튼을 누르면 이 값들로 되돌아감. 최초 실행 기본값이기도 함.")]
    [Range(0f, 1f)] [SerializeField] float defaultMasterVolume = 1f;
    [Range(0f, 1f)] [SerializeField] float defaultBgmVolume    = 1f;
    [Range(0f, 1f)] [SerializeField] float defaultSfxVolume    = 1f;
    [Range(MinChatFontSize, MaxChatFontSize)] [SerializeField] float defaultChatFontSize = 14f;

    public float  MasterVolume  { get; private set; } = 1f;
    public float  BgmVolume     { get; private set; } = 1f;
    public float  SfxVolume     { get; private set; } = 1f;
    public bool   MicMuted      { get; private set; }
    /// <summary>빈 문자열 = 시스템 기본 마이크(Dissonance/Microphone API의 null과 동일 취급).</summary>
    public string MicDeviceName { get; private set; } = "";
    public float  ChatFontSize  { get; private set; } = 14f;

    /// <summary>
    /// 채팅 글자 크기가 바뀔 때 발생(옵션 슬라이더 조작 + 기본값 리셋 공통 경로).
    /// InGameChatUI가 구독해서 이미 떠 있는 채팅 메시지들에도 즉시 반영함.
    ///
    /// [§1 pull 원칙의 유일한 예외 — 왜 push인가]
    /// 볼륨(§1)이 push를 금지한 이유는 "초기값 적용 시점"이 Awake 순서 비결정성에 걸려있기
    /// 때문(다른 오브젝트의 Awake가 아직 안 끝났을 수 있음). 이 이벤트는 그 케이스가 아님 —
    /// 양쪽 다 초기화가 끝난 뒤 사용자가 슬라이더를 조작하는 "런타임 중" 에만 발동하므로 그
    /// race가 없음. 그리고 pull만으로는 이미 생성된 TMP 메시지 오브젝트의 글자 크기를 바꿀
    /// 방법이 없어서(누가 "다시 읽어라"라고 알려줘야 함) push가 구조적으로 필요함 — 새 메시지는
    /// InGameChatUI.CurrentFontSize로 여전히 pull(§1과 동일 패턴), 기존 메시지 갱신만 이 이벤트로 push.
    /// </summary>
    public event Action<float> ChatFontSizeChanged;

    /// <summary>
    /// 마이크 mute 상태가 바뀔 때 발생(옵션 패널 토글 + 인게임 M키 단축키 공통 경로).
    /// SetMicMuted를 부르는 쪽이 어디든(OptionsMenuController.OnMicMuteChanged 또는
    /// MicMuteHotkeyUI의 M키 핸들러) 이 이벤트로 다른 쪽 UI에 즉시 반영됨 — §1 pull 원칙의
    /// ChatFontSizeChanged와 동일한 이유(이미 떠 있는 UI를 push로 갱신해야 함)로 예외 허용.
    /// </summary>
    public event Action<bool> MicMutedChanged;

    /// <summary>
    /// 모니터의 진짜 네이티브(최대) 해상도. 저장된 해상도를 적용하기 전(ApplySavedDisplay 호출 전)
    /// Awake에서 딱 한 번만 캡처해서 고정함.
    /// Screen.currentResolution이 아니라 Screen.resolutions 중 최대값을 쓰는 이유: currentResolution은
    /// "지금 OS가 실제로 떠 있는 해상도"라서 SetResolution(특히 독점 전체화면) 호출 후 그 값 자체가
    /// 바뀌고, 이전 실행 종료 시 OS가 원래 해상도로 복원 못 했으면(비정상 종료 등) 다음 실행에서도
    /// 낮은 값을 그대로 캡처해버림. Screen.resolutions는 모니터/드라이버가 지원하는 디스플레이 모드
    /// 목록이라 현재 OS 상태와 무관하게 항상 모니터의 진짜 최대 해상도가 포함돼 있어 더 안전함.
    /// </summary>
    public Resolution NativeResolution { get; private set; }

    static Resolution QueryNativeResolution()
    {
        Resolution[] resolutions = Screen.resolutions;
        if (resolutions == null || resolutions.Length == 0) return Screen.currentResolution;

        Resolution max = resolutions[0];
        for (int i = 1; i < resolutions.Length; i++)
            if (resolutions[i].width * resolutions[i].height > max.width * max.height)
                max = resolutions[i];
        return max;
    }

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

        MasterVolume  = PlayerPrefs.GetFloat(KeyMasterVolume, defaultMasterVolume);
        BgmVolume     = PlayerPrefs.GetFloat(KeyBgmVolume, defaultBgmVolume);
        SfxVolume     = PlayerPrefs.GetFloat(KeySfxVolume, defaultSfxVolume);
        MicMuted      = PlayerPrefs.GetInt(KeyMicMuted, 0) == 1;
        MicDeviceName = PlayerPrefs.GetString(KeyMicDevice, "");
        ChatFontSize  = PlayerPrefs.GetFloat(KeyChatFontSize, defaultChatFontSize);

        NativeResolution = QueryNativeResolution();
        ApplySavedDisplay();
        StartCoroutine(ApplySavedMicSettingsWhenReady());
    }

    // ── 마이크 ────────────────────────────────────────────────────

    /// <summary>
    /// DissonanceComms는 0.Title 로드 시점에 이미 Start()가 끝나있을 수도, 아닐 수도 있어
    /// (같은 GameObject라도 컴포넌트 순서 비결정적 위험 회피 — §1 pull 원칙과 동일 이유)
    /// GetSingleton()이 준비될 때까지 폴링 후 적용한다(CheerKeywordEngine과 동일 패턴).
    /// </summary>
    IEnumerator ApplySavedMicSettingsWhenReady()
    {
        DissonanceComms comms = null;
        while (comms == null)
        {
            comms = DissonanceComms.GetSingleton();
            yield return null;
        }

        comms.IsMuted = MicMuted;
        if (!string.IsNullOrEmpty(MicDeviceName))
            comms.MicrophoneName = MicDeviceName;
    }

    /// <summary>옵션 메뉴 마이크 음소거 토글에서 호출. 즉시 적용 + 저장.
    /// Dissonance IsMuted는 네트워크 전송(인코더)만 끊고 로컬 캡처는 유지하므로
    /// CheerKeywordEngine의 SubscribeToRecordedAudio 기반 응원 키워드 감지에는 영향 없음.</summary>
    public void SetMicMuted(bool value)
    {
        MicMuted = value;
        PlayerPrefs.SetInt(KeyMicMuted, value ? 1 : 0);

        DissonanceComms comms = DissonanceComms.GetSingleton();
        if (comms != null) comms.IsMuted = value;

        MicMutedChanged?.Invoke(value);
    }

    /// <summary>옵션 메뉴 마이크 입력장치 드롭다운에서 호출. 즉시 적용 + 저장.
    /// deviceName이 비어있으면 시스템 기본 마이크로 되돌림.</summary>
    public void SetMicDevice(string deviceName)
    {
        MicDeviceName = deviceName ?? "";
        PlayerPrefs.SetString(KeyMicDevice, MicDeviceName);

        DissonanceComms comms = DissonanceComms.GetSingleton();
        if (comms != null) comms.MicrophoneName = string.IsNullOrEmpty(MicDeviceName) ? null : MicDeviceName;
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

    // ── 채팅 ──────────────────────────────────────────────────────

    /// <summary>옵션 메뉴 채팅 글자 크기 슬라이더에서 호출. 즉시 적용 + 저장.
    /// InGameChatUI가 ChatFontSizeChanged를 구독해 이미 떠 있는 메시지에도 즉시 반영함.</summary>
    public void SetChatFontSize(float value)
    {
        ChatFontSize = Mathf.Clamp(value, MinChatFontSize, MaxChatFontSize);
        PlayerPrefs.SetFloat(KeyChatFontSize, ChatFontSize);
        ChatFontSizeChanged?.Invoke(ChatFontSize);
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

    // ── 초기화(Reset) ─────────────────────────────────────────────

    /// <summary>
    /// 옵션 메뉴 "기본값" 버튼에서 호출. 볼륨·채팅 글자 크기는 Inspector 기본값, 화면은 현 모니터
    /// 네이티브 해상도 + 전체화면(독점), 언어는 수동 선택 해제 후 Steam/systemLanguage 자동감지로 되돌림.
    /// 밝기는 아직 구현된 설정이 아니라 범위에서 제외(SoundAndSettingsDesign.md §8).
    /// </summary>
    public void ResetToDefaults()
    {
        SetMasterVolume(defaultMasterVolume);
        SetBgmVolume(defaultBgmVolume);
        SetSfxVolume(defaultSfxVolume);
        SetChatFontSize(defaultChatFontSize);

        ApplyDisplay(NativeResolution.width, NativeResolution.height, FullScreenMode.ExclusiveFullScreen);

        PlayerPrefs.DeleteKey(GameLocalizationBootstrap.ManualLocaleOverrideKey);
        GameLocalizationBootstrap.Instance?.ReapplyAutoDetectedLocale();
    }
}
