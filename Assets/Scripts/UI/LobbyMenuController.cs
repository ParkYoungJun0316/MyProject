using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

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
/// - characterPortraits : [0]BERRY [1]GUMA [2]SSUK [3]DANHO 순 Sprite
/// - portraitImage      : Slot0/CharacterArea/Image
/// - characterDropdown  : Slot0 내 TMP_Dropdown (OnValueChanged 연결)
/// - screenFader / fadeOutDuration : 페이드 (선택)
///
/// [Inspector — 온라인 전용 (오프라인 시 숨길 UI 묶음)]
/// - onlineOnlyRoot     : 룸코드·슬롯1~3·Kick·SteamInvite 등 묶은 부모 GameObject
/// - readyRoot          : Ready 버튼 + checkImage 묶음
/// - roomCodeText       : 룸코드 표시 TMP_Text
/// - readySprite / notReadySprite / checkImage
///
/// [버튼 OnClick 연결]
/// Btn_Start           → OnClickStart()
/// Btn_Ready           → OnClickReady()   (온라인만 활성)
/// Btn_Quit            → OnClickQuit()
/// Btn_Copy            → OnClickCopy()    (온라인만 활성)
/// Btn_SteamInvite     → OnClickSteamInvite()
/// Kick                → OnClickKick()
/// Dropdown OnValueChanged → OnCharacterChanged(Int32)
/// </summary>
public class LobbyMenuController : MonoBehaviour
{
    [Header("씬 전환")]
    [Tooltip("Start 버튼으로 로드할 씬. Build Settings 이름과 정확히 일치.")]
    [SerializeField] private string stageSceneName = "M.Stage1";

    [Tooltip("Quit 버튼으로 복귀할 씬.")]
    [SerializeField] private string titleSceneName = "0.Title";

    [Header("캐릭터 초상화")]
    [Tooltip("드롭다운 인덱스 순: [0]BERRY  [1]GUMA  [2]SSUK  [3]DANHO")]
    [SerializeField] private Sprite[] characterPortraits = new Sprite[4];

    [Tooltip("Slot0/CharacterArea/Image")]
    [SerializeField] private Image portraitImage;

    [Tooltip("Slot0 내 TMP_Dropdown")]
    [SerializeField] private TMP_Dropdown characterDropdown;

    [Header("온라인 전용 UI (오프라인 시 숨김)")]
    [Tooltip("룸코드 패널·슬롯1~3·Kick·SteamInvite 등을 묶은 부모 GameObject.\n" +
             "오프라인 모드에서 SetActive(false) 됨.")]
    [SerializeField] private GameObject onlineOnlyRoot;

    [Tooltip("Ready 버튼 + checkImage 묶음. 오프라인에서 숨김.")]
    [SerializeField] private GameObject readyRoot;

    [Tooltip("룸코드 표시 텍스트 (온라인 Host 시 자동 생성된 코드 표시)")]
    [SerializeField] private TMP_Text roomCodeText;

    [Header("Ready 상태 (온라인)")]
    [SerializeField] private Image   checkImage;
    [SerializeField] private Sprite  readySprite;
    [SerializeField] private Sprite  notReadySprite;

    [Tooltip("모두 Ready 시 숨길 대기 문구 오브젝트")]
    [SerializeField] private GameObject waitingTextObject;

    [Header("페이드 (선택)")]
    [SerializeField] private ScreenFader screenFader;

    [Tooltip("페이드아웃 시간(초). 0이면 즉시.")]
    [SerializeField] private float fadeOutDuration = 0f;

