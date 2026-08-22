using System.Collections;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
///
/// [입력 우선권, 2026-08-22]
/// 열려있는 동안 키보드 입력의 최우선권을 가진다 — Enter는 확정 제출(InGameChatUI는 무시/자동 닫힘),
/// Esc는 이 패널을 닫음(EscMenuController는 무시). 같은 프레임에 Esc가 눌렸을 때 "패널이 닫히자마자
/// Esc 메뉴가 같이 뜨는" 이중 소비를 막기 위해, 실행 순서에 의존하지 않고 <see cref="ConsumedEscThisFrame"/>
/// 명시적 플래그로 "이번 프레임에 Esc를 이미 이 패널이 소비했음"을 알린다(스크립트 실행 순서 가정 대신
/// 명시적 상태로 — Bug Hunter 리뷰 3항목 중 3번 수정).
///
/// [커서 공유, 2026-08-22]
/// 커서 lock/visible을 직접 건드리지 않고 <see cref="CursorUnlockRequestUtil"/>에 요청만 한다 —
/// EscMenu·이모트 메뉴가 동시에 열려 있을 때 "마지막에 닫은 UI가 무조건 잠금"으로 서로 덮어쓰지
/// 않도록(Bug Hunter 리뷰 3항목 중 2번 수정). 요청/해제는 Open()/Close()가 아니라 OnEnable/OnDisable에
/// 걸어, 씬 리로드로 패널이 열린 채 파괴돼도(Close() 호출 없이) Unity가 파괴 직전 자동 호출하는
/// OnDisable에서 요청이 반드시 정리된다. 다만 그 파괴가 씬 통째 언로드(TitleReturnFlow 등)로 인한
/// 것이면 실제 Cursor는 건드리지 않고 목록에서만 빠진다(<see cref="CursorUnlockRequestUtil.Forget"/>) —
/// 그렇지 않으면 타이틀 복귀 시 이미 풀어둔 커서를 도로 잠가버리는 회귀가 생긴다(2026-08-22 재수정).
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

    [Header("커서")]
    [Tooltip("패널 닫을 때 커서를 다시 잠글지 여부. ThirdPersonCamera.lockCursor 설정과 일치시키세요 " +
             "(EscMenuController/PlayerEmoteMenuUI와 동일 패턴).")]
    [SerializeField] bool lockCursorOnClose = true;

    /// <summary>패널이 열려있는 동안 true — Player.cs가 이동 입력을 잠그는 데 사용
    /// (InGameChatUI.IsChatOpen과 동일 패턴, §7.3 타이핑 중 WASD 새는 문제 방지).</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>이번 프레임에 Esc로 이 패널이 막 닫혔는지 — EscMenuController가 같은 프레임에
    /// 자기 메뉴를 열지 않도록 확인하는 명시적 플래그(실행 순서 비의존).</summary>
    public static bool ConsumedEscThisFrame => s_escClosedFrame == Time.frameCount;
    static int s_escClosedFrame = -1;

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

    void OnEnable()
    {
        IsOpen = true;
        // Esc를 눌러야만 커서가 풀리던 문제 — 패널이 열리면 즉시 커서를 풀어 마우스로 바로
        // 입력창/확정 버튼을 클릭할 수 있게 한다. OnEnable/OnDisable 짝으로 걸어 씬 파괴 시에도
        // Release가 보장된다(클래스 doc [커서 공유] 참고).
        CursorUnlockRequestUtil.Request(this);
    }

    void OnDisable()
    {
        IsOpen = false;

        // 씬이 통째로 언로드되는 중(예: TitleReturnFlow의 SceneManager.LoadScene)이면 자동으로
        // OnDisable이 불려도 목록 제거만 하고 실제 Cursor는 건드리지 않는다 — 그 시점엔 이미
        // TitleReturnFlow 등이 최종 커서 상태를 정해뒀으므로 여기서 다시 잠그면 그걸 덮어써버려
        // "타이틀 씬에서 마우스가 사라지는" 회귀가 생긴다(2026-08-22 수정). 사용자가 직접 닫은
        // 경우(씬은 그대로 로드된 채 SetActive(false)만 됨)만 실제로 Release해서 잠근다.
        if (!gameObject.scene.isLoaded)
        {
            CursorUnlockRequestUtil.Forget(this);
            return;
        }
        CursorUnlockRequestUtil.Release(this, lockCursorOnClose);
    }

    // ── 상호작용 표지판에서 호출 (TutorialCheerNameSignboard) ────────

    /// <summary>패널 열기. 이미 열려있으면 아무 것도 안 함(중복 호출 안전).</summary>
    public void Open()
    {
        if (gameObject.activeSelf) return;
        gameObject.SetActive(true);
        StartCoroutine(FocusInputNextFrame());
    }

    /// <summary>InGameChatUI.ActivateInputNextFrame과 동일 패턴 — SetActive 직후 바로 활성화하면
    /// interactable=false 상태(첫 오픈 시 _mySync 미발견)일 수 있어 1프레임 대기 후 포커스한다.</summary>
    IEnumerator FocusInputNextFrame()
    {
        yield return null;
        if (nameInputField == null || !gameObject.activeSelf) yield break;
        nameInputField.ActivateInputField();
        EventSystem.current?.SetSelectedGameObject(nameInputField.gameObject);
    }

    /// <summary>패널 닫기 — 확정 여부와 무관, 타이핑 중이던 미확정 글자는 버려짐(§3.4상 문제 없음, 확정 전엔 로컬일 뿐).</summary>
    public void Close()
    {
        if (!gameObject.activeSelf) return;
        gameObject.SetActive(false); // 커서 Release는 OnDisable에서 처리
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
        // Esc는 닫기 버튼 대신 이 패널을 최우선으로 닫는다(§요청 3) — EscMenuController는
        // ConsumedEscThisFrame 플래그를 확인해 같은 프레임엔 자기 메뉴를 열지 않는다(실행 순서 비의존).
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            s_escClosedFrame = Time.frameCount;
            Close();
            return;
        }

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
            // 확정되면 닫기 버튼 대신 바로 닫는다(§요청 3) — 확정 결과는 패널 밖 닉네임 표시(예:
            // PlayerHPUI selfNameLabel)로 바로 반영되므로 패널 안에서 문구를 보여줄 필요가 없다.
            nameInputField.text = "";
            Close();
        }
        else
        {
            ShowFeedback(ResolveErrorMessage(errorKey));
            StartCoroutine(FocusInputNextFrame()); // 실패 시 바로 다시 고쳐 쓸 수 있게 재포커스
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
