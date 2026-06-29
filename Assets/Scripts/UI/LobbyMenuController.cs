using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 로비 씬 메인 컨트롤러.
/// LobbyContext.Mode 에 따라 Offline / OnlineHost / OnlineClient 동작을 분기한다.
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
    [Header("씬 전환")]
    [SerializeField] private string stageSceneName = "M.Stage1";
    [SerializeField] private string titleSceneName  = "0.Title";

    [Header("캐릭터 초상화")]
    [Tooltip("드롭다운 인덱스 순: [0]Blue [1]Purple [2]Green [3]Yellow")]
    [SerializeField] private Sprite[] characterPortraits = new Sprite[4];

    [Tooltip("Slot0/CharacterArea/Image — 로컬 플레이어 드롭다운 초상화")]
    [SerializeField] private Image portraitImage;

    [Tooltip("Slot0 내 TMP_Dropdown")]
    [SerializeField] private TMP_Dropdown characterDropdown;

    [Header("온라인 전용 UI (오프라인 시 숨김)")]
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

    [Header("페이드 (선택)")]
    [SerializeField] private ScreenFader screenFader;
    [SerializeField] private float       fadeOutDuration = 0f;

    // ── 색상 매핑 ────────────────────────────────────────────────

    static readonly PlayerColorType[] IndexToColor =
    {
        PlayerColorType.Blue,
        PlayerColorType.Purple,
        PlayerColorType.Green,
        PlayerColorType.Yellow,
    };

    // ── 런타임 상태 ──────────────────────────────────────────────

    bool _isReady;
    bool _isNetworkSubscribed;

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        // 오프라인 모드: LobbySlotUI 없이 직접 dropdown 사용
        // 온라인 모드: LobbySlotUI가 드롭다운 이벤트를 담당하므로 여기서는 오프라인만 구독
        if (characterDropdown != null && LobbyContext.IsOffline)
            characterDropdown.onValueChanged.AddListener(OnCharacterChanged);
    }

    void OnDestroy()
    {
        if (characterDropdown != null)
            characterDropdown.onValueChanged.RemoveListener(OnCharacterChanged);

        UnsubscribeNetworkEvents();
    }

    void Start()
    {
        ApplyModeUI();
        RefreshRoomCode();

        int initial = characterDropdown != null ? characterDropdown.value : 0;
        RefreshPortrait(initial);

        if (LobbyContext.IsOnline)
            SubscribeNetworkEvents();
    }

    // ── 네트워크 이벤트 구독 ──────────────────────────────────────

    void SubscribeNetworkEvents()
    {
        if (LobbyNetworkManager.Instance != null)
        {
            LobbyNetworkManager.Instance.OnSlotsChanged += RefreshAllSlots;
            _isNetworkSubscribed = true;
            RefreshAllSlots();
        }
        // Instance가 아직 null이면 Update에서 재시도

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback += OnNetworkDisconnected;
    }

    // Instance가 나중에 생기는 경우 대비 (Client 타이밍 이슈)
    void Update()
    {
        if (!_isNetworkSubscribed && LobbyContext.IsOnline && LobbyNetworkManager.Instance != null)
        {
            LobbyNetworkManager.Instance.OnSlotsChanged += RefreshAllSlots;
            _isNetworkSubscribed = true;
            RefreshAllSlots();
        }
    }

    void UnsubscribeNetworkEvents()
    {
        if (LobbyNetworkManager.Instance != null)
            LobbyNetworkManager.Instance.OnSlotsChanged -= RefreshAllSlots;

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnNetworkDisconnected;
    }

    // ── 모드 분기 ─────────────────────────────────────────────────

    void ApplyModeUI()
    {
        bool isOffline = LobbyContext.IsOffline;

        if (onlineOnlyRoot != null) onlineOnlyRoot.SetActive(!isOffline);
        // 호스트는 Ready 없음 — 클라이언트(팀원)만 Ready 버튼 표시
        if (readyRoot != null) readyRoot.SetActive(LobbyContext.IsOnlineClient);

        // Start 버튼은 Host만 표시
        if (startButtonRoot != null)
            startButtonRoot.SetActive(LobbyContext.IsOnlineHost || isOffline);

        if (!isOffline)
            RefreshReadyVisual();
    }

    // ── 버튼 콜백 ─────────────────────────────────────────────────

    /// <summary>
    /// Start 버튼.
    /// 오프라인: 색 1개 적용 후 씬 전환.
    /// 온라인 Host: LobbyNetworkManager.StartGameServerRpc() — NGO가 씬 전환 처리.
    /// </summary>
    public void OnClickStart()
    {
        if (LobbyContext.IsOffline)
        {
            ApplySoloColor();
            StartCoroutine(LoadSceneWithFade(stageSceneName));
            return;
        }

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
        if (LobbyContext.IsOffline) return;

        _isReady = !_isReady;
        RefreshReadyVisual();

        LobbyNetworkManager.Instance?.SetReadyServerRpc(_isReady);
    }

    /// <summary>Quit 버튼 — 세션 전체 정리 후 타이틀 복귀.</summary>
    public void OnClickQuit()
    {
        // 온라인: NGO Shutdown + LanDiscovery 중단 + 세션 데이터 초기화
        if (LobbyContext.IsOnline)
            NetworkManagerSetup.Instance?.Shutdown();

        // 공통: GameSession 런타임 리셋 + 모드 초기화
        GameSession.Instance?.ResetSession();
        LobbyContext.Mode = LobbyMode.Offline;

        StartCoroutine(LoadSceneWithFade(titleSceneName));
    }

    /// <summary>
    /// Copy 버튼 — 전체 6자리 룸코드 클립보드 복사.
    /// Host / Client 모두 동일한 코드 복사 (SharedRoomCode NetworkVariable).
    /// </summary>
    public void OnClickCopy()
    {
        if (LobbyContext.IsOffline) return;

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

    /// <summary>Dropdown OnValueChanged — 초상화 갱신 + 온라인이면 색 동기화.</summary>
    public void OnCharacterChanged(int index)
    {
        RefreshPortrait(index);

        if (LobbyContext.IsOnline)
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

    // ── 솔로 전용 ─────────────────────────────────────────────────

    void ApplySoloColor()
    {
        if (GameSession.Instance == null) return;

        int index = characterDropdown != null ? characterDropdown.value : 0;
        int safe  = Mathf.Clamp(index, 0, IndexToColor.Length - 1);
        GameSession.Instance.SetActiveColors(new[] { IndexToColor[safe] });
        Debug.Log($"[LobbyMenuController] 솔로 색상 적용: {IndexToColor[safe]}");
    }

    // ── 네트워크 이벤트 핸들러 ────────────────────────────────────

    /// <summary>
    /// 킥됐거나 호스트가 나간 경우 타이틀로 복귀.
    /// OnClientDisconnectCallback은 Host/Client 모두에서 발행됨.
    /// </summary>
    void OnNetworkDisconnected(ulong clientId)
    {
        // 내 연결이 끊긴 경우만 처리 (Host는 다른 클라이언트 이탈도 여기 들어옴)
        bool isSelf = NetworkManager.Singleton == null ||
                      !NetworkManager.Singleton.IsListening ||
                      clientId == NetworkManager.Singleton.LocalClientId;

        if (!isSelf) return;

        Debug.Log("[LobbyMenuController] 연결 종료 — 타이틀로 복귀");
        UnsubscribeNetworkEvents();
        StartCoroutine(LoadSceneWithFade(titleSceneName));
    }

    // ── 씬 전환 ───────────────────────────────────────────────────

    IEnumerator LoadSceneWithFade(string sceneName)
    {
        if (screenFader != null && fadeOutDuration > 0f)
        {
            screenFader.FadeOut(fadeOutDuration);
            yield return new WaitForSeconds(fadeOutDuration);
        }

        SceneManager.LoadScene(sceneName);
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 솔로 Start")]
    void Debug_SoloStart()
    {
        LobbyContext.Mode = LobbyMode.Offline;
        ApplyModeUI();
        ApplySoloColor();
        Debug.Log("[LobbyMenuController] 솔로 색상 적용 완료");
    }

    [ContextMenu("테스트: 온라인 UI 적용")]
    void Debug_OnlineUI()
    {
        LobbyContext.Mode = LobbyMode.OnlineHost;
        ApplyModeUI();
    }

    [ContextMenu("테스트: 오프라인 UI 적용")]
    void Debug_OfflineUI()
    {
        LobbyContext.Mode = LobbyMode.Offline;
        ApplyModeUI();
    }

    [ContextMenu("테스트: Ready 토글")]
    void Debug_Ready() => OnClickReady();

    [ContextMenu("테스트: 슬롯 전체 갱신")]
    void Debug_RefreshSlots() => RefreshAllSlots();
#endif
}
