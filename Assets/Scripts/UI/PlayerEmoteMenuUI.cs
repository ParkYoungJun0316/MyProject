using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// T 홀드로 도넛 이모트 휠을 열고, 커서가 올라간 조각에서 T를 떼면 그 이모트를 재생.
/// 가운데 구멍·링 바깥에서 떼면 취소. 클릭은 쓰지 않는다(각도 판정 전용).
///
/// [루프 vs 원샷]
/// Yes/No/Hide/Point: 루프 → Bool. NetworkAnimator(Owner Authority)의 파라미터 폴링이
/// 그대로 실어 보낸다. 이동 입력이 들어오면 즉시 취소.
/// Thanks/Shame/Fly/Surprise: 원샷 → Trigger. Trigger는 그 폴링 대상이 아니라
/// (Bool/Int/Float만 비교·전송) NetworkAnimator.SetTrigger로 보낸다 — PlayOneShotEmote 참고.
///
/// [각도 — 피자 조각]
/// 링 이미지(Figma/Emot/Union.png)의 분할선이 0°/45°/90°/135°(수직·수평·대각)이므로
/// 조각 k = [k*45°, (k+1)*45°) 이고, 아이콘은 그 조각 한가운데(22.5° + k*45°)에 놓인다.
/// 12시 오른쪽 조각부터 시계방향: Yes, No, Thanks, Hide, Point, Shame, Fly, Surprise.
///
/// [배치 — 에디터 소유]
/// UI.prefab EmoteMenuController에 부착.
/// emoteMenuPanel: 휠 루트(Emote_Panel). 슬롯은 자식 Btn.Yes~Btn.Surprise를 자동으로 찾는다.
/// 아이콘 위치·크기·색, 라벨(각 아이콘 또는 패널의 자식으로 직접 배치), 링 배경 이미지,
/// 패널 크기는 **전부 에디터에서 설정한 값을 그대로 쓴다** — 코드는 RectTransform이나
/// 스프라이트를 덮어쓰지 않는다(2026-09-05: 코드가 매번 되돌려 라벨을 옮길 수 없던 문제).
/// 판정 반지름(innerRadius/outerRadius)만 인스펙터로 맞춘다.
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

    [Header("패널")]
    [Tooltip("T 홀드 동안 켤 휠 루트 (Emote_Panel)")]
    [SerializeField] GameObject emoteMenuPanel;

    [Header("슬롯 (12시 오른쪽 조각부터 시계방향)")]
    [Tooltip("비워두면 Emote_Panel의 Btn.Yes ~ Btn.Surprise를 자동으로 찾습니다.")]
    [SerializeField] Image[] slotImages;

    [Header("판정 반지름 (패널 로컬 단위)")]
    [Tooltip("이 거리보다 안쪽이면 취소 (링 가운데)")]
    [SerializeField] float innerRadius = 80f;
    [Tooltip("이 거리보다 바깥이면 취소. 링 이미지의 실제 반지름과 맞추세요 (패널 500 → 250).")]
    [SerializeField] float outerRadius = 250f;

    [Header("하이라이트")]
    [Tooltip("커서가 올라간 조각의 아이콘 색. 평소 색·크기는 에디터에서 설정한 값을 그대로 씁니다.")]
    [SerializeField] Color slotHighlightColor = Color.white;
    [Tooltip("에디터에서 설정한 아이콘 크기에 곱할 배율")]
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
    NetworkAnimator _netAnim;
    Canvas _canvas;
    RectTransform _wheelRoot;
    bool _isOpen;
    int _hoveredIndex = -1;
    bool _slotsPrepared;
    bool _loggedMissingSlots;

    /// <summary>에디터에서 설정한 슬롯 색·크기 — 하이라이트를 풀 때 되돌릴 원본.</summary>
    Color[] _slotBaseColors;
    Vector3[] _slotBaseScales;

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

    void InitAnimator()
    {
        _anim = _player.GetComponentInChildren<Animator>();
        _netAnim = _player.GetComponentInChildren<NetworkAnimator>();

        if (_netAnim == null)
            Debug.LogWarning("[PlayerEmoteMenuUI] Player에 NetworkAnimator가 없어 원샷 이모트를 보낼 수 없습니다.");
    }

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
            if (!Keyboard.current.tKey.wasPressedThisFrame) return;

            // ESC 메뉴·채팅·치어네임이 떠 있으면 열지 않는다. 커서 해제 요청 목록이 "지금 마우스를
            // 쓰는 UI가 있나"의 SSOT라 UI별 플래그를 따로 볼 필요가 없다(EscMenuController는
            // 공개 IsOpen이 없으므로 이 경로가 유일한 게이트).
            if (CursorUnlockRequestUtil.IsRequested) return;

            OpenMenu();
            // 여기서 return하지 않고 같은 프레임의 호버·릴리스까지 처리한다 — 프레임이 길 때
            // T를 한 프레임 안에 눌렀다 떼면 wasReleasedThisFrame을 놓쳐 휠이 열린 채 남았다.
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

    /// <summary>원샷 이모트 재생. Animator에 직접 SetTrigger하면 안 된다 — NetworkAnimator의 파라미터
    /// 폴링은 Int/Bool/Float만 비교·전송하고 Trigger 타입은 어느 분기에도 걸리지 않아, 원격 클라이언트에
    /// 전달되는 건 상태(state) 동기화에 얹혀가는 우연뿐이다. NGO가 따로 제공하는 SetTrigger로 보낸다
    /// (Owner 권한이라 로컬에도 즉시 적용 + 서버로 큐잉, 2026-09-05).
    /// 루프 이모트 재생 중이었다면 먼저 꺼서 원샷 종료 후 루프로 되돌아가는 것을 방지.</summary>
    void PlayOneShotEmote(string trigger)
    {
        CancelActiveLoop();
        if (_netAnim != null) _netAnim.SetTrigger(trigger);
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

        // Atan2(x, y): 0 = 12시, 양수 = 시계방향. 링 이미지의 분할선이 0°/45°/90°/135°라 조각 k는
        // [k*45°, (k+1)*45°) 이고 아이콘이 그 한가운데에 있다 — 반 칸 오프셋을 주면 안 된다
        // (2026-09-05: 오프셋 때문에 아이콘 중앙이 경계선에 걸쳐 옆 칸이 잡혔음).
        float angle = Mathf.Atan2(local.x, local.y);
        if (angle < 0f) angle += Mathf.PI * 2f;
        return Mathf.FloorToInt(angle / SliceRadians) % SlotCount;
    }

    void PrepareWheel()
    {
        if (_slotsPrepared || emoteMenuPanel == null) return;

        _wheelRoot = emoteMenuPanel.transform as RectTransform;
        // 패널은 Awake에서 이미 비활성이므로 includeInactive를 켜야 캔버스를 찾는다.
        _canvas = emoteMenuPanel.GetComponentInParent<Canvas>(true);

        if (!TryResolveSlots())
        {
            if (!_loggedMissingSlots)
            {
                Debug.LogWarning("[PlayerEmoteMenuUI] 휠 슬롯 8개를 찾지 못했습니다. Emote_Panel 아래 Btn.Yes ~ Btn.Surprise가 있는지 확인하세요.");
                _loggedMissingSlots = true;
            }
            return;
        }

        CacheSlotBaseVisuals();
        _slotsPrepared = true;
    }

    /// <summary>위치·크기·스프라이트·라벨은 전부 에디터 소유라 코드는 원본 색·크기만 기억한다.
    /// 클릭을 쓰지 않으므로 Button과 레이캐스트만 끈다 — Button을 살려두면 그 ColorTint 전환이
    /// 호버 하이라이트 색을 덮어쓴다.</summary>
    void CacheSlotBaseVisuals()
    {
        _slotBaseColors = new Color[SlotCount];
        _slotBaseScales = new Vector3[SlotCount];

        for (int i = 0; i < SlotCount; i++)
        {
            Image img = slotImages[i];

            var button = img.GetComponent<Button>();
            if (button != null) button.enabled = false;
            img.raycastTarget = false;

            _slotBaseColors[i] = img.color;
            _slotBaseScales[i] = img.rectTransform.localScale;
        }
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

    void ApplyAllSlotVisuals()
    {
        if (slotImages == null || _slotBaseColors == null) return;
        int count = Mathf.Min(slotImages.Length, SlotCount);
        for (int i = 0; i < count; i++)
        {
            Image img = slotImages[i];
            if (img == null) continue;
            bool on = i == _hoveredIndex;
            img.color = on ? slotHighlightColor : _slotBaseColors[i];
            img.rectTransform.localScale = on
                ? _slotBaseScales[i] * highlightScale
                : _slotBaseScales[i];
        }
    }

    /// <summary>로컬 오너 플레이어 — NetworkObject.IsOwner 기준. 솔로도 Host 1인이라 같은 경로다.</summary>
    static Player FindLocalOwnerPlayer()
    {
        foreach (var p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            var netObj = p.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsOwner) return p;
        }
        return null;
    }
}
