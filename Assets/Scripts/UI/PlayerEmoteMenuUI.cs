using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// T 홀드로 도넛 이모트 휠을 열고, 커서가 올라간 칸에서 T를 떼면 그 이모트를 재생.
/// 가운데 구멍·링 바깥에서 떼면 취소. 클릭은 쓰지 않는다.
///
/// 로컬 오너 Player Animator에 SetBool/SetTrigger — NetworkAnimator(Owner Authority)가
/// 다른 클라이언트에 동기화 (Player.cs doHit/doDie, isRun과 동일).
///
/// [루프 vs 원샷]
/// Yes/No/Hide/Point: 루프 → Bool, 이동 입력이 들어오면 즉시 취소.
/// Thanks/Shame/Fly/Surprise: 원샷 → Trigger.
///
/// [칸 순서 — 12시부터 시계방향]
/// Yes, No, Thanks, Hide, Point, Shame, Fly, Surprise.
///
/// [배치]
/// UI.prefab EmoteMenuController에 부착.
/// emoteMenuPanel: 휠 루트. 슬롯은 Emote_Panel 자식 Btn.Yes~Btn.Surprise를 자동으로 찾는다.
/// </summary>
public class PlayerEmoteMenuUI : MonoBehaviour
{
    const int SlotCount = 8;
    const float SliceRadians = Mathf.PI * 2f / SlotCount;

    static readonly string[] SlotChildNames =
    {
        "Btn.Yes", "Btn.No", "Btn.Thanks", "Btn.Hide",
        "Btn.Point", "Btn.Shame", "Btn.Fly", "Btn.Surprise"
    };

    static readonly string[] SlotLabels =
    {
        "Yes", "No", "Thanks", "Hide", "Point", "Shame", "Fly", "Surprise"
    };

    const string SlotLabelName = "Label";
    const int RingTextureSize = 512;

    [Header("패널")]
    [Tooltip("T 홀드 동안 켤 휠 루트 (기존 Emote_Panel)")]
    [SerializeField] GameObject emoteMenuPanel;

    [Header("슬롯 (12시부터 시계방향)")]
    [Tooltip("비워두면 Emote_Panel의 Btn.Yes ~ Btn.Surprise를 자동으로 찾습니다.")]
    [SerializeField] Image[] slotImages;

