using System.Collections;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비 슬롯 1개의 UI를 완전히 담당.
/// Slot0~Slot3 각 GameObject에 부착.
/// LobbyMenuController.RefreshAllSlots()에서 Refresh / SetEmpty 호출.
///
/// [Inspector 연결 — 전부 연결할 것]
/// - slotContentRoot      : 슬롯 내 모든 UI를 묶은 부모 (Empty 시 통째로 숨김)
/// - portrait             : 캐릭터 초상화 Image
/// - nameText             : 캐릭터 이름 TMP_Text — 타인 슬롯 표시용 (로컬은 입력창으로 대체)
/// - cheerNameInput       : TMP_InputField — 로컬 슬롯만 표시. Enter 시 SetCheerNameServerRpc
/// - cheerNameErrorRoot   : 이름 거절 시 표시할 오류 인디케이터 (3초 후 자동 숨김)
/// - characterDropdown    : 캐릭터 선택 TMP_Dropdown (로컬 플레이어 슬롯에서만 표시)
/// - statusText           : READY / WAITING TMP_Text
/// - readyIndicator       : 체크 아이콘 GameObject
/// - hostIndicator        : 별표/왕관 아이콘 (Host 슬롯에서만 표시)
/// - kickButtonRoot       : Kick 버튼 부모 (Host만, 타인 슬롯에서만 표시)
///
/// [Heard 말풍선 인디케이터]
/// - heardIndicatorRoot   : 말풍선 부모 (평소 hidden)
/// - heardIconsContainer  : 떡 아이콘 배열 부모 (HorizontalLayoutGroup 권장)
/// - heardDotSprite       : 원형/떡 스프라이트 (null 이면 흰 사각형 대체)
/// - playerColors         : ColorIndex 별 색상 (0=Blue 1=Purple 2=Green 3=Yellow)
/// LobbyMenuController.OnLobbyCheerDetected → ShowHeardBy(speakerColorIndex) 호출.
///
/// [버튼 OnClick 연결]
/// Kick 버튼 → OnClickKick()
/// </summary>
public class LobbySlotUI : MonoBehaviour
{
    [Tooltip("빈 슬롯 전용 비주얼 (발판 이미지 등). 플레이어 없을 때만 표시됨.")]
    [SerializeField] private GameObject     emptyVisualRoot;

    [Tooltip("점유 슬롯 전용 비주얼 (플레이어 + 이름 + 드롭다운 등). 플레이어 있을 때만 표시됨.")]
    [SerializeField] private GameObject     slotContentRoot;

    [SerializeField] private Image          portrait;

    [Tooltip("타인 슬롯 이름 TMP_Text. 로컬 슬롯은 cheerNameInput으로 대체됨.")]
    [SerializeField] private TMP_Text       nameText;

    [Header("CheerName 편집 (로컬 슬롯 전용)")]
    [Tooltip("CheerName 입력창. 로컬 슬롯에서만 표시. placeholder는 인스펙터에서 설정. text = 커스텀값(빈칸=기본값).")]
    [SerializeField] private TMP_InputField cheerNameInput;

    [Tooltip("이름 거절 시 표시. 3초 후 자동 숨김.")]
    [SerializeField] private GameObject     cheerNameErrorRoot;

    [Tooltip("캐릭터 선택 드롭다운. 로컬 플레이어 슬롯에서만 표시됨.")]
    [SerializeField] private TMP_Dropdown   characterDropdown;

    [Tooltip("READY / WAITING 상태 TMP_Text")]
    [SerializeField] private TMP_Text       statusText;

    [Tooltip("체크 아이콘 (Ready=활성, Waiting=비활성)")]
    [SerializeField] private GameObject     readyIndicator;

    [Tooltip("별표/왕관 아이콘 (Host 슬롯에서만 표시)")]
    [SerializeField] private GameObject     hostIndicator;

    [SerializeField] private GameObject     kickButtonRoot;

    // ── Heard 말풍선 인디케이터 ──────────────────────────────────

