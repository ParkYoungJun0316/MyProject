using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Dialogue_Panel에 붙이는 스크립트.
/// 자식 Text(TMP) 오브젝트를 순서대로 하나씩 표시.
/// 스페이스 키로 다음 줄로 넘어가고, 마지막 줄 이후 자동으로 닫힘.
///
/// [설정법]
/// 1. Dialogue_Panel에 이 스크립트 부착
/// 2. Dialogue_Panel 자식으로 Text(TMP) 오브젝트를 원하는 줄 수만큼 추가
/// 3. 각 Text(TMP)에 문구 입력 (Rich Text 태그 사용 가능)
///    예) 빨리 <color=red><b>피해!</b></color>
/// 4. dialogueLines 배열에 순서대로 연결
/// </summary>
public class DialogueUI : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("이 UI의 주인 플레이어. 비우면 씬 첫 번째 Player로 자동 탐색.")]
    [SerializeField] Player player;

    [Header("배경")]
    [Tooltip("비우면 단색으로만 표시")]
    [SerializeField] Sprite bgSprite;
    [SerializeField] Color  bgColor = new Color(0f, 0f, 0f, 0.6f);

    [Header("대화 내용")]
    [Tooltip("순서대로 표시할 Text(TMP) 오브젝트 목록.\n자식으로 Text(TMP)를 추가하고 여기에 연결.")]
    [SerializeField] TextMeshProUGUI[] dialogueLines;

    Image _bgImage;
    int   _lineIndex;
    bool  _isPlaying;

    void Awake()
    {
        if (player == null)
            player = FindFirstObjectByType<Player>();

        SetupBackground();
        HideAllLines();

        if (dialogueLines != null && dialogueLines.Length > 0)
            StartSequence();
        else
            gameObject.SetActive(false);
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

    void Update()
    {
        if (!_isPlaying) return;
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            NextLine();
    }

    // ── 외부 호출 ─────────────────────────────────────────────────

    /// <summary>Inspector에 연결된 dialogueLines 시퀀스 시작.</summary>
    public void StartSequence()
    {
        if (dialogueLines == null || dialogueLines.Length == 0) return;
        _lineIndex = 0;
        _isPlaying = true;
        HideAllLines();
        gameObject.SetActive(true);
        ShowCurrentLine();
    }

    /// <summary>대화창 강제 숨김.</summary>
    public void Hide()
    {
        _isPlaying = false;
        HideAllLines();
        gameObject.SetActive(false);
    }

    // ── 내부 ──────────────────────────────────────────────────────

    void ShowCurrentLine()
    {
        if (dialogueLines[_lineIndex] != null)
            dialogueLines[_lineIndex].gameObject.SetActive(true);
    }

    void NextLine()
    {
        if (dialogueLines[_lineIndex] != null)
            dialogueLines[_lineIndex].gameObject.SetActive(false);

        _lineIndex++;
        if (_lineIndex >= dialogueLines.Length)
        {
            Hide();
            return;
        }
        ShowCurrentLine();
    }
}
