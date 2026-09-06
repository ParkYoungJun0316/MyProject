using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// TeamStatus_Panel에 붙이는 스크립트.
///
/// [고정 슬롯 설계 (2026-09-06)]
/// 예전엔 씬의 Player를 매 리빌드마다 FindObjectsByType으로 스캔해서 슬롯 GameObject를
/// Destroy→재생성했다. Tutorial처럼 인원이 한 명씩 순차 합류하는 상황에서 이 방식이
/// NetworkDesign.md §6B.7 버그3/4(리빌드 데드락, "Player" 고착, 명단 소스 불일치)의
/// 근본 원인이었다. 지금은 <see cref="PlayerColorUtil.ColorOrder"/> 4색 슬롯을 인스펙터에서
/// 미리 배치해두고(에디터에서 크기/폰트/간격을 Scene뷰로 직접 조절 가능), 런타임엔 슬롯을
/// "채우거나 비우기"만 한다 — Destroy/Instantiate 없음.
///
/// - 슬롯 = 색 고정(0=Blue/1=Purple/2=Green/3=Yellow, ColorOrder 순). "내 색" 슬롯은 항상
///   완전히 숨김(root.SetActive(false)) — VerticalLayoutGroup이 비활성 자식을 레이아웃에서
///   자동으로 빼므로 빈 줄 없이 나머지가 위로 붙는다.
/// - 아직 아무도 없는 색 슬롯은 <see cref="showEmptySlots"/>가 true면(Tutorial 게이트 전
///   인스턴스) emptyGroup을 보여주고, false면(M/T 스테이지 인스턴스, 기존 동작) 완전히 숨김.
/// - 플레이어는 ChangeColorCooldownUI 등으로 게임 중 색을 바꿀 수 있어(PlayerEvents.
///   OnColorTypeChanged) 슬롯 소속 자체가 바뀔 수 있다. 그래서 "지금 씬에 있는 모든 Player"
///   (나 포함)의 OnColorTypeChanged를 감시해 변경 시 전체 재배치(RequestRebuild)한다 — 슬롯
///   하나만 갱신하면 옛 슬롯이 그대로 남는다.
///
/// [레이아웃]
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

    /// <summary>
    /// 고정 색 슬롯 하나. 전부 에디터에서 미리 배치 — Scene뷰에서 크기/폰트/간격을 직접 조절.
    /// 런타임 코드는 root/filledGroup/emptyGroup의 SetActive와 nameText/heartImages 내용만 건드린다.
    /// </summary>
    [System.Serializable]
    public class ColorSlot
    {
        [Tooltip("이 슬롯이 담당하는 색. PlayerColorUtil.ColorOrder 순서(Blue/Purple/Green/Yellow)와 맞출 것.")]
        public PlayerColorType colorType;

        [Tooltip("슬롯 전체 컨테이너. 비활성화하면 VerticalLayoutGroup이 자리를 접어 나머지가 붙는다.")]
        public GameObject root;

        [Tooltip("실제 데이터(이름+하트) 표시 그룹.")]
        public GameObject filledGroup;
        public TextMeshProUGUI nameText;
        [Tooltip("Player.maxHeart(고정 프리팹 기준) 개수만큼 미리 배치. 실제 maxHeart보다 많으면 나머지는 자동 숨김.")]
        public Image[] heartImages;

        [Tooltip("아직 합류하지 않은 자리 표시 그룹(플레이스홀더). showEmptySlots가 true일 때만 사용.")]
        public GameObject emptyGroup;

        // ── 런타임 상태 (인스펙터 비노출) ──────────────────────────
        [System.NonSerialized] public int colorIndex = -1;
        [System.NonSerialized] public Player player;
        [System.NonSerialized] public PlayerEvents events;
        [System.NonSerialized] public System.Action onDamaged;
        [System.NonSerialized] public System.Action onHealed;
        [System.NonSerialized] public System.Action onDied;
        [System.NonSerialized] public System.Action onRespawned;
    }

    [Header("연결")]
    [Tooltip("팀원 목록에서 제외할 플레이어 (내 캐릭터). 비워두면 자동 탐색.")]
    [SerializeField] Player excludePlayer;

    [Header("고정 슬롯 — ColorOrder 순(Blue/Purple/Green/Yellow)으로 4개 등록")]
    [SerializeField] ColorSlot[] slots = new ColorSlot[4];

    [Header("빈 자리 표시")]
    [Tooltip("체크하면 아직 합류하지 않은 색 슬롯을 emptyGroup으로 표시한다. " +
             "Tutorial(게이트 전) 인스턴스에서만 켠다 — M/T 스테이지 인스턴스는 꺼서 미참여 색을 완전히 숨긴다(기존 동작).")]
    [SerializeField] bool showEmptySlots = false;

    [Header("스프라이트")]
    [SerializeField] Sprite fullHeartSprite;
    [SerializeField] Sprite emptyHeartSprite;

    [Header("색별 하트 스프라이트")]
    [SerializeField] ColorHeartEntry[] colorHeartMap;

    // BuildSlots 재호출 코얼레싱 — OnPlayersReady/OnRosterChanged/색변경이 같은 프레임에 몰려도 1회만 실행
    bool _rebuildPending;

    // "지금 씬에 있는 모든 Player"(나 포함)의 색변경 감시용 — 슬롯 소속 자체가 바뀔 수 있어서
    // 슬롯에 들어간 player만 보면 안 되고 전원을 봐야 한다.
    readonly HashSet<PlayerEvents> _watchedColorEvents = new();
    readonly HashSet<PlayerEvents> _liveColorEvents = new();   // SyncColorWatchers 스크래치(매 리빌드 재사용)

    // ── 초기화 ───────────────────────────────────────────────────

    void Awake()
    {
        // ContentSizeFitter가 슬롯 수만큼 세로로 늘 때, 피벗이 중앙이면 패널이
        // HP_Panel/CheerName 쪽으로 올라간다. 상단 고정 → 아래로만 늘어남.
        var rt = (RectTransform)transform;
        Vector2 pivot = rt.pivot;
        pivot.y = 1f;
        rt.pivot = pivot;

        foreach (var slot in slots)
            if (slot != null)
                slot.colorIndex = System.Array.IndexOf(PlayerColorUtil.ColorOrder, slot.colorType);

        ValidateSlotWiring();
    }

    /// <summary>
    /// 슬롯은 전부 에디터 연결에 의존하므로(코드가 더 이상 생성하지 않음) 빠진 연결을 조용히
    /// 넘기지 않고 1회 경고한다 — 안 그러면 "패널이 그냥 안 보인다"로만 관측된다.
    /// </summary>
    void ValidateSlotWiring()
    {
        int usable = 0;
        var seen = new HashSet<PlayerColorType>();

        foreach (var slot in slots)
        {
            if (slot == null) continue;

            if (slot.colorIndex < 0)
            {
                Debug.LogWarning($"[TeamStatusUI] 슬롯 colorType이 {slot.colorType} — ColorOrder(Blue/Purple/Green/Yellow)에 없어 이 슬롯은 항상 숨겨집니다. ({name})", this);
                continue;
            }
            if (!seen.Add(slot.colorType))
                Debug.LogWarning($"[TeamStatusUI] 슬롯 colorType {slot.colorType}이 중복 등록됐습니다 — 같은 플레이어가 두 칸에 표시됩니다. ({name})", this);
            if (slot.root == null)
            {
                Debug.LogWarning($"[TeamStatusUI] {slot.colorType} 슬롯의 root가 비어 있어 표시되지 않습니다. ({name})", this);
                continue;
            }
            if (slot.nameText == null)
                Debug.LogWarning($"[TeamStatusUI] {slot.colorType} 슬롯의 nameText가 비어 있습니다 — 닉네임이 표시되지 않습니다. ({name})", this);
            if (slot.heartImages == null || slot.heartImages.Length == 0)
                Debug.LogWarning($"[TeamStatusUI] {slot.colorType} 슬롯의 heartImages가 비어 있습니다 — HP가 표시되지 않습니다. ({name})", this);
            if (showEmptySlots && slot.emptyGroup == null)
                Debug.LogWarning($"[TeamStatusUI] {slot.colorType} 슬롯의 emptyGroup이 비어 있습니다 — showEmptySlots가 켜져 있는데 빈 자리 표시가 없습니다. ({name})", this);

            usable++;
        }

        if (usable == 0)
            Debug.LogWarning($"[TeamStatusUI] 사용 가능한 고정 슬롯이 없습니다 — 인스펙터에서 ColorOrder 4색 슬롯을 연결해야 팀 상태가 표시됩니다. ({name})", this);
    }

    void Start()
    {
        PlayerSpawnCoordinator.OnPlayersReady += RequestRebuild;
        PlayerSpawnCoordinator.OnRosterChanged += RequestRebuild;
        PlayerDisplayNameSync.OnAnyDisplayNameChanged += RefreshAllSlotNames;
        if (PlayerSpawnCoordinator.IsReady) RequestRebuild();
    }

    /// <summary>
    /// 패널이 (다시) 켜질 때 재구성. 구독은 Start/OnDestroy 짝이라 비활성 중에도 Ready/Roster를
    /// 받는데, 그 요청들은 RequestRebuild가 흘려보내므로(아래) 켜지는 시점에 한 번 따라잡는다.
    /// </summary>
    void OnEnable() => RequestRebuild();

    /// <summary>비활성화 중 예약된 리빌드는 Unity가 코루틴을 정지시키므로 플래그를 되돌려 놓는다.</summary>
    void OnDisable() => _rebuildPending = false;

    void OnDestroy()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= RequestRebuild;
        PlayerSpawnCoordinator.OnRosterChanged -= RequestRebuild;
        PlayerDisplayNameSync.OnAnyDisplayNameChanged -= RefreshAllSlotNames;
        UnsubscribeAllSlots();
        ClearColorWatchers();
    }

    /// <summary>
    /// RefreshSlots() 재호출을 다음 프레임으로 1회만 합친다(디바운스).
    /// - M/T 배치 스폰: N명이 한 프레임에 각자 OnRosterChanged를 발행 + 곧이어 OnPlayersReady까지 →
    ///   원래는 N+1번 리빌드되던 것을 1번으로 줄인다.
    /// - Despawn 직후 즉시 리빌드하면 문제: NGO는 OnNetworkDespawn(=OnRosterChanged 발행 시점)을
    ///   IsSpawned=false 갱신·GameObject Destroy()보다 먼저 호출한다(InvokeBehaviourNetworkDespawn →
    ///   ResetOnDespawn → Destroy 순서, NetworkSpawnManager.OnDespawnObject 확인됨). 즉시 돌리면
    ///   방금 나간 플레이어가 FindObjectsByType에 여전히 잡혀 그 색 슬롯이 안 비워진다. 한 프레임
    ///   미루면 그 사이 Destroy()가 반영되어 정상적으로 빠진다.
    /// </summary>
    void RequestRebuild()
    {
        if (_rebuildPending) return;
        // 비활성 상태에서는 플래그를 세우지 않는다 — StartCoroutine이 코루틴을 시작하지 못하는데
        // _rebuildPending만 true로 남으면 되돌릴 곳이 RebuildNextFrame뿐이라 이후 모든 요청이
        // 첫 줄에서 막혀 그 씬 내내 다시 그려지지 않는다(2026-09-05, Steam 4인 테스트).
        // 놓친 요청은 OnEnable이 따라잡는다.
        if (!isActiveAndEnabled) return;
        _rebuildPending = true;
        StartCoroutine(RebuildNextFrame());
    }

    System.Collections.IEnumerator RebuildNextFrame()
    {
        yield return null;
        _rebuildPending = false;
        RefreshSlots();
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
    /// ColorOrder 인덱스(0=Blue …). PlayerSpawnCoordinator(NetworkList) 우선, 없으면 playerColorType.
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

    // ── 슬롯 배치 ─────────────────────────────────────────────────

    void RefreshSlots()
    {
        if (!isActiveAndEnabled) return;

        if (excludePlayer == null)
            excludePlayer = FindLocalOwnerPlayer();

        var allPlayers = FindObjectsByType<Player>(FindObjectsSortMode.None);
        SyncColorWatchers(allPlayers);

        int myColorIndex = ResolveColorIndex(excludePlayer);

        var byColor = new Player[PlayerColorUtil.ColorOrder.Length];
        foreach (var p in allPlayers)
        {
            if (p == null || p == excludePlayer) continue;
            var net = p.GetComponent<NetworkObject>();
            if (net != null && !net.IsSpawned) continue;
            int idx = ResolveColorIndex(p);
            if (idx >= 0 && idx < byColor.Length) byColor[idx] = p;
        }

        foreach (var slot in slots)
        {
            if (slot == null || slot.root == null) continue;

            // 슬롯의 색은 colorType(=colorIndex) 하나만이 SSOT. 인스펙터 배열 순서는 "화면에 위에서
            // 아래로 어떤 순서로 보일지"만 결정하며, 어떤 플레이어가 들어갈지에는 관여하지 않는다.
            // (배열 순서로 인덱싱하면 사용자가 순서를 바꿔 배치했을 때 이름은 colorIndex 기준,
            //  HP는 배열 위치 기준이 되어 서로 다른 사람의 정보가 한 슬롯에 섞인다.)
            int ci = slot.colorIndex;
            if (ci < 0 || ci >= byColor.Length)
            {
                // colorType 미설정(기본값 Common) 등 — ColorOrder에 없는 색. 표시할 대상이 없다.
                SetSlotPlayer(slot, null);
                slot.root.SetActive(false);
                continue;
            }

            bool isMine = ci == myColorIndex;
            ApplySlot(slot, isMine ? null : byColor[ci], isMine);
        }
    }

    void ApplySlot(ColorSlot slot, Player player, bool isMine)
    {
        if (slot == null || slot.root == null) return;

        if (isMine)
        {
            SetSlotPlayer(slot, null);
            slot.root.SetActive(false);
            return;
        }

        if (player != null)
        {
            SetSlotPlayer(slot, player);
            slot.root.SetActive(true);
            SetFilledVisible(slot, true);
            if (slot.emptyGroup != null) slot.emptyGroup.SetActive(false);
            RefreshSlotVisual(slot);   // 하트 개수(maxHeart)까지 여기서 정리 — SetFilledVisible 뒤에 와야 한다
        }
        else
        {
            SetSlotPlayer(slot, null);
            if (showEmptySlots)
            {
                slot.root.SetActive(true);
                SetFilledVisible(slot, false);
                if (slot.emptyGroup != null) slot.emptyGroup.SetActive(true);
            }
            else
            {
                slot.root.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 이름+하트(실데이터) 표시 on/off. filledGroup을 연결했으면 그것만 토글하고,
    /// 연결하지 않은 배치(이름/하트를 root 직속으로 둔 경우)에는 개별로 끈다 —
    /// 안 그러면 빈 자리에 직전 플레이어의 닉네임·하트가 그대로 남는다.
    /// </summary>
    static void SetFilledVisible(ColorSlot slot, bool visible)
    {
        if (slot.filledGroup != null)
        {
            slot.filledGroup.SetActive(visible);
            return;
        }

        if (slot.nameText != null) slot.nameText.gameObject.SetActive(visible);
        if (slot.heartImages != null)
            foreach (var h in slot.heartImages)
                if (h != null) h.gameObject.SetActive(visible);
    }

    /// <summary>슬롯이 담당할 Player를 바꾼다. 실제로 바뀔 때만 PlayerEvents 구독을 갈아탄다.</summary>
    void SetSlotPlayer(ColorSlot slot, Player player)
    {
        if (slot.player == player) return;

        if (slot.events != null)
        {
            if (slot.onDamaged != null) slot.events.OnDamaged -= slot.onDamaged;
            if (slot.onHealed != null) slot.events.OnHealed -= slot.onHealed;
            if (slot.onDied != null) slot.events.OnDied -= slot.onDied;
            if (slot.onRespawned != null) slot.events.OnRespawned -= slot.onRespawned;
        }

        slot.player = player;
        slot.events = player != null ? player.GetComponent<PlayerEvents>() : null;

        if (slot.events != null)
        {
            var s = slot; // 클로저 캡처 안전화
            slot.onDamaged = () => RefreshSlotVisual(s);
            slot.onHealed = () => RefreshSlotVisual(s);
            slot.onDied = () => SetDead(s, true);
            slot.onRespawned = () => SetDead(s, false);
            slot.events.OnDamaged += slot.onDamaged;
            slot.events.OnHealed += slot.onHealed;
            slot.events.OnDied += slot.onDied;
            slot.events.OnRespawned += slot.onRespawned;
        }
        else
        {
            slot.onDamaged = slot.onHealed = slot.onDied = slot.onRespawned = null;
        }
    }

    void UnsubscribeAllSlots()
    {
        foreach (var slot in slots)
            if (slot != null) SetSlotPlayer(slot, null);
    }

    // ── 색 변경 감시 (슬롯 소속 자체가 바뀌는 케이스) ───────────────

    /// <summary>
    /// 지금 씬의 모든 Player(나 포함)의 OnColorTypeChanged를 구독/해제 동기화.
    /// 색이 바뀌면 슬롯 하나만 갱신해서는 안 된다 — 옛 색 슬롯은 비워지고 새 색 슬롯이 채워져야
    /// 하므로 전체 재배치(RequestRebuild)를 태운다.
    /// </summary>
    void SyncColorWatchers(IEnumerable<Player> allPlayers)
    {
        _liveColorEvents.Clear();
        foreach (var p in allPlayers)
        {
            var evt = p != null ? p.GetComponent<PlayerEvents>() : null;
            if (evt != null) _liveColorEvents.Add(evt);
        }

        foreach (var evt in _liveColorEvents)
            if (_watchedColorEvents.Add(evt))
                evt.OnColorTypeChanged += OnAnyColorChanged;

        // 이미 사라진(Destroy된) PlayerEvents는 Unity의 == 오버로드가 null로 취급하므로
        // 구독 해제 시도 없이 목록에서만 빼면 된다 — 델리게이트는 컴포넌트와 함께 죽는다.
        _watchedColorEvents.RemoveWhere(evt =>
        {
            if (evt != null && _liveColorEvents.Contains(evt)) return false;
            if (evt != null) evt.OnColorTypeChanged -= OnAnyColorChanged;
            return true;
        });
    }

    void OnAnyColorChanged(PlayerColorType _) => RequestRebuild();

    void ClearColorWatchers()
    {
        foreach (var evt in _watchedColorEvents)
            if (evt != null) evt.OnColorTypeChanged -= OnAnyColorChanged;
        _watchedColorEvents.Clear();
    }

    // ── 이름 ─────────────────────────────────────────────────────

    void RefreshAllSlotNames()
    {
        if (!isActiveAndEnabled) return;
        foreach (var slot in slots)
            if (slot != null && slot.player != null)
                RefreshSlotVisual(slot);
    }

    /// <summary>
    /// colorIndex → Steam 표시 이름(닉네임). 매핑 실패 시 "???".
    /// 우선순위는 CheerService.GetCheerName과 동일 규칙(세션 확정값 우선 → 실시간 NV 폴백) —
    /// 게이트 후엔 스냅샷을 쓰고, 게이트 전(Tutorial)이거나 그 색 슬롯이 미확정이면
    /// PlayerDisplayNameSync 실시간 NV를 스캔한다. DisplayName은 재제출 UI가 없어 스테이지
    /// 재스폰 때마다 같은 값이 그대로 재보고되므로 두 값은 항상 수렴한다.
    /// GetSessionDisplayName은 미확정 슬롯에 빈 문자열을 돌려주므로(2026-09-05, 예전 "Player"
    /// 폴백이 이 아래 실시간 NV 스캔을 도달 불가하게 만들어 "Player" 고착 버그가 있었음)
    /// HasSessionDisplayNames를 따로 볼 필요 없이 반환값만으로 판단한다.
    /// CheerName("BERRY" 등)은 응원 시 혼동을 줄이기 위해 캐릭터 머리 위(PlayerNameTagUI)로 이전했고,
    /// 이 코너 패널은 "실제로 누구인지" 확인용 Steam 닉네임을 표시한다.
    /// </summary>
    static string GetPlayerDisplayName(int colorIndex)
    {
        if (GameSession.Instance != null)
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

    // ── 갱신 ─────────────────────────────────────────────────────

    Sprite GetFullHeartSprite(PlayerColorType colorType)
    {
        if (colorHeartMap != null)
            foreach (var entry in colorHeartMap)
                if (entry.colorType == colorType) return entry.fullHeartSprite;
        return fullHeartSprite;
    }

    void RefreshSlotVisual(ColorSlot slot)
    {
        if (slot == null || slot.player == null) return;
        if (slot.root == null) return; // 방어

        if (slot.nameText != null)
            slot.nameText.text = GetPlayerDisplayName(slot.colorIndex);

        if (slot.heartImages == null) return;
        Sprite resolvedFull = GetFullHeartSprite(slot.player.playerColorType);
        int max = slot.player.maxHeart;
        for (int i = 0; i < slot.heartImages.Length; i++)
        {
            var img = slot.heartImages[i];
            if (img == null) continue;
            bool inUse = i < max;
            if (img.gameObject.activeSelf != inUse) img.gameObject.SetActive(inUse);
            if (inUse) img.sprite = i < slot.player.heart ? resolvedFull : emptyHeartSprite;
        }
    }

    /// <summary>사망 시 하트를 전부 빈 하트로 표시. 죽으면 씬이 리셋되므로 별도 배경/텍스트 색 연출은 불필요.</summary>
    void SetDead(ColorSlot slot, bool isDead)
    {
        if (slot == null || slot.root == null) return;

        if (isDead && slot.heartImages != null)
            foreach (var h in slot.heartImages)
                if (h != null) h.sprite = emptyHeartSprite;

        if (!isDead) RefreshSlotVisual(slot);
    }
}
