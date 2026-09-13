using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using TMPro;

/// <summary>
/// Dialogue_Panel에 붙이는 순수 표시 전용 스크립트.
/// 줄 표시·숨김·열기·닫기만 담당.
///
/// [진행 방식 — 각자 로컬 진행]
/// 각 피어(Host/Client)가 자기 화면의 **마우스 왼쪽 클릭**으로 자기 줄만 넘긴다(handleInputLocally=true).
/// [2026-09-14] Space → 좌클릭 변경 — Space는 개인 버프·SequenceRing 전용(CheerSystemDesign.md §6.1).
/// 대화창이 떠 있는 동안 좌클릭은 넘기기 전용이라 PlayerPunch가 펀치를 내지 않는다(BlocksPrimaryClick).
/// 줄 넘김 자체는 네트워크 동기화하지 않는다 — 읽는 속도는 순수 로컬 UI 상태.
/// "전원이 다 읽었는지"는 PhaseDialogueGate가 완료 이벤트(OnSequenceComplete)를
/// 모아서 판단한다 (OnAllReady 발동 타이밍 결정용).
///
/// [설정법]
/// 1. Dialogue_Panel에 이 스크립트 부착
/// 2. 자식으로 Text(TMP) 오브젝트를 원하는 줄 수만큼 추가
/// 3. 각 Text(TMP)에 문구 입력 (Rich Text 태그 사용 가능)
///    예) 각자 <color=#3B82F6><b>색</b></color> 존에 서세요.
/// 4. dialogueLines 배열에 순서대로 연결
/// 5. handleInputLocally = true
/// 6. PhaseDialogueGate.dialogueUI 에 이 컴포넌트 연결
/// </summary>
public class DialogueUI : MonoBehaviour
{
    // 대화창은 Host RPC가 정한 임의 시점에 열린다. 펀치를 연타하던 클릭이 첫 줄을 바로 넘기지 않게
    // 열린 직후 잠깐은 넘기기 입력을 받지 않는다(이 동안에도 BlocksPrimaryClick이라 펀치는 안 나감).
    const float OpenInputGraceSeconds = 0.25f;

    [Header("배경")]
    [Tooltip("비우면 단색으로만 표시")]
    [SerializeField] Sprite bgSprite;
    [SerializeField] Color  bgColor = new Color(0f, 0f, 0f, 0.6f);

    [Header("스킵 안내")]
    [FormerlySerializedAs("hostOnlyHint")]
    [Tooltip("좌클릭 스킵 안내 이미지. 전원이 각자 좌클릭으로 넘기므로 모든 플레이어에게 표시됨.")]
    [SerializeField] GameObject skipHint;

    [Header("대화 내용")]
    [Tooltip("순서대로 표시할 Text(TMP) 오브젝트 목록")]
    [SerializeField] TextMeshProUGUI[] dialogueLines;

    [Header("입력")]
    [Tooltip("true: 이 컴포넌트가 직접 좌클릭 입력을 처리해 자기 화면의 줄만 넘김.\n" +
             "PhaseDialogueGate와 함께 쓸 때도 true로 설정 (각자 로컬 진행 방식).")]
    [SerializeField] bool handleInputLocally = false;

    [Header("이벤트")]
    [Tooltip("마지막 줄까지 모두 완료됐을 때 발동")]
    public UnityEvent OnSequenceComplete;

    // ── 런타임 상태 ──────────────────────────────────────────────

    Image _bgImage;
    int   _lineIndex;
    bool  _isPlaying;
    bool  _countedOpen;
    float _inputEnabledAt;

    // 동시에 열린(좌클릭을 소비하는) 대화창 수. bool 하나로 두면 두 창이 겹칠 때 하나가 닫히는 순간
    // 다른 창이 열려 있어도 false가 된다.
    static int s_openCount;
    static int s_closedFrame = -1;

    // ── 프로퍼티 (외부 참조용) ────────────────────────────────────

    public int  LineCount        => dialogueLines?.Length ?? 0;
    public int  CurrentLineIndex => _lineIndex;
    public bool IsPlaying        => _isPlaying;

    /// <summary>좌클릭을 넘기기로 소비하는 대화창이 하나라도 열려 있는지.</summary>
    public static bool IsOpen => s_openCount > 0;

    /// <summary>
    /// 지금 좌클릭을 펀치로 쓰면 안 되는지 — PlayerPunch.OnAttack이 확인.
    /// 열린 동안 + 방금 닫힌 프레임(마지막 줄을 넘긴 그 클릭이 실행 순서에 따라 같은 프레임에
    /// 펀치로도 새는 것 방지, Esc의 ConsumedEscThisFrame과 같은 패턴).
    /// </summary>
    public static bool BlocksPrimaryClick => IsOpen || s_closedFrame == Time.frameCount;

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

        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

        // 채팅 입력·치어네임·ESC 메뉴 버튼을 누르는 클릭이 대사를 넘기지 않게 양보.
        if (InGameChatUI.IsChatOpen || TutorialCheerNameUI.IsOpen || CursorUnlockRequestUtil.IsRequested) return;
        if (Time.unscaledTime < _inputEnabledAt) return;

        NextLine();
    }

    /// <summary>씬 파괴(스테이지 전환 등)로 Hide() 없이 사라져도 열림 카운트가 남아
    /// 다음 씬에서 펀치가 영구히 막히지 않도록 하는 안전장치.</summary>
    void OnDestroy()
    {
        if (!_countedOpen) return;
        _countedOpen = false;
        s_openCount = Mathf.Max(0, s_openCount - 1);
    }

    // ── 외부 호출 (public API) ────────────────────────────────────

    /// <summary>0번 줄부터 순서대로 시작.</summary>
    public void StartSequence()
    {
        if (dialogueLines == null || dialogueLines.Length == 0) return;
        _lineIndex = 0;
        _isPlaying = true;
        _inputEnabledAt = Time.unscaledTime + OpenInputGraceSeconds;
        SetOpen(true);
        HideAllLines();
        gameObject.SetActive(true);
        ApplySkipHint();
        ShowCurrentLine();
    }

    /// <summary>
    /// 다음 줄로 넘어감. handleInputLocally=true 일 때 각 피어의 좌클릭 입력으로 호출됨.
    /// 마지막 줄 이후엔 Hide() + OnSequenceComplete 발동 (이 피어만의 완료 — 다른 피어와 무관).
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
        SetOpen(false);
        HideAllLines();
        if (skipHint != null) skipHint.SetActive(false);
        gameObject.SetActive(false);
    }

    // ── 내부 ──────────────────────────────────────────────────────

    /// <summary>좌클릭을 소비하는 창(handleInputLocally)만 열림 카운트에 넣는다. 같은 인스턴스는 1회만 센다.</summary>
    void SetOpen(bool open)
    {
        if (!handleInputLocally || _countedOpen == open) return;
        _countedOpen = open;
        if (open)
        {
            s_openCount++;
        }
        else
        {
            s_openCount = Mathf.Max(0, s_openCount - 1);
            s_closedFrame = Time.frameCount;
        }
    }

    /// <summary>스킵 안내 표시. 전원이 각자 좌클릭으로 넘기므로 항상 켬.</summary>
    void ApplySkipHint()
    {
        if (skipHint != null) skipHint.SetActive(true);
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
