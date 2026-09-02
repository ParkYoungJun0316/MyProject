using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// TeamStatus_Panel에 붙이는 스크립트.
/// 씬의 모든 Player(자신 제외)를 자동 수집해 슬롯 생성.
///
/// [각 슬롯 레이아웃]
/// 이름(위) / HP 하트(아래). 그 외 아이콘 없음.
///
/// 팀워드 응원 진행도는 이 패널이 아니라 캐릭터 머리 위 하트(PlayerCheerHeartsUI)로 표시한다
/// (CheerSystemDesign.md §10.3, 사용자 결정 2026-09-01 — 코너 패널에 새 아이콘 추가하지 않음).
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
    [Tooltip("슬롯 최소 높이. 이름 폰트+하트+간격보다 작으면 자동으로 늘어남(VLG가 슬롯 간격을 유지하므로 플레이어끼리 겹치지 않음).")]
    [SerializeField] float slotHeight   = 50f;
    [SerializeField] float slotSpacing  = 6f;
    [SerializeField] float heartSize    = 20f;
    [SerializeField] float heartSpacing = 2f;
    [Tooltip("Steam 닉네임 글자 크기. 길면 한 줄에서 …으로 잘림(오토사이즈 없음).")]
    [SerializeField] float nameFontSize = 22f;
    [Tooltip("이름과 HP 하트 사이 세로 간격(px).")]
    [SerializeField] float nameHeartGap = 4f;

    [Header("스프라이트")]
    [SerializeField] Sprite fullHeartSprite;
    [SerializeField] Sprite emptyHeartSprite;

    [Header("색별 하트 스프라이트")]
    [SerializeField] ColorHeartEntry[] colorHeartMap;

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

        // BuildSlots 재호출 시 언구독용 (람다 캡처 해제 필수)
        public System.Action                   onDamaged;
        public System.Action                   onHealed;
        public System.Action                   onDied;
        public System.Action                   onRespawned;
        public System.Action<PlayerColorType>  onColorTypeChanged;
    }

    readonly List<PlayerSlot> slots = new();

    // BuildSlots 재호출 코얼레싱 — OnPlayersReady/OnRosterChanged가 같은 프레임에 몰려도 1회만 실행
    bool _rebuildPending;

    // ── 초기화 ───────────────────────────────────────────────────

    void Start()
    {
        PlayerSpawnCoordinator.OnPlayersReady += RequestRebuild;
        PlayerSpawnCoordinator.OnRosterChanged += RequestRebuild;
        PlayerDisplayNameSync.OnAnyDisplayNameChanged += RefreshAllSlotNames;
        if (PlayerSpawnCoordinator.IsReady) RequestRebuild();
    }

    void OnDestroy()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= RequestRebuild;
        PlayerSpawnCoordinator.OnRosterChanged -= RequestRebuild;
        PlayerDisplayNameSync.OnAnyDisplayNameChanged -= RefreshAllSlotNames;
        UnsubscribeAllSlots();
    }

    /// <summary>
    /// BuildSlots() 재호출을 다음 프레임으로 1회만 합친다(디바운스).
    /// - M/T 배치 스폰: N명이 한 프레임에 각자 OnRosterChanged를 발행 + 곧이어 OnPlayersReady까지 →
    ///   원래는 N+1번 리빌드되던 것을 1번으로 줄인다.
    /// - Despawn 직후 즉시 리빌드하면 문제: NGO는 OnNetworkDespawn(=OnRosterChanged 발행 시점)을
    ///   IsSpawned=false 갱신·GameObject Destroy()보다 먼저 호출한다(InvokeBehaviourNetworkDespawn →
    ///   ResetOnDespawn → Destroy 순서, NetworkSpawnManager.OnDespawnObject 확인됨). 즉시 BuildSlots를
    ///   돌리면 방금 나간 플레이어가 FindObjectsByType에 여전히 잡혀 슬롯이 남는다. 한 프레임 미루면
    ///   그 사이 Destroy()가 반영되어 정상적으로 빠진다.
    /// </summary>
    void RequestRebuild()
    {
        if (_rebuildPending) return;
        _rebuildPending = true;
        StartCoroutine(RebuildNextFrame());
    }

    System.Collections.IEnumerator RebuildNextFrame()
    {
        yield return null;
        _rebuildPending = false;
        BuildSlots();
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
        if (!isActiveAndEnabled) return;

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
        {
            if (p == null || p == excludePlayer) continue;
            var net = p.GetComponent<NetworkObject>();
            if (net != null && !net.IsSpawned) continue;
            slots.Add(CreateSlot(p));
        }
    }

    void RefreshAllSlotNames()
    {
        if (!isActiveAndEnabled) return;
        foreach (var slot in slots)
            RefreshSlot(slot);
    }

    void UnsubscribeAllSlots()
    {
        foreach (var slot in slots)
        {
            if (slot?.events == null) continue;
            if (slot.onDamaged != null)         slot.events.OnDamaged          -= slot.onDamaged;
            if (slot.onHealed != null)          slot.events.OnHealed           -= slot.onHealed;
            if (slot.onDied != null)            slot.events.OnDied             -= slot.onDied;
            if (slot.onRespawned != null)       slot.events.OnRespawned        -= slot.onRespawned;
            if (slot.onColorTypeChanged != null) slot.events.OnColorTypeChanged -= slot.onColorTypeChanged;
        }
    }

    /// <summary>
    /// colorIndex → Steam 표시 이름(닉네임). 매핑 실패 시 "???".
    /// 우선순위는 CheerService.GetCheerName과 동일 규칙(세션 확정값 우선 → 실시간 NV 폴백) —
    /// 게이트 후(세션 확정)엔 스냅샷을 그대로 쓰고, 게이트 전(Tutorial, 세션 미확정)에만
    /// PlayerDisplayNameSync 실시간 NV를 스캔한다. DisplayName은 재제출 UI가 없어 스테이지
    /// 재스폰 때마다 같은 값이 그대로 재보고되므로 두 값은 항상 수렴하지만, 우선순위 규칙을
    /// CheerName과 동일하게 맞춰 SSOT 판단 기준을 하나로 통일한다.
    /// CheerName("BERRY" 등)은 응원 시 혼동을 줄이기 위해 캐릭터 머리 위(PlayerNameTagUI)로 이전했고,
    /// 이 코너 패널은 "실제로 누구인지" 확인용 Steam 닉네임을 표시한다.
    /// </summary>
    static string GetPlayerDisplayName(int colorIndex)
    {
        if (GameSession.Instance != null && GameSession.Instance.HasSessionDisplayNames)
        {
            string session = GameSession.Instance.GetSessionDisplayName(colorIndex);
            if (!string.IsNullOrEmpty(session)) return session;
        }

        foreach (var (clientId, name) in PlayerDisplayNameSync.GetAllEffectiveNames())
        {
            if (string.IsNullOrEmpty(name)) continue;
            if (!PlayerSpawnCoordinator.TryGetColor(clientId, out var color)) continue;
            if (PlayerColorUtil.ColorTypeToIndex(color) == colorIndex)
                return name;
        }

        return "???";
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

        const float topPad    = 2f;
        const float bottomPad = 3f;
        float nameRowH = Mathf.Max(8f, nameFontSize + 8f);
        float gap      = Mathf.Max(0f, nameHeartGap);
        float usedH    = topPad + nameRowH + gap + heartSize + bottomPad;
        float h        = Mathf.Max(slotHeight, usedH);

        // ── 슬롯 루트 ─────────────────────────────────────────────
        var root   = new GameObject(player.name);
        root.transform.SetParent(transform, false);
        var rootRt = root.AddComponent<RectTransform>();
        rootRt.sizeDelta  = new Vector2(slotWidth, h);
        slot.slotBg       = root.AddComponent<Image>();
        slot.slotBg.color = slotBgColor;

        // ── 이름 (위). 한 줄 고정, 길면 … ────────────────────────
        var nameObj = new GameObject("Name");
        nameObj.transform.SetParent(root.transform, false);
        slot.nameText                  = nameObj.AddComponent<TextMeshProUGUI>();
        slot.nameText.text             = GetPlayerDisplayName(slot.colorIndex);
        slot.nameText.fontSize         = nameFontSize;
        slot.nameText.fontStyle        = FontStyles.Bold;
        slot.nameText.color            = Color.white;
        slot.nameText.alignment        = TextAlignmentOptions.MidlineLeft;
        slot.nameText.textWrappingMode = TextWrappingModes.NoWrap;
        slot.nameText.overflowMode     = TextOverflowModes.Ellipsis;
        slot.nameText.enableAutoSizing = false;
        var nameRt = nameObj.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 1f);
        nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.offsetMin = new Vector2(16f, -topPad - nameRowH);
        nameRt.offsetMax = new Vector2(-4f, -topPad);

        // ── 아래쪽 행 (HorizontalLayoutGroup) ────────────────────
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
        rowRt.anchorMax = new Vector2(1f, 0f);
        rowRt.offsetMin = new Vector2(6f, bottomPad);
        rowRt.offsetMax = new Vector2(-4f, bottomPad + heartSize);

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
            slot.onDamaged         = () => RefreshSlot(slot);
            slot.onHealed          = () => RefreshSlot(slot);
            slot.onDied            = () => SetDead(slot, true);
            slot.onRespawned       = () => SetDead(slot, false);
            slot.onColorTypeChanged = _ => RefreshSlot(slot);
            slot.events.OnDamaged          += slot.onDamaged;
            slot.events.OnHealed           += slot.onHealed;
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
