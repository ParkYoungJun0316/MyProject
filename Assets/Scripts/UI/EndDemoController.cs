using UnityEngine;

/// <summary>
/// End.Demo 씬 컨트롤러.
/// 검은 화면 + 타이틀 복귀 버튼만 있는 씬.
///
/// [배치 방법]
/// End.Demo 씬 Canvas 또는 빈 GameObject에 부착.
///
/// [버튼 OnClick 연결]
/// 타이틀 복귀 버튼  → OnClickReturnToTitle()
/// Discord 버튼      → OnClickDiscord()
/// Steam 위시리스트  → OnClickSteamWishlist()
///
/// [타이틀 복귀 처리]
/// TitleReturnFlow.Request(FullRunReset)으로 위임.
/// 페이드·Shutdown·ResetSession 등은 TitleReturnFlow가 통합 처리.
///
/// [커서]
/// Awake에서 커서를 UI용(자유 이동·표시)으로 복원한다.
/// 오디오(Dissonance)는 타이틀 복귀 전까지 유지된다.
/// </summary>
public class EndDemoController : MonoBehaviour
{
    [Header("외부 링크")]
    [Tooltip("Discord 초대 링크")]
    [SerializeField] private string discordUrl      = "https://discord.gg/";
    [Tooltip("Steam 스토어 페이지 URL (위시리스트 버튼용). Steamworks 연동 전까지 임시 URL 사용 가능.")]
    [SerializeField] private string steamStoreUrl   = "https://store.steampowered.com/";

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        // 스테이지에서 ThirdPersonCamera가 잠근 커서를 UI용으로 복원.
        // Dissonance 오디오는 건드리지 않으므로 팀원 보이스 연결 유지.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    // ── 버튼 콜백 ─────────────────────────────────────────────────

    /// <summary>타이틀 복귀 버튼 OnClick에 연결.</summary>
    public void OnClickReturnToTitle()
    {
        TitleReturnFlow.Instance?.Request(new TitleReturnOptions
        {
            Reason = TitleReturnReason.EndDemo,
            Scope  = TitleReturnScope.FullRunReset,
        });
    }

    /// <summary>Discord 버튼 OnClick에 연결.</summary>
    public void OnClickDiscord()
    {
        if (string.IsNullOrEmpty(discordUrl)) return;
        Application.OpenURL(discordUrl);
    }

    /// <summary>Steam 위시리스트 버튼 OnClick에 연결.</summary>
    public void OnClickSteamWishlist()
    {
        if (string.IsNullOrEmpty(steamStoreUrl)) return;
        Application.OpenURL(steamStoreUrl);
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 타이틀 복귀")]
    void Debug_Return() => OnClickReturnToTitle();
#endif
}
