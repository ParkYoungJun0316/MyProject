using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// End 씬 엔딩 크레딧. 커튼이 걷히면 크레딧과 버튼이 화면 아래에서 함께 올라와 씬 배치 그대로 멈춘다
/// (2026-09-28 — 한 장 고정 화면이 T.Boss 추락 직후 갑자기 떠서 당황스럽다는 피드백. 추락 → 아래에서
/// 올라와 감속 정지라 움직임이 이어진다). 끝까지 스크롤해 빠져나가지 않는다.
/// 자동 복귀 없음 — Return to Title 버튼 또는 Esc로만 타이틀 복귀(올라오는 중에도 Esc 즉시 복귀).
/// Discord(버그 제보) 버튼은 Discord 링크만 연다.
/// Space/Enter는 받지 않는다 — 엔딩 직후 습관적으로 눌러 화면을 보자마자 튕겨 나가지 않게.
///
/// [문구·배치는 씬이 SSOT]
/// 크레딧 문구와 최종 위치는 End 씬의 Txt.Credits(TMP)·버튼에 그대로 들어 있다 — 에디터에서 보이는 화면이
/// 곧 멈춘 뒤의 게임 화면. 이 컴포넌트는 문구·레이아웃을 바꾸지 않고, 시작할 때 riseTargets를 같은 거리만큼
/// 아래로 내렸다가 원래 자리로 되돌릴 뿐이다.
///
/// [배치]
/// End 씬 Canvas에 부착.
/// creditsText → Txt.Credits (글자 맨 위를 재서 화면 바로 아래에서 출발시키는 데 쓴다)
/// riseTargets → Txt.Credits, Btn.ReturntoTitle, Btn.Discord (비우면 연출 없이 고정 화면)
/// Discord(버그 제보) 버튼 OnClick → OnClickDiscord()
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

    [Header("올라오는 연출")]
    [Tooltip("글자 맨 위 위치를 재는 데 쓴다. 비우면 riseTargets 사각형 맨 위 기준(빈 화면 구간이 길어질 수 있음).")]
    [SerializeField] TMP_Text creditsText;

    [Tooltip("같이 올라올 대상. 씬에 놓인 위치가 멈추는 위치다. 비우면 연출 없이 고정 화면.")]
    [SerializeField] RectTransform[] riseTargets;

    [Tooltip("다 올라오는 데 걸리는 시간(초). 처음엔 조금 빠르고 끝에서 감속해 멈춘다(ease-out quad).\n" +
             "2026-09-28: 4초 cubic은 첫 0.5초에 1/3을 올라가 너무 빠르다는 피드백 → 7초 quad(첫 0.5초 약 14%).")]
    [SerializeField] float riseDuration = 7f;

    [Tooltip("출발 위치 = 글자 맨 위가 화면 아래 끝보다 이만큼(캔버스 단위) 더 아래.")]
    [SerializeField] float riseStartMargin = 20f;

    [Header("외부 링크")]
    [SerializeField] string discordUrl = "https://discord.gg/j2vqzcugtM";

    bool  _returning;
    float _shownAt = -1f;

    bool      _rising;
    Vector2[] _finalPos;
    float[]   _riseOffset; // 대상별 부모 로컬 단위 하강 거리

    void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    void Start() => PrepareRise();

    void Update()
    {
        if (_returning) return;

        // 전환 커튼이 걷히기 전엔 Esc를 받지 않는다 — 안 보이는 화면에서 나가지 않게.
        // 잠금 시간도 화면이 보인 순간부터 센다. (버튼 클릭은 덮인 동안 커튼이 막는다.)
        // 올라오는 연출도 이 순간(커튼 페이드 시작)부터 센다 — 배경이 검정이라 페이드 중 출발해도 티가 안 난다.
        if (LoadingCurtain.Instance != null && LoadingCurtain.Instance.IsCovered) return;
        if (_shownAt < 0f) _shownAt = Time.unscaledTime;

        if (_rising) TickRise();

        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame &&
            Time.unscaledTime - _shownAt >= skipLockSeconds)
            ReturnToTitle();
    }

    // ── 올라오는 연출 ────────────────────────────────────────────

    /// <summary>
    /// 씬 배치(최종 위치)를 기억하고 전 대상을 같은 거리만큼 내린다. 거리 = 글자 맨 위가 화면 아래 끝
    /// 바로 밑(riseStartMargin)에 오도록 — 커튼이 걷히자마자 제목이 들어오기 시작해 빈 검은 화면이 없다.
    /// 커튼에 덮인 첫 프레임 전에 끝나므로 최종 배치가 잠깐 보였다 내려가는 일은 없다.
    /// </summary>
    void PrepareRise()
    {
        if (riseTargets == null || riseTargets.Length == 0) return;

        var canvas = GetComponentInParent<Canvas>();
        var canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        if (canvasRect == null)
        {
            Debug.LogWarning("[EndCreditsController] 상위 Canvas 없음 — 올라오는 연출 없이 고정 화면으로 표시.", this);
            return;
        }

        Canvas.ForceUpdateCanvases();

        if (!TryGetContentTopWorld(out Vector3 topWorld)) return;

        float topY     = canvasRect.InverseTransformPoint(topWorld).y;
        float distance = topY - canvasRect.rect.yMin + Mathf.Max(0f, riseStartMargin);
        if (distance <= 0f) return;

        Vector3 worldDelta = canvasRect.TransformVector(0f, distance, 0f);

        _finalPos   = new Vector2[riseTargets.Length];
        _riseOffset = new float[riseTargets.Length];
        for (int i = 0; i < riseTargets.Length; i++)
        {
            RectTransform rt = riseTargets[i];
            if (rt == null) continue;

            _finalPos[i]   = rt.anchoredPosition;
            _riseOffset[i] = rt.parent != null ? rt.parent.InverseTransformVector(worldDelta).y : distance;
            rt.anchoredPosition = _finalPos[i] - new Vector2(0f, _riseOffset[i]);
        }
        _rising = true;
    }

    /// <summary>보이는 내용의 맨 위(월드). 글자가 있으면 글자 기준, 없으면 대상 사각형 맨 위.</summary>
    bool TryGetContentTopWorld(out Vector3 topWorld)
    {
        topWorld = default;
        bool found = false;

        if (creditsText != null)
        {
            creditsText.ForceMeshUpdate();
            if (creditsText.textInfo.characterCount > 0)
            {
                // textBounds는 글자 오브젝트 로컬 공간.
                topWorld = creditsText.transform.TransformPoint(new Vector3(0f, creditsText.textBounds.max.y, 0f));
                return true;
            }
        }

        var corners = new Vector3[4];
        foreach (RectTransform rt in riseTargets)
        {
            if (rt == null) continue;
            rt.GetWorldCorners(corners); // 1 = 왼쪽 위
            if (!found || corners[1].y > topWorld.y) topWorld = corners[1];
            found = true;
        }
        return found;
    }

    void TickRise()
    {
        float t = riseDuration > 0f ? Mathf.Clamp01((Time.unscaledTime - _shownAt) / riseDuration) : 1f;
        float remain = (1f - t) * (1f - t); // ease-out quad: 남은 비율

        for (int i = 0; i < riseTargets.Length; i++)
        {
            RectTransform rt = riseTargets[i];
            if (rt == null) continue;
            rt.anchoredPosition = _finalPos[i] - new Vector2(0f, _riseOffset[i] * remain);
        }

        if (t >= 1f) _rising = false;
    }

    /// <summary>Discord(버그 제보) 버튼 OnClick.</summary>
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
