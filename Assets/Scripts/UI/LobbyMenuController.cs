using System.Collections;
using System.Collections.Generic;
using Steamworks;
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
/// - 캐릭터 표시는 3D 프리뷰(LobbyCharacterPreview, 슬롯당 1개)로 처리 — 색은 LobbySlotUI.Refresh()가
///   state.ColorIndex를 그대로 넘겨 각 슬롯이 자기 프리뷰에 반영. 이 컨트롤러는 관여하지 않음.
///
/// [Inspector — 온라인 전용]
/// - onlineOnlyRoot     : 룸코드·슬롯1~3·Kick·SteamInvite 등 묶은 부모 GameObject
/// - readyRoot          : Ready 버튼 묶음 (Client에서만 표시, Host 숨김)
/// - roomCodeText       : 룸코드 표시 TMP_Text
/// - checkImage / readySprite / notReadySprite : Ready 비주얼 (NetworkVariable 동기화)
/// - waitingTextObject  : 모두 Ready 전 대기 문구
/// - allSlotUIs         : Slot0~3 에 붙인 LobbySlotUI 컴포넌트 4개 (순서 고정)
/// - startButton        : Start 버튼 (CanStart() 기반 interactable 제어)
/// - startButtonRoot    : Start 버튼 부모 (Host만 표시)
/// - duplicateColorWarning : 색 중복 시 표시할 경고 GameObject
///
/// [로비 Cheer Say Test (Vosk)]
/// - lobbyCheerEngine  : 로비 씬에 배치한 CheerKeywordEngine (_lobbyTestMode=true)
///   Vosk가 CheerName 감지 → OnKeywordDetected → ReportLobbyHeardServerRpc → 말풍선.
///   버프·ServerRpc 미발생. 슬롯 이름 변경 시 grammar 자동 갱신.
///
/// [버튼 OnClick 연결]
/// Btn_Start       → OnClickStart()
/// Btn_Ready       → OnClickReady()
/// Btn_Quit        → OnClickQuit()
/// Btn_Copy        → OnClickCopy()
/// Btn_SteamInvite → OnClickSteamInvite()
/// Option          → OnClickSettings()
/// (설정 패널 내부 닫기(X) 버튼은 OptionsMenuController.OnClickClose()에 직결 — 패널 자신을 SetActive(false).
///  OnClickCloseSettings()는 코드에서 강제로 닫아야 할 때 쓰는 보조 API.)
/// Slot1~3 Kick    → 각 LobbySlotUI.OnClickKick()
///
/// [디버그: 스테이지 선택 (테스트 전용, Host만 노출)]
/// stageSelectRoot 하위에 stageDropdown(TMP_Dropdown) + startAtStageButton(Btn) 배치.
/// Btn_StartAtStage → OnClickStartAtStage() — 드롭다운에서 고른 M./T. 스테이지로 바로 시작.
/// 정식 스테이지 선택 UX가 아니라 친구 테스트/QA용 — 필요 없으면 stageSelectRoot를 비워두면 비활성.
/// </summary>
public class LobbyMenuController : MonoBehaviour
{
    [Header("설정 패널")]
    [Tooltip("Option 버튼 클릭 시 열릴 패널 (OptionsMenuController 부착). 비워두면 클릭 무시.")]
    [SerializeField] private GameObject settingsPanel;

    [Header("온라인 전용 UI")]
    [SerializeField] private GameObject onlineOnlyRoot;
    [SerializeField] private GameObject readyRoot;
    [SerializeField] private TMP_Text   roomCodeText;

    [Header("Ready 상태")]
    [SerializeField] private Image      checkImage;
    [SerializeField] private Sprite     readySprite;
    [SerializeField] private Sprite     notReadySprite;
    [SerializeField] private GameObject waitingTextObject;

    [Header("슬롯 UI (Slot0~3, LobbySlotUI 컴포넌트 순서 고정)")]
    [Tooltip("Slot0(로컬), Slot1, Slot2, Slot3 순. LobbySlotUI 컴포넌트를 드래그.")]
    [SerializeField] private LobbySlotUI[] allSlotUIs = new LobbySlotUI[4];

