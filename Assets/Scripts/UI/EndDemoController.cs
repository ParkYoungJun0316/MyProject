using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// End.Demo 씬 컨트롤러.
/// 검은 화면 + 타이틀 복귀 버튼만 있는 씬.
///
/// [배치 방법]
/// End.Demo 씬 Canvas 또는 빈 GameObject에 부착.
///
/// [Inspector 연결]
/// - titleSceneName   : "0.Title" (기본값)
/// - screenFader      : 선택. 페이드아웃 연출
/// - fadeOutDuration  : 페이드 시간(초)
///
/// [버튼 OnClick 연결]
/// 타이틀 복귀 버튼 → OnClickReturnToTitle()
///
/// [타이틀 복귀 시 처리 순서]
/// 1. NGO Shutdown + LanDiscovery 중단 + NetworkSessionData 초기화
/// 2. GameSession 런타임 리셋
/// 3. LobbyContext 오프라인 초기화
/// 4. SceneManager.LoadScene("0.Title") — NGO 종료 후 일반 로드
/// </summary>
public class EndDemoController : MonoBehaviour
{
    [Header("씬 전환")]
    [Tooltip("복귀할 타이틀 씬 이름. Build Settings 이름과 정확히 일치해야 함.")]
    [SerializeField] private string titleSceneName = "0.Title";

    [Header("페이드 (선택)")]
    [SerializeField] private ScreenFader screenFader;

    [Tooltip("페이드아웃 시간(초). 0이면 즉시 전환.")]
    [SerializeField] private float fadeOutDuration = 0f;

    // ── 버튼 콜백 ─────────────────────────────────────────────────

    /// <summary>타이틀 복귀 버튼 OnClick에 연결.</summary>
    public void OnClickReturnToTitle()
    {
        StartCoroutine(ReturnToTitle());
    }

    // ── 내부 ──────────────────────────────────────────────────────

    IEnumerator ReturnToTitle()
    {
        if (screenFader != null && fadeOutDuration > 0f)
        {
            screenFader.FadeOut(fadeOutDuration);
            yield return new WaitForSeconds(fadeOutDuration);
        }

        // ① NGO Shutdown + LanDiscovery 중단 + 세션 데이터 초기화
        NetworkManagerSetup.Instance?.Shutdown();

        // ② GameSession 런타임 리셋
        GameSession.Instance?.ResetSession();

        // ③ LobbyContext 초기화
        LobbyContext.Mode = LobbyMode.Offline;

        // ④ 타이틀 복귀 — NGO 종료 후 일반 SceneManager 사용
        SceneManager.LoadScene(titleSceneName);
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 타이틀 복귀")]
    void Debug_Return() => OnClickReturnToTitle();
#endif
}
