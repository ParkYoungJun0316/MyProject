using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// TeamStatus_Panel에 붙이는 스크립트.
/// 씬의 모든 Player(자신 제외)를 자동 수집해 슬롯 생성.
///
/// [각 슬롯 레이아웃 — bottomRow]
/// [버프 아이콘] [하트...] [Cheering 라벨]
///  ← HP 왼쪽       HP      HP 오른쪽 →
///
/// - 버프 아이콘: 해당 팀원이 현재 버프 중일 때만 표시
/// - Cheering  : 해당 팀원이 '나를' 응원 중일 때만 표시
///               (다른 팀원을 응원 중이면 표시하지 않음)
/// </summary>
public class TeamStatusUI : MonoBehaviour
{
    // ── 버프 아이콘 매핑 ─────────────────────────────────────────
    [System.Serializable]
    public class BuffIconEntry
    {
        public PlayerBuffSystem.BuffType type;
        public Sprite icon;
    }

    // ── 색별 하트 매핑 ───────────────────────────────────────────
    [System.Serializable]
    public class ColorHeartEntry
    {
        public PlayerColorType colorType;
        public Sprite fullHeartSprite;
    }

    [Header("연결")]
    [Tooltip("팀원 목록에서 제외할 플레이어 (내 캐릭터). 비워두면 자동 탐색.")]
    [SerializeField] Player excludePlayer;

    [Header("슬롯 크기")]
    [SerializeField] float slotWidth    = 220f;
    [SerializeField] float slotHeight   = 50f;
    [SerializeField] float slotSpacing  = 6f;
    [SerializeField] float heartSize    = 20f;
    [SerializeField] float heartSpacing = 2f;
    [SerializeField] float buffIconSize = 20f;

    [Header("스프라이트")]
    [SerializeField] Sprite fullHeartSprite;
    [SerializeField] Sprite emptyHeartSprite;

    [Header("색별 하트 스프라이트")]
    [SerializeField] ColorHeartEntry[] colorHeartMap;

    [Header("버프 아이콘")]
    [SerializeField] BuffIconEntry[] buffIconMap;

    [Header("색상")]
    [SerializeField] Color slotBgColor   = Color.clear;
    [SerializeField] Color deadBgColor   = new Color(0.6f, 0f, 0f, 0.5f);
    [SerializeField] Color deadTextColor = new Color(1f, 0.3f, 0.3f, 1f);