    [Header("Start 버튼")]
    [Tooltip("CanStart() 결과로 interactable 제어. Host만 표시.")]
    [SerializeField] private Button     startButton;

    [Tooltip("Start 버튼 부모 — Host만 SetActive(true).")]
    [SerializeField] private GameObject startButtonRoot;

    [Tooltip("색 중복 시 표시할 경고 GameObject.\n" +
             "예) TMP_Text: '같은 색을 선택한 플레이어가 있습니다. 다른 색을 선택해주세요.'")]
    [SerializeField] private GameObject duplicateColorWarning;

    [Header("디버그: 스테이지 선택 (테스트 전용)")]
    [Tooltip("Host에게만 표시할 스테이지 선택 UI 묶음 (드롭다운+버튼 부모). 비워두면 기능 비활성.\n" +
             "친구 테스트 등 QA 목적 — 정식 스테이지 선택 UX가 아님. 릴리즈 전 startButtonRoot만 쓰려면 비워두면 됨.")]
    [SerializeField] private GameObject stageSelectRoot;

    [Tooltip("SceneFlowManager.sceneSequence 중 M./T. 접두사 씬만 채워지는 드롭다운.")]
    [SerializeField] private TMP_Dropdown stageDropdown;

    [Tooltip("드롭다운에서 고른 스테이지로 바로 시작하는 버튼 (CanStart() 조건은 Start와 동일하게 적용됨).")]
    [SerializeField] private Button startAtStageButton;

    [Header("로비 Cheer Say Test (Vosk)")]
    [Tooltip("로비 씬에 배치한 CheerKeywordEngine. _lobbyTestMode=true로 설정할 것.\n" +
             "null이면 Vosk 피드백 비활성.")]
    [SerializeField] private CheerKeywordEngine lobbyCheerEngine;

    // ── 런타임 상태 ──────────────────────────────────────────────

    bool        _isReady;              // NV 미러. RefreshAllSlots에서 서버값으로 덮어씀
    bool        _lnmSubscribed;        // LobbyNetworkManager 이벤트 구독 여부
    bool        _nmSubscribed;         // NetworkManager.OnClientDisconnectCallback 구독 여부
    bool        _cheerSubscribed;      // CheerKeywordEngine.OnKeywordDetected 구독 여부
    bool        _inviteSubscribed;     // SteamLobbyManager.OnInviteAccepted 구독 여부
    bool        _invitePending;        // 초대 이동 처리 중 — 중복 수락 무시용
    LobbySlotUI _localSlotUI;          // 로컬 플레이어 슬롯 캐시 — CheerName 결과 전달용

    // stageDropdown.value → SceneFlowManager.sceneSequence 인덱스 매핑 (M./T. 접두사만 필터링해 담음)
    readonly List<int> _stageSceneIndices = new();

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    void OnDestroy()
    {
        UnsubscribeAll();
    }

    void Start()
    {
        Debug.Log($"[LobbyMenuController][DIAG] Start() 진입 — 로비 씬 로드 완료. Mode={LobbyContext.Mode}, " +
                  $"LocalClientId={(NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId.ToString() : "null")}");
        ApplyModeUI();
        RefreshRoomCode();
        try { VoskModelLoader.LoadSync(); }
        catch (System.Exception e) { Debug.LogWarning($"[LobbyMenuController] VoskModelLoader 로드 실패 — {e.Message}"); }
        SubscribeAll();

        if (!_lnmSubscribed)
            StartCoroutine(WaitForLobbyNetworkManager());
    }

    /// <summary>LobbyNetworkManager가 아직 스폰 안 됐을 때 한 프레임씩 대기 후 재시도.</summary>
    IEnumerator WaitForLobbyNetworkManager()
    {
        while (LobbyNetworkManager.Instance == null)
            yield return null;
        SubscribeAll();
    }

    // ── 이벤트 구독 / 해제 ────────────────────────────────────────

