using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비 씬 메인 컨트롤러.
/// LobbyContext.Mode(OnlineHost / OnlineClient)에 따라 동작을 분기한다.
///
/// [배치 방법]
/// 로비 씬의 Canvas 또는 빈 GameObject에 부착.
///
/// [Inspector — 공통]
/// - stageSceneName     : Start 시 로드할 씬 이름 (기본 "M.Stage1")
/// - titleSceneName     : Quit 시 복귀할 씬 이름 (기본 "0.Title")
/// - characterPortraits : [0]Blue [1]Purple [2]Green [3]Yellow 순 Sprite
/// - portraitImage      : Slot0/CharacterArea/Image (로컬 플레이어 드롭다운 초상화)
/// - characterDropdown  : Slot0 내 TMP_Dropdown
/// - screenFader / fadeOutDuration : 페이드 (선택)
///
/// [Inspector — 온라인 전용]
/// - onlineOnlyRoot     : 룸코드·슬롯1~3·Kick·SteamInvite 등 묶은 부모 GameObject
/// - readyRoot          : Ready 버튼 묶음
/// - roomCodeText       : 룸코드 표시 TMP_Text
/// - checkImage / readySprite / notReadySprite : Ready 비주얼
/// - waitingTextObject  : 모두 Ready 전 대기 문구
/// - allSlotUIs         : Slot0~3 에 붙인 LobbySlotUI 컴포넌트 4개 (순서 고정)
/// - startButton        : Start 버튼 (CanStart() 기반 interactable 제어)
/// - startButtonRoot    : Start 버튼 부모 (Host만 표시)
///
/// [로비 Cheer Say Test (Vosk)]
/// - lobbyCheerEngine  : 로비 씬에 배치한 CheerKeywordEngine (_lobbyTestMode=true)
/// Vosk가 CheerName 감지 → OnKeywordDetected(targetColorIndex) → 타겟 슬롯 ShowHeardBy(myColor)
/// 말풍선 + 색별 떡 2.5초 표시. 버프·ServerRpc 미발생.
/// 슬롯 이름 변경 시 RebuildLobbyGrammarIfNeeded() 로 grammar 자동 갱신.
///
/// [버튼 OnClick 연결]
/// Btn_Start       → OnClickStart()
/// Btn_Ready       → OnClickReady()
/// Btn_Quit        → OnClickQuit()
/// Btn_Copy        → OnClickCopy()
/// Btn_SteamInvite → OnClickSteamInvite()
/// Slot1~3 Kick    → 각 LobbySlotUI.OnClickKick()
/// Dropdown OnValueChanged → OnCharacterChanged(Int32)
/// </summary>
public class LobbyMenuController : MonoBehaviour
{
    [Header("캐릭터 초상화")]
    [Tooltip("드롭다운 인덱스 순: [0]Blue [1]Purple [2]Green [3]Yellow")]
    [SerializeField] private Sprite[] characterPortraits = new Sprite[4];

    [Tooltip("Slot0/CharacterArea/Image — 로컬 플레이어 드롭다운 초상화")]
    [SerializeField] private Image portraitImage;

    [Tooltip("Slot0 내 TMP_Dropdown")]
    [SerializeField] private TMP_Dropdown characterDropdown;

    [Header("온라인 전용 UI")]
    [SerializeField] private GameObject onlineOnlyRoot;
    [SerializeField] private GameObject readyRoot;
    [SerializeField] private TMP_Text   roomCodeText;

    [Header("Ready 상태")]
    [SerializeField] private Image   checkImage;
    [SerializeField] private Sprite  readySprite;
    [SerializeField] private Sprite  notReadySprite;
    [SerializeField] private GameObject waitingTextObject;

    [Header("슬롯 UI (Slot0~3, LobbySlotUI 컴포넌트 순서 고정)")]
    [Tooltip("Slot0(로컬), Slot1, Slot2, Slot3 순. LobbySlotUI 컴포넌트를 드래그.")]
    [SerializeField] private LobbySlotUI[] allSlotUIs = new LobbySlotUI[4];

