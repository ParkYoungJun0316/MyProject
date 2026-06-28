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

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        if (characterDropdown != null)
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
            // 구독 직후 즉시 갱신 — OnNetworkSpawn이 이미 끝난 경우 대비
            RefreshAllSlots();
        }

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback += OnNetworkDisconnected;
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

    /// <summary>Quit 버튼 — 온라인이면 NetworkManager Shutdown 후 타이틀 복귀.</summary>
    public void OnClickQuit()
    {
        if (LobbyContext.IsOnline)
            NetworkManagerSetup.Instance?.Shutdown();

        StartCoroutine(LoadSceneWithFade(titleSceneName));
    }

    /// <summary>Copy 버튼 — 전체 6자리 룸코드 클립보드 복사.</summary>
    public void OnClickCopy()
    {
        if (LobbyContext.IsOffline) return;

        string code = NetworkManagerSetup.Instance != null
            ? NetworkManagerSetup.Instance.RoomCode
            : string.Empty;

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
    /// 로컬 플레이어 → Slot0 UI / 나머지 → Slot1~3 UI
    /// </summary>
    void RefreshAllSlots()
    {
        if (LobbyNetworkManager.Instance == null) return;

        ulong localId = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.LocalClientId
            : ulong.MaxValue;

        bool isHost = LobbyContext.IsOnlineHost;

        // 로컬 vs 타인 분리
        LobbyPlayerState localState = LobbyPlayerState.Empty;
        var others = new List<LobbyPlayerState>();

        for (int i = 0; i < LobbyNetworkManager.Instance.SlotCount; i++)
        {
            LobbyPlayerState s = LobbyNetworkManager.Instance.GetSlot(i);
            if (s.ClientId == localId) localState = s;
            else                       others.Add(s);
        }

        // Slot0 = 로컬 플레이어 (Kick 불가)
        if (allSlotUIs.Length > 0 && allSlotUIs[0] != null)
        {
            if (localState.IsOccupied)
                allSlotUIs[0].Refresh(localState, GetPortrait(localState.ColorIndex), false);
            else
                allSlotUIs[0].SetEmpty();
        }

        // Slot1~3 = 타 플레이어
        for (int i = 1; i < allSlotUIs.Length; i++)
        {
            if (allSlotUIs[i] == null) continue;
            int oi = i - 1;
            if (oi < others.Count)
                allSlotUIs[i].Refresh(others[oi], GetPortrait(others[oi].ColorIndex), isHost);
            else
                allSlotUIs[i].SetEmpty();
        }

        // Start 버튼 interactable (Host만)
        if (startButton != null)
            startButton.interactable = LobbyNetworkManager.Instance.CanStart();

        // LobbyNetworkManager.OnSlotsChanged 구독이 늦을 수 있으므로 재구독 시도
        LobbyNetworkManager.Instance.OnSlotsChanged -= RefreshAllSlots;
        LobbyNetworkManager.Instance.OnSlotsChanged += RefreshAllSlots;
    }

    void RefreshRoomCode()
    {
        if (!LobbyContext.IsOnlineHost) return;
        if (roomCodeText == null || NetworkManagerSetup.Instance == null) return;

        roomCodeText.text = LanDiscovery.FormatDisplayCode(NetworkManagerSetup.Instance.RoomCode);
    }

    void RefreshReadyVisual()
    {
        if (checkImage != null)
            checkImage.sprite = _isReady ? readySprite : notReadySprite;

        if (waitingTextObject != null)
            waitingTextObject.SetActive(!_isReady);
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
