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
/// Setting 버튼 → 미구현 (추후 추가)
///
/// [동작]
/// Esc 키   : 패널 열기 / 닫기 토글
/// 패널 열림 : 커서 표시·잠금 해제, Reset 버튼 활성 여부 갱신
/// 패널 닫힘 : 커서 숨김·잠금 (lockCursorOnClose = true 일 때)
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

    [Header("커서")]
    [Tooltip("패널 닫을 때 커서를 다시 잠글지 여부. ThirdPersonCamera.lockCursor 설정과 일치시키세요.")]
    [SerializeField] private bool lockCursorOnClose = true;

    bool _isOpen;

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        if (escPanel != null)
            escPanel.SetActive(false);
    }

    // ── 입력 ──────────────────────────────────────────────────────

    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (_isOpen) OnClickResume();
            else         OpenPanel();
        }
    }

    // ── 패널 열기 ─────────────────────────────────────────────────

    void OpenPanel()
    {
        _isOpen = true;

        if (escPanel != null)
            escPanel.SetActive(true);

        RefreshResetButton();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
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

        if (lockCursorOnClose)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }
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

    // Setting 버튼: 미구현. 추후 OnClickSetting() 추가.

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 패널 열기")]
    void Debug_Open() => OpenPanel();

    [ContextMenu("테스트: 패널 닫기")]
    void Debug_Close() => OnClickResume();
#endif
}