    [Header("도넛")]
    [Tooltip("아이콘이 놓일 중심부터의 거리 (패널 로컬 단위)")]
    [SerializeField] float iconOrbitRadius = 160f;
    [SerializeField] Vector2 iconSize = new Vector2(120f, 120f);
    [Tooltip("이 거리보다 안쪽이면 취소 (구멍)")]
    [SerializeField] float innerRadius = 80f;
    [Tooltip("이 거리보다 바깥이면 취소")]
    [SerializeField] float outerRadius = 250f;
    [Tooltip("비워두면 반투명 링을 코드로 그립니다. Figma 링 PNG를 넣으면 그걸 씁니다.")]
    [SerializeField] Sprite donutSprite;
    [SerializeField] Color ringColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] Color ringLineColor = new Color(1f, 1f, 1f, 0.18f);

    [Header("라벨")]
    [SerializeField] TMP_FontAsset labelFont;
    [SerializeField] float labelFontSize = 22f;
    [SerializeField] Color labelColor = Color.white;
    [SerializeField] float labelGap = 6f;

    [Header("하이라이트")]
    [SerializeField] Color slotNormalColor = Color.white;
    [SerializeField] Color slotHighlightColor = Color.white;
    [SerializeField] float highlightScale = 1.2f;

    [Header("커서")]
    [Tooltip("메뉴 닫을 때 커서를 다시 잠글지 여부. ThirdPersonCamera.lockCursor 설정과 일치시키세요.")]
    [SerializeField] bool lockCursorOnClose = true;

    /// <summary>휠이 열려있는 동안 true — EmoteHintUI가 힌트를 숨기는 데 사용
    /// (InGameChatUI.IsChatOpen / TutorialCheerNameUI.IsOpen과 동일 패턴).</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>이번 프레임에 Esc로 휠을 닫았는지 — EscMenuController가 같은 프레임에
    /// ESC 메뉴를 열지 않도록 하는 플래그 (TutorialCheerNameUI.ConsumedEscThisFrame과 동일).</summary>
    public static bool ConsumedEscThisFrame => s_escClosedFrame == Time.frameCount;
    static int s_escClosedFrame = -1;

    Player _player;
    Animator _anim;
    Canvas _canvas;
    RectTransform _wheelRoot;
    bool _isOpen;
    int _hoveredIndex = -1;
    bool _slotsPrepared;
    bool _loggedMissingSlots;
    Texture2D _ringTex;
    Sprite _generatedRingSprite;

    /// <summary>현재 재생 중인 루프 이모트 Bool 파라미터 이름. 없으면 null.</summary>
    string _activeLoopParam;

    void Awake()
    {
        if (emoteMenuPanel != null) emoteMenuPanel.SetActive(false);
        PrepareWheel();
    }

    void Start()
    {
        _player = FindLocalOwnerPlayer();
        if (_player != null) { InitAnimator(); return; }

        PlayerSpawnCoordinator.OnPlayersReady += FindAndInit;
        if (PlayerSpawnCoordinator.IsReady) FindAndInit();
    }

    void FindAndInit()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= FindAndInit;

        _player = FindLocalOwnerPlayer();
        if (_player == null)
        {
            Debug.LogWarning("[PlayerEmoteMenuUI] OnPlayersReady 시점에도 로컬 오너 플레이어를 찾지 못했습니다.");
            return;
        }

        InitAnimator();
    }

    void InitAnimator() => _anim = _player.GetComponentInChildren<Animator>();

    void OnDestroy()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= FindAndInit;

        // 씬 파괴(TitleReturnFlow의 SceneManager.LoadScene 등) 시 메뉴가 열려 있던 채로
        // 파괴돼도(CloseMenu 없이) 요청 목록에 잔여 참조가 새지 않도록 하는 안전장치.
        // Release가 아니라 Forget을 쓴다 — 여기서 실제 Cursor를 잠그면 TitleReturnFlow가 그
        // 직전에 이미 풀어둔 커서를 도로 잠가 "타이틀 씬에서 마우스가 사라지는" 회귀가 생긴다
        // (2026-08-22 수정, EscMenuController와 동일 원인).
        if (_isOpen) CursorUnlockRequestUtil.Forget(this);
        IsOpen = false;
        DestroyGeneratedRing();
    }

    void Update()
    {
        if (_player == null || Keyboard.current == null) return;

        // 루프 이모트 재생 중 이동 입력이 들어오면 즉시 취소 (메뉴/채팅 상태와 무관하게 항상 체크)
        if (_activeLoopParam != null && _player.moveInput.sqrMagnitude > 0.0001f)
            CancelActiveLoop();

        // 채팅/치어네임이 열리면 휠은 양보하고, 홀드 중이었다면 재생 없이 닫는다.
        // (홀드-릴리스라서 채팅 중에 T up을 놓치면 휠이 열린 채로 남는 것을 방지)
        if (InGameChatUI.IsChatOpen || TutorialCheerNameUI.IsOpen)
        {
            if (_isOpen) CloseMenu();
            return;
        }

        if (_player.IsDead)
        {
            if (_isOpen) CloseMenu();
            CancelActiveLoop();
            return;
        }

        if (!_isOpen)
        {
            if (Keyboard.current.tKey.wasPressedThisFrame)
                OpenMenu();
            return;
        }

        UpdateHover();

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            s_escClosedFrame = Time.frameCount;
            CloseMenu();
            return;
        }

        if (Keyboard.current.tKey.wasReleasedThisFrame)
            ConfirmHovered();
    }

    void OpenMenu()
    {
        PrepareWheel();

        _isOpen = true;
        IsOpen = true;
        _hoveredIndex = -1;
        ApplyAllSlotVisuals();

        if (emoteMenuPanel != null) emoteMenuPanel.SetActive(true);

        CursorUnlockRequestUtil.Request(this);
    }

    void CloseMenu()
    {
        _isOpen = false;
        IsOpen = false;
        _hoveredIndex = -1;
        ApplyAllSlotVisuals();

        if (emoteMenuPanel != null) emoteMenuPanel.SetActive(false);

        CursorUnlockRequestUtil.Release(this, lockCursorOnClose);
    }

    void ConfirmHovered()
    {
        int index = _hoveredIndex;
        if (index >= 0 && index < SlotCount)
            PlayByIndex(index);
        else
            CloseMenu();
    }

    void PlayByIndex(int index)
    {
        switch (index)
        {
            case 0: PlayLoopEmote("isYes"); break;
            case 1: PlayLoopEmote("isNo"); break;
            case 2: PlayOneShotEmote("doThanks"); break;
            case 3: PlayLoopEmote("isHide"); break;
            case 4: PlayLoopEmote("isPoint"); break;
            case 5: PlayOneShotEmote("doShame"); break;
            case 6: PlayOneShotEmote("doFly"); break;
            case 7: PlayOneShotEmote("doSurprise"); break;
            default: CloseMenu(); break;
        }
    }

    /// <summary>루프 이모트 시작. 다른 루프가 재생 중이면 먼저 끄고 교체.</summary>
    void PlayLoopEmote(string boolParam)
    {
        CancelActiveLoop();
        if (_anim != null) _anim.SetBool(boolParam, true);
        _activeLoopParam = boolParam;
        CloseMenu();
    }

    /// <summary>원샷 이모트 재생. 루프 이모트 재생 중이었다면 먼저 꺼서 원샷 종료 후 루프로 되돌아가는 것을 방지.</summary>
    void PlayOneShotEmote(string trigger)
    {
        CancelActiveLoop();
        if (_anim != null) _anim.SetTrigger(trigger);
        CloseMenu();
    }

    /// <summary>재생 중인 루프 이모트 Bool을 꺼서 Idle로 되돌린다.</summary>
    void CancelActiveLoop()
    {
        if (_activeLoopParam == null) return;
        if (_anim != null) _anim.SetBool(_activeLoopParam, false);
        _activeLoopParam = null;
    }

    void UpdateHover()
    {
        int next = ResolveHoveredIndex();
        if (next == _hoveredIndex) return;
        _hoveredIndex = next;
        ApplyAllSlotVisuals();
    }

    int ResolveHoveredIndex()
    {
        if (_wheelRoot == null || Mouse.current == null) return -1;

        Camera eventCam = null;
        if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCam = _canvas.worldCamera;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_wheelRoot, screenPos, eventCam, out Vector2 local))
            return -1;

        float dist = local.magnitude;
        if (dist < innerRadius || dist > outerRadius) return -1;

        // Atan2(x, y): 0 = 12시, 양수 = 시계방향. 슬롯 중심이 슬라이스 한가운데가 되도록 반 칸 오프셋.
        float angle = Mathf.Atan2(local.x, local.y);
        if (angle < 0f) angle += Mathf.PI * 2f;
        int index = Mathf.FloorToInt((angle + SliceRadians * 0.5f) / SliceRadians);
        return index % SlotCount;
    }

    void PrepareWheel()
    {
        if (_slotsPrepared || emoteMenuPanel == null) return;

        _wheelRoot = emoteMenuPanel.transform as RectTransform;
        _canvas = emoteMenuPanel.GetComponentInParent<Canvas>();

        var layout = emoteMenuPanel.GetComponent<LayoutGroup>();
        if (layout != null) layout.enabled = false;

        var fitter = emoteMenuPanel.GetComponent<ContentSizeFitter>();
        if (fitter != null) fitter.enabled = false;

        if (_wheelRoot != null)
        {
            _wheelRoot.anchorMin = _wheelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _wheelRoot.pivot = new Vector2(0.5f, 0.5f);
            _wheelRoot.anchoredPosition = Vector2.zero;
            float size = Mathf.Max(
                outerRadius * 2f,
                (iconOrbitRadius + iconSize.y * 0.5f + labelFontSize + labelGap + 20f) * 2f);
            _wheelRoot.sizeDelta = new Vector2(size, size);
        }

        ApplyDonutBackground();

        var buttons = emoteMenuPanel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
            buttons[i].enabled = false;

        if (!TryResolveSlots())
        {
            if (!_loggedMissingSlots)
            {
                Debug.LogWarning("[PlayerEmoteMenuUI] 휠 슬롯 8개를 찾지 못했습니다. Emote_Panel 아래 Btn.Yes ~ Btn.Surprise가 있는지 확인하세요.");
                _loggedMissingSlots = true;
            }
            return;
        }

        HideNonSlotChildren();
        LayoutSlots();
        _slotsPrepared = true;
    }

    bool TryResolveSlots()
    {
        if (HasAllSlots()) return true;
        if (emoteMenuPanel == null) return false;

        var resolved = new Image[SlotCount];
        for (int i = 0; i < SlotCount; i++)
        {
            if (slotImages != null && i < slotImages.Length && slotImages[i] != null)
            {
                resolved[i] = slotImages[i];
                continue;
            }

            Transform child = emoteMenuPanel.transform.Find(SlotChildNames[i]);
            if (child != null) resolved[i] = child.GetComponent<Image>();
        }

        slotImages = resolved;
        return HasAllSlots();
    }

    bool HasAllSlots()
    {
        if (slotImages == null || slotImages.Length < SlotCount) return false;
        for (int i = 0; i < SlotCount; i++)
        {
            if (slotImages[i] == null) return false;
        }
        return true;
    }

    void HideNonSlotChildren()
    {
        for (int i = 0; i < emoteMenuPanel.transform.childCount; i++)
        {
            var child = emoteMenuPanel.transform.GetChild(i).gameObject;
            bool isSlot = false;
            for (int s = 0; s < slotImages.Length; s++)
            {
                if (slotImages[s] != null && slotImages[s].gameObject == child)
                {
                    isSlot = true;
                    break;
                }
            }

            if (!isSlot) child.SetActive(false);
        }
    }

    void LayoutSlots()
    {

        for (int i = 0; i < SlotCount; i++)
        {
            Image img = slotImages[i];
            if (img == null) continue;

            var button = img.GetComponent<Button>();
            if (button != null) button.enabled = false;

            img.raycastTarget = false;
            img.preserveAspect = true;
            img.color = slotNormalColor;

            var layoutElement = img.GetComponent<LayoutElement>();
            if (layoutElement != null) layoutElement.ignoreLayout = true;

            RectTransform rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = iconSize;
            rt.localScale = Vector3.one;

            float ang = i * SliceRadians;
            rt.anchoredPosition = new Vector2(Mathf.Sin(ang), Mathf.Cos(ang)) * iconOrbitRadius;

            EnsureSlotLabel(img, SlotLabels[i]);
        }
    }

    void ApplyAllSlotVisuals()
    {
        if (slotImages == null) return;
        int count = Mathf.Min(slotImages.Length, SlotCount);
        for (int i = 0; i < count; i++)
        {
            Image img = slotImages[i];
            if (img == null) continue;
            bool on = i == _hoveredIndex;
            img.color = on ? slotHighlightColor : slotNormalColor;
            img.rectTransform.localScale = Vector3.one * (on ? highlightScale : 1f);
        }
    }

    void ApplyDonutBackground()
    {
        var bg = emoteMenuPanel.GetComponent<Image>();
        if (bg == null) return;

        bg.raycastTarget = false;
        bg.preserveAspect = true;
        bg.type = Image.Type.Simple;

        if (donutSprite != null)
        {
            DestroyGeneratedRing();
            bg.sprite = donutSprite;
            bg.color = Color.white;
            return;
        }

        float panelRadius = _wheelRoot != null ? _wheelRoot.sizeDelta.x * 0.5f : outerRadius;
        float innerRatio = panelRadius > 0.01f ? innerRadius / panelRadius : 0.32f;
        bg.sprite = GetOrCreateRingSprite(innerRatio);
        bg.color = Color.white;
    }

    Sprite GetOrCreateRingSprite(float innerRatio)
    {
        if (_generatedRingSprite != null) return _generatedRingSprite;

        _ringTex = new Texture2D(RingTextureSize, RingTextureSize, TextureFormat.RGBA32, false);
        _ringTex.name = "EmoteDonutRing";
        _ringTex.wrapMode = TextureWrapMode.Clamp;
        _ringTex.filterMode = FilterMode.Bilinear;

        var pixels = new Color[RingTextureSize * RingTextureSize];
        float cx = (RingTextureSize - 1) * 0.5f;
        float outer = cx;
        float inner = Mathf.Clamp(cx * innerRatio, 0f, outer - 2f);
        const float edge = 2f;

        for (int y = 0; y < RingTextureSize; y++)
        {
            for (int x = 0; x < RingTextureSize; x++)
            {
                float dx = x - cx;
                float dy = y - cx;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                Color c = Color.clear;
                if (r <= outer && r >= inner)
                {
                    c = ringColor;
                    if (r > outer - edge) c.a *= Mathf.Clamp01((outer - r) / edge);
                    if (r < inner + edge) c.a *= Mathf.Clamp01((r - inner) / edge);

                    float angle = Mathf.Atan2(dx, dy);
                    if (angle < 0f) angle += Mathf.PI * 2f;
                    float wrapped = Mathf.Repeat(angle + SliceRadians * 0.5f, SliceRadians);
                    float distToLine = Mathf.Min(wrapped, SliceRadians - wrapped) * r;
                    if (distToLine < 1.6f)
                    {
                        float t = 1f - distToLine / 1.6f;
                        c = Color.Lerp(c, ringLineColor, t * ringLineColor.a);
                    }
                }

                pixels[y * RingTextureSize + x] = c;
            }
        }

        _ringTex.SetPixels(pixels);
        _ringTex.Apply(false, true);
        _generatedRingSprite = Sprite.Create(
            _ringTex,
            new Rect(0f, 0f, RingTextureSize, RingTextureSize),
            new Vector2(0.5f, 0.5f),
            100f);
        _generatedRingSprite.name = "EmoteDonutRingSprite";
        return _generatedRingSprite;
    }

    void DestroyGeneratedRing()
    {
        if (_generatedRingSprite != null)
        {
            Destroy(_generatedRingSprite);
            _generatedRingSprite = null;
        }

        if (_ringTex != null)
        {
            Destroy(_ringTex);
            _ringTex = null;
        }
    }

    void EnsureSlotLabel(Image img, string text)
    {
        Transform existing = img.transform.Find(SlotLabelName);
        TextMeshProUGUI tmp = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
        if (tmp == null)
        {
            var go = new GameObject(SlotLabelName, typeof(RectTransform));
            go.layer = img.gameObject.layer;
            go.transform.SetParent(img.transform, false);
            tmp = go.AddComponent<TextMeshProUGUI>();
        }

        var rt = tmp.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, labelGap);
        rt.sizeDelta = new Vector2(180f, labelFontSize + 10f);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        TMP_FontAsset font = ResolveLabelFont();
        if (font != null) tmp.font = font;

        tmp.text = text;
        tmp.fontSize = labelFontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Bottom;
        tmp.color = labelColor;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
    }

    TMP_FontAsset ResolveLabelFont()
    {
        if (labelFont != null) return labelFont;
        if (_canvas != null)
        {
            var existing = _canvas.GetComponentInChildren<TextMeshProUGUI>(true);
            if (existing != null && existing.font != null) return existing.font;
        }

        return TMP_Settings.defaultFontAsset;
    }

    /// <summary>오프라인: isOwnerControlled=true, 온라인: NetworkObject.IsOwner 기준으로 탐색.</summary>
    static Player FindLocalOwnerPlayer()
    {
        foreach (var p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            var netObj = p.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsOwner) return p;
            if (p.isOwnerControlled) return p;
        }
        return null;
    }
}