    [Header("Heard 말풍선 인디케이터")]
    [Tooltip("말풍선 루트 오브젝트. Vosk로 이 슬롯의 이름이 감지되면 표시 → 3초 후 자동 숨김.")]
    [SerializeField] private GameObject heardIndicatorRoot;

    [Tooltip("떡 아이콘들을 담는 컨테이너. HorizontalLayoutGroup 권장.")]
    [SerializeField] private Transform  heardIconsContainer;

    [Tooltip("색별 떡 스프라이트 (null 이면 흰 사각형). ColorIndex 순: 0=Blue 1=Purple 2=Green 3=Yellow.")]
    [SerializeField] private Sprite     heardDotSprite;

    [Tooltip("떡 아이콘 크기 (px).")]
    [SerializeField] private float      heardDotSize = 24f;

    [Tooltip("ColorIndex 별 색상 (0=Blue 1=Purple 2=Green 3=Yellow).")]
    [SerializeField] private Color[] playerColors = new Color[]
    {
        new Color(0.25f, 0.55f, 1f,   1f),  // 0 Blue
        new Color(0.65f, 0.30f, 1f,   1f),  // 1 Purple
        new Color(0.25f, 0.80f, 0.35f,1f),  // 2 Green
        new Color(1f,    0.85f, 0.15f,1f),  // 3 Yellow
    };

    // ── 런타임 상태 ──────────────────────────────────────────────

    private ulong      _assignedClientId   = ulong.MaxValue;
    private bool       _dropdownListening  = false;
    private bool       _inputListening     = false;
    private string     _confirmedCheerName = "";
    private Coroutine  _errorHideCoroutine = null;
    private int        _pendingColorIndex  = -1;  // 서버 확인 전 클라이언트 선택값 (-1=없음)

