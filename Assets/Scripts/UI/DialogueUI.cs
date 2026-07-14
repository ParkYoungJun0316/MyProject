using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using Unity.Netcode;

/// <summary>
/// Dialogue_Panel에 붙이는 순수 표시 전용 스크립트.
/// 줄 표시·숨김·열기·닫기만 담당. 입력과 네트워크 제어는 DialogueGateController가 전담.
///
/// [설정법]
/// 1. Dialogue_Panel에 이 스크립트 부착
/// 2. 자식으로 Text(TMP) 오브젝트를 원하는 줄 수만큼 추가
/// 3. 각 Text(TMP)에 문구 입력 (Rich Text 태그 사용 가능)
///    예) 각자 <color=#3B82F6><b>색</b></color> 존에 서세요.
/// 4. dialogueLines 배열에 순서대로 연결
/// 5. DialogueGateController.dialogueUI 에 이 컴포넌트 연결
/// </summary>
public class DialogueUI : MonoBehaviour
{
    [Header("배경")]
    [Tooltip("비우면 단색으로만 표시")]
    [SerializeField] Sprite bgSprite;
    [SerializeField] Color  bgColor = new Color(0f, 0f, 0f, 0.6f);

    [Header("Host 전용")]
    [Tooltip("Host에게만 보이는 오브젝트 (예: Space 스킵 안내 이미지).\n" +
             "Client 또는 오프라인 솔로 플레이어에게는 자동으로 숨겨짐.")]
    [SerializeField] GameObject hostOnlyHint;

    [Header("대화 내용")]
    [Tooltip("순서대로 표시할 Text(TMP) 오브젝트 목록")]
    [SerializeField] TextMeshProUGUI[] dialogueLines;

    [Header("입력 (단독 사용 시)")]
    [Tooltip("true: 이 컴포넌트가 직접 Space 입력 처리.\n" +
             "DialogueGateController 없이 단독 사용할 때만 체크.\n" +
             "DialogueGateController와 함께 쓸 때는 반드시 false.")]
    [SerializeField] bool handleInputLocally = false;

    [Header("이벤트")]
    [Tooltip("마지막 줄까지 모두 완료됐을 때 발동")]
    public UnityEvent OnSequenceComplete;

    // ── 런타임 상태 ──────────────────────────────────────────────

    Image _bgImage;
    int   _lineIndex;
    bool  _isPlaying;

    // ── 프로퍼티 (외부 참조용) ────────────────────────────────────

    public int  LineCount        => dialogueLines?.Length ?? 0;
    public int  CurrentLineIndex => _lineIndex;
    public bool IsPlaying        => _isPlaying;

    // ── 라이프사이클 ──────────────────────────────────────────────

    void Awake()
    {
        SetupBackground();
        HideAllLines();
        // 비활성 초기 상태는 프리팹/씬에서 Dialogue_Panel을 inactive로 설정해 관리.
        // 여기서 SetActive(false)를 호출하면, 처음 비활성 상태에서 열릴 때
        // StartSequence → SetActive(true) → Awake → SetActive(false) 루프가 발생하므로 제거.
    }

    void Update()
    {
        if (!_isPlaying || !handleInputLocally) return;
        if (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
            NextLine();
    }

    // ── 외부 호출 (public API) ────────────────────────────────────

    /// <summary>0번 줄부터 순서대로 시작. 오프라인/로컬 단독 사용.</summary>
    public void StartSequence()
    {
        if (dialogueLines == null || dialogueLines.Length == 0) return;
        _lineIndex = 0;
        _isPlaying = true;
        HideAllLines();
        gameObject.SetActive(true);
        ApplyHostHint();
        ShowCurrentLine();
    }

    /// <summary>
    /// 특정 줄을 직접 표시. DialogueGateController가 Host 줄 번호를 Client에 동기화할 때 사용.
    /// </summary>
    public void ShowLine(int index)
    {
        if (dialogueLines == null || index < 0 || index >= dialogueLines.Length) return;
        HideAllLines();
        _lineIndex = index;
        _isPlaying = true;
        gameObject.SetActive(true);
        ApplyHostHint();
        dialogueLines[index].gameObject.SetActive(true);
    }

    /// <summary>
    /// 다음 줄로 넘어감. DialogueGateController가 Host Space 입력 시 호출.
    /// 마지막 줄 이후엔 Hide() + OnSequenceComplete 발동.
    /// </summary>
    public void NextLine()
    {
        if (!_isPlaying) return;

        if (dialogueLines[_lineIndex] != null)
            dialogueLines[_lineIndex].gameObject.SetActive(false);

        _lineIndex++;
        if (_lineIndex >= dialogueLines.Length)
        {
            Hide();
            OnSequenceComplete?.Invoke();
            return;
        }
        ShowCurrentLine();
    }

    /// <summary>대화창 강제 숨김.</summary>
    public void Hide()
    {
        _isPlaying = false;
        HideAllLines();
        if (hostOnlyHint != null) hostOnlyHint.SetActive(false);
        gameObject.SetActive(false);
    }

    // ── 내부 ──────────────────────────────────────────────────────

    /// <summary>hostOnlyHint를 Host일 때만 활성화.</summary>
    void ApplyHostHint()
    {
        if (hostOnlyHint == null) return;
        var nm = NetworkManager.Singleton;
        bool isHost = nm != null && nm.IsHost;
        hostOnlyHint.SetActive(isHost);
    }

    void SetupBackground()
    {
        _bgImage        = GetComponent<Image>();
        if (_bgImage == null) _bgImage = gameObject.AddComponent<Image>();
        _bgImage.sprite = bgSprite;
        _bgImage.color  = bgColor;
        if (bgSprite != null) _bgImage.type = Image.Type.Sliced;
    }

    void HideAllLines()
    {
        if (dialogueLines == null) return;
        foreach (var line in dialogueLines)
            if (line != null) line.gameObject.SetActive(false);
    }

    void ShowCurrentLine()
    {
        if (dialogueLines[_lineIndex] != null)
            dialogueLines[_lineIndex].gameObject.SetActive(true);
    }
}
