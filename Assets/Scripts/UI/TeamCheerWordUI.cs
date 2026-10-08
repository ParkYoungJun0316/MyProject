using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 인게임 HUD의 팀 구호 표시 — CheerSystemDesign.md §14.7 (2026-10-07, 구 "대문자 단어" 표시 교체).
/// 구호가 소리라 보여줄 글자가 없다 → "[R] 팀 구호 듣기" 라벨 + R키로 로컬 재생(창 안팎 언제든).
/// 10/7: 등록했으면 **내 1번 녹음**(판정이 내 녹음 기준이므로), 등록 못 한 사람(T키 응원)은 Host 소리.
/// 자동 재생은 없다(사용자 결정). 클래스 이름은 UI.prefab 연결 호환용으로 유지.
///
/// [배치] UI.prefab 루트(HP_Panel · Txt.Nickname 형제), 좌측 하단 마이크 아이콘 바로 위(2026-10-08 이동).
/// R키 아이콘(Img.RKeyIcon) + "팀 구호 듣기" 라벨을 Visual 묶음에 넣고 visualRoot로 연결 — 소리 없으면 통째로 숨김.
/// 라벨 문구는 LocalizeStringEvent(Tutorial.HUD.TeamCheerListen),
/// 이 스크립트는 R키·버튼 재생과 "기준 소리 없음" 숨김만 한다. 미연결이면 이 HUD만 없음 — 판정은 그대로.
/// </summary>
public class TeamCheerWordUI : MonoBehaviour
{
    [Header("표시")]
    [Tooltip("위 줄 캡션(선택). raycast만 끈다.")]
    [SerializeField] TextMeshProUGUI captionLabel;
    [Tooltip("\"[R] 팀 구호 듣기\" 라벨. 문구는 프리팹/로컬라이즈에서.")]
    [SerializeField] TMP_Text wordLabel;
    [Tooltip("마우스로도 재생(선택).")]
    [SerializeField] Button playButton;
    [Tooltip("기준 소리가 없을 때 통째로 숨길 루트(비우면 이 GameObject).")]
    [SerializeField] GameObject visualRoot;

    void Awake()
    {
        if (captionLabel != null) captionLabel.raycastTarget = false;
        if (wordLabel is TextMeshProUGUI wordUgui) wordUgui.raycastTarget = false;
        if (playButton != null) playButton.onClick.AddListener(CheerSoundPlayback.PlayListenClip);
    }

    void OnEnable()
    {
        CheerSoundLocalState.HostSoundChanged += Refresh;
        Refresh();
    }

    void OnDisable() => CheerSoundLocalState.HostSoundChanged -= Refresh;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.rKey.wasPressedThisFrame) return;
        if (InGameChatUI.IsChatOpen || TutorialCheerNameUI.IsOpen || CostumePanelUI.IsOpen) return;
        if (CursorUnlockRequestUtil.IsRequested) return; // ESC 메뉴 등 커서를 쓰는 창이 떠 있는 동안
        CheerSoundPlayback.PlayListenClip();
    }

    void Refresh()
    {
        var root = visualRoot != null ? visualRoot : gameObject;
        bool has = CheerSoundLocalState.HasHostSound;
        if (root == gameObject)
        {
            // 자기 자신을 끄면 Update(R키)도 멈추므로 라벨만 숨긴다
            if (wordLabel != null) wordLabel.enabled = has;
            if (captionLabel != null) captionLabel.enabled = has;
            if (playButton != null) playButton.gameObject.SetActive(has);
        }
        else if (root.activeSelf != has) root.SetActive(has);
    }
}