    void SubscribeAll()
    {
        if (!_nmSubscribed && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnNetworkDisconnected;
            _nmSubscribed = true;
        }

        if (!_cheerSubscribed && lobbyCheerEngine != null)
        {
            lobbyCheerEngine.OnKeywordDetected += OnLobbyCheerDetected;
            _cheerSubscribed = true;
        }

        // 로비에 있는 동안 도착하는 Steam 초대는 여기서만 받는다 — 인게임 씬엔 구독자가 없어
        // 확정 정책(로비에서만 이동, 인게임은 무시)이 구조적으로 보장된다.
        if (!_inviteSubscribed && !NetworkManagerSetup.UseLocalNetworkPath && SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.OnInviteAccepted += OnSteamInviteAccepted;
            _inviteSubscribed = true;
        }

        if (_lnmSubscribed || LobbyNetworkManager.Instance == null) return;

        LobbyNetworkManager.Instance.OnSlotsChanged        += RefreshAllSlots;
        LobbyNetworkManager.Instance.OnCheerNameResult     += OnCheerNameResult;
        LobbyNetworkManager.Instance.OnLobbyHeardBroadcast += OnLobbyHeardBroadcast;
        _lnmSubscribed = true;
        RefreshAllSlots();
    }

    void UnsubscribeAll()
    {
        if (_lnmSubscribed && LobbyNetworkManager.Instance != null)
        {
            LobbyNetworkManager.Instance.OnSlotsChanged        -= RefreshAllSlots;
            LobbyNetworkManager.Instance.OnCheerNameResult     -= OnCheerNameResult;
            LobbyNetworkManager.Instance.OnLobbyHeardBroadcast -= OnLobbyHeardBroadcast;
            _lnmSubscribed = false;
        }

        if (_nmSubscribed && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnNetworkDisconnected;
            _nmSubscribed = false;
        }

        if (_cheerSubscribed && lobbyCheerEngine != null)
        {
            lobbyCheerEngine.OnKeywordDetected -= OnLobbyCheerDetected;
            _cheerSubscribed = false;
        }

        if (_inviteSubscribed && SteamLobbyManager.Instance != null)
        {
            SteamLobbyManager.Instance.OnInviteAccepted -= OnSteamInviteAccepted;
            _inviteSubscribed = false;
        }
    }

    // ── Steam 초대 (로비에서 수락) ────────────────────────────────

    /// <summary>
    /// 로비에 있는 동안 Steam 초대를 수락한 경우 — 현재 방을 정리하고 초대받은 방으로 이동한다.
    /// 초대 소비자가 타이틀 씬(TitleMenuController)에만 있어서 로비에서는 초대가 조용히 소멸했던
    /// 버그의 대응 경로(SteamworksIntegrationDesign.md 트랙6).
    /// Host가 수락하면 이 방은 종료된다 — 기존 §12 정책 그대로이며 별도 확인 절차는 두지 않는다.
    /// </summary>
    void OnSteamInviteAccepted(SteamId lobbyId)
    {
        if (_invitePending) return;
        _invitePending = true;
        StartCoroutine(MoveToInvitedLobby(lobbyId));
    }

    IEnumerator MoveToInvitedLobby(SteamId lobbyId)
    {
        Debug.Log($"[LobbyMenuController] 로비에서 초대 수락 — 현재 방을 종료하고 lobbyId={lobbyId}로 이동합니다.");

        if (LobbyNetworkManager.Instance != null && LobbyNetworkManager.Instance.IsHost)
            LobbyNetworkManager.Instance.NotifyHostQuit();

        // 위 ClientRpc가 실제로 전송된 뒤에 Shutdown이 오도록 한 프레임 양보한다.
        yield return null;

        NetworkManagerSetup.Instance?.Shutdown();

        if (NetworkManagerSetup.RestartWithConnectLobby(lobbyId)) yield break;

        Debug.LogError("[LobbyMenuController] 재실행 실패 — 초대 이동을 중단하고 타이틀로 복귀합니다.");
        _invitePending = false;
        TitleReturnFlow.Instance?.Request(new TitleReturnOptions
        {
            Reason = TitleReturnReason.LobbyQuit,
            Scope  = TitleReturnScope.SessionOnly,
        });
    }

