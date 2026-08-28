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
/// [숫자키 아이콘] [하트...]
///  ← HP 왼쪽        HP
///
/// - 숫자키 아이콘: 해당 팀원을 응원할 때 눌러야 할 숫자키(1~4) 안내.
///   실제로 존재하는 팀원(자신 제외)만 슬롯이 생성되므로 항상 유효한 대상만 노출됨.
/// </summary>
public class TeamStatusUI : MonoBehaviour
{
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
    [SerializeField] float slotWidth    = 400f;
    [SerializeField] float slotHeight   = 50f;
    [SerializeField] float slotSpacing  = 6f;
    [SerializeField] float heartSize    = 20f;
    [SerializeField] float heartSpacing = 2f;

    [Header("스프라이트")]
    [SerializeField] Sprite fullHeartSprite;
    [SerializeField] Sprite emptyHeartSprite;

    [Header("색별 하트 스프라이트")]
    [SerializeField] ColorHeartEntry[] colorHeartMap;

    [Tooltip("colorIndex(0=Blue/1=Purple/2=Green/3=Yellow) 순서 — CheerDigitInput 1~4 키에 대응하는 키캡 아이콘")]
    [Header("숫자키 아이콘")]
    [SerializeField] Sprite[] keyIconSprites = new Sprite[4];

    [Header("색상")]
    [SerializeField] Color slotBgColor = Color.clear;

    // ── 런타임 슬롯 ──────────────────────────────────────────────
    class PlayerSlot
    {
        public Player           player;
        public PlayerEvents     events;
        public int              colorIndex;   // PlayerColorUtil.ColorOrder 기준
        public Image            slotBg;
        public TextMeshProUGUI  nameText;
        public Image[]          heartImages;
        public Image            keyIcon;      // 숫자키 안내 아이콘

        // BuildSlots 재호출 시 언구독용 (람다 캡처 해제 필수)
        public System.Action<bool>             onDamaged;
        public System.Action                   onDied;
        public System.Action                   onRespawned;
        public System.Action<PlayerColorType>  onColorTypeChanged;
    }

    readonly List<PlayerSlot> slots = new();

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
            return System.Array.IndexOf(PlayerColorUtil.ColorOrder, sessionColor);

        return System.Array.IndexOf(PlayerColorUtil.ColorOrder, player.playerColorType);
    }

    void BuildSlots()
    {
        if (excludePlayer == null)
            excludePlayer = FindLocalOwnerPlayer();

        // BuildSlots 재호출 시 UI만 Destroy하고 OnDied 구독을 남기면
        // RaiseDied → destroyed Image 접근으로 ForceKillClientRpc가 예외 중단
        // → Owner ForceKill/씬 리로드가 깨진다. 반드시 전부 언구독 후 재구성.
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

    /// <summary>
    /// colorIndex → Steam 표시 이름(닉네임). 매핑 실패 시 "???".
    /// CheerName("BERRY" 등)은 응원 시 혼동을 줄이기 위해 캐릭터 머리 위(PlayerNameTagUI)로 이전했고,
    /// 이 코너 패널은 "실제로 누구인지" 확인용 Steam 닉네임을 표시한다.
    /// </summary>
    static string GetPlayerDisplayName(int colorIndex)
    {
        string name = GameSession.Instance != null ? GameSession.Instance.GetSessionDisplayName(colorIndex) : null;
        return string.IsNullOrEmpty(name) ? "???" : name;
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
        slot.nameText                = nameObj.AddComponent<TextMeshProUGUI>();
        slot.nameText.text           = GetPlayerDisplayName(slot.colorIndex);
        slot.nameText.fontSize       = 13f;
        slot.nameText.fontStyle      = FontStyles.Bold;
        slot.nameText.color          = Color.white;
        // 긴 Steam 닉네임이 줄바꿈되어 좁은 슬롯 높이에서 잘려 보이는 것을 방지:
        // 줄바꿈 금지 + 폭에 안 맞으면 폰트를 자동 축소.
        slot.nameText.textWrappingMode = TextWrappingModes.NoWrap;
        slot.nameText.enableAutoSizing = true;
        slot.nameText.fontSizeMin      = 8f;
        slot.nameText.fontSizeMax      = 13f;
        var nameRt = nameObj.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 0.55f);
        nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.offsetMin = new Vector2(16f, 0f);
        nameRt.offsetMax = new Vector2(-4f, -2f);

        // ── 아래쪽 행 (HorizontalLayoutGroup) ────────────────────
        // 순서: [숫자키 아이콘] [하트(들)]
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

        // ── 숫자키 아이콘 (하트 왼쪽) ─────────────────────────────
        if (keyIconSprites != null && slot.colorIndex >= 0 && slot.colorIndex < keyIconSprites.Length
            && keyIconSprites[slot.colorIndex] != null)
        {
            var kObj = new GameObject("KeyIcon");
            kObj.transform.SetParent(bottomRow.transform, false);
            var kImg = kObj.AddComponent<Image>();
            kImg.sprite         = keyIconSprites[slot.colorIndex];
            kImg.color          = PlayerColorUtil.GetUniqueColor(PlayerColorUtil.ColorOrder[slot.colorIndex]);
            kImg.preserveAspect = true;
            kObj.GetComponent<RectTransform>().sizeDelta = new Vector2(heartSize, heartSize);
            slot.keyIcon = kImg;
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
            slot.nameText.text = GetPlayerDisplayName(slot.colorIndex);

        if (slot.keyIcon != null && keyIconSprites != null
            && slot.colorIndex >= 0 && slot.colorIndex < keyIconSprites.Length && keyIconSprites[slot.colorIndex] != null)
        {
            slot.keyIcon.sprite = keyIconSprites[slot.colorIndex];
            slot.keyIcon.color  = PlayerColorUtil.GetUniqueColor(PlayerColorUtil.ColorOrder[slot.colorIndex]);
        }

        Sprite resolvedFull = GetFullHeartSprite(slot.player.playerColorType);
        if (slot.heartImages == null) return;
        for (int i = 0; i < slot.heartImages.Length; i++)
        {
            if (slot.heartImages[i] == null) continue;
            slot.heartImages[i].sprite = i < slot.player.heart ? resolvedFull : emptyHeartSprite;
        }
    }

    /// <summary>사망 시 하트를 전부 빈 하트로 표시. 죽으면 씬이 리셋되므로 별도 배경/텍스트 색 연출은 불필요.</summary>
    void SetDead(PlayerSlot slot, bool isDead)
    {
        if (slot == null || slot.slotBg == null) return;

        if (isDead && slot.heartImages != null)
            foreach (var h in slot.heartImages)
                if (h != null) h.sprite = emptyHeartSprite;

        if (!isDead) RefreshSlot(slot);
    }
}