    // 말풍선 — ColorIndex 4슬롯 미리 생성
    private Image[]    _heardDots;
    private Coroutine[] _dotTimers = new Coroutine[4];

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        BuildHeardDots();
        if (heardIndicatorRoot != null) heardIndicatorRoot.SetActive(false);
    }

    void BuildHeardDots()
    {
        if (heardIconsContainer == null) return;
        _heardDots = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            var go  = new GameObject($"HeardDot{i}");
            go.transform.SetParent(heardIconsContainer, false);
            var img = go.AddComponent<Image>();
            img.sprite           = heardDotSprite;
            img.preserveAspect   = true;
            img.color            = i < playerColors.Length ? playerColors[i] : Color.white;
            var rt               = go.GetComponent<RectTransform>();
            rt.sizeDelta         = new Vector2(heardDotSize, heardDotSize);
            go.SetActive(false);
            _heardDots[i]        = img;
        }
    }

    // ── 공개 API ──────────────────────────────────────────────────

    /// <summary>슬롯 내용을 갱신.</summary>
    public void Refresh(LobbyPlayerState state, Sprite portraitSprite,
                        bool canKick, bool isHostSlot = false, bool isLocalSlot = false)
    {
        _assignedClientId = state.ClientId;

        if (emptyVisualRoot != null) emptyVisualRoot.SetActive(false);
        if (slotContentRoot != null) slotContentRoot.SetActive(true);

        // 초상화
        if (portrait != null)
        {
            portrait.gameObject.SetActive(true);
            if (portraitSprite != null) portrait.sprite = portraitSprite;
        }

        // 이름 표시: 로컬=입력창, 타인=텍스트
        string effectiveName = LobbyNetworkManager.GetEffectiveCheerName(state);
        bool   useInput      = isLocalSlot && cheerNameInput != null;

        if (nameText != null)
        {
            nameText.gameObject.SetActive(!useInput);
            if (!useInput) nameText.text = effectiveName.ToUpper();
        }

        if (cheerNameInput != null)
        {
            cheerNameInput.gameObject.SetActive(useInput);
            if (useInput)
            {
                string custom = state.CheerName.ToString();
                SetInputSilent(custom);

                cheerNameInput.interactable = !state.IsReady;
                SubscribeInput();
            }
            else
            {
                UnsubscribeInput();
            }
        }

        // 드롭다운: 로컬만
        if (characterDropdown != null)
        {
            characterDropdown.gameObject.SetActive(isLocalSlot);
            if (isLocalSlot)
            {
                SetDropdownSilent(state.ColorIndex);
                characterDropdown.interactable = !state.IsReady;
                SubscribeDropdown();
            }
            else              { UnsubscribeDropdown(); }
        }

        if (statusText     != null) statusText.text = state.IsReady ? "READY" : "WAITING";
        if (readyIndicator != null) readyIndicator.SetActive(state.IsReady);
        if (hostIndicator  != null) hostIndicator.SetActive(isHostSlot);
        if (kickButtonRoot != null) kickButtonRoot.SetActive(canKick);
    }

    /// <summary>빈 슬롯으로 표시 — 발판(emptyVisualRoot)만 보이고 플레이어 UI는 숨김.</summary>
    public void SetEmpty()
    {
        _assignedClientId = ulong.MaxValue;
        UnsubscribeDropdown();
        UnsubscribeInput();

        HideAllHeardDots();

        if (emptyVisualRoot != null) emptyVisualRoot.SetActive(true);

        if (slotContentRoot != null)
        {
            slotContentRoot.SetActive(false);
            return;
        }

        // slotContentRoot 미연결 시 개별 처리
        if (portrait           != null) portrait.gameObject.SetActive(false);
        if (nameText           != null) nameText.text = "";
        if (cheerNameInput     != null) cheerNameInput.gameObject.SetActive(false);
        if (cheerNameErrorRoot != null) cheerNameErrorRoot.SetActive(false);
        if (characterDropdown  != null) characterDropdown.gameObject.SetActive(false);
        if (statusText         != null) statusText.text = "";
        if (readyIndicator     != null) readyIndicator.SetActive(false);
        if (hostIndicator      != null) hostIndicator.SetActive(false);
        if (kickButtonRoot     != null) kickButtonRoot.SetActive(false);
    }

    /// <summary>
    /// SetCheerNameServerRpc 결과 수신. LobbyMenuController 에서 로컬 슬롯에만 호출.
    /// </summary>
    public void ShowCheerNameResult(bool success, string errorKey)
    {
        if (cheerNameErrorRoot == null) return;

        if (success)
        {
            cheerNameErrorRoot.SetActive(false);
            if (_errorHideCoroutine != null) { StopCoroutine(_errorHideCoroutine); _errorHideCoroutine = null; }
        }
        else
        {
            cheerNameErrorRoot.SetActive(true);
            if (_errorHideCoroutine != null) StopCoroutine(_errorHideCoroutine);
            _errorHideCoroutine = StartCoroutine(HideErrorAfterDelay(3f));
            Debug.Log($"[LobbySlotUI] CheerName 거절: {errorKey}");
        }
    }

    /// <summary>
    /// speakerColorIndex 플레이어가 이 슬롯의 이름을 불렀을 때 호출.
    /// 말풍선에 해당 색 떡이 3초간 표시된다.
    /// LobbyMenuController.OnLobbyCheerDetected 에서 타겟 슬롯에 호출.
    /// </summary>
    public void ShowHeardBy(int speakerColorIndex)
    {
        if (heardIndicatorRoot == null || _heardDots == null) return;
        if (speakerColorIndex < 0 || speakerColorIndex >= _heardDots.Length) return;

        heardIndicatorRoot.SetActive(true);
        _heardDots[speakerColorIndex].gameObject.SetActive(true);

        if (_dotTimers[speakerColorIndex] != null)
            StopCoroutine(_dotTimers[speakerColorIndex]);
        _dotTimers[speakerColorIndex] = StartCoroutine(HideHeardDotAfterDelay(speakerColorIndex, 2f));
    }

    // ── 버튼 OnClick ──────────────────────────────────────────────

    /// <summary>Kick 버튼 OnClick.</summary>
    public void OnClickKick()
    {
        if (_assignedClientId == ulong.MaxValue) return;
        LobbyNetworkManager.Instance?.KickPlayerServerRpc(_assignedClientId);
    }

    // ── CheerName 입력 ────────────────────────────────────────────

    void SubscribeInput()
    {
        if (_inputListening || cheerNameInput == null) return;
        cheerNameInput.onEndEdit.AddListener(OnInputEndEdit);
        _inputListening = true;
    }

    void UnsubscribeInput()
    {
        if (!_inputListening || cheerNameInput == null) return;
        cheerNameInput.onEndEdit.RemoveListener(OnInputEndEdit);
        _inputListening = false;
    }

    void SetInputSilent(string value)
    {
        if (cheerNameInput == null) return;
        cheerNameInput.onEndEdit.RemoveListener(OnInputEndEdit);
        cheerNameInput.text = value;
        _confirmedCheerName = value;
        if (_inputListening)
            cheerNameInput.onEndEdit.AddListener(OnInputEndEdit);
    }

    void OnInputEndEdit(string value)
    {
        string trimLower = value.Trim().ToLower();
        if (trimLower == _confirmedCheerName) return;
        LobbyNetworkManager.Instance?.SetCheerNameServerRpc(new FixedString32Bytes(trimLower));
    }

    IEnumerator HideErrorAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (cheerNameErrorRoot != null) cheerNameErrorRoot.SetActive(false);
        _errorHideCoroutine = null;
    }

    // ── 말풍선 인디케이터 ─────────────────────────────────────────

    IEnumerator HideHeardDotAfterDelay(int colorIndex, float delay)
    {
        yield return new WaitForSeconds(delay);
        _dotTimers[colorIndex] = null;

        if (_heardDots != null && colorIndex < _heardDots.Length)
            _heardDots[colorIndex].gameObject.SetActive(false);

        // 남은 떡 없으면 말풍선 전체 숨김
        if (heardIndicatorRoot == null || _heardDots == null) yield break;
        bool anyActive = false;
        foreach (var d in _heardDots)
            if (d != null && d.gameObject.activeSelf) { anyActive = true; break; }
        if (!anyActive) heardIndicatorRoot.SetActive(false);
    }

    void HideAllHeardDots()
    {
        if (_heardDots == null) return;
        for (int i = 0; i < _heardDots.Length; i++)
        {
            if (_dotTimers[i] != null) { StopCoroutine(_dotTimers[i]); _dotTimers[i] = null; }
            if (_heardDots[i] != null) _heardDots[i].gameObject.SetActive(false);
        }
        if (heardIndicatorRoot != null) heardIndicatorRoot.SetActive(false);
    }

    // ── 드롭다운 이벤트 ───────────────────────────────────────────

    void SubscribeDropdown()
    {
        if (_dropdownListening || characterDropdown == null) return;
        characterDropdown.onValueChanged.AddListener(OnDropdownChanged);
        _dropdownListening = true;
    }

    void UnsubscribeDropdown()
    {
        if (!_dropdownListening || characterDropdown == null) return;
        characterDropdown.onValueChanged.RemoveListener(OnDropdownChanged);
        _dropdownListening = false;
        _pendingColorIndex = -1;
    }

    void OnDropdownChanged(int index)
    {
        _pendingColorIndex = index;
        LobbyNetworkManager.Instance?.SetColorServerRpc(index);
    }

    void SetDropdownSilent(int value)
    {
        if (characterDropdown == null) return;

        if (_pendingColorIndex >= 0)
        {
            if (value == _pendingColorIndex)
                _pendingColorIndex = -1;   // 서버가 확인 → pending 해제 후 정상 진행
            else
                return;                    // 서버 응답 전 스냅백 차단
        }

        characterDropdown.onValueChanged.RemoveListener(OnDropdownChanged);
        characterDropdown.value = value;
        if (_dropdownListening)
            characterDropdown.onValueChanged.AddListener(OnDropdownChanged);
    }

    void OnDestroy()
    {
        UnsubscribeDropdown();
        UnsubscribeInput();
    }
}
