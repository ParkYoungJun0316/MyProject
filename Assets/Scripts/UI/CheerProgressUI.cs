using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 내 플레이어에 대한 응원 현황 슬롯 UI.
/// 화면에 1개만 존재하며, 내가 받는 응원의 상태를 표시한다.
///
/// [상태 전환]
/// Idle      → 빈 슬롯(emptySlotSprite) N개 표시
/// Cheering  → 응원한 플레이어 색상 아이콘으로 슬롯 채워짐
/// BuffActive → 버프 아이콘 1개 + 검정 오버레이(위→아래로 채워짐)
/// Cooldown  → 쿨타임 숫자 카운트다운 (15, 14, 13...)
///
/// [배치]
/// 인게임 HUD Canvas 하위 빈 GameObject에 부착.
/// RectTransform 크기를 먼저 설정한 뒤 Inspector에서 스프라이트 연결.
///
/// [Inspector 연결 요소]
/// - emptySlotSprite   : 빈 자리 아이콘 (회색 떡 등)
/// - colorIconMap[]    : PlayerColorType별 응원자 아이콘 (컬러 떡)
/// - buffIconMap[]     : BuffType별 버프 아이콘 (귀신, 바람 등)
/// - iconSize          : 아이콘 한 변 크기 (px)
/// - iconSpacing       : 아이콘 간격 (px)
/// - cooldownFontSize  : 쿨타임 숫자 폰트 크기
/// </summary>
public class CheerProgressUI : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────

    [Header("스프라이트")]
    [SerializeField] Sprite emptySlotSprite;

    [System.Serializable]
    public class ColorIconEntry
    {
        public PlayerColorType colorType;
        public Sprite          icon;
    }
    [SerializeField] ColorIconEntry[] colorIconMap;

    [System.Serializable]
    public class BuffIconEntry
    {
        public PlayerBuffSystem.BuffType buffType;
        public Sprite                    icon;
    }
    [SerializeField] BuffIconEntry[] buffIconMap;

    [Header("레이아웃")]
    [SerializeField] float iconSize           = 60f;
    [SerializeField] float iconSpacing        = 8f;

    [Header("쿨타임 텍스트")]
    [SerializeField] float cooldownFontSize   = 36f;
    [SerializeField] Color cooldownTextColor  = Color.white;

    [Header("배경")]
    [Tooltip("스크립트가 부착된 루트 GameObject의 Image 컴포넌트. 없으면 무시.")]
    [SerializeField] Image backgroundImage;
    [SerializeField] Color backgroundColor    = new Color(0.1f, 0.08f, 0.12f, 0.85f);
    [SerializeField] Sprite backgroundSprite;

    [Header("버프 슬롯 (BuffStatusUI와 동일 세팅)")]
    [Tooltip("슬롯 원형 배경 스프라이트. BG와 Fill 둘 다 이 스프라이트를 사용 (원 밖 투명).")]
    [SerializeField] Sprite bgSprite;
    [Tooltip("슬롯 배경 색상")]
    [SerializeField] Color  bgColor    = new Color(0.9f, 0.87f, 0.75f, 1f);
    [Tooltip("쿨다운 Fill 덮개 색상 (보통 검정)")]
    [SerializeField] Color  fillColor  = new Color(0f, 0f, 0f, 0.85f);

    // ── 상태 ──────────────────────────────────────────────────────

    enum CheerState { Idle, Cheering, BuffActive, Cooldown }
    CheerState _state = CheerState.Idle;

    int   _myColorIndex   = -1;
    int   _requiredVotes  = 1;
    int[] _cheererColors  = System.Array.Empty<int>(); // 나를 응원 중인 플레이어 colorIndex 목록

    float _buffStartTime;
    float _buffDuration;
    float _cooldownStartTime;
    float _cooldownDuration;

    PlayerBuffSystem _localBuffSystem;

    // ── 생성된 UI 요소 ────────────────────────────────────────────

    GameObject          _iconRow;           // HorizontalLayoutGroup
    List<Image>         _slotImages = new();// 슬롯 아이콘 이미지 목록
    GameObject          _buffContainer;     // 버프 아이콘 + 오버레이 컨테이너
    Image               _buffIconImage;     // 버프 스프라이트
    Image               _buffOverlayImage;  // 검정 fill 오버레이
    TextMeshProUGUI     _cooldownText;      // 쿨타임 숫자

    // ── 초기화 ────────────────────────────────────────────────────

    void Start()
    {
        BuildUI();
        PlayerSpawnCoordinator.OnPlayersReady += Init;
        if (PlayerSpawnCoordinator.IsReady) Init();
    }

    /// <summary>
    /// OnPlayersReady 시점에 1회 호출. 이 시점에는 Player · CheerService 모두 씬에 존재 보장.
    /// </summary>
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

        int total = NetworkSessionData.ClientColors.Count;
        _requiredVotes = (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            ? Mathf.Max(1, total - 1)
            : 1;

        // 현재 활성 상태라면 즉시 구독 (OnEnable은 이미 지나갔으므로)
        if (gameObject.activeInHierarchy)
            SubscribeEvents();

        // 로컬 플레이어의 PlayerBuffSystem 구독 (Shield 소모 즉시 UI 종료용)
        SubscribeBuffSystem();

        SetState(CheerState.Idle);
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
        // 중복 구독 방지: 먼저 해제 후 등록
        svc.OnVoteChanged     -= HandleVoteChanged;
        svc.OnCheerersChanged -= HandleCheerersChanged;
        svc.OnBuffActivated   -= HandleBuffActivated;
        svc.OnVoteReset       -= HandleVoteReset;
        svc.OnCooldownStart   -= HandleCooldownStart;
        svc.OnVoteChanged     += HandleVoteChanged;
        svc.OnCheerersChanged += HandleCheerersChanged;
        svc.OnBuffActivated   += HandleBuffActivated;
        svc.OnVoteReset       += HandleVoteReset;
        svc.OnCooldownStart   += HandleCooldownStart;
    }

    void UnsubscribeEvents()
    {
        var svc = CheerService.Instance;
        if (svc == null) return;
        svc.OnVoteChanged    -= HandleVoteChanged;
        svc.OnCheerersChanged -= HandleCheerersChanged;
        svc.OnBuffActivated  -= HandleBuffActivated;
        svc.OnVoteReset      -= HandleVoteReset;
        svc.OnCooldownStart  -= HandleCooldownStart;
    }

    void SubscribeBuffSystem()
    {
        UnsubscribeBuffSystem();
        _localBuffSystem = FindLocalBuffSystem();
        if (_localBuffSystem != null)
            _localBuffSystem.OnBuffRemoved += HandleBuffRemoved;
    }

    void UnsubscribeBuffSystem()
    {
        if (_localBuffSystem != null)
            _localBuffSystem.OnBuffRemoved -= HandleBuffRemoved;
        _localBuffSystem = null;
    }

    static PlayerBuffSystem FindLocalBuffSystem()
    {
        var all = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (var p in all)
        {
            var netObj = p.GetComponent<NetworkObject>();
            bool isOwner = (netObj != null && netObj.IsOwner) || p.isOwnerControlled;
            if (isOwner) return p.GetComponent<PlayerBuffSystem>();
        }
        return null;
    }

    /// <summary>Shield charge 소모로 버프가 제거되면 즉시 Cooldown 전환.</summary>
    void HandleBuffRemoved(PlayerBuffSystem.BuffType type)
    {
        if (type != PlayerBuffSystem.BuffType.Shield) return;
        if (_state != CheerState.BuffActive) return;
        _cooldownStartTime = Time.time;
        SetState(CheerState.Cooldown);
    }

    // ── UI 생성 ───────────────────────────────────────────────────

    void BuildUI()
    {
        // 배경 이미지 설정 (Inspector에서 backgroundImage 연결 또는 루트에 Image 컴포넌트 추가 시 자동 적용)
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();
        if (backgroundImage != null)
        {
            backgroundImage.color  = backgroundColor;
            if (backgroundSprite != null)
                backgroundImage.sprite = backgroundSprite;
        }

        // 아이콘 행
        _iconRow = new GameObject("IconRow");
        _iconRow.transform.SetParent(transform, false);
        var hlg = _iconRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = iconSpacing;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.childAlignment         = TextAnchor.MiddleCenter;
        var rowRt = _iconRow.GetComponent<RectTransform>();
        rowRt.anchorMin = Vector2.zero;
        rowRt.anchorMax = Vector2.one;
        rowRt.offsetMin = rowRt.offsetMax = Vector2.zero;

        // 버프 컨테이너 — BuffStatusUI와 동일한 3레이어 구조
        _buffContainer = new GameObject("BuffContainer");
        _buffContainer.transform.SetParent(transform, false);
        var buffContainerRt = _buffContainer.GetComponent<RectTransform>();
        if (buffContainerRt == null) buffContainerRt = _buffContainer.AddComponent<RectTransform>();
        buffContainerRt.anchorMin        = new Vector2(0.5f, 0.5f);
        buffContainerRt.anchorMax        = new Vector2(0.5f, 0.5f);
        buffContainerRt.sizeDelta        = new Vector2(iconSize, iconSize);
        buffContainerRt.anchoredPosition = Vector2.zero;

        // 레이어 1: 배경 (원형 스프라이트, bgColor)
        var bgObj            = new GameObject("BG");
        bgObj.transform.SetParent(_buffContainer.transform, false);
        var bgImg            = bgObj.AddComponent<Image>();
        bgImg.sprite         = bgSprite;
        bgImg.color          = bgColor;
        bgImg.preserveAspect = false;
        var bgRt             = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;

        // 레이어 2: 버프 아이콘 (슬롯 안쪽 10~90%)
        var buffIconObj           = new GameObject("Icon");
        buffIconObj.transform.SetParent(_buffContainer.transform, false);
        _buffIconImage            = buffIconObj.AddComponent<Image>();
        _buffIconImage.preserveAspect = true;
        var buffIconRt            = buffIconObj.GetComponent<RectTransform>();
        buffIconRt.anchorMin      = new Vector2(0.1f, 0.1f);
        buffIconRt.anchorMax      = new Vector2(0.9f, 0.9f);
        buffIconRt.sizeDelta      = Vector2.zero;

        // 레이어 3: Fill 덮개 (원형 스프라이트 + fillColor, Vertical Top)
        // bgSprite와 같은 원 스프라이트를 쓰므로 원 안에서만 채워짐
        var overlayObj               = new GameObject("Fill");
        overlayObj.transform.SetParent(_buffContainer.transform, false);
        _buffOverlayImage            = overlayObj.AddComponent<Image>();
        _buffOverlayImage.sprite     = bgSprite;
        _buffOverlayImage.color      = fillColor;
        _buffOverlayImage.type       = Image.Type.Filled;
        _buffOverlayImage.fillMethod = Image.FillMethod.Vertical;
        _buffOverlayImage.fillOrigin = (int)Image.OriginVertical.Top;
        _buffOverlayImage.fillClockwise = true;
        _buffOverlayImage.fillAmount = 0f;
        var overlayRt                = overlayObj.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.sizeDelta = Vector2.zero;

        // 쿨타임 텍스트
        var cdObj = new GameObject("CooldownText");
        cdObj.transform.SetParent(transform, false);
        _cooldownText = cdObj.AddComponent<TextMeshProUGUI>();
        _cooldownText.fontSize          = cooldownFontSize;
        _cooldownText.fontStyle         = FontStyles.Bold;
        _cooldownText.color             = cooldownTextColor;
        _cooldownText.alignment         = TextAlignmentOptions.Center;
        _cooldownText.text              = string.Empty;
        var cdRt = cdObj.GetComponent<RectTransform>();
        cdRt.anchorMin = Vector2.zero;
        cdRt.anchorMax = Vector2.one;
        cdRt.offsetMin = cdRt.offsetMax = Vector2.zero;

        // 초기: 모두 숨김
        _iconRow.SetActive(false);
        _buffContainer.SetActive(false);
        _cooldownText.gameObject.SetActive(false);
    }

    // ── 슬롯 동적 생성 ────────────────────────────────────────────

    void RebuildIconSlots(int count)
    {
        // 기존 슬롯 제거
        foreach (Transform child in _iconRow.transform)
            Destroy(child.gameObject);
        _slotImages.Clear();

        for (int i = 0; i < count; i++)
        {
            var obj = new GameObject($"Slot{i}");
            obj.transform.SetParent(_iconRow.transform, false);
            var img = obj.AddComponent<Image>();
            img.sprite         = emptySlotSprite;
            img.preserveAspect = true;
            obj.GetComponent<RectTransform>().sizeDelta = new Vector2(iconSize, iconSize);
            _slotImages.Add(img);
        }
    }

    // ── 상태 전환 ─────────────────────────────────────────────────

    void SetState(CheerState next)
    {
        _state = next;

        _iconRow.SetActive(next is CheerState.Idle or CheerState.Cheering);
        _buffContainer.SetActive(next == CheerState.BuffActive);
        _cooldownText.gameObject.SetActive(next == CheerState.Cooldown);

        switch (next)
        {
            case CheerState.Idle:
                RebuildIconSlots(_requiredVotes);
                FillSlots(System.Array.Empty<int>());
                break;

            case CheerState.Cheering:
                RebuildIconSlots(_requiredVotes);
                FillSlots(_cheererColors);
                break;

            case CheerState.BuffActive:
                _buffIconImage.sprite = GetBuffSprite(CheerService.Instance?.StageBuffType
                    ?? PlayerBuffSystem.BuffType.Shield);
                _buffOverlayImage.fillAmount = 0f;
                break;

            case CheerState.Cooldown:
                _cooldownText.text = Mathf.CeilToInt(_cooldownDuration).ToString();
                break;
        }
    }

    // ── 슬롯 채우기 ───────────────────────────────────────────────

    /// <summary>응원자 colorIndex 목록으로 슬롯을 채운다. 나머지는 빈 슬롯.</summary>
    void FillSlots(int[] cheererColorIndices)
    {
        for (int i = 0; i < _slotImages.Count; i++)
        {
            if (_slotImages[i] == null) continue;

            if (i < cheererColorIndices.Length)
            {
                Sprite icon = GetColorIcon(cheererColorIndices[i]);
                _slotImages[i].sprite = icon != null ? icon : emptySlotSprite;
            }
            else
            {
                _slotImages[i].sprite = emptySlotSprite;
            }
        }
    }

    // ── Update (버프 오버레이 + 쿨타임 숫자) ──────────────────────

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
            {
                SetState(CheerState.Idle);
            }
            else
            {
                _cooldownText.text = Mathf.CeilToInt(remaining).ToString();
            }
        }
    }

    // ── CheerService 이벤트 핸들러 ────────────────────────────────

    void HandleVoteChanged(int targetIdx, int current, int required)
    {
        if (targetIdx != _myColorIndex) return;
        _requiredVotes = required;
        // 슬롯 수가 바뀔 수 있으므로 Cheering 상태에서 리빌드
        if (_state is CheerState.Idle or CheerState.Cheering)
        {
            _state = CheerState.Cheering;
            RebuildIconSlots(_requiredVotes);
            FillSlots(_cheererColors);
            _iconRow.SetActive(true);
        }
    }

    void HandleCheerersChanged(int targetIdx, int[] cheererColorIndices)
    {
        if (targetIdx != _myColorIndex) return;
        _cheererColors = cheererColorIndices;

        if (_state is CheerState.Idle or CheerState.Cheering)
        {
            bool hasVotes = cheererColorIndices.Length > 0;
            _state = hasVotes ? CheerState.Cheering : CheerState.Idle;
            RebuildIconSlots(_requiredVotes);
            FillSlots(_cheererColors);
            _iconRow.SetActive(true);
            _buffContainer.SetActive(false);
            _cooldownText.gameObject.SetActive(false);
        }
    }

    void HandleBuffActivated(int targetIdx)
    {
        if (targetIdx != _myColorIndex) return;
        _cheererColors = System.Array.Empty<int>();
        _buffStartTime = Time.time;
        if (CheerService.Instance != null)
            _buffDuration = CheerService.Instance.BuffDuration;
        SetState(CheerState.BuffActive);
    }

    void HandleVoteReset(int targetIdx)
    {
        if (targetIdx != _myColorIndex) return;
        _cheererColors = System.Array.Empty<int>();
        if (_state is CheerState.Idle or CheerState.Cheering)
            SetState(CheerState.Idle);
    }

    void HandleCooldownStart(int targetIdx, float seconds)
    {
        if (targetIdx != _myColorIndex) return;
        // Shield 소모로 이미 Cooldown 진입한 경우 재진입 방지
        if (_state == CheerState.Cooldown) return;
        _cooldownStartTime = Time.time;
        _cooldownDuration  = seconds;
        SetState(CheerState.Cooldown);
    }

    // ── 스프라이트 조회 ───────────────────────────────────────────

    Sprite GetColorIcon(int colorIndex)
    {
        if (colorIndex < 0 || colorIndex >= LobbyNetworkManager.ColorOrder.Length) return null;
        PlayerColorType colorType = LobbyNetworkManager.ColorOrder[colorIndex];
        if (colorIconMap == null) return null;
        foreach (var entry in colorIconMap)
            if (entry.colorType == colorType) return entry.icon;
        return null;
    }

    Sprite GetBuffSprite(PlayerBuffSystem.BuffType buffType)
    {
        if (buffIconMap == null) return null;
        foreach (var entry in buffIconMap)
            if (entry.buffType == buffType) return entry.icon;
        return null;
    }

    // ── 내 colorIndex 조회 ────────────────────────────────────────

    static int GetMyColorIndex()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            ulong myId = NetworkManager.Singleton.LocalClientId;
            if (NetworkSessionData.ClientColors.TryGetValue(myId, out var color))
                return System.Array.IndexOf(LobbyNetworkManager.ColorOrder, color);
        }
        // 솔로: 로컬 오너 플레이어 색상
        var all = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (var p in all)
        {
            var netObj = p.GetComponent<NetworkObject>();
            bool isOwner = (netObj != null && netObj.IsOwner) || p.isOwnerControlled;
            if (isOwner)
                return System.Array.IndexOf(LobbyNetworkManager.ColorOrder, p.playerColorType);
        }
        return -1;
    }
}
