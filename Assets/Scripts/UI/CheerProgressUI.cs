using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 내 플레이어가 받은 버프 상태 UI + 버프 선택 입력(Q키).
///
/// [상태]
/// Idle       → 상시 표시. 지금 선택해둔 버프 타입 아이콘 (fill 없음, Q로 바꾸면 즉시 갱신)
/// BuffActive → 버프 아이콘 + 위→아래 fill (남은 시간)
/// Cooldown   → 다음 버프까지 카운트다운 숫자 (아이콘 대신 숫자만)
///
/// [버프 선택 입력 — 구 BuffSelectHotkeyInput 흡수, 2026-08-28]
/// Q 키 → 로컬에서 "지금 내 버프 활성 중?" 확인(즉시 판정) → 활성 중 아니면
/// NetworkPlayerSetup.RequestToggleBuffTypeServerRpc() 호출 → Host가 다시 검증
/// (CheerService.IsBuffActive) 후 NetworkVariable 갱신 → Idle 아이콘이 즉시 갱신되는 것 자체가 피드백.
/// 활성 중이면 조용히 무시(별도 UI 없음 — Tutorial에서 규칙 설명).
///
/// [이전 역할 이동]
/// "나를 응원 중인 플레이어" 표시 → TeamStatusUI로 이동.
/// 이 컴포넌트는 버프 지속 시간 + 쿨타임 + 버프 선택 입력만 담당.
///
/// [Inspector 연결 요소]
/// - buffIconMap   : BuffType별 버프 아이콘
/// - bgSprite      : 원형 슬롯 배경 스프라이트
/// - iconSize      : 아이콘 크기 (px)
/// - cooldownFontSize : 쿨타임 숫자 폰트 크기
/// </summary>
public class CheerProgressUI : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────

    [System.Serializable]
    public class BuffIconEntry
    {
        public PlayerBuffSystem.BuffType buffType;
        public Sprite                    icon;
    }

    [Header("버프 아이콘 매핑")]
    [SerializeField] BuffIconEntry[] buffIconMap;

    [Header("레이아웃")]
    [SerializeField] float iconSize          = 60f;

    [Header("쿨타임 텍스트")]
    [SerializeField] float cooldownFontSize  = 36f;
    [SerializeField] Color cooldownTextColor = Color.white;

    [Header("배경")]
    [Tooltip("루트 GameObject의 Image 컴포넌트. 없으면 무시.")]
    [SerializeField] Image  backgroundImage;
    [SerializeField] Color  backgroundColor  = new Color(0.1f, 0.08f, 0.12f, 0.85f);
    [SerializeField] Sprite backgroundSprite;

    [Header("버프 슬롯")]
    [Tooltip("원형 배경 스프라이트 (BG·Fill 공용)")]
    [SerializeField] Sprite bgSprite;
    [SerializeField] Color  bgColor   = new Color(0.9f, 0.87f, 0.75f, 1f);
    [SerializeField] Color  fillColor = new Color(0f, 0f, 0f, 0.85f);

    // ── 상태 ──────────────────────────────────────────────────────

    enum CheerState { Idle, BuffActive, Cooldown }
    CheerState _state = CheerState.Idle;

    // 응원으로 발동 가능한 버프 타입들. 플레이어가 선택하는 타입이 이 안에서만 정해지므로
    // "이 UI 슬롯이 반응해야 할 버프인지" 필터로 사용 (스테이지 고정 StageBuffType 대체).
    static readonly PlayerBuffSystem.BuffType[] CheerBuffTypes =
        { PlayerBuffSystem.BuffType.Shield, PlayerBuffSystem.BuffType.SpeedUp };

    static bool IsCheerBuffType(PlayerBuffSystem.BuffType type)
        => System.Array.IndexOf(CheerBuffTypes, type) >= 0;

    int   _myColorIndex    = -1;
    float _buffStartTime;
    float _buffDuration;
    float _cooldownStartTime;
    float _cooldownDuration;

    // 지금 활성 중(혹은 방금 활성화된)인 버프의 실제 타입 — 플레이어 선택에 따라 달라짐.
    PlayerBuffSystem.BuffType _activeBuffType = PlayerBuffSystem.BuffType.Shield;

    PlayerBuffSystem _localBuffSystem;
    NetworkPlayerSetup _localSetup;
    Player       _localPlayer;
    PlayerEvents _localEvents;

    // ── 생성된 UI ─────────────────────────────────────────────────

    GameObject      _buffContainer;
    Image           _buffIconImage;
    Image           _buffOverlayImage;
    TextMeshProUGUI _cooldownText;

    // ── 초기화 ────────────────────────────────────────────────────

    void Start()
    {
        BuildUI();
        PlayerSpawnCoordinator.OnPlayersReady += Init;
        if (PlayerSpawnCoordinator.IsReady) Init();
    }

    void Init()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= Init;
        _myColorIndex = GetMyColorIndex();

        // 지속시간은 버프 종류(BuffType) 소속이라 스테이지 전역값이 없다 — 첫 활성화 전까지의
        // 임시값일 뿐이며 HandleBuffApplied/HandleBuffActivated에서 실제 타입 기준으로 갱신된다.
        _buffDuration = 5f;

        var svc = CheerService.Instance;
        if (svc != null)
            _cooldownDuration = svc.CooldownDuration;

        if (gameObject.activeInHierarchy)
            SubscribeEvents();

        SubscribeBuffSystem();
        _localSetup = FindLocalNetworkPlayerSetup();
        if (_localSetup != null) _localSetup.OnSelectedBuffTypeChanged += HandleSelectedBuffTypeChanged;
        SetState(CheerState.Idle);
    }

    void OnDestroy()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= Init;
        UnsubscribeEvents();
        UnsubscribeBuffSystem();
        if (_localSetup != null) _localSetup.OnSelectedBuffTypeChanged -= HandleSelectedBuffTypeChanged;
    }

    void OnEnable()  => SubscribeEvents();
    void OnDisable() => UnsubscribeEvents();

    void SubscribeEvents()
    {
        var svc = CheerService.Instance;
        if (svc == null) return;
        svc.OnBuffActivated -= HandleBuffActivated;
        svc.OnCooldownStart -= HandleCooldownStart;
        svc.OnBuffActivated += HandleBuffActivated;
        svc.OnCooldownStart += HandleCooldownStart;
    }

    void UnsubscribeEvents()
    {
        var svc = CheerService.Instance;
        if (svc == null) return;
        svc.OnBuffActivated -= HandleBuffActivated;
        svc.OnCooldownStart -= HandleCooldownStart;
    }

    void SubscribeBuffSystem()
    {
        UnsubscribeBuffSystem();
        _localBuffSystem = FindLocalBuffSystem();
        if (_localBuffSystem != null)
        {
            _localBuffSystem.OnBuffApplied += HandleBuffApplied;
            _localBuffSystem.OnBuffRemoved += HandleBuffRemoved;

            _localPlayer = _localBuffSystem.GetComponent<Player>();
            _localEvents = _localPlayer != null ? _localPlayer.GetComponent<PlayerEvents>() : null;
            if (_localEvents != null)
            {
                _localEvents.OnBlackWhiteChanged  += HandlePlayerColorChanged;
                _localEvents.OnUniqueColorChanged += HandlePlayerColorChanged;
            }
        }
    }

    void UnsubscribeBuffSystem()
    {
        if (_localBuffSystem != null)
        {
            _localBuffSystem.OnBuffApplied -= HandleBuffApplied;
            _localBuffSystem.OnBuffRemoved -= HandleBuffRemoved;
        }
        if (_localEvents != null)
        {
            _localEvents.OnBlackWhiteChanged  -= HandlePlayerColorChanged;
            _localEvents.OnUniqueColorChanged -= HandlePlayerColorChanged;
        }
        _localBuffSystem = null;
        _localPlayer     = null;
        _localEvents     = null;
    }

    static PlayerBuffSystem FindLocalBuffSystem()
    {
        foreach (var p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            var net      = p.GetComponent<NetworkObject>();
            bool isOwner = (net != null && net.IsOwner) || p.isOwnerControlled;
            if (isOwner) return p.GetComponent<PlayerBuffSystem>();
        }
        return null;
    }

    /// <summary>HandleBuffActivated(ClientRpc 경로) 폴백에서 "내 선택 타입"을 조회하기 위한 참조.</summary>
    static NetworkPlayerSetup FindLocalNetworkPlayerSetup()
    {
        foreach (var p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            var net      = p.GetComponent<NetworkObject>();
            bool isOwner = (net != null && net.IsOwner) || p.isOwnerControlled;
            if (isOwner) return p.GetComponent<NetworkPlayerSetup>();
        }
        return null;
    }

    /// <summary>
    /// 로컬 PlayerBuffSystem에 버프가 적용될 때 직접 감지.
    /// CheerService 이벤트 경로(colorIndex 필터·구독 타이밍)에 무관하게 동작.
    /// </summary>
    void HandleBuffApplied(PlayerBuffSystem.BuffType type, float duration)
    {
        if (!IsCheerBuffType(type)) return;
        _activeBuffType = type;
        _buffStartTime  = Time.time;
        _buffDuration   = duration;
        SetState(CheerState.BuffActive);
    }

    /// <summary>Shield charge 소모로 버프가 제거되면 즉시 Cooldown 전환.</summary>
    void HandleBuffRemoved(PlayerBuffSystem.BuffType type)
    {
        if (!IsCheerBuffType(type)) return;
        if (type != _activeBuffType) return; // 다른 버프 타입 제거 이벤트는 무시
        if (_state != CheerState.BuffActive) return;
        _cooldownStartTime = Time.time;
        SetState(CheerState.Cooldown);
    }

    // ── UI 생성 ───────────────────────────────────────────────────

    void BuildUI()
    {
        // 배경 이미지
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();
        if (backgroundImage != null)
        {
            backgroundImage.color = backgroundColor;
            if (backgroundSprite != null)
                backgroundImage.sprite = backgroundSprite;
        }

        // 버프 컨테이너 (BG + 아이콘 + Fill 덮개)
        _buffContainer = new GameObject("BuffContainer");
        _buffContainer.transform.SetParent(transform, false);
        RectTransform cRt = _buffContainer.GetComponent<RectTransform>();
        if (cRt == null) cRt = _buffContainer.AddComponent<RectTransform>();
        cRt.anchorMin        = new Vector2(0.5f, 0.5f);
        cRt.anchorMax        = new Vector2(0.5f, 0.5f);
        cRt.sizeDelta        = new Vector2(iconSize, iconSize);
        cRt.anchoredPosition = Vector2.zero;

        // BG
        var bgObj = new GameObject("BG");
        bgObj.transform.SetParent(_buffContainer.transform, false);
        var bgImg = bgObj.AddComponent<Image>();
        bgImg.sprite = bgSprite;
        bgImg.color  = bgColor;
        StretchFull(bgObj.GetComponent<RectTransform>());

        // Icon
        var icObj = new GameObject("Icon");
        icObj.transform.SetParent(_buffContainer.transform, false);
        _buffIconImage               = icObj.AddComponent<Image>();
        _buffIconImage.preserveAspect = true;
        var icRt = icObj.GetComponent<RectTransform>();
        icRt.anchorMin = new Vector2(0.1f, 0.1f);
        icRt.anchorMax = new Vector2(0.9f, 0.9f);
        icRt.sizeDelta = Vector2.zero;

        // Fill 덮개
        var fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(_buffContainer.transform, false);
        _buffOverlayImage               = fillObj.AddComponent<Image>();
        _buffOverlayImage.sprite        = bgSprite;
        _buffOverlayImage.color         = fillColor;
        _buffOverlayImage.type          = Image.Type.Filled;
        _buffOverlayImage.fillMethod    = Image.FillMethod.Vertical;
        _buffOverlayImage.fillOrigin    = (int)Image.OriginVertical.Top;
        _buffOverlayImage.fillClockwise = true;
        _buffOverlayImage.fillAmount    = 0f;
        StretchFull(fillObj.GetComponent<RectTransform>());

        // 쿨타임 텍스트
        var cdObj = new GameObject("CooldownText");
        cdObj.transform.SetParent(transform, false);
        _cooldownText           = cdObj.AddComponent<TextMeshProUGUI>();
        _cooldownText.fontSize  = cooldownFontSize;
        _cooldownText.fontStyle = FontStyles.Bold;
        _cooldownText.color     = cooldownTextColor;
        _cooldownText.alignment = TextAlignmentOptions.Center;
        _cooldownText.text      = string.Empty;
        StretchFull(cdObj.GetComponent<RectTransform>());

        _buffContainer.SetActive(false);
        _cooldownText.gameObject.SetActive(false);
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // ── 상태 전환 ─────────────────────────────────────────────────

    void SetState(CheerState next)
    {
        _state = next;
        // Idle·BuffActive 둘 다 아이콘 슬롯을 보여준다 — 차이는 fill 유무뿐.
        _buffContainer.SetActive(next == CheerState.Idle || next == CheerState.BuffActive);
        _cooldownText.gameObject.SetActive(next == CheerState.Cooldown);

        if (next == CheerState.BuffActive)
        {
            SetIcon(GetBuffSprite(_activeBuffType));
            _buffOverlayImage.fillAmount = 0f;
        }
        else if (next == CheerState.Idle)
        {
            var selected = _localSetup != null ? _localSetup.SelectedBuffType : PlayerBuffSystem.BuffType.Shield;
            SetIcon(GetBuffSprite(selected));
            _buffOverlayImage.fillAmount = 0f;
        }
        else if (next == CheerState.Cooldown)
        {
            _cooldownText.text = Mathf.CeilToInt(_cooldownDuration).ToString();
        }
    }

    // ── Update ────────────────────────────────────────────────────

    void Update()
    {
        HandleBuffSelectInput();

        if (_state == CheerState.BuffActive)
        {
            float elapsed = Time.time - _buffStartTime;
            _buffOverlayImage.fillAmount = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _buffDuration));
        }
        else if (_state == CheerState.Cooldown)
        {
            float remaining = _cooldownDuration - (Time.time - _cooldownStartTime);
            if (remaining <= 0f)
                SetState(CheerState.Idle);
            else
                _cooldownText.text = Mathf.CeilToInt(remaining).ToString();
        }
    }

    /// <summary>
    /// Q키 → 버프 선택 토글 (구 BuffSelectHotkeyInput 흡수).
    /// InGameChatUI/TutorialCheerNameUI 열려있으면 무시 (CheerDigitInput과 동일 게이팅).
    /// </summary>
    void HandleBuffSelectInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (InGameChatUI.IsChatOpen || TutorialCheerNameUI.IsOpen) return;
        if (!kb.qKey.wasPressedThisFrame) return;
        if (_localSetup == null) return;

        // 로컬 선(先)검증 — 내 버프가 지금 활성 중이면 조용히 무시.
        // (권위 있는 최종 판정은 여전히 Host의 RequestToggleBuffTypeServerRpc.)
        bool isActive = _localBuffSystem != null &&
            (_localBuffSystem.IsActive(PlayerBuffSystem.BuffType.Shield) ||
             _localBuffSystem.IsActive(PlayerBuffSystem.BuffType.SpeedUp));
        if (isActive) return;

        _localSetup.RequestToggleBuffTypeServerRpc();
    }

    // ── 이벤트 핸들러 ─────────────────────────────────────────────

    // PlayerBuffSystem.OnBuffApplied가 primary. 이 핸들러는 colorIndex가 확실할 때만 보조.
    void HandleBuffActivated(int targetIdx)
    {
        if (_myColorIndex < 0 || targetIdx != _myColorIndex) return;
        if (_state == CheerState.BuffActive) return; // OnBuffApplied에서 이미 처리됨
        _buffStartTime = Time.time;
        // 이 폴백 경로엔 실제 타입 파라미터가 없으므로 내가 지금 선택해둔 타입으로 추정한다
        // (버프 발동 시 CheerService.ApplyBuff가 실제로 적용하는 값과 항상 일치).
        if (_localSetup != null) _activeBuffType = _localSetup.SelectedBuffType;
        var setting = _localBuffSystem?.GetSetting(_activeBuffType);
        _buffDuration = setting?.duration ?? 5f;
        SetState(CheerState.BuffActive);
    }

    void HandleCooldownStart(int targetIdx, float seconds)
    {
        if (targetIdx != _myColorIndex) return;
        if (_state == CheerState.Cooldown) return;
        _cooldownStartTime = Time.time;
        _cooldownDuration  = seconds;
        SetState(CheerState.Cooldown);
    }

    /// <summary>Q키로 버프 선택이 바뀌면(NetworkPlayerSetup) Idle 상태일 때만 아이콘 즉시 갱신.</summary>
    void HandleSelectedBuffTypeChanged(PlayerBuffSystem.BuffType type)
    {
        if (_state != CheerState.Idle) return;
        SetIcon(GetBuffSprite(type));
    }

    /// <summary>흑/백·고유색 전환 시 표시 중인 아이콘 색을 즉시 갱신.</summary>
    void HandlePlayerColorChanged(bool _) => RefreshIconColor();
    void HandlePlayerColorChanged(int _)  => RefreshIconColor();

    // ── 유틸 ─────────────────────────────────────────────────────

    /// <summary>아이콘 스프라이트 + 플레이어 고유색(흑/백 포함) 틴트를 함께 적용.</summary>
    void SetIcon(Sprite sprite)
    {
        _buffIconImage.sprite = sprite;
        RefreshIconColor();
    }

    void RefreshIconColor()
    {
        if (_buffIconImage == null) return;
        _buffIconImage.color = _localPlayer != null ? _localPlayer.GetCurrentBaseColor() : Color.white;
    }

    Sprite GetBuffSprite(PlayerBuffSystem.BuffType buffType)
    {
        if (buffIconMap == null) return null;
        foreach (var e in buffIconMap)
            if (e.buffType == buffType) return e.icon;
        return null;
    }

    static int GetMyColorIndex()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            ulong myId = NetworkManager.Singleton.LocalClientId;
            // PlayerSpawnCoordinator(NetworkList) — 클라이언트에서도 레이스 없이 항상 최신값
            if (PlayerSpawnCoordinator.TryGetColor(myId, out var color))
                return System.Array.IndexOf(PlayerColorUtil.ColorOrder, color);
        }
        foreach (var p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            var net      = p.GetComponent<NetworkObject>();
            bool isOwner = (net != null && net.IsOwner) || p.isOwnerControlled;
            if (isOwner)
                return System.Array.IndexOf(PlayerColorUtil.ColorOrder, p.playerColorType);
        }
        return -1;
    }
}
