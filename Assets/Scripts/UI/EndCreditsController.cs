using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// End 씬 엔딩 크레딧. 검은 화면 한 장에 크레딧 전체가 고정으로 떠 있다(스크롤 없음, 2026-09-27).
/// 자동 복귀 없음 — Return to Title 버튼 또는 Esc로만 타이틀 복귀. Report a Bug 버튼은 Discord 링크만 연다.
/// Space/Enter는 받지 않는다 — 엔딩 직후 습관적으로 눌러 화면을 보자마자 튕겨 나가지 않게.
///
/// [문구·배치는 씬이 SSOT]
/// 크레딧 문구와 위치는 End 씬의 Txt.Credits(TMP)에 그대로 들어 있다 — 에디터에서 보이는 화면이 곧 게임 화면.
/// 이 컴포넌트는 문구나 레이아웃을 건드리지 않는다.
///
/// [배치]
/// End 씬 Canvas에 부착.
/// Report a Bug 버튼 OnClick → OnClickDiscord()
/// Return to Title 버튼 OnClick → OnClickReturnToTitle()
///
/// [단독 Play 주의]
/// End만 열고 Play하면 TitleReturnFlow(Title에서 생성·DDOL)가 없어 복귀가 안 된다(경고 로그만).
/// 확인은 Tutorial 스테이지 바로가기의 End 버튼으로 한다.
/// </summary>
public class EndCreditsController : MonoBehaviour
{
    [Tooltip("화면이 보인 뒤 Esc를 받기 시작할 때까지의 시간(초).")]
    [SerializeField] float skipLockSeconds = 0.4f;

    [Header("외부 링크")]
    [SerializeField] string discordUrl = "https://discord.gg/BGNs5F2eg";

    bool  _returning;
    float _shownAt = -1f;

    void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    void Update()
    {
        if (_returning) return;

        // 전환 커튼이 걷히기 전엔 Esc를 받지 않는다 — 안 보이는 화면에서 나가지 않게.
        // 잠금 시간도 화면이 보인 순간부터 센다. (버튼 클릭은 덮인 동안 커튼이 막는다.)
        if (LoadingCurtain.Instance != null && LoadingCurtain.Instance.IsCovered) return;
        if (_shownAt < 0f) _shownAt = Time.unscaledTime;

        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame &&
            Time.unscaledTime - _shownAt >= skipLockSeconds)
            ReturnToTitle();
    }

    /// <summary>Report a Bug(Discord) 버튼 OnClick.</summary>
    public void OnClickDiscord()
    {
        if (string.IsNullOrEmpty(discordUrl))
        {
            Debug.LogWarning("[EndCreditsController] discordUrl이 비어 있습니다.", this);
            return;
        }
        Application.OpenURL(discordUrl);
    }

    /// <summary>타이틀 복귀 버튼 OnClick.</summary>
    public void OnClickReturnToTitle() => ReturnToTitle();

    void ReturnToTitle()
    {
        if (_returning) return;

        if (TitleReturnFlow.Instance == null)
        {
            Debug.LogWarning("[EndCreditsController] TitleReturnFlow 없음 — 타이틀 복귀 불가 (End 씬 단독 실행?)", this);
            return;
        }

        _returning = true;
        TitleReturnFlow.Instance.Request(new TitleReturnOptions
        {
            Reason = TitleReturnReason.EndDemo,
            Scope  = TitleReturnScope.FullRunReset,
        });
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: 타이틀 복귀")]
    void Debug_Return() => ReturnToTitle();
#endif
}
