using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 로비 씬 메인 컨트롤러 (A1 오프라인 셸).
/// 네트워크(NGO / Ready 동기화)는 A4에서 추가 예정.
///
/// [배치 방법]
/// TitleCanvas 또는 빈 GameObject에 부착.
///
/// [Inspector 연결]
/// - stageSceneName     : Start 버튼으로 로드할 씬 이름 (기본 "M.Stage1")
/// - titleSceneName     : Quit 버튼으로 복귀할 씬 이름 (기본 "0.Title")
/// - dummyRoomCode      : Btn_Copy가 클립보드에 복사할 더미 룸코드
/// - characterPortraits : [0]BERRY [1]GUMA [2]SSUK [3]DANHO 순 Sprite 배열
/// - portraitImage      : Slot0/CharacterArea/Image
/// - characterDropdown  : TMP_Dropdown (OnValueChanged → OnCharacterChanged 연결)
/// - checkImage         : Ready 상태 표시 Image
/// - readySprite        : Check.png
/// - notReadySprite     : NotReady.png
/// - screenFader        : 선택. 씬 전환 전 페이드아웃
/// - fadeOutDuration    : 페이드 시간(초). 0이면 즉시
///
/// [버튼 OnClick 연결]
/// Btn_Ready           → OnClickReady()
/// Btn_Start           → OnClickStart()
/// Btn_Quit            → OnClickQuit()
/// Btn_Copy            → OnClickCopy()
/// Btn_SteamInvite     → OnClickSteamInvite()
/// Kick                → OnClickKick()
/// Dropdown OnValueChanged → OnCharacterChanged(Int32)
/// </summary>
public class LobbyMenuController : MonoBehaviour
{
    [Header("씬 전환")]
    [Tooltip("Start 버튼으로 로드할 씬 이름. Build Settings 이름과 정확히 일치.")]
    [SerializeField] private string stageSceneName = "M.Stage1";

    [Tooltip("Quit 버튼으로 복귀할 씬 이름.")]
    [SerializeField] private string titleSceneName = "0.Title";

    [Header("룸코드")]
    [Tooltip("Btn_Copy 클릭 시 클립보드에 복사할 더미 룸코드.")]
    [SerializeField] private string dummyRoomCode = "DEMO-0000";

    [Header("캐릭터 초상화")]
    [Tooltip("드롭다운 인덱스 순: [0]BERRY  [1]GUMA  [2]SSUK  [3]DANHO\n" +
             "Figma/Lobby/PlayerB.png · PlayerP.png · PlayerG.png · PlayerY.png 할당")]
    [SerializeField] private Sprite[] characterPortraits = new Sprite[4];

    [Tooltip("Slot0/CharacterArea/Image 연결")]
    [SerializeField] private Image portraitImage;

    [Tooltip("Slot0 내 TMP_Dropdown 연결")]
    [SerializeField] private TMP_Dropdown characterDropdown;

    [Header("Ready 상태")]
    [Tooltip("Ready 아이콘 Image 컴포넌트 (Slot0 내 Check 오브젝트)")]
    [SerializeField] private Image checkImage;

    [Tooltip("Ready 상태 스프라이트 — Check.png")]
    [SerializeField] private Sprite readySprite;

    [Tooltip("NotReady 상태 스프라이트 — NotReady.png")]
    [SerializeField] private Sprite notReadySprite;

    [Tooltip("모두 Ready 시 숨길 대기 문구 오브젝트 — Txt_Waiting")]
    [SerializeField] private GameObject waitingTextObject;

    [Header("페이드 (선택)")]
    [Tooltip("씬 전환 전 페이드아웃. 비워두면 즉시 전환.")]
    [SerializeField] private ScreenFader screenFader;

    [Tooltip("페이드아웃 시간(초). 0이면 즉시.")]
    [SerializeField] private float fadeOutDuration = 0f;

    private bool _isReady;

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
        RefreshReadyVisual();

        int initialIndex = characterDropdown != null ? characterDropdown.value : 0;
        RefreshPortrait(initialIndex);
    }

    // ── 버튼 콜백 ─────────────────────────────────────────────────

    /// <summary>Btn_Ready — 로컬 Ready 토글. A4에서 네트워크 동기화 추가.</summary>
    public void OnClickReady()
    {
        _isReady = !_isReady;
        RefreshReadyVisual();
    }

    /// <summary>Btn_Start — M.Stage1 로드.</summary>
    public void OnClickStart()
    {
        StartCoroutine(LoadSceneWithFade(stageSceneName));
    }

    /// <summary>Btn_Quit — 타이틀 복귀.</summary>
    public void OnClickQuit()
    {
        StartCoroutine(LoadSceneWithFade(titleSceneName));
    }

    /// <summary>Btn_Copy — 더미 룸코드를 클립보드에 복사.</summary>
    public void OnClickCopy()
    {
        GUIUtility.systemCopyBuffer = dummyRoomCode;
        Debug.Log($"[LobbyMenuController] 룸코드 복사됨: {dummyRoomCode}");
    }

    /// <summary>Btn_SteamInvite — A4 전 스텁.</summary>
    public void OnClickSteamInvite()
    {
        Debug.Log("[LobbyMenuController] Steam 초대는 멀티플레이어 버전(A4)에서 지원합니다.");
    }

    /// <summary>Kick 버튼 — A4 전 스텁.</summary>
    public void OnClickKick()
    {
        Debug.Log("[LobbyMenuController] Kick은 멀티플레이어 버전(A4)에서 지원합니다.");
    }

    /// <summary>Dropdown OnValueChanged — 캐릭터 초상화 교체.</summary>
    public void OnCharacterChanged(int index)
    {
        RefreshPortrait(index);
    }

    // ── 내부 ──────────────────────────────────────────────────────

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
    [ContextMenu("테스트: Ready 토글")]
    void Debug_Ready() => OnClickReady();

    [ContextMenu("테스트: 룸코드 복사")]
    void Debug_Copy() => OnClickCopy();

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
