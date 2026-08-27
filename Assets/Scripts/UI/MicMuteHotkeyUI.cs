using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// M키로 마이크 mute를 토글하고, mute 중일 때 HUD 아이콘을 표시.
///
/// GameSettingsManager.SetMicMuted()가 SSOT — ESC 설정 패널의 마이크 토글
/// (OptionsMenuController.micMuteToggle)과 동일한 상태를 공유한다. 이 컴포넌트가
/// SetMicMuted를 직접 부르고, GameSettingsManager.MicMutedChanged 이벤트를 구독해서
/// 반대로 설정 패널 쪽에서 mute가 바뀌어도 이 아이콘이 즉시 따라 갱신된다(양방향 동기화).
///
/// [배치]
/// UI.prefab(로컬 HUD)의 항상 활성 상태인 빈 GameObject에 부착 — 아이콘 자체를
/// 이 컴포넌트가 붙은 GameObject로 쓰면 mute 해제 시 Update()가 멈춰 M키로 다시 풀 수
/// 없게 되므로, 아이콘들은 반드시 별도의 자식 GameObject로 연결할 것.
/// mutedIcon/unmutedIcon: 항상 둘 중 하나만 활성화 — mute 상태면 mutedIcon만, 아니면
/// unmutedIcon만 켜짐(둘이 동시에 켜지거나 꺼지는 일이 없도록 RefreshIcon에서 배타적으로 처리).
/// </summary>
public class MicMuteHotkeyUI : MonoBehaviour
{
    [Header("아이콘")]
    [Tooltip("마이크 mute 상태일 때 활성화되는 아이콘 GameObject. " +
             "이 컴포넌트가 붙은 GameObject와는 별도의 자식으로 연결할 것(자기 자신 X).")]
    [SerializeField] GameObject mutedIcon;
    [Tooltip("마이크 mute 해제 상태일 때 활성화되는 아이콘 GameObject. " +
             "이 컴포넌트가 붙은 GameObject와는 별도의 자식으로 연결할 것(자기 자신 X).")]
    [SerializeField] GameObject unmutedIcon;

    void OnEnable()
    {
        GameSettingsManager settings = GameSettingsManager.Instance;
        if (settings == null) return;

        settings.MicMutedChanged += OnMicMutedChanged;
        RefreshIcon(settings.MicMuted);
    }

    void OnDisable()
    {
        GameSettingsManager settings = GameSettingsManager.Instance;
        if (settings != null) settings.MicMutedChanged -= OnMicMutedChanged;
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        // 채팅/치어네임 입력 중엔 'm' 타이핑이 mute 토글로 새지 않도록 양보
        // (PlayerEmoteMenuUI의 T키 게이팅과 동일한 우선순위).
        if (InGameChatUI.IsChatOpen || TutorialCheerNameUI.IsOpen) return;

        if (Keyboard.current.mKey.wasPressedThisFrame)
        {
            GameSettingsManager settings = GameSettingsManager.Instance;
            if (settings != null) settings.SetMicMuted(!settings.MicMuted);
        }
    }

    void OnMicMutedChanged(bool muted) => RefreshIcon(muted);

    void RefreshIcon(bool muted)
    {
        if (mutedIcon != null) mutedIcon.SetActive(muted);
        if (unmutedIcon != null) unmutedIcon.SetActive(!muted);
    }
}
