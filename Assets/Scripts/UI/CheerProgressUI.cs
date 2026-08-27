using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 내 플레이어가 받은 버프 상태 UI.
///
/// [상태]
/// Hidden     → 숨김 (버프/쿨타임 없음)
/// BuffActive → 버프 아이콘 + 위→아래 fill (남은 시간)
/// Cooldown   → 다음 버프까지 카운트다운 숫자
///
/// [이전 역할 이동]
/// "나를 응원 중인 플레이어" 표시 → TeamStatusUI로 이동.
/// 이 컴포넌트는 버프 지속 시간 + 쿨타임만 담당.
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

    enum CheerState { Hidden, BuffActive, Cooldown }
    CheerState _state = CheerState.Hidden;

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

        var svc = CheerService.Instance;
        if (svc != null)
        {
            _buffDuration     = svc.BuffDuration;
            _cooldownDuration = svc.CooldownDuration;
        }

        if (gameObject.activeInHierarchy)
            SubscribeEvents();

        SubscribeBuffSystem();
        _localSetup = FindLocalNetworkPlayerSetup();
        SetState(CheerState.Hidden);
    }

    void OnDestroy()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= Init;
        UnsubscribeEvents();
        UnsubscribeBuffSystem();
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
        }
    }

    void UnsubscribeBuffSystem()
    {
        if (_localBuffSystem != null)
        {
            _localBuffSystem.OnBuffApplied -= HandleBuffApplied;
            _localBuffSystem.OnBuffRemoved -= HandleBuffRemoved;
        }
        _localBuffSystem = null;
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
        _buffContainer.SetActive(next == CheerState.BuffActive);
        _cooldownText.gameObject.SetActive(next == CheerState.Cooldown);

        if (next == CheerState.BuffActive)
        {
            _buffIconImage.sprite        = GetBuffSprite(_activeBuffType);
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
        if (_state == CheerState.BuffActive)
        {
            float elapsed = Time.time - _buffStartTime;
            _buffOverlayImage.fillAmount = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _buffDuration));
        }
        else if (_state == CheerState.Cooldown)
        {
            float remaining = _cooldownDuration - (Time.time - _cooldownStartTime);
            if (remaining <= 0f)
                SetState(CheerState.Hidden);
            else
                _cooldownText.text = Mathf.CeilToInt(remaining).ToString();
        }
    }

    // ── 이벤트 핸들러 ─────────────────────────────────────────────

    // PlayerBuffSystem.OnBuffApplied가 primary. 이 핸들러는 colorIndex가 확실할 때만 보조.
    void HandleBuffActivated(int targetIdx)
    {
        if (_myColorIndex < 0 || targetIdx != _myColorIndex) return;
        if (_state == CheerState.BuffActive) return; // OnBuffApplied에서 이미 처리됨
        _buffStartTime = Time.time;
        if (CheerService.Instance != null)
            _buffDuration = CheerService.Instance.BuffDuration;
        // 이 폴백 경로엔 실제 타입 파라미터가 없으므로 내가 지금 선택해둔 타입으로 추정한다
        // (버프 발동 시 CheerService.ApplyBuff가 실제로 적용하는 값과 항상 일치).
        if (_localSetup != null) _activeBuffType = _localSetup.SelectedBuffType;
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

    // ── 유틸 ─────────────────────────────────────────────────────

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