    [Header("Start 버튼")]
    [Tooltip("CanStart() 결과로 interactable 제어. Host만 표시.")]
    [SerializeField] private Button startButton;

    [Tooltip("Start 버튼 부모 — Host만 SetActive(true).")]
    [SerializeField] private GameObject startButtonRoot;

    [Tooltip("색 중복 시 표시할 경고 GameObject.\n" +
             "예) TMP_Text: '같은 색을 선택한 플레이어가 있습니다. 다른 색을 선택해주세요.'")]
    [SerializeField] private GameObject duplicateColorWarning;

    [Header("로비 Cheer Say Test (Vosk)")]
    [Tooltip("로비 씬에 배치한 CheerKeywordEngine. _lobbyTestMode=true로 설정할 것.\n" +
             "null이면 Vosk 피드백 비활성 (이름 편집은 영향 없음).")]
    [SerializeField] private CheerKeywordEngine lobbyCheerEngine;


    // ── 색상 매핑 ────────────────────────────────────────────────

    static readonly PlayerColorType[] IndexToColor =
    {
        PlayerColorType.Blue,
        PlayerColorType.Purple,
        PlayerColorType.Green,
        PlayerColorType.Yellow,
    };

    // ── 런타임 상태 ──────────────────────────────────────────────

    bool         _isReady;
    bool         _isNetworkSubscribed;
    LobbySlotUI  _localSlotUI;       // 로컬 플레이어 슬롯 캐시 — CheerName 결과 전달용
    string       _lastLobbyGrammar;  // grammar 중복 재빌드 방지 캐시

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        // LobbySlotUI가 드롭다운 이벤트를 담당.
    }

    void OnDestroy()
    {
        if (characterDropdown != null)
            characterDropdown.onValueChanged.RemoveListener(OnCharacterChanged);

        UnsubscribeNetworkEvents();

        // 솔로 모드에서도 해제 (이중 해제는 무해함)
        if (lobbyCheerEngine != null)
            lobbyCheerEngine.OnKeywordDetected -= OnLobbyCheerDetected;
    }

    void Start()
    {
        // Host/Client 버튼 분기는 Vosk 로드보다 먼저 — LoadSync 실패·블로킹 시에도 UI가 올바르게 적용되도록
        ApplyModeUI();
        RefreshRoomCode();

        // 로비 진입 시 Vosk 모델 동기 로드 (이미 로드됐으면 no-op)
        VoskModelLoader.LoadSync();

        int initial = characterDropdown != null ? characterDropdown.value : 0;
        RefreshPortrait(initial);

        SubscribeNetworkEvents();
    }

    // ── 네트워크 이벤트 구독 ──────────────────────────────────────

    void SubscribeNetworkEvents()
    {
        if (LobbyNetworkManager.Instance != null)
        {
            LobbyNetworkManager.Instance.OnSlotsChanged       += RefreshAllSlots;
            LobbyNetworkManager.Instance.OnCheerNameResult    += OnCheerNameResult;
            LobbyNetworkManager.Instance.OnLobbyHeardBroadcast += OnLobbyHeardBroadcast;
            _isNetworkSubscribed = true;
            RefreshAllSlots();
        }
        // Instance가 아직 null이면 Update에서 재시도

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback += OnNetworkDisconnected;

        // Vosk 로비 테스트 — CheerEngine 이벤트 구독
        if (lobbyCheerEngine != null)
            lobbyCheerEngine.OnKeywordDetected += OnLobbyCheerDetected;
    }

    // Instance가 나중에 생기는 경우 대비 (Client 타이밍 이슈)
    void Update()
    {
        if (!_isNetworkSubscribed && LobbyContext.IsOnline && LobbyNetworkManager.Instance != null)
        {
            LobbyNetworkManager.Instance.OnSlotsChanged       += RefreshAllSlots;
            LobbyNetworkManager.Instance.OnCheerNameResult    += OnCheerNameResult;
            LobbyNetworkManager.Instance.OnLobbyHeardBroadcast += OnLobbyHeardBroadcast;
            _isNetworkSubscribed = true;
            RefreshAllSlots();
        }
    }

    void UnsubscribeNetworkEvents()
    {
        if (LobbyNetworkManager.Instance != null)
        {
            LobbyNetworkManager.Instance.OnSlotsChanged       -= RefreshAllSlots;
            LobbyNetworkManager.Instance.OnCheerNameResult    -= OnCheerNameResult;
            LobbyNetworkManager.Instance.OnLobbyHeardBroadcast -= OnLobbyHeardBroadcast;
        }

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnNetworkDisconnected;

        if (lobbyCheerEngine != null)
            lobbyCheerEngine.OnKeywordDetected -= OnLobbyCheerDetected;
    }

    /// <summary>CheerName 설정 결과 — 로컬 슬롯 UI에 전달.</summary>
    void OnCheerNameResult(bool success, string errorKey)
    {
        _localSlotUI?.ShowCheerNameResult(success, errorKey);
    }

    /// <summary>
    /// Vosk가 CheerName 감지.
    /// Host에 ServerRpc 보고 → 전원 ClientRpc로 말풍선 공유.
    /// </summary>
    void OnLobbyCheerDetected(int targetColorIndex)
    {
        LobbyNetworkManager.Instance?.ReportLobbyHeardServerRpc(targetColorIndex);
    }

    /// <summary>
    /// Host → 전원 Heard 브로드캐스트 수신.
    /// LobbyNetworkManager.OnLobbyHeardBroadcast 이벤트 핸들러.
    /// </summary>
    void OnLobbyHeardBroadcast(int targetColorIndex, int speakerColorIndex)
    {
        ShowHeardOnSlot(targetColorIndex, speakerColorIndex);
    }

    /// <summary>targetColorIndex 슬롯의 말풍선에 speakerColorIndex 떡 표시.</summary>
    void ShowHeardOnSlot(int targetColorIndex, int speakerColorIndex)
    {
        if (LobbyNetworkManager.Instance == null) return;

        for (int i = 0; i < allSlotUIs.Length; i++)
        {
            if (allSlotUIs[i] == null) continue;
            if (i >= LobbyNetworkManager.Instance.SlotCount) break;

            var s = LobbyNetworkManager.Instance.GetSlot(i);
            if (s.ColorIndex == targetColorIndex)
            {
                allSlotUIs[i].ShowHeardBy(speakerColorIndex);
                Debug.Log($"[LobbyMenuController] Heard UI: target={targetColorIndex} speaker={speakerColorIndex}");
                break;
            }
        }
    }

    /// <summary>
    /// 슬롯 이름이 바뀔 때 Vosk grammar 갱신.
    /// grammar 문자열이 동일하면 워커에 신호를 보내지 않는다.
    /// </summary>
    void RebuildLobbyGrammarIfNeeded()
    {
        if (lobbyCheerEngine == null || LobbyNetworkManager.Instance == null) return;

        int count = LobbyNetworkManager.Instance.SlotCount;
        var names = new string[count];
        for (int i = 0; i < count; i++)
            names[i] = LobbyNetworkManager.GetEffectiveCheerName(LobbyNetworkManager.Instance.GetSlot(i));

        string newJson = CheerLexiconBuilder.BuildGrammarJson(names);
        if (newJson == _lastLobbyGrammar) return;

        _lastLobbyGrammar = newJson;
        lobbyCheerEngine.ApplySessionGrammar(names);
    }

    int GetLocalColorIndex()
    {
        if (LobbyNetworkManager.Instance == null || NetworkManager.Singleton == null) return 0;
        ulong localId = NetworkManager.Singleton.LocalClientId;
        for (int i = 0; i < LobbyNetworkManager.Instance.SlotCount; i++)
        {
            var s = LobbyNetworkManager.Instance.GetSlot(i);
            if (s.ClientId == localId) return s.ColorIndex;
        }
        return 0;
    }

    // ── 모드 분기 ─────────────────────────────────────────────────

    void ApplyModeUI()
    {
        if (onlineOnlyRoot != null) onlineOnlyRoot.SetActive(true);
        if (readyRoot != null) readyRoot.SetActive(LobbyContext.IsOnlineClient);
        if (startButtonRoot != null) startButtonRoot.SetActive(LobbyContext.IsOnlineHost);
        RefreshReadyVisual();
    }

    // ── 버튼 콜백 ─────────────────────────────────────────────────

    /// <summary>
    /// Start 버튼.
    /// Host: LobbyNetworkManager.StartGameServerRpc() — NGO가 씬 전환 처리.
    /// </summary>
    public void OnClickStart()
    {
        if (!LobbyContext.IsOnlineHost) return;

        if (LobbyNetworkManager.Instance == null)
        {
            Debug.LogWarning("[LobbyMenuController] LobbyNetworkManager를 찾을 수 없습니다.");
            return;
        }

        LobbyNetworkManager.Instance.StartGameServerRpc();
    }

    /// <summary>Ready 버튼 — 토글 후 ServerRpc로 동기화.</summary>
    public void OnClickReady()
    {
        _isReady = !_isReady;
        RefreshReadyVisual();

        LobbyNetworkManager.Instance?.SetReadyServerRpc(_isReady);
    }

    /// <summary>Quit 버튼 — TitleReturnFlow에 복귀 위임.</summary>
    public void OnClickQuit()
    {
        TitleReturnFlow.Instance?.Request(new TitleReturnOptions
        {
            Reason = TitleReturnReason.LobbyQuit,
            Scope  = TitleReturnScope.SessionOnly,
        });
    }

    /// <summary>
    /// Copy 버튼 — 전체 6자리 룸코드 클립보드 복사.
    /// Host / Client 모두 동일한 코드 복사 (SharedRoomCode NetworkVariable).
    /// </summary>
    public void OnClickCopy()
    {
        // SharedRoomCode: NetworkVariable이므로 Host·Client 모두 동일 값
        string code = LobbyNetworkManager.Instance != null
            ? LobbyNetworkManager.Instance.SharedRoomCode
            : string.Empty;

        // 폴백: LobbyNetworkManager 미초기화 시 Host는 직접 읽음
        if (string.IsNullOrEmpty(code) && NetworkManagerSetup.Instance != null)
            code = NetworkManagerSetup.Instance.RoomCode;

        if (!string.IsNullOrEmpty(code))
        {
            GUIUtility.systemCopyBuffer = code;
            Debug.Log($"[LobbyMenuController] 룸코드 복사됨: {code}");
        }
    }

    /// <summary>SteamInvite — Post-MVP 스텁.</summary>
    public void OnClickSteamInvite()
    {
        Debug.Log("[LobbyMenuController] Steam 초대는 Post-MVP에서 지원합니다.");
    }

    /// <summary>Dropdown OnValueChanged — 초상화 갱신 + 색 동기화.</summary>
    public void OnCharacterChanged(int index)
    {
        RefreshPortrait(index);
        LobbyNetworkManager.Instance?.SetColorServerRpc(index);
    }

    // ── UI 갱신 ───────────────────────────────────────────────────

    /// <summary>
    /// 전체 슬롯 UI 갱신. LobbyNetworkManager.OnSlotsChanged 이벤트에서 호출됨.
    /// _slots 순서대로 표시 → Host 항상 Slot0, 이후 접속 순.
    /// 모든 화면에서 동일한 순서로 보임.
    /// </summary>
    void RefreshAllSlots()
    {
        if (LobbyNetworkManager.Instance == null) return;

        ulong localId = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.LocalClientId
            : ulong.MaxValue;

        ulong hostId = LobbyNetworkManager.Instance.HostClientId;
        bool  isHost = LobbyContext.IsOnlineHost;

        // _slots 순서대로 UI 갱신 (Host=0번, 이후 접속 순)
        _localSlotUI = null;
        for (int i = 0; i < allSlotUIs.Length; i++)
        {
            if (allSlotUIs[i] == null) continue;

            if (i < LobbyNetworkManager.Instance.SlotCount)
            {
                LobbyPlayerState s = LobbyNetworkManager.Instance.GetSlot(i);
                bool isLocalSlot = s.ClientId == localId;
                bool isHostSlot  = s.ClientId == hostId;
                bool canKick     = isHost && !isLocalSlot;

                allSlotUIs[i].Refresh(s, GetPortrait(s.ColorIndex), canKick, isHostSlot, isLocalSlot);
                if (isLocalSlot) _localSlotUI = allSlotUIs[i];
            }
            else
            {
                allSlotUIs[i].SetEmpty();
            }
        }

        bool canStart      = LobbyNetworkManager.Instance.CanStart();
        bool hasDuplicate  = LobbyNetworkManager.Instance.HasDuplicateColors();

        if (startButton       != null) startButton.interactable   = canStart;
        if (waitingTextObject != null) waitingTextObject.SetActive(!canStart);

        // 색 중복 경고: 중복 있을 때 표시 (Start 비활성 이유를 명확히 알려줌)
        if (duplicateColorWarning != null)
            duplicateColorWarning.SetActive(hasDuplicate);

        // 룸코드 갱신 (NetworkVariable이므로 Host·Client 모두 동일)
        RefreshRoomCode();

        // 이름 변경 시 Vosk grammar 갱신
        RebuildLobbyGrammarIfNeeded();
    }

    void RefreshRoomCode()
    {
        if (roomCodeText == null) return;
        if (!LobbyContext.IsOnline) return;
        if (LobbyNetworkManager.Instance == null) return;

        string code = LobbyNetworkManager.Instance.SharedRoomCode;
        if (!string.IsNullOrEmpty(code))
            roomCodeText.text = LanDiscovery.FormatDisplayCode(code);
    }

    void RefreshReadyVisual()
    {
        if (checkImage != null)
            checkImage.sprite = _isReady ? readySprite : notReadySprite;
        // waitingTextObject는 RefreshAllSlots()의 CanStart() 기준으로 제어
    }

    void RefreshPortrait(int index)
    {
        if (portraitImage == null) return;
        Sprite s = GetPortrait(index);
        if (s != null) portraitImage.sprite = s;
    }

    Sprite GetPortrait(int colorIndex)
    {
        if (characterPortraits == null || characterPortraits.Length == 0) return null;
        int i = Mathf.Clamp(colorIndex, 0, characterPortraits.Length - 1);
        return characterPortraits[i];
    }

    // ── 네트워크 이벤트 핸들러 ────────────────────────────────────

    /// <summary>
    /// 킥됐거나 호스트가 나간 경우 타이틀로 복귀.
    /// OnClientDisconnectCallback은 Host/Client 모두에서 발행됨.
    /// </summary>
    void OnNetworkDisconnected(ulong clientId)
    {
        bool isSelf = NetworkManager.Singleton == null ||
                      !NetworkManager.Singleton.IsListening ||
                      clientId == NetworkManager.Singleton.LocalClientId;

        if (!isSelf) return;

        Debug.Log("[LobbyMenuController] 연결 종료 — 타이틀로 복귀");
        UnsubscribeNetworkEvents();
        TitleReturnFlow.Instance?.Request(new TitleReturnOptions
        {
            Reason = TitleReturnReason.ClientDisconnected,
            Scope  = TitleReturnScope.SessionOnly,
        });
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 온라인 UI 적용")]
    void Debug_OnlineUI()
    {
        LobbyContext.Mode = LobbyMode.OnlineHost;
        ApplyModeUI();
    }

    [ContextMenu("테스트: Ready 토글")]
    void Debug_Ready() => OnClickReady();

    [ContextMenu("테스트: 슬롯 전체 갱신")]
    void Debug_RefreshSlots() => RefreshAllSlots();
#endif
}