    // ── Cheer / Heard 핸들러 ──────────────────────────────────────

    /// <summary>SetCheerNameServerRpc 결과 — 로컬 슬롯 UI에 전달.</summary>
    void OnCheerNameResult(bool success, string errorKey)
    {
        _localSlotUI?.ShowCheerNameResult(success, errorKey);
    }

    /// <summary>Vosk가 CheerName 감지 → Host에 ServerRpc 보고.</summary>
    void OnLobbyCheerDetected(int targetColorIndex)
    {
        LobbyNetworkManager.Instance?.ReportLobbyHeardServerRpc(targetColorIndex);
    }

    /// <summary>Host → 전원 Heard 브로드캐스트 수신.</summary>
    void OnLobbyHeardBroadcast(int targetColorIndex, int speakerColorIndex)
    {
        ShowHeardOnSlot(targetColorIndex, speakerColorIndex);
    }

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
                Debug.Log($"[LobbyMenuController] Heard: target={targetColorIndex} speaker={speakerColorIndex}");
                break;
            }
        }
    }

    /// <summary>슬롯 이름 변경 시 Vosk grammar 갱신. 중복 재빌드는 CheerEngine 내부에서 차단.</summary>
    void RebuildLobbyGrammarIfNeeded()
    {
        if (lobbyCheerEngine == null || LobbyNetworkManager.Instance == null) return;

        int count = LobbyNetworkManager.Instance.SlotCount;
        var names = new string[count];
        for (int i = 0; i < count; i++)
            names[i] = LobbyNetworkManager.GetEffectiveCheerName(LobbyNetworkManager.Instance.GetSlot(i));

        lobbyCheerEngine.ApplySessionGrammar(names);
    }

    // ── 모드 분기 ─────────────────────────────────────────────────

    void ApplyModeUI()
    {
        if (onlineOnlyRoot  != null) onlineOnlyRoot.SetActive(true);
        if (readyRoot       != null) readyRoot.SetActive(LobbyContext.IsOnlineClient);
        if (startButtonRoot != null) startButtonRoot.SetActive(LobbyContext.IsOnlineHost);
        if (stageSelectRoot != null) stageSelectRoot.SetActive(LobbyContext.IsOnlineHost);
        RefreshReadyVisual();
        PopulateStageDropdown();
    }

    /// <summary>
    /// SceneFlowManager.sceneSequence 중 "M." / "T." 접두사 씬(스테이지·보스)만 골라 드롭다운을 채운다.
    /// 0.Title / 1.Lobby / End.Demo 등은 테스트 시작 대상이 아니라 제외.
    /// </summary>
    void PopulateStageDropdown()
    {
        if (stageDropdown == null) return;
        _stageSceneIndices.Clear();
        stageDropdown.ClearOptions();

        if (SceneFlowManager.Instance == null) return;

        var labels = new List<string>();
        for (int i = 0; i < SceneFlowManager.Instance.SceneCount; i++)
        {
            string name = SceneFlowManager.Instance.GetSceneName(i);
            if (string.IsNullOrEmpty(name)) continue;
            if (!name.StartsWith("M.") && !name.StartsWith("T.")) continue;

            _stageSceneIndices.Add(i);
            labels.Add(name);
        }

        stageDropdown.AddOptions(labels);
    }

    // ── 버튼 콜백 ─────────────────────────────────────────────────

    /// <summary>Start 버튼. Host만 호출. LobbyNetworkManager.StartGameServerRpc().</summary>
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

    /// <summary>
    /// 디버그 스테이지 선택 버튼. Host만 호출.
    /// stageDropdown에서 고른 씬으로 바로 시작 — 조건(전원 Ready 등)은 OnClickStart와 동일하게
    /// LobbyNetworkManager.CanStart()가 서버 측에서 검증한다. 테스트/QA 목적.
    /// </summary>
    public void OnClickStartAtStage()
    {
        if (!LobbyContext.IsOnlineHost) return;

        if (LobbyNetworkManager.Instance == null)
        {
            Debug.LogWarning("[LobbyMenuController] LobbyNetworkManager를 찾을 수 없습니다.");
            return;
        }

        if (stageDropdown == null || stageDropdown.value < 0 || stageDropdown.value >= _stageSceneIndices.Count)
        {
            Debug.LogWarning("[LobbyMenuController] stageDropdown 선택값이 유효하지 않습니다.");
            return;
        }

        int sceneIndex = _stageSceneIndices[stageDropdown.value];
        LobbyNetworkManager.Instance.StartGameAtStageServerRpc(sceneIndex);
    }

    /// <summary>
    /// Ready 버튼 토글 후 ServerRpc로 동기화.
    /// _isReady는 낙관적 UI용 — 서버 확인 후 RefreshAllSlots에서 NV값으로 덮어씌워짐.
    /// </summary>
    public void OnClickReady()
    {
        _isReady = !_isReady;
        RefreshReadyVisual();
        LobbyNetworkManager.Instance?.SetReadyServerRpc(_isReady);
    }

    /// <summary>Quit 버튼 — TitleReturnFlow에 복귀 위임.</summary>
    public void OnClickQuit()
    {
        // Host가 나가면 Listen-Server 구조상 접속 중인 Client 전원도 함께 끊긴다.
        // Shutdown() 전에 먼저 알려서 Client가 곧바로 타이틀로 복귀하게 한다
        // (LobbyNetworkManager.OnClientDisconnectedSelf가 그물망 역할로 한 번 더 받아준다).
        if (LobbyNetworkManager.Instance != null && LobbyNetworkManager.Instance.IsHost)
            LobbyNetworkManager.Instance.NotifyHostQuit();

        TitleReturnFlow.Instance?.Request(new TitleReturnOptions
        {
            Reason = TitleReturnReason.LobbyQuit,
            Scope  = TitleReturnScope.SessionOnly,
        });
    }

    /// <summary>Option 버튼 OnClick에 연결.</summary>
    public void OnClickSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
        else Debug.LogWarning("[LobbyMenuController] settingsPanel이 연결되지 않았습니다.");
    }

    /// <summary>
    /// 설정 패널 닫기 — 코드에서 강제로 닫아야 할 때 쓰는 보조 API.
    /// 패널 내부 닫기(X) 버튼은 OptionsMenuController.OnClickClose()에 직결되어 있어 이 메서드를 거치지 않는다.
    /// </summary>
    public void OnClickCloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    /// <summary>Copy 버튼 — 전체 6자리 룸코드 클립보드 복사.</summary>
    public void OnClickCopy()
    {
        string code = LobbyNetworkManager.Instance != null
            ? LobbyNetworkManager.Instance.SharedRoomCode
            : string.Empty;

        if (string.IsNullOrEmpty(code) && NetworkManagerSetup.Instance != null)
            code = NetworkManagerSetup.Instance.RoomCode;

        if (!string.IsNullOrEmpty(code))
        {
            GUIUtility.systemCopyBuffer = code;
            Debug.Log($"[LobbyMenuController] 룸코드 복사됨: {code}");
        }
    }

    /// <summary>SteamInvite — Invite Overlay 열기 (SteamworksIntegrationDesign.md §3).</summary>
    public void OnClickSteamInvite()
    {
        if (SteamLobbyManager.Instance != null && SteamLobbyManager.Instance.IsInLobby)
        {
            SteamLobbyManager.Instance.OpenInviteOverlay();
        }
        else
        {
            Debug.Log("[LobbyMenuController] Steam Lobby가 없습니다 (로컬 경로 세션이거나 Lobby 미생성).");
        }
    }

    // ── UI 갱신 ───────────────────────────────────────────────────

    /// <summary>
    /// 전체 슬롯 UI 갱신. LobbyNetworkManager.OnSlotsChanged 이벤트에서 호출됨.
    /// _slots 순서대로 표시 → Host 항상 Slot0, 이후 접속 순.
    /// </summary>
    void RefreshAllSlots()
    {
        if (LobbyNetworkManager.Instance == null)
        {
            Debug.LogWarning("[LobbyMenuController][DIAG] RefreshAllSlots — LobbyNetworkManager.Instance가 null이라 스킵");
            return;
        }

        ulong localId = NetworkManager.Singleton != null
            ? NetworkManager.Singleton.LocalClientId
            : ulong.MaxValue;

        ulong hostId = LobbyNetworkManager.Instance.HostClientId;
        bool  isHost = LobbyContext.IsOnlineHost;

        Debug.Log($"[LobbyMenuController][DIAG] RefreshAllSlots 실행 — SlotCount={LobbyNetworkManager.Instance.SlotCount}, " +
                  $"localId={localId}, hostId={hostId}, isHost={isHost}, " +
                  $"NGO.ConnectedClients={(NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClients.Count : -1)}");

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

                Debug.Log($"[LobbyMenuController][DIAG] Slot[{i}] clientId={s.ClientId} color={s.ColorIndex} " +
                          $"ready={s.IsReady} isLocalSlot={isLocalSlot} isHostSlot={isHostSlot}");

                allSlotUIs[i].Refresh(s, canKick, isHostSlot, isLocalSlot);

                if (isLocalSlot)
                {
                    _localSlotUI = allSlotUIs[i];
                    _isReady     = s.IsReady; // NetworkVariable → 로컬 동기화
                }
            }
            else
            {
                Debug.Log($"[LobbyMenuController][DIAG] Slot[{i}] SetEmpty (SlotCount={LobbyNetworkManager.Instance.SlotCount} 이하 범위 밖)");
                allSlotUIs[i].SetEmpty();
            }
        }

        RefreshReadyVisual();

        bool canStart     = LobbyNetworkManager.Instance.CanStart();
        bool hasDuplicate = LobbyNetworkManager.Instance.HasDuplicateColors();

        if (startButton          != null) startButton.interactable   = canStart;
        if (startAtStageButton   != null) startAtStageButton.interactable = canStart;
        if (waitingTextObject    != null) waitingTextObject.SetActive(!canStart);
        if (duplicateColorWarning != null) duplicateColorWarning.SetActive(hasDuplicate);

        RefreshRoomCode();
        RebuildLobbyGrammarIfNeeded();
    }

    /// <summary>
    /// 룸코드 표시. 로컬 경로(6자리)는 기존 LanDiscovery 마스킹, Steam 경로(Lobby Id)는
    /// SteamLobbyManager.MaskCode로 마스킹한다(§3, `7**1` 형태). 코드 자체는
    /// LobbyNetworkManager.SharedRoomCode(NetworkVariable) 하나로 두 경로 모두 동일하게 전달된다.
    /// </summary>
    void RefreshRoomCode()
    {
        if (roomCodeText == null) return;
        if (LobbyNetworkManager.Instance == null) return;

        string code = LobbyNetworkManager.Instance.SharedRoomCode;
        if (string.IsNullOrEmpty(code)) return;

        roomCodeText.text = code.Length == 6
            ? LanDiscovery.FormatDisplayCode(code)
            : SteamLobbyManager.MaskCode(code);
    }

    void RefreshReadyVisual()
    {
        if (checkImage != null)
            checkImage.sprite = _isReady ? readySprite : notReadySprite;
    }

    // ── 네트워크 이벤트 핸들러 ────────────────────────────────────

    /// <summary>킥됐거나 호스트가 나간 경우 타이틀로 복귀.</summary>
    void OnNetworkDisconnected(ulong clientId)
    {
        bool isSelf = NetworkManager.Singleton == null ||
                      !NetworkManager.Singleton.IsListening ||
                      clientId == NetworkManager.Singleton.LocalClientId;

        if (!isSelf) return;

        Debug.Log("[LobbyMenuController] 연결 종료 — 타이틀로 복귀");
        UnsubscribeAll();
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
