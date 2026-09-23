using System;
using System.Collections;
using System.Collections.Generic;
using Dissonance;
using Unity.Netcode;
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
    const string KeyMicVolume    = "Settings.MicVolume";
    const string KeyMicDevice    = "Settings.MicDevice";
    const string KeyChatFontSize = "Settings.ChatFontSize";
    const string KeyDigitCheer   = "Settings.DigitCheerEnabled";
    // 구 "Settings.TipEnabled"(표시 여부, 기본 ON)와 의미가 달라 키를 새로 둔다 — 옛 저장값이 "항상 표시"로 새지 않게.
    const string KeyTipAlwaysShow = "Settings.TipAlwaysShow";
    const string KeyMouseSensitivity = "Settings.MouseSensitivity";

    /// <summary>채팅 글자 크기 슬라이더 min/max — OptionsMenuController Slider Inspector 값과 맞춰야 함.</summary>
    public const float MinChatFontSize = 10f;
    public const float MaxChatFontSize = 24f;

    /// <summary>마우스(카메라 회전) 감도 배율 슬라이더 min/max — ThirdPersonCamera의
    /// sensitivityX/sensitivityY(Inspector 기본값)에 곱연산되는 배율. 1.0 = 원래 감도 그대로.</summary>
    public const float MinMouseSensitivity = 0.1f;
    public const float MaxMouseSensitivity = 2f;

    [Header("기본값(Reset) 값")]
    [Tooltip("옵션 메뉴 '기본값' 버튼을 누르면 이 값들로 되돌아감. 최초 실행 기본값이기도 함.")]
    [Range(0f, 1f)] [SerializeField] float defaultMasterVolume = 1f;
    [Range(0f, 1f)] [SerializeField] float defaultBgmVolume    = 1f;
    [Range(0f, 1f)] [SerializeField] float defaultSfxVolume    = 1f;
    [Range(0f, 2f)] [SerializeField] float defaultMicVolume    = 1f;
    [Range(MinChatFontSize, MaxChatFontSize)] [SerializeField] float defaultChatFontSize = 14f;
    [Range(MinMouseSensitivity, MaxMouseSensitivity)] [SerializeField] float defaultMouseSensitivity = 1f;

    public float  MasterVolume  { get; private set; } = 1f;
    public float  BgmVolume     { get; private set; } = 1f;
    public float  SfxVolume     { get; private set; } = 1f;
    public float  MicVolume     { get; private set; } = 1f;
    public bool   MicMuted      { get; private set; }
    /// <summary>빈 문자열 = 시스템 기본 마이크(Dissonance/Microphone API의 null과 동일 취급).</summary>
    public string MicDeviceName { get; private set; } = "";
    public float  ChatFontSize  { get; private set; } = 14f;
    /// <summary>T키 팀 응원(CheerDigitInput). 기본 OFF — 음성이 기본 수단. Options에서 켠다.</summary>
    public bool   DigitCheerEnabled { get; private set; }
    /// <summary>인게임 Tip 본문 항상 표시. 기본 OFF — OFF면 "[Tab] Tip" 헤더만 보이고 Tab 홀드 중에만 본문.
    /// ON이면 Tab과 무관하게 본문이 항상 보인다. 로컬 표시 설정 — 네트워크 무관.</summary>
    public bool   TipAlwaysShow { get; private set; }
    /// <summary>마우스(카메라 회전) 감도 배율. ThirdPersonCamera가 매 프레임 이 값을 pull해서
    /// sensitivityX/sensitivityY(Inspector 기본값)에 곱연산 — §1 pull 원칙과 동일 패턴(push 아님).</summary>
    public float  MouseSensitivity { get; private set; } = 1f;

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
    /// Tip 항상 표시 설정이 바뀔 때 발생(옵션 패널 토글 + 기본값 리셋 공통 경로).
    /// TipUI가 구독해서 이미 떠 있는 Tip 패널을 즉시 숨기거나 다시 보여줌 —
    /// ChatFontSizeChanged와 같은 이유(런타임 중 이미 떠 있는 UI 갱신)로 §1 pull 원칙 예외.
    /// </summary>
    public event Action<bool> TipAlwaysShowChanged;

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

    /// <summary>
    /// 마지막으로 적용한 화면모드 / 해상도. 옵션 UI는 `Screen.fullScreenMode`·`Screen.width`가 아니라
    /// 이 값을 읽는다 — `Screen.SetResolution`은 프레임 끝에 반영되는 지연 호출이라 `ApplyDisplay`
    /// 직후에 Screen을 읽으면 아직 이전 값이 나오고, 드롭다운이 방금 고른 값과 다른 걸 가리키게 됨.
    /// </summary>
    public FullScreenMode CurrentDisplayMode { get; private set; }
    public Resolution CurrentResolution { get; private set; }

    /// <summary>
    /// 사용자가 해상도 드롭다운에서 "직접 고른" 해상도. 화면모드 전환 때문에 강제로 낮아진 값
    /// (창모드로 갈 때 네이티브 → 한 단계 아래)이나 테두리없는 창모드의 네이티브 고정값은 여기에
    /// 기록하지 않는다 — 그래야 창모드를 거쳤다 전체화면으로 돌아왔을 때 낮은 해상도가 눌러앉지 않음.
    /// </summary>
    public Resolution PreferredResolution { get; private set; }

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
        MicVolume     = PlayerPrefs.GetFloat(KeyMicVolume, defaultMicVolume);
        MicMuted      = PlayerPrefs.GetInt(KeyMicMuted, 0) == 1;
        MicDeviceName = PlayerPrefs.GetString(KeyMicDevice, "");
        ChatFontSize  = PlayerPrefs.GetFloat(KeyChatFontSize, defaultChatFontSize);
        DigitCheerEnabled = PlayerPrefs.GetInt(KeyDigitCheer, 0) == 1;
        TipAlwaysShow = PlayerPrefs.GetInt(KeyTipAlwaysShow, 0) == 1;
        MouseSensitivity = PlayerPrefs.GetFloat(KeyMouseSensitivity, defaultMouseSensitivity);

        NativeResolution   = QueryNativeResolution();
        CurrentDisplayMode = Screen.fullScreenMode;
        CurrentResolution  = new Resolution { width = Screen.width, height = Screen.height };
        PreferredResolution = new Resolution
        {
            width  = PlayerPrefs.GetInt(KeyResWidth,  Screen.width),
            height = PlayerPrefs.GetInt(KeyResHeight, Screen.height),
        };
        ApplySavedDisplay();
        StartCoroutine(ApplySavedMicSettingsWhenReady());
    }

    // ── 마이크 ────────────────────────────────────────────────────

    /// <summary>
    /// DissonanceComms는 0.Title 로드 시점에 이미 Start()가 끝나있을 수도, 아닐 수도 있어
    /// (같은 GameObject라도 컴포넌트 순서 비결정적 위험 회피 — §1 pull 원칙과 동일 이유)
    /// GetSingleton()이 준비될 때까지 폴링 후 적용한다(CheerKeywordEngine과 동일 패턴).
    ///
    /// [MicVolume은 여기서 push하지 않음 — 2026-09-10 수정]
    /// 예전엔 여기서도 ApplyMicTransmitVolume()(no-arg)을 불렀다. 하지만 그 오버로드는
    /// NetworkManager.LocalClient.PlayerObject로 트리거를 "다시 찾는" 경로라, 이 코루틴이
    /// 도는 시점(Title 부팅 중, 아직 세션 전)엔 NetworkManager가 리스닝 전이라 항상 no-op
    /// Log만 찍고 아무 일도 안 했다 — 즉 정상 흐름에서는 원래도 죽은 호출이었다. 문제는
    /// NetworkManager가 이례적으로 이미 리스닝 중인데 로컬 플레이어가 아직 스폰 전인
    /// 좁은 틈(예: DevStageHostBootstrap처럼 Title을 건너뛰고 즉석으로 Host를 띄우는 경로)에
    /// 걸리면 no-op이 아니라 LogWarning으로 떨어진다는 것 — 실제로 아무것도 깨지진 않지만
    /// 콘솔에 가짜 경고를 남긴다. MicVolume은 NetworkPlayerSetup.SetupOwner()가 스폰 시점에
    /// 이미 캐시해둔 트리거로 ApplyMicTransmitVolume(trigger)를 확정적으로 호출해 적용하므로
    /// (타이밍 레이스 없음), 여기서의 push는 애초에 불필요했다. IsMuted/MicrophoneName은
    /// 스폰 시점에 재적용되는 경로가 따로 없어서 그대로 둔다.
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

    /// <summary>
    /// 로컬 Owner의 VoiceBroadcastTrigger 송신 게인에 MicVolume을 반영.
    /// Dissonance 로컬 VoicePlayerState.Volume setter는 미지원(에러만 남김)이라
    /// ActivationFader를 쓴다. CheerKeywordEngine 캡처/Vosk 경로에는 영향 없음.
    ///
    /// [옵션 슬라이더 조작 경로 전용 — 스폰 직후엔 쓰지 말 것]
    /// NetworkManager.LocalClient.PlayerObject로 트리거를 다시 찾는다. 이 필드는 NGO
    /// NetworkSpawnManager가 InvokeBehaviourNetworkSpawn() 이후에야 채우므로, Player의
    /// OnNetworkSpawn(NetworkPlayerSetup.SetupOwner) 안에서 호출하면 아직 null이라 no-op된다
    /// (2026-09-01 실측 — Library/PackageCache 소스 대조 확인). 스폰 시점엔 트리거를 이미
    /// 들고 있는 ApplyMicTransmitVolume(VoiceBroadcastTrigger) 오버로드를 쓴다.
    /// </summary>
    public void ApplyMicTransmitVolume()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            // 옵션 슬라이더는 세션 중(인게임)에만 노출되므로 이 분기는 정상 흐름에서 거의 안 탄다 — Warning 아님.
            Debug.Log($"[GameSettingsManager] ApplyMicTransmitVolume(no-arg) no-op — NetworkManager 없음/미리스닝, 아직 세션 전 (MicVolume={MicVolume})");
            return;
        }
        var localClient = NetworkManager.Singleton.LocalClient;
        if (localClient == null || localClient.PlayerObject == null)
        {
            Debug.LogWarning($"[GameSettingsManager] ApplyMicTransmitVolume(no-arg) no-op — LocalClient.PlayerObject 없음 (MicVolume={MicVolume})");
            return;
        }

        var trigger = localClient.PlayerObject.GetComponent<VoiceBroadcastTrigger>();
        if (trigger == null)
        {
            Debug.LogWarning("[GameSettingsManager] ApplyMicTransmitVolume(no-arg) no-op — PlayerObject에 VoiceBroadcastTrigger 없음");
            return;
        }
        ApplyMicTransmitVolume(trigger);
    }

    /// <summary>
    /// 위와 동일한 적용 로직이지만 트리거를 직접 받는다 — NetworkPlayerSetup.SetupOwner()가
    /// OnNetworkSpawn 시점에 이미 캐시해둔 자기 자신의 VoiceBroadcastTrigger를 넘겨 호출.
    /// NetworkManager.LocalClient.PlayerObject 타이밍 문제(위 설명)를 완전히 우회한다.
    /// </summary>
    public void ApplyMicTransmitVolume(VoiceBroadcastTrigger trigger)
    {
        if (trigger == null)
        {
            Debug.LogWarning($"[GameSettingsManager] ApplyMicTransmitVolume(trigger) no-op — trigger null (MicVolume={MicVolume})");
            return;
        }
        trigger.ActivationFader.Volume = MicVolume;
        Debug.Log($"[GameSettingsManager] ApplyMicTransmitVolume 적용 — MicVolume={MicVolume} → trigger={trigger.name} ActivationFader.Volume={trigger.ActivationFader.Volume}");
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

    /// <summary>옵션 메뉴 "T키로 응원하기" 토글에서 호출. 즉시 적용 + 저장. 기본 OFF.</summary>
    public void SetDigitCheerEnabled(bool value)
    {
        DigitCheerEnabled = value;
        PlayerPrefs.SetInt(KeyDigitCheer, value ? 1 : 0);
    }

    /// <summary>옵션 메뉴 "Tip 항상 표시" 토글에서 호출(ON = 본문 항상 표시). 즉시 적용 + 저장. 기본 OFF.</summary>
    public void SetTipAlwaysShow(bool value)
    {
        TipAlwaysShow = value;
        PlayerPrefs.SetInt(KeyTipAlwaysShow, value ? 1 : 0);
        TipAlwaysShowChanged?.Invoke(value);
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

    /// <summary>옵션 메뉴 마이크 볼륨 슬라이더에서 호출. 즉시 적용 + 저장.
    /// 로컬 송신 게인(VoiceBroadcastTrigger.ActivationFader)만 바꾸고, 응원 키워드 감지에는 영향 없음.
    /// 범위 0~2(200%) — Dissonance 채널 진폭 배율(`ChannelProperties.AmplitudeMultiplier`,
    /// `RoomChannel`/`PlayerChannel`)이 원래 0~2를 지원하는 프로토콜이라 클램프만 늘림(플러그인
    /// 미수정). 0이면 채널 진폭이 정확히 0이 되어 수신측 디코더가 프레임을 하드 클리어하므로
    /// 이미 완전 무음(별도 IsMuted 결합 불필요, 2026-09-10 확인).</summary>
    public void SetMicVolume(float value)
    {
        MicVolume = Mathf.Clamp(value, 0f, 2f);
        PlayerPrefs.SetFloat(KeyMicVolume, MicVolume);
        Debug.Log($"[GameSettingsManager] SetMicVolume 호출 — value={value} → MicVolume={MicVolume}");
        ApplyMicTransmitVolume();
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

    // ── 카메라 ────────────────────────────────────────────────────

    /// <summary>옵션 메뉴 마우스 감도 슬라이더에서 호출. 즉시 적용 + 저장.
    /// ThirdPersonCamera가 pull 방식(§1과 동일 원칙)으로 매 프레임 이 값을 읽어 반영하므로
    /// 여기서는 저장만 하고 별도 이벤트/push가 필요 없음.</summary>
    public void SetMouseSensitivity(float value)
    {
        MouseSensitivity = Mathf.Clamp(value, MinMouseSensitivity, MaxMouseSensitivity);
        PlayerPrefs.SetFloat(KeyMouseSensitivity, MouseSensitivity);
    }

    // ── 화면 ──────────────────────────────────────────────────────

    /// <summary>
    /// 해상도 드롭다운 후보 목록의 원본(SSOT). `Screen.resolutions`를 목록으로 쓰지 않는 이유:
    /// 드라이버가 `1176x664` / `1440x1080` / `1600x1024` / `2048x1536` / `1920x2160`(세로 분할) /
    /// `720x576`(PAL) 같은 레거시·TV 모드까지 전부 보고해서(4K 모니터 실측 30종) 게임 옵션으로는
    /// 쓸 수 없는 목록이 나옴 — 2026-09-23 사용자 확정으로 "흔히 쓰이는 해상도"만 큐레이션해서 쓰고,
    /// `Screen.resolutions`는 <see cref="QueryNativeResolution"/>(네이티브 판정)에만 남겨둔다.
    /// 화면비별로 묶어둔 건 <see cref="GetSelectableResolutions"/>가 모니터 화면비와 다른 항목을
    /// 걸러내기 때문(4K 16:9 기준 6종만 남음).
    /// </summary>
    static readonly (int w, int h)[] CatalogResolutions =
    {
        // 16:9
        (3840, 2160), (2560, 1440), (1920, 1080), (1600, 900), (1366, 768), (1280, 720),
        // 16:10
        (2560, 1600), (1920, 1200), (1680, 1050), (1440, 900), (1280, 800),
        // 21:9
        (3440, 1440), (2560, 1080),
        // 4:3 — 16:9/16:10 모니터에선 화면비 필터에 걸려 안 보이고, 구형 모니터용 폴백으로만 의미 있음.
        (1600, 1200), (1280, 960), (1024, 768),
    };

    /// <summary>모니터 화면비와 같은 것으로 취급할 오차. 1366x768(1.77865) vs 16:9(1.77778)를 같게 보려고 둠.</summary>
    const float AspectTolerance = 0.02f;

    /// <summary>
    /// 현재 모니터 + 주어진 화면모드에서 고를 수 있는 해상도 목록(픽셀 수 내림차순, 매 호출 새 List).
    ///
    /// - 네이티브보다 큰 항목, 모니터 화면비와 다른 항목 제외(2026-09-23 사용자 확정)
    /// - 네이티브 해상도는 카탈로그에 없는 특이 모니터여도 항상 포함
    /// - **창모드는 네이티브 이상을 제외**: 네이티브 크기 창은 타이틀바가 화면 밖으로 밀려 전체화면과
    ///   구분이 안 돼 "창모드로 바꿔도 창모드가 아닌" 상태가 됨(사용자 실측 보고). 예전 코드는 이걸
    ///   "Windows 특성이라 코드로 해결 불가"로 보고 사용자가 직접 해상도를 낮추게 뒀는데, 목록에서
    ///   빼버리면 그냥 해결되는 문제라 방침을 뒤집음.
    /// - 테두리없는 창모드는 정의상 네이티브 고정이라 목록도 한 줄(드롭다운은 비활성 상태로 그 값만 표시)
    ///
    /// 화면비 필터 결과가 2개 미만이면(21:9처럼 카탈로그에 단계가 거의 없는 비율) 필터를 풀어
    /// 카탈로그 전체로 폴백하고 — 고를 게 1개뿐인 드롭다운은 없느니만 못하므로 —
    /// 그래도 비면(네이티브가 카탈로그 최소보다 작은 초소형 화면) 창모드 제약까지 풀어 네이티브를 허용한다.
    /// </summary>
    public List<Resolution> GetSelectableResolutions(FullScreenMode mode)
    {
        if (mode == FullScreenMode.FullScreenWindow)
            return new List<Resolution> { NativeResolution };

        List<Resolution> list = BuildResolutionList(mode, matchAspect: true);
        if (list.Count < 2) list = BuildResolutionList(mode, matchAspect: false);
        if (list.Count == 0) list = BuildResolutionList(FullScreenMode.ExclusiveFullScreen, matchAspect: false);
        return list;
    }

    List<Resolution> BuildResolutionList(FullScreenMode mode, bool matchAspect)
    {
        Resolution native = NativeResolution;
        float nativeAspect = (float)native.width / Mathf.Max(1, native.height);
        bool windowed = mode == FullScreenMode.Windowed;
        var list = new List<Resolution>();

        void TryAdd(int w, int h)
        {
            if (w > native.width || h > native.height) return;
            if (windowed && (w >= native.width || h >= native.height)) return;
            if (matchAspect && Mathf.Abs((float)w / Mathf.Max(1, h) - nativeAspect) > AspectTolerance) return;

            foreach (Resolution existing in list)
                if (existing.width == w && existing.height == h) return;

            list.Add(new Resolution { width = w, height = h });
        }

        TryAdd(native.width, native.height);
        foreach ((int w, int h) in CatalogResolutions) TryAdd(w, h);

        list.Sort((a, b) => (b.width * b.height).CompareTo(a.width * a.height));
        return list;
    }

    /// <summary>
    /// 화면모드가 강제하는 해상도 보정. 목록에 있는 값이면 그대로, 아니면 그보다 작은 것 중 가장 큰 것으로
    /// 내린다. "창모드 + 네이티브"(= 사실상 전체화면)나 옛 버전이 저장해둔 값이 여기서 교정됨.
    /// </summary>
    public Resolution ClampForMode(Resolution res, FullScreenMode mode)
    {
        List<Resolution> options = GetSelectableResolutions(mode);
        if (options.Count == 0) return NativeResolution;

        foreach (Resolution r in options)
            if (r.width == res.width && r.height == res.height) return r;

        // options는 내림차순이라 조건을 만족하는 첫 항목이 "요청값 이하 중 가장 큰 것".
        foreach (Resolution r in options)
            if (r.width <= res.width && r.height <= res.height) return r;

        return options[options.Count - 1];
    }

    void ApplySavedDisplay()
    {
        // 저장된 적 없으면(최초 실행) Unity/Player Settings 기본값을 그대로 둠 — 불필요한 강제 전환 방지.
        if (!PlayerPrefs.HasKey(KeyResWidth)) return;

        FullScreenMode mode = (FullScreenMode)PlayerPrefs.GetInt(KeyDisplayMode, (int)Screen.fullScreenMode);
        ApplyInternal(ClampForMode(PreferredResolution, mode), mode);
    }

    /// <summary>
    /// 옵션 메뉴에서 해상도/화면모드 변경 시 호출. 화면모드 제약(<see cref="ClampForMode"/>)을 적용해
    /// 즉시 반영 + 저장.
    /// </summary>
    /// <param name="rememberResolution">
    /// 사용자가 해상도를 직접 고른 경우에만 true. 화면모드 드롭다운에서 넘어온 호출은 false여야
    /// <see cref="PreferredResolution"/>이 유지되어, 창모드용으로 낮아진 해상도가 전체화면 복귀 후에도
    /// 눌러앉는 일이 없음.
    /// </param>
    public void ApplyDisplay(int width, int height, FullScreenMode mode, bool rememberResolution = true)
    {
        var requested = new Resolution { width = width, height = height };

        // 테두리없는 창모드는 해상도 선택지 자체가 없으므로(네이티브 고정) 사용자의 선택으로 치지 않는다.
        if (rememberResolution && mode != FullScreenMode.FullScreenWindow)
            PreferredResolution = requested;

        ApplyInternal(ClampForMode(requested, mode), mode);
    }

    void ApplyInternal(Resolution res, FullScreenMode mode)
    {
        CurrentResolution  = res;
        CurrentDisplayMode = mode;
        Screen.SetResolution(res.width, res.height, mode);

        // 저장은 보정 전 "사용자가 고른" 값 기준 — 다음 실행에서 모드에 맞게 다시 보정된다.
        PlayerPrefs.SetInt(KeyResWidth,  PreferredResolution.width);
        PlayerPrefs.SetInt(KeyResHeight, PreferredResolution.height);
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
    /// 숫자키 응원은 기본 OFF, Tip 항상 표시도 기본 OFF. 밝기는 아직 구현된 설정이 아니라 범위에서 제외(SoundAndSettingsDesign.md §8).
    /// </summary>
    public void ResetToDefaults()
    {
        SetMasterVolume(defaultMasterVolume);
        SetBgmVolume(defaultBgmVolume);
        SetSfxVolume(defaultSfxVolume);
        SetMicVolume(defaultMicVolume);
        SetChatFontSize(defaultChatFontSize);
        SetDigitCheerEnabled(false);
        SetTipAlwaysShow(false);
        SetMouseSensitivity(defaultMouseSensitivity);

        ApplyDisplay(NativeResolution.width, NativeResolution.height, FullScreenMode.ExclusiveFullScreen);

        PlayerPrefs.DeleteKey(GameLocalizationBootstrap.ManualLocaleOverrideKey);
        GameLocalizationBootstrap.Instance?.ReapplyAutoDetectedLocale();
    }
}
