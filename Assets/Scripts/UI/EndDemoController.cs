using UnityEngine;

/// <summary>
/// End.Demo 씬 컨트롤러.
/// 검은 화면 + 타이틀 복귀 버튼만 있는 씬.
///
/// [배치 방법]
/// End.Demo 씬 Canvas 또는 빈 GameObject에 부착.
///
/// [버튼 OnClick 연결]
/// 타이틀 복귀 버튼 → OnClickReturnToTitle()
///
/// [타이틀 복귀 처리]
/// TitleReturnFlow.Request(FullRunReset)으로 위임.
/// 페이드·Shutdown·ResetSession 등은 TitleReturnFlow가 통합 처리.
/// </summary>
public class EndDemoController : MonoBehaviour
{
    // Inspector 직렬화 값 보존용 (TitleReturnFlow 도입 전 설정 유지)
    [Header("미사용 (TitleReturnFlow로 이전됨)")]
    [SerializeField] private string titleSceneName  = "0.Title";
    [SerializeField] private ScreenFader screenFader;
    [SerializeField] private float fadeOutDuration  = 0f;

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

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 타이틀 복귀")]
    void Debug_Return() => OnClickReturnToTitle();
#endif
}
