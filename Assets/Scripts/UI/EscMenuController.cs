using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 인게임 ESC 메뉴 패널 컨트롤러.
///
/// [배치 방법]
/// UI.prefab (또는 각 스테이지 씬)의 빈 GameObject에 부착.
/// escPanel: ESC 눌렀을 때 표시할 패널 GameObject 연결.
///
/// [버튼 OnClick 연결]
/// Resume  버튼 → OnClickResume()
/// Reset   버튼 → OnClickReset()
/// Quit    버튼 → DisconnectManager.OnClickLeaveRoom()   (Inspector 직접 연결)
/// Setting 버튼 → OnClickSettings()
/// (설정 패널 내부 닫기(X) 버튼은 OptionsMenuController.OnClickClose()에 직결 — 패널 자신을 SetActive(false).
///  OnClickCloseSettings()는 코드에서 강제로 닫아야 할 때 쓰는 보조 API.)
///
/// [동작]
/// Esc 키   : 설정 패널이 열려 있으면 그것부터 닫고, 아니면 ESC 메뉴 열기/닫기 토글
///           (Setting_Panel은 ESC_Panel과 형제 GameObject라 Resume은 서로 건드리지 않음)
/// 패널 열림 : CursorUnlockRequestUtil에 커서 해제 요청, Reset 버튼 활성 여부 갱신
/// 패널 닫힘 : CursorUnlockRequestUtil에 해제 통보 — 치어네임/이모트 메뉴 등 다른 UI가 아직
///           커서를 요청 중이면 실제로는 잠기지 않는다(공유 카운트, 2026-08-22).
///
/// [Reset 동작]
/// Host만 씬 리로드 가능 (NGO 정책).
/// Client는 Reset 버튼이 회색(interactable = false)으로 표시됨.
/// </summary>
public class EscMenuController : MonoBehaviour
{
    [Header("패널")]
    [Tooltip("ESC 키로 열고 닫을 패널 GameObject")]
    [SerializeField] private GameObject escPanel;

    [Header("버튼")]
    [Tooltip("Reset 버튼. Host일 때만 interactable = true.")]
    [SerializeField] private Button resetButton;

    [Header("설정 패널")]
    [Tooltip("Setting 버튼 클릭 시 열릴 패널 (OptionsMenuController 부착). 비워두면 클릭 무시.")]
    [SerializeField] private GameObject settingsPanel;

    [Header("커서")]
    [Tooltip("패널 닫을 때 커서를 다시 잠글지 여부. ThirdPersonCamera.lockCursor 설정과 일치시키세요.")]
    [SerializeField] private bool lockCursorOnClose = true;

    bool _isOpen;

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        if (escPanel != null)
            escPanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    /// <summary>씬 파괴(TitleReturnFlow의 SceneManager.LoadScene 등) 시 패널이 열려 있던 채로
    /// 파괴돼도(OnClickResume 없이) 요청 목록에 잔여 참조가 새지 않도록 하는 안전장치.
    /// Release가 아니라 Forget을 쓴다 — OnClickLeaveRoom()은 Esc 메뉴를 먼저 닫지 않고 바로
    /// TitleReturnFlow.Request를 호출하므로 Quit을 누르는 순간엔 항상 _isOpen==true인 채로 이
    /// 씬이 통째로 파괴된다. 여기서 Release로 실제 Cursor를 잠그면, TitleReturnFlow가 그 직전에
    /// 이미 풀어둔 커서를 도로 잠가 "타이틀 씬에서 마우스가 사라지는" 회귀가 생긴다(2026-08-22 수정).</summary>
    void OnDestroy()
    {
        if (_isOpen) CursorUnlockRequestUtil.Forget(this);
    }

    // ── 입력 ──────────────────────────────────────────────────────

    void Update()
    {
        if (Keyboard.current == null) return;

        // CheerName 설정 패널이 열려 있으면 Esc 최우선권을 그쪽에 넘긴다(우선순위: cheername > esc 메뉴).
        // IsOpen뿐 아니라 ConsumedEscThisFrame도 함께 확인 — 실행 순서와 무관하게 "이번 프레임에
        // Esc를 CheerName 패널이 이미 처리했음"을 명시적으로 알 수 있어 이중 소비를 막는다.
        if (TutorialCheerNameUI.IsOpen || TutorialCheerNameUI.ConsumedEscThisFrame) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            // Setting_Panel은 ESC_Panel과 형제(sibling)라 OnClickResume()이 이를 건드리지 않는다.
            // 설정 패널이 열려 있으면 Esc는 그것부터 닫는다(뒤에 남은 ESC 메뉴 상태는 유지).
            if (settingsPanel != null && settingsPanel.activeSelf)
                settingsPanel.SetActive(false);
            else if (_isOpen)
                OnClickResume();
            else
                OpenPanel();
        }
    }

    // ── 패널 열기 ─────────────────────────────────────────────────

    void OpenPanel()
    {
        _isOpen = true;

        if (escPanel != null)
            escPanel.SetActive(true);

        RefreshResetButton();

        CursorUnlockRequestUtil.Request(this);
    }

    // ── Reset 버튼 활성 여부 ──────────────────────────────────────

    void RefreshResetButton()
    {
        if (resetButton == null) return;

        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        resetButton.interactable = isHost;
    }

    // ── 버튼 콜백 ─────────────────────────────────────────────────

    /// <summary>Resume 버튼 OnClick에 연결. 패널을 닫고 게임을 재개합니다.</summary>
    public void OnClickResume()
    {
        _isOpen = false;

        if (escPanel != null)
            escPanel.SetActive(false);

        CursorUnlockRequestUtil.Release(this, lockCursorOnClose);
    }

    /// <summary>
    /// Reset 버튼 OnClick에 연결 (Host 전용 버튼).
    /// 사망과 동일 문으로 리로드 — 새 시드 배포 + 전원 씬 리로드 (NetworkDesign §11.1).
    /// </summary>
    public void OnClickReset()
    {
        if (StageNetworkState.Instance != null)
            StageNetworkState.Instance.NotifyPlayerDeathServerRpc();
        else
            Debug.LogWarning("[EscMenuController] StageNetworkState가 없어 Reset을 수행할 수 없습니다.");
    }

    /// <summary>Setting 버튼 OnClick에 연결.</summary>
    public void OnClickSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
        else Debug.LogWarning("[EscMenuController] settingsPanel이 연결되지 않았습니다.");
    }

    /// <summary>
    /// 설정 패널 닫기 — 코드에서 강제로 닫아야 할 때 쓰는 보조 API.
    /// 패널 내부 닫기(X) 버튼은 OptionsMenuController.OnClickClose()에 직결되어 있어 이 메서드를 거치지 않는다.
    /// </summary>
    public void OnClickCloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 패널 열기")]
    void Debug_Open() => OpenPanel();

    [ContextMenu("테스트: 패널 닫기")]
    void Debug_Close() => OnClickResume();
#endif
}