    [Header("Cheering 라벨")]
    [SerializeField] Sprite cheeringBgSprite;
    [SerializeField] Color  cheeringBgColor   = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] Color  cheeringTextColor = Color.black;
    [SerializeField] float  cheeringFontSize  = 11f;
    [SerializeField] float  cheeringWidth     = 60f;

    // ── 런타임 슬롯 ──────────────────────────────────────────────
    class PlayerSlot
    {
        public Player           player;
        public PlayerBuffSystem buffSystem;
        public PlayerEvents     events;
        public int              colorIndex;   // LobbyNetworkManager.ColorOrder 기준
        public Image            slotBg;
        public TextMeshProUGUI  nameText;
        public Image[]          heartImages;
        public Dictionary<PlayerBuffSystem.BuffType, Image> buffIcons
            = new Dictionary<PlayerBuffSystem.BuffType, Image>();
        public GameObject cheeringPanel;     // "Cheering" 배경 패널 (자식에 텍스트 포함)

        // BuildSlots 재호출 시 언구독용 (람다 캡처 해제 필수)
        public System.Action<bool>             onDamaged;
        public System.Action                   onDied;
        public System.Action                   onRespawned;
        public System.Action<PlayerColorType>  onColorTypeChanged;
    }

    readonly List<PlayerSlot> slots = new();

    // ── 응원 추적 ─────────────────────────────────────────────────
    int              _myColorIndex           = -1;
    readonly HashSet<int> _myCheerersColorIndices = new();

    // ── 초기화 ───────────────────────────────────────────────────

    void Start()
    {
        PlayerSpawnCoordinator.OnPlayersReady += BuildSlots;
        if (PlayerSpawnCoordinator.IsReady) BuildSlots();
    }

    void OnDestroy()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= BuildSlots;
        UnsubscribeAllSlots();
        UnsubscribeCheerService();
    }

    static Player FindLocalOwnerPlayer()
    {
        foreach (var p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            var net = p.GetComponent<NetworkObject>();
            if (net != null && net.IsOwner) return p;
            if (p.isOwnerControlled) return p;
        }
        return null;
    }

    /// <summary>
    /// ColorOrder 인덱스(0=berry …). PlayerSpawnCoordinator(NetworkList) 우선, 없으면 playerColorType.
    /// </summary>
    static int ResolveColorIndex(Player player)
    {
        if (player == null) return -1;

        var net = player.GetComponent<NetworkObject>();
        // PlayerSpawnCoordinator(NetworkList) — 클라이언트에서도 레이스 없이 항상 최신값
        if (net != null
            && PlayerSpawnCoordinator.TryGetColor(net.OwnerClientId, out var sessionColor))
            return System.Array.IndexOf(LobbyNetworkManager.ColorOrder, sessionColor);

        return System.Array.IndexOf(LobbyNetworkManager.ColorOrder, player.playerColorType);
    }

    static int GetMyColorIndex()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            ulong myId = NetworkManager.Singleton.LocalClientId;
            if (PlayerSpawnCoordinator.TryGetColor(myId, out var color))
                return System.Array.IndexOf(LobbyNetworkManager.ColorOrder, color);
        }
        foreach (var p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            var net      = p.GetComponent<NetworkObject>();
            bool isOwner = (net != null && net.IsOwner) || p.isOwnerControlled;
            if (isOwner) return ResolveColorIndex(p);
        }
        return -1;
    }

    void BuildSlots()
    {
        if (excludePlayer == null)
            excludePlayer = FindLocalOwnerPlayer();

        // 플레이어는 destroyWithScene:false(DDOL). 씬 리로드/BuildSlots 재호출 시
        // UI만 Destroy하고 OnDied를 남기면 RaiseDied → destroyed Image 접근으로
        // ForceKillClientRpc가 예외 중단 → Owner ForceKill/씬 리로드가 깨진다.
        UnsubscribeAllSlots();

        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
        slots.Clear();

        var vlg = GetComponent<VerticalLayoutGroup>() ?? gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing                = slotSpacing;
        vlg.childControlWidth      = false;
        vlg.childControlHeight     = false;
        vlg.childForceExpandWidth  = false;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment         = TextAnchor.UpperLeft;

        foreach (var p in FindObjectsByType<Player>(FindObjectsSortMode.None))
            if (p != null && p != excludePlayer)
                slots.Add(CreateSlot(p));

        // 응원 추적 초기화 및 CheerService 구독
        _myColorIndex = GetMyColorIndex();
        _myCheerersColorIndices.Clear();
        UnsubscribeCheerService();
        SubscribeCheerService();
    }

    void UnsubscribeAllSlots()
    {
        foreach (var slot in slots)
        {
            if (slot?.events == null) continue;
            if (slot.onDamaged != null)         slot.events.OnDamaged          -= slot.onDamaged;
            if (slot.onDied != null)            slot.events.OnDied             -= slot.onDied;
            if (slot.onRespawned != null)       slot.events.OnRespawned        -= slot.onRespawned;
            if (slot.onColorTypeChanged != null) slot.events.OnColorTypeChanged -= slot.onColorTypeChanged;
        }
    }

    // ── CheerService 구독 ─────────────────────────────────────────

    void SubscribeCheerService()
    {
        var svc = CheerService.Instance;
        if (svc == null) return;
        svc.OnCheerersChanged -= HandleCheerersChanged;
        svc.OnVoteReset       -= HandleVoteReset;
        svc.OnCheerersChanged += HandleCheerersChanged;
        svc.OnVoteReset       += HandleVoteReset;
    }

    /// <summary>colorIndex → "BERRY" 형태 대문자 CheerName. 매핑 실패 시 "???".</summary>
    static string GetCheerDisplayName(int colorIndex)
    {
        string name = CheerService.GetCheerName(colorIndex);
        return string.IsNullOrEmpty(name) ? "???" : name.ToUpper();
    }

    void UnsubscribeCheerService()
    {
        var svc = CheerService.Instance;
        if (svc == null) return;
        svc.OnCheerersChanged -= HandleCheerersChanged;
        svc.OnVoteReset       -= HandleVoteReset;
    }

    /// <summary>서버/솔로에서 응원자 목록이 바뀔 때 호출.</summary>
    void HandleCheerersChanged(int targetIdx, int[] cheererColorIndices)
    {
        // 초기화 시점 타이밍 문제로 -1이면 재계산
        if (_myColorIndex < 0) _myColorIndex = GetMyColorIndex();

        // 내가 응원 대상일 때만 처리
        if (_myColorIndex < 0 || targetIdx != _myColorIndex) return;

        _myCheerersColorIndices.Clear();
        foreach (int c in cheererColorIndices)
            _myCheerersColorIndices.Add(c);

        RefreshCheeringLabels();
    }

    /// <summary>표가 초기화(타임아웃·버프 발동)되면 Cheering 라벨 모두 숨김.</summary>
    void HandleVoteReset(int targetIdx)
    {
        if (_myColorIndex < 0 || targetIdx != _myColorIndex) return;
        _myCheerersColorIndices.Clear();
        RefreshCheeringLabels();
    }

    void RefreshCheeringLabels()
    {
        foreach (var slot in slots)
        {
            if (slot?.cheeringPanel == null) continue;
            slot.cheeringPanel.SetActive(_myCheerersColorIndices.Contains(slot.colorIndex));
        }
    }

    // ── 슬롯 생성 ─────────────────────────────────────────────────

    Sprite GetFullHeartSprite(PlayerColorType colorType)
    {
        if (colorHeartMap != null)
            foreach (var entry in colorHeartMap)
                if (entry.colorType == colorType) return entry.fullHeartSprite;
        return fullHeartSprite;
    }

    PlayerSlot CreateSlot(Player player)
    {
        var slot        = new PlayerSlot();
        slot.player     = player;
        slot.buffSystem = player.GetComponent<PlayerBuffSystem>();
        slot.colorIndex = ResolveColorIndex(player);

        // ── 슬롯 루트 ─────────────────────────────────────────────
        var root   = new GameObject(player.name);
        root.transform.SetParent(transform, false);
        var rootRt = root.AddComponent<RectTransform>();
        rootRt.sizeDelta  = new Vector2(slotWidth, slotHeight);
        slot.slotBg       = root.AddComponent<Image>();
        slot.slotBg.color = slotBgColor;

        // ── 이름 (위쪽 절반) ──────────────────────────────────────
        var nameObj = new GameObject("Name");
        nameObj.transform.SetParent(root.transform, false);
        slot.nameText           = nameObj.AddComponent<TextMeshProUGUI>();
        slot.nameText.text      = GetCheerDisplayName(slot.colorIndex);
        slot.nameText.fontSize  = 13f;
        slot.nameText.fontStyle = FontStyles.Bold;
        slot.nameText.color     = Color.white;
        var nameRt = nameObj.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 0.55f);
        nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.offsetMin = new Vector2(16f, 0f);
        nameRt.offsetMax = new Vector2(-4f, -2f);

        // ── 아래쪽 행 (HorizontalLayoutGroup) ────────────────────
        // 순서: [버프 아이콘(들)] [하트(들)] [Cheering 라벨]
        var bottomRow = new GameObject("BottomRow");
        bottomRow.transform.SetParent(root.transform, false);
        var rowHlg = bottomRow.AddComponent<HorizontalLayoutGroup>();
        rowHlg.spacing                = heartSpacing;
        rowHlg.childControlWidth      = false;
        rowHlg.childControlHeight     = false;
        rowHlg.childForceExpandWidth  = false;
        rowHlg.childForceExpandHeight = false;
        rowHlg.childAlignment         = TextAnchor.MiddleLeft;
        var rowRt = bottomRow.GetComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0f, 0f);
        rowRt.anchorMax = new Vector2(1f, 0.55f);
        rowRt.offsetMin = new Vector2(6f, 3f);
        rowRt.offsetMax = new Vector2(-4f, 0f);

        // ── 버프 아이콘 (하트 왼쪽) ───────────────────────────────
        if (slot.buffSystem != null && buffIconMap != null && buffIconMap.Length > 0)
        {
            foreach (var entry in buffIconMap)
            {
                var bObj = new GameObject(entry.type.ToString());
                bObj.transform.SetParent(bottomRow.transform, false);
                var bImg = bObj.AddComponent<Image>();
                bImg.sprite         = entry.icon;
                bImg.preserveAspect = true;
                bObj.GetComponent<RectTransform>().sizeDelta = new Vector2(buffIconSize, buffIconSize);
                bObj.SetActive(false); // 기본 숨김, Update에서 IsActive() 기준으로 갱신
                slot.buffIcons[entry.type] = bImg;
            }
        }

        // ── 하트 ─────────────────────────────────────────────────
        slot.heartImages = new Image[player.maxHeart];
        Sprite resolvedFull = GetFullHeartSprite(player.playerColorType);
        for (int i = 0; i < player.maxHeart; i++)
        {
            var hObj = new GameObject($"H{i}");
            hObj.transform.SetParent(bottomRow.transform, false);
            var hImg = hObj.AddComponent<Image>();
            hImg.sprite         = resolvedFull;
            hImg.preserveAspect = true;
            hObj.GetComponent<RectTransform>().sizeDelta = new Vector2(heartSize, heartSize);
            slot.heartImages[i] = hImg;
        }

        // ── Cheering 라벨 (하트 오른쪽) ──────────────────────────
        // 배경 패널
        var cheerBg = new GameObject("CheeringPanel");
        cheerBg.transform.SetParent(bottomRow.transform, false);
        var cheerBgImg = cheerBg.AddComponent<Image>();
        cheerBgImg.color  = cheeringBgColor;
        if (cheeringBgSprite != null)
        {
            cheerBgImg.sprite = cheeringBgSprite;
            cheerBgImg.type   = Image.Type.Sliced;
        }
        var cheerBgRt = cheerBg.GetComponent<RectTransform>();
        cheerBgRt.sizeDelta = new Vector2(cheeringWidth, heartSize);

        // 텍스트
        var cheerTxt = new GameObject("CheeringText");
        cheerTxt.transform.SetParent(cheerBg.transform, false);
        var cheerTmp           = cheerTxt.AddComponent<TextMeshProUGUI>();
        cheerTmp.text          = "Cheering";
        cheerTmp.fontSize      = cheeringFontSize;
        cheerTmp.color         = cheeringTextColor;
        cheerTmp.fontStyle     = FontStyles.Bold;
        cheerTmp.alignment     = TextAlignmentOptions.Center;
        var cheerTxtRt = cheerTxt.GetComponent<RectTransform>();
        cheerTxtRt.anchorMin = Vector2.zero;
        cheerTxtRt.anchorMax = Vector2.one;
        cheerTxtRt.offsetMin = cheerTxtRt.offsetMax = Vector2.zero;

        cheerBg.SetActive(false); // 기본 숨김, HandleCheerersChanged에서 갱신
        slot.cheeringPanel = cheerBg;

        // ── PlayerEvents 구독 ─────────────────────────────────────
        slot.events = player.GetComponent<PlayerEvents>();
        if (slot.events != null)
        {
            slot.onDamaged         = _ => RefreshSlot(slot);
            slot.onDied            = () => SetDead(slot, true);
            slot.onRespawned       = () => SetDead(slot, false);
            slot.onColorTypeChanged = _ => RefreshSlot(slot);
            slot.events.OnDamaged          += slot.onDamaged;
            slot.events.OnDied             += slot.onDied;
            slot.events.OnRespawned        += slot.onRespawned;
            slot.events.OnColorTypeChanged += slot.onColorTypeChanged;
        }

        RefreshSlot(slot);
        return slot;
    }

    // ── 갱신 ─────────────────────────────────────────────────────

    void RefreshSlot(PlayerSlot slot)
    {
        if (slot == null || slot.player == null) return;
        if (slot.slotBg == null) return; // Destroy된 슬롯 잔존 핸들러 방어

        slot.colorIndex = ResolveColorIndex(slot.player);
        if (slot.nameText != null)
            slot.nameText.text = GetCheerDisplayName(slot.colorIndex);

        Sprite resolvedFull = GetFullHeartSprite(slot.player.playerColorType);
        if (slot.heartImages == null) return;
        for (int i = 0; i < slot.heartImages.Length; i++)
        {
            if (slot.heartImages[i] == null) continue;
            slot.heartImages[i].sprite = i < slot.player.heart ? resolvedFull : emptyHeartSprite;
        }
    }

    void SetDead(PlayerSlot slot, bool isDead)
    {
        if (slot == null || slot.slotBg == null) return;
        slot.slotBg.color = isDead ? deadBgColor : slotBgColor;
        if (slot.nameText != null)
            slot.nameText.color = isDead ? deadTextColor : Color.white;

        if (isDead && slot.heartImages != null)
            foreach (var h in slot.heartImages)
                if (h != null) h.sprite = emptyHeartSprite;

        if (!isDead) RefreshSlot(slot);
    }

    // ── Update ───────────────────────────────────────────────────

    void Update()
    {
        foreach (var slot in slots)
        {
            if (slot?.buffSystem == null) continue;
            foreach (var kv in slot.buffIcons)
            {
                if (kv.Value == null) continue;
                kv.Value.gameObject.SetActive(slot.buffSystem.IsActive(kv.Key));
            }
        }
    }
}