    // ── 색상 매핑 (드롭다운 인덱스 → PlayerColorType) ─────────────
    static readonly PlayerColorType[] IndexToColor =
    {
        PlayerColorType.Blue,
        PlayerColorType.Purple,
        PlayerColorType.Green,
        PlayerColorType.Yellow,
    };

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
    }

    void Start()
    {
        ApplyModeUI();
        RefreshRoomCode();

        int initialIndex = characterDropdown != null ? characterDropdown.value : 0;
        RefreshPortrait(initialIndex);
    }

    // ── 모드 분기 ─────────────────────────────────────────────────

    void ApplyModeUI()
    {
        bool isOffline = LobbyContext.IsOffline;

        // 온라인 전용 UI: 오프라인이면 숨김
        if (onlineOnlyRoot != null) onlineOnlyRoot.SetActive(!isOffline);
        if (readyRoot       != null) readyRoot.SetActive(!isOffline);

        if (!isOffline)
            RefreshReadyVisual();
    }

    // ── 버튼 콜백 ─────────────────────────────────────────────────

    /// <summary>
    /// Start 버튼.
    /// 오프라인: 드롭다운 색 1개를 GameSession에 적용하고 바로 스테이지 로드.
    /// 온라인  : Ready 상태 확인 후 로드 (NGO 연동 전까지는 로컬 Ready만 확인).
    /// </summary>
    public void OnClickStart()
    {
        if (LobbyContext.IsOffline)
        {
            ApplySoloColor();
            StartCoroutine(LoadSceneWithFade(stageSceneName));
            return;
        }

        // 온라인 — NGO 연동 전 로컬 Ready 임시 확인
        if (!_isReady)
        {
            Debug.Log("[LobbyMenuController] 전원 Ready 후 시작 가능합니다.");
            return;
        }

        StartCoroutine(LoadSceneWithFade(stageSceneName));
    }

    /// <summary>Ready 버튼 — 온라인 전용. NGO 연동 전엔 로컬 토글.</summary>
    public void OnClickReady()
    {
        if (LobbyContext.IsOffline) return;

        _isReady = !_isReady;
        RefreshReadyVisual();
    }

    /// <summary>Quit 버튼 — 타이틀 복귀.</summary>
    public void OnClickQuit()
    {
        StartCoroutine(LoadSceneWithFade(titleSceneName));
    }

    /// <summary>Copy 버튼 — 전체 6자리 룸코드를 클립보드에 복사 (온라인 전용).</summary>
    public void OnClickCopy()
    {
        if (LobbyContext.IsOffline) return;

        // 표시는 마스킹(12**56)이지만 복사는 전체 6자리
        string code = NetworkManagerSetup.Instance != null
            ? NetworkManagerSetup.Instance.RoomCode
            : string.Empty;

        if (!string.IsNullOrEmpty(code))
        {
            GUIUtility.systemCopyBuffer = code;
            Debug.Log($"[LobbyMenuController] 룸코드 복사됨: {code}");
        }
    }

    /// <summary>SteamInvite — 온라인 연동 전 스텁.</summary>
    public void OnClickSteamInvite()
    {
        Debug.Log("[LobbyMenuController] Steam 초대는 멀티플레이어 버전에서 지원합니다.");
    }

    /// <summary>Kick — 온라인 호스트 전용. 연동 전 스텁.</summary>
    public void OnClickKick()
    {
        if (!LobbyContext.IsOnlineHost) return;
        Debug.Log("[LobbyMenuController] Kick — 네트워크 연동 후 구현됩니다.");
    }

    /// <summary>Dropdown OnValueChanged — 캐릭터 초상화 교체.</summary>
    public void OnCharacterChanged(int index)
    {
        RefreshPortrait(index);
    }

    // ── 내부 ──────────────────────────────────────────────────────

    /// <summary>솔로 모드: 드롭다운 선택 색을 GameSession에 1인으로 적용.</summary>
    void ApplySoloColor()
    {
        if (GameSession.Instance == null) return;

        int index = characterDropdown != null ? characterDropdown.value : 0;
        int safeIndex = Mathf.Clamp(index, 0, IndexToColor.Length - 1);
        PlayerColorType chosen = IndexToColor[safeIndex];

        GameSession.Instance.SetActiveColors(new[] { chosen });
        Debug.Log($"[LobbyMenuController] 솔로 색상 적용: {chosen}");
    }

    /// <summary>
    /// 온라인 Host일 때 룸코드를 마스킹 형식(12**56)으로 표시.
    /// Start()에서 자동 호출.
    /// </summary>
    void RefreshRoomCode()
    {
        if (!LobbyContext.IsOnlineHost) return;
        if (roomCodeText == null) return;
        if (NetworkManagerSetup.Instance == null) return;

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
        if (characterPortraits == null || characterPortraits.Length == 0) return;

        int safeIndex = Mathf.Clamp(index, 0, characterPortraits.Length - 1);
        if (characterPortraits[safeIndex] != null)
            portraitImage.sprite = characterPortraits[safeIndex];
    }

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
        Debug.Log("[LobbyMenuController] 솔로 색상 적용 완료 (씬 전환 없음)");
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

    [ContextMenu("테스트: 캐릭터 0 (BERRY)")]
    void Debug_Portrait0() => RefreshPortrait(0);

    [ContextMenu("테스트: 캐릭터 1 (GUMA)")]
    void Debug_Portrait1() => RefreshPortrait(1);

    [ContextMenu("테스트: 캐릭터 2 (SSUK)")]
    void Debug_Portrait2() => RefreshPortrait(2);

    [ContextMenu("테스트: 캐릭터 3 (DANHO)")]
    void Debug_Portrait3() => RefreshPortrait(3);
#endif
}
