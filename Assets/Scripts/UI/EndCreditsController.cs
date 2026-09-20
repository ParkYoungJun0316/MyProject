using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// End 씬 엔딩 크레딧. 검은 화면에서 크레딧 블록이 아래에서 위로 올라간다.
/// 끝까지 올라가면 잠시 멈춘 뒤 TitleReturnFlow로 타이틀 복귀.
/// Discord / Return to Title 버튼, 또는 Space / Enter / Esc로 타이틀 복귀.
///
/// [배치]
/// End 씬 Canvas에 부착. viewport·creditsRect를 인스펙터로 연결.
/// Discord 버튼 OnClick → OnClickDiscord()
/// Return to Title 버튼 OnClick → OnClickReturnToTitle()
/// </summary>
public class EndCreditsController : MonoBehaviour
{
    const string DefaultCredits =
        "Kkul-tteok!\n" +
        "A traditional South Korean rice cake.\n" +
        "This game was inspired by my favorite ricecake.\n" +
        "\n" +
        "Made by\n" +
        "youngjun0316\n" +
        "Solo developer\n" +
        "\n" +
        "E-mail: youngjunpark0316@gmail.com\n" +
        "Bug reports: discord.gg/BGNs5F2eg\n" +
        "\n" +
        "\n" +
        "Thank you for playing\n" +
        "\n" +
        "Season 2\n" +
        "See you in the stomach";

    [Header("스크롤")]
    [SerializeField] RectTransform viewport;
    [SerializeField] RectTransform creditsRect;
    [SerializeField] TextMeshProUGUI creditsText;
    [SerializeField] [TextArea(12, 24)] string credits = DefaultCredits;
    [SerializeField] float scrollSpeed = 70f;
    [SerializeField] float holdAfterEndSeconds = 1.5f;
    [SerializeField] float skipLockSeconds = 0.4f;

    [Header("외부 링크")]
    [SerializeField] string discordUrl = "https://discord.gg/BGNs5F2eg";

    bool  _returning;
    bool  _ready;
    float _endY;

    void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        if (creditsText != null)
            creditsText.text = credits;
    }

    void Start()
    {
        if (viewport == null || creditsRect == null)
        {
            Debug.LogError("[EndCreditsController] viewport / creditsRect 미연결 — 크레딧 스크롤 비활성", this);
            enabled = false;
            return;
        }

        if (creditsText != null)
        {
            creditsText.ForceMeshUpdate();
            creditsRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                Mathf.Max(creditsText.preferredHeight, 1f));
        }

        Canvas.ForceUpdateCanvases();

        float viewH = viewport.rect.height;
        float textH = creditsRect.rect.height;
        // 피벗 (0.5, 1): y = 텍스트 상단. 제목이 화면 아래에서 올라오기 시작.
        creditsRect.anchoredPosition = new Vector2(0f, -viewH * 0.5f - 40f);
        _endY = viewH * 0.5f + textH;
        _ready = true;
    }

    void Update()
    {
        if (_returning || !_ready) return;

        if (Time.timeSinceLevelLoad >= skipLockSeconds && WantsSkip())
        {
            ReturnToTitle(skipped: true);
            return;
        }

        Vector2 pos = creditsRect.anchoredPosition;
        pos.y += scrollSpeed * Time.deltaTime;
        creditsRect.anchoredPosition = pos;

        if (pos.y >= _endY)
            ReturnToTitle(skipped: false);
    }

    bool WantsSkip()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.spaceKey.wasPressedThisFrame) return true;
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) return true;
            if (kb.escapeKey.wasPressedThisFrame) return true;
        }

        return false;
    }

    /// <summary>Discord 버튼 OnClick.</summary>
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
    public void OnClickReturnToTitle() => ReturnToTitle(skipped: true);

    void ReturnToTitle(bool skipped)
    {
        if (_returning) return;
        _returning = true;
        StartCoroutine(ReturnRoutine(skipped));
    }

    IEnumerator ReturnRoutine(bool skipped)
    {
        if (!skipped && holdAfterEndSeconds > 0f)
            yield return new WaitForSeconds(holdAfterEndSeconds);

        if (TitleReturnFlow.Instance == null)
        {
            Debug.LogWarning("[EndCreditsController] TitleReturnFlow 없음 — 타이틀 복귀 불가 (End 씬 단독 실행?)", this);
            yield break;
        }

        TitleReturnFlow.Instance.Request(new TitleReturnOptions
        {
            Reason = TitleReturnReason.EndDemo,
            Scope  = TitleReturnScope.FullRunReset,
        });
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: 타이틀 복귀")]
    void Debug_Return() => ReturnToTitle(skipped: true);
#endif
}
