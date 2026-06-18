using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// TeamStatus_Panel에 붙이는 스크립트.
/// 씬의 모든 Player를 자동 수집해 슬롯을 생성.
/// 각 슬롯에 이름, 하트, 버프 아이콘, 사망 오버레이 표시.
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
    [Tooltip("팀원 목록에서 제외할 플레이어 (내 캐릭터). HP_Panel의 Player와 동일하게 연결.")]
    [SerializeField] Player excludePlayer;

    [Header("슬롯 크기")]
    [SerializeField] float slotWidth        = 180f;
    [SerializeField] float slotHeight       = 50f;
    [SerializeField] float slotSpacing      = 6f;
    [SerializeField] float heartSize        = 20f;
    [SerializeField] float heartSpacing     = 2f;
    [SerializeField] float buffIconSize     = 18f;
    [Tooltip("하트 마지막 칸과 버프 아이콘 사이 간격 (px)")]
    [SerializeField] float heartBuffSpacing = 6f;

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

    // ── 런타임 슬롯 ──────────────────────────────────────────────
    class PlayerSlot
    {
        public Player            player;
        public PlayerBuffSystem  buffSystem;
        public Image             slotBg;
        public TextMeshProUGUI   nameText;
        public Image[]           heartImages;
        public Dictionary<PlayerBuffSystem.BuffType, Image> buffIcons
            = new Dictionary<PlayerBuffSystem.BuffType, Image>();
    }

    List<PlayerSlot> slots = new List<PlayerSlot>();

    // ── 초기화 ───────────────────────────────────────────────────

    void Start()
    {
        BuildSlots();
    }

    void BuildSlots()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
        slots.Clear();

        VerticalLayoutGroup vlg = GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing                = slotSpacing;
        vlg.childControlWidth      = false;
        vlg.childControlHeight     = false;
        vlg.childForceExpandWidth  = false;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment         = TextAnchor.UpperLeft;

        // GameSession이 있으면 활성 플레이어만, 없으면 씬 전체
        IEnumerable<Player> players;
        if (GameSession.Instance != null)
            players = GameSession.Instance.GetActivePlayers();
        else
            players = FindObjectsByType<Player>(FindObjectsSortMode.None);

        foreach (var p in players)
            if (p != null && p != excludePlayer)
                slots.Add(CreateSlot(p));
    }

    Sprite GetFullHeartSprite(PlayerColorType colorType)
    {
        if (colorHeartMap != null)
            foreach (var entry in colorHeartMap)
                if (entry.colorType == colorType)
                    return entry.fullHeartSprite;
        return fullHeartSprite;
    }

    PlayerSlot CreateSlot(Player player)
    {
        PlayerSlot slot = new PlayerSlot();
        slot.player     = player;
        slot.buffSystem = player.GetComponent<PlayerBuffSystem>();

        // 슬롯 루트
        GameObject root = new GameObject(player.name);
        root.transform.SetParent(transform, false);
        RectTransform rootRt = root.AddComponent<RectTransform>();
        rootRt.sizeDelta = new Vector2(slotWidth, slotHeight);
        slot.slotBg       = root.AddComponent<Image>();
        slot.slotBg.color = slotBgColor;

        // 이름
        GameObject nameObj = new GameObject("Name");
        nameObj.transform.SetParent(root.transform, false);
        slot.nameText           = nameObj.AddComponent<TextMeshProUGUI>();
        slot.nameText.text      = player.name;
        slot.nameText.fontSize  = 13f;
        slot.nameText.fontStyle = FontStyles.Bold;
        slot.nameText.color     = Color.white;
        RectTransform nameRt = nameObj.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 0.55f);
        nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.offsetMin = new Vector2(6f, 0f);
        nameRt.offsetMax = new Vector2(-4f, -2f);

        // 하트 + 버프 인라인 행
        GameObject bottomRow = new GameObject("BottomRow");
        bottomRow.transform.SetParent(root.transform, false);
        HorizontalLayoutGroup rowHlg = bottomRow.AddComponent<HorizontalLayoutGroup>();
        rowHlg.spacing                = heartSpacing;
        rowHlg.childControlWidth      = false;
        rowHlg.childControlHeight     = false;
        rowHlg.childForceExpandWidth  = false;
        rowHlg.childForceExpandHeight = false;
        rowHlg.childAlignment         = TextAnchor.MiddleLeft;
        RectTransform bottomRowRt = bottomRow.GetComponent<RectTransform>();
        bottomRowRt.anchorMin = new Vector2(0f, 0f);
        bottomRowRt.anchorMax = new Vector2(1f, 0.55f);
        bottomRowRt.offsetMin = new Vector2(6f, 3f);
        bottomRowRt.offsetMax = new Vector2(-4f, 0f);

        // 하트 생성 (플레이어 색에 맞는 스프라이트)
        slot.heartImages = new Image[player.maxHeart];
        Sprite resolvedFull = GetFullHeartSprite(player.playerColorType);
        for (int i = 0; i < player.maxHeart; i++)
        {
            GameObject hObj = new GameObject($"H{i}");
            hObj.transform.SetParent(bottomRow.transform, false);
            Image hImg = hObj.AddComponent<Image>();
            hImg.sprite         = resolvedFull;
            hImg.preserveAspect = true;
            hObj.GetComponent<RectTransform>().sizeDelta = new Vector2(heartSize, heartSize);
            slot.heartImages[i] = hImg;
        }

        // 하트-버프 스페이서
        if (slot.buffSystem != null && buffIconMap != null && buffIconMap.Length > 0 && heartBuffSpacing > 0f)
        {
            GameObject spacerObj = new GameObject("HeartBuffSpacer");
            spacerObj.transform.SetParent(bottomRow.transform, false);
            LayoutElement le = spacerObj.AddComponent<LayoutElement>();
            le.minWidth       = heartBuffSpacing;
            le.preferredWidth = heartBuffSpacing;
        }

        // 버프 아이콘 (하트 끝에 인라인)
        if (slot.buffSystem != null && buffIconMap != null && buffIconMap.Length > 0)
        {
            foreach (var entry in buffIconMap)
            {
                GameObject bObj = new GameObject(entry.type.ToString());
                bObj.transform.SetParent(bottomRow.transform, false);
                Image bImg = bObj.AddComponent<Image>();
                bImg.sprite         = entry.icon;
                bImg.preserveAspect = true;
                bObj.GetComponent<RectTransform>().sizeDelta = new Vector2(buffIconSize, buffIconSize);
                bObj.SetActive(false);
                slot.buffIcons[entry.type] = bImg;
            }
        }

        // 이벤트 구독
        PlayerEvents events = player.GetComponent<PlayerEvents>();
        if (events != null)
        {
            events.OnDamaged   += _ => RefreshSlot(slot);
            events.OnDied      +=  () => SetDead(slot, true);
            events.OnRespawned +=  () => SetDead(slot, false);
        }

        RefreshSlot(slot);
        return slot;
    }

    // ── 갱신 ─────────────────────────────────────────────────────

    void RefreshSlot(PlayerSlot slot)
    {
        if (slot == null || slot.player == null) return;

        Sprite resolvedFull = GetFullHeartSprite(slot.player.playerColorType);
        if (slot.heartImages != null)
        {
            for (int i = 0; i < slot.heartImages.Length; i++)
            {
                if (slot.heartImages[i] == null) continue;
                slot.heartImages[i].sprite = i < slot.player.heart
                    ? resolvedFull : emptyHeartSprite;
            }
        }
    }

    void SetDead(PlayerSlot slot, bool isDead)
    {
        if (slot == null) return;
        slot.slotBg.color      = isDead ? deadBgColor  : slotBgColor;
        slot.nameText.color    = isDead ? deadTextColor : Color.white;

        if (isDead && slot.heartImages != null)
            foreach (var h in slot.heartImages)
                if (h != null) h.sprite = emptyHeartSprite;

        if (!isDead) RefreshSlot(slot);
    }

    // 버프 아이콘은 Update에서 매 프레임 갱신 (버프 수가 적어 부담 없음)
    void Update()
    {
        foreach (var slot in slots)
        {
            if (slot?.buffSystem == null) continue;
            foreach (var kv in slot.buffIcons)
                kv.Value.gameObject.SetActive(slot.buffSystem.IsActive(kv.Key));
        }
    }
}
