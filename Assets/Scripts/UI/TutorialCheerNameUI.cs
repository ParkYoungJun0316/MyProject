using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tutorial CheerName 입력·확정 UI — NetworkDesign.md §6B.7 P6 / CheerAndTutorialDesign.md §3.4·§8.3.
/// 네트워크 코어(PlayerCheerNameSync)는 이미 있음 — 이 스크립트는 그걸 붙잡는 로컬 입력 UI만 담당.
///
/// [배치] Tutorial 상시 HUD의 Canvas 자식(씬에 1개, TutorialRoomCodeDisplay와 형제)에 부착.
/// Player 프리팹에 붙이지 않는다 — 각 클라이언트는 자기 화면의 UI 하나만 보면 되므로 인원수만큼
/// 중복 생성할 필요가 없다(§6B.2 동적 합류와도 무관하게 항상 씬에 1개만 존재).
///
/// [상시 표시 → 상호작용 표지판 개폐로 변경, 2026-08-19]
/// 이전엔 항상 화면에 떠 있었으나, 화면을 계속 가리고 "그 순간 지나면 다시 못 여는" DialogueUI식
/// 1회성 노출의 단점을 피하고자 Tutorial 씬의 상호작용 표지판(TutorialCheerNameSignboard)이
/// Open()/Close()를 호출해 여닫는 방식으로 변경. 이 GameObject 자체(패널 루트)가 활성/비활성으로
/// 토글된다 — 씬에는 기본 비활성 상태로 배치할 것(사용자 에디터 작업).
/// </summary>
public class TutorialCheerNameUI : MonoBehaviour
{
    [Header("입력")]
    [SerializeField] TMP_InputField nameInputField;
    [SerializeField] Button confirmButton;
    [SerializeField] int maxLength = 12;

    [Header("닫기")]
    [Tooltip("비워도 됨 — 상호작용 표지판에서 다시 상호작용해도 닫힘(토글).")]
    [SerializeField] Button closeButton;

    [Header("표시")]
    [SerializeField] TMP_Text currentNameText;
    [SerializeField] TMP_Text feedbackText;
    [SerializeField] float feedbackDisplaySeconds = 2.5f;

    /// <summary>패널이 열려있는 동안 true — Player.cs가 이동 입력을 잠그는 데 사용
    /// (InGameChatUI.IsChatOpen과 동일 패턴, §7.3 타이핑 중 WASD 새는 문제 방지).</summary>
    public static bool IsOpen { get; private set; }

    PlayerCheerNameSync _mySync;
    ulong _myClientId;
    string _lastShownName;
    float _feedbackHideAt = -1f;

    void Awake()
    {
        if (nameInputField != null)
        {
            nameInputField.characterLimit = maxLength;
            nameInputField.onValidateInput = ValidateCharacter;
            nameInputField.onSubmit.AddListener(_ => OnConfirmClicked());
        }
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        SetInteractable(false); // 내 캐릭터를 찾기 전까지 비활성
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
    }

    void OnEnable()  => IsOpen = true;
    void OnDisable() => IsOpen = false;

    // ── 상호작용 표지판에서 호출 (TutorialCheerNameSignboard) ────────

    /// <summary>패널 열기. 이미 열려있으면 아무 것도 안 함(중복 호출 안전).</summary>
    public void Open()
    {
        if (gameObject.activeSelf) return;
        gameObject.SetActive(true);
    }

    /// <summary>패널 닫기 — 확정 여부와 무관, 타이핑 중이던 미확정 글자는 버려짐(§3.4상 문제 없음, 확정 전엔 로컬일 뿐).</summary>
    public void Close()
    {
        if (!gameObject.activeSelf) return;
        gameObject.SetActive(false);
    }

    /// <summary>열려있으면 닫고, 닫혀있으면 연다 — 표지판 상호작용 1개 입력으로 개폐 겸용.</summary>
    public void Toggle()
    {
        if (gameObject.activeSelf) Close();
        else Open();
    }

    void OnDestroy()
    {
        if (_mySync != null) _mySync.OnSubmitResult -= HandleSubmitResult;
    }

    void Update()
    {
        if (_mySync == null)
        {
            TryFindLocalSync();
            if (_mySync == null) return;
        }

        RefreshCurrentNameDisplay();

        if (_feedbackHideAt >= 0f && Time.time >= _feedbackHideAt && feedbackText != null)
        {
            feedbackText.gameObject.SetActive(false);
            _feedbackHideAt = -1f;
        }
    }

    // ── 내 캐릭터 탐색 (1회, 찾으면 폴링 중단) ──────────────────────

    void TryFindLocalSync()
    {
        var all = FindObjectsByType<PlayerCheerNameSync>(FindObjectsSortMode.None);
        foreach (var sync in all)
        {
            var netObj = sync.GetComponent<NetworkObject>();
            if (netObj == null || !netObj.IsOwner) continue;

            _mySync = sync;
            _myClientId = netObj.OwnerClientId;
            _mySync.OnSubmitResult += HandleSubmitResult;
            SetInteractable(true);
            break;
        }
    }

    // ── 입력 확정 ────────────────────────────────────────────────

    /// <summary>서버 규칙(CheerNameValidator)과 동일한 문자만 입력창에 타이핑 가능(편의용, 최종 검증은 Host).</summary>
    static char ValidateCharacter(string text, int charIndex, char addedChar)
    {
        char c = char.ToLowerInvariant(addedChar);
        bool allowed = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
        return allowed ? c : '\0';
    }

    void OnConfirmClicked()
    {
        if (_mySync == null || nameInputField == null) return;

        _mySync.SubmitCheerNameServerRpc(new FixedString32Bytes(nameInputField.text));
        SetInteractable(false); // 응답 오기 전까지 중복 제출 방지
        ShowFeedback("확인 중...", persistent: true);
    }

    void HandleSubmitResult(bool success, string errorKey)
    {
        SetInteractable(true);

        if (success)
        {
            ShowFeedback("이름이 확정되었습니다.");
            nameInputField.text = "";
        }
        else
        {
            ShowFeedback(ResolveErrorMessage(errorKey));
        }
    }

    static string ResolveErrorMessage(string key) => key switch
    {
        "format"   => "2~12자, 영문 소문자/숫자/밑줄(_)만 사용할 수 있어요.",
        "reserved" => "시스템 예약어라 사용할 수 없는 이름이에요.",
        "blocked"  => "사용할 수 없는 단어가 포함되어 있어요.",
        "taken"    => "이미 다른 팀원이 사용 중인 이름이에요.",
        _          => "이름을 확정할 수 없어요.",
    };

    // ── 표시 ────────────────────────────────────────────────────

    void RefreshCurrentNameDisplay()
    {
        if (currentNameText == null) return;

        string effective = "";
        foreach (var (clientId, name) in PlayerCheerNameSync.GetAllEffectiveNames())
        {
            if (clientId != _myClientId) continue;
            effective = name;
            break;
        }

        if (effective == _lastShownName) return;
        _lastShownName = effective;
        currentNameText.text = string.IsNullOrEmpty(effective) ? "현재 이름: (없음)" : $"현재 이름: {effective}";
    }

    void ShowFeedback(string message, bool persistent = false)
    {
        if (feedbackText == null) return;
        feedbackText.gameObject.SetActive(true);
        feedbackText.text = message;
        _feedbackHideAt = persistent ? -1f : Time.time + feedbackDisplaySeconds;
    }

    void SetInteractable(bool value)
    {
        if (nameInputField != null) nameInputField.interactable = value;
        if (confirmButton != null)  confirmButton.interactable = value;
    }
}
