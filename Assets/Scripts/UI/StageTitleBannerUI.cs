using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 씬 진입 시 장소 제목을 상단에 잠깐 띄우는 로컬 배너. SSOT: Assets/Docs/StageTitleBanner.md.
///
/// [표시 규칙]
/// - 키는 씬 이름에서 만든다: <c>Title.</c> + 씬 이름 (예: Title.M.Stage1). 씬별 인스펙터 설정 없음.
///   <see cref="KoreanFallback"/>에 없는 씬(Tutorial, End.Demo …)은 아무것도 안 한다.
/// - 로딩 커튼이 걷히는 순간 페이드인 → 유지 → 페이드아웃 (CanvasGroup 알파만).
///   커튼이 없거나(에디터 직접 Play) 처음부터 안 덮여 있으면 바로 띄운다.
/// - 사망 리로드·준비 실패 리로드 때는 안 띄운다 — DialogueUI 인트로와 같은 GameSession 본 키를 쓴다.
///   "봤다" 기록은 실제로 띄우는 순간에 남긴다 (커튼 뒤에서 리로드되면 아직 못 본 것).
/// - 배너가 끝날 때까지 DialogueUI 열기를 미룬다(<see cref="WhenClear"/>) — 순서: 배너 → 대사.
///
/// [배치]
/// UI.prefab 안에 오브젝트 하나(활성 상태로 둘 것) + 이 스크립트 + CanvasGroup, 자식 TMP를 titleText에 연결.
/// 위치는 화면 상단 1/3.
///
/// NGO 쓰기 없음. 모든 피어가 같은 씬 로드·리로드를 겪으므로 각자 로컬 판정으로 결과가 같다.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class StageTitleBannerUI : MonoBehaviour
{
    public const string TableName = "StageTitle";
    const string KeyPrefix = "Title.";
    const string SeenKeyPrefix = "StageTitle/";

    [Header("연결")]
    [Tooltip("제목 TMP. 비우면 자식에서 찾는다.")]
    [SerializeField] TextMeshProUGUI titleText;

    [Header("타이밍(초, unscaled)")]
    [Tooltip("커튼이 걷히기 시작한 뒤 배너가 나오기까지 대기.")]
    [SerializeField] float startDelay      = 0.2f;
    [SerializeField] float fadeInDuration  = 0.5f;
    [SerializeField] float holdDuration    = 2f;
    [SerializeField] float fadeOutDuration = 0.7f;

    /// <summary>이 씬에서 배너가 아직 안 끝났다 — 대화창은 기다려야 한다.</summary>
    public static bool IsHolding { get; private set; }

    static Action s_whenClear;

    readonly LocalizedString _query = new LocalizedString { TableReference = TableName };

    CanvasGroup _canvasGroup;
    string _sceneName;
    bool _waitingForCurtain;

    /// <summary>배너가 끝났으면 바로, 아니면 끝난 뒤에 실행. 씬이 바뀌면 대기 중인 것은 버린다.</summary>
    public static void WhenClear(Action action)
    {
        if (action == null) return;
        if (!IsHolding) { action(); return; }
        s_whenClear += action;
    }

    // Awake에서 IsHolding을 세운다 — PhaseManager.Start → Phase 0 → 인트로 대화가 같은 Start 패스에서
    // 열릴 수 있고 Start 순서는 보장되지 않는다. Awake는 씬의 모든 Start보다 먼저다.
    void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _canvasGroup.alpha          = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable   = false;

        if (titleText == null) titleText = GetComponentInChildren<TextMeshProUGUI>(true);

        _sceneName = SceneManager.GetActiveScene().name;
        if (!KoreanFallback.ContainsKey(KeyPrefix + _sceneName)) return;
        if (GameSession.Instance != null && GameSession.Instance.IsIntroSeen(SeenKeyPrefix + _sceneName)) return;

        IsHolding          = true;
        _waitingForCurtain = true;
    }

    // 이벤트 대신 상태를 본다 — 구독 전에 걷혀버려도 놓치지 않고, 타임아웃으로 포기하고 걷혀도 똑같이 잡힌다.
    void Update()
    {
        if (!_waitingForCurtain) return;

        var curtain = LoadingCurtain.Instance;
        if (curtain != null && curtain.IsCovered) return;

        _waitingForCurtain = false;
        StartCoroutine(PlayRoutine());
    }

    void OnDestroy()
    {
        // 배너 도중 씬이 바뀌었다 — 대기 중인 대화창은 이미 파괴됐거나 다음 씬 것이 아니다.
        if (!IsHolding) return;
        IsHolding   = false;
        s_whenClear = null;
    }

    IEnumerator PlayRoutine()
    {
        GameSession.Instance?.MarkIntroSeen(SeenKeyPrefix + _sceneName);

        yield return LocalizationSettings.InitializationOperation;
        ApplyLocaleFont();
        if (titleText != null) titleText.text = ResolveText(KeyPrefix + _sceneName);

        if (startDelay > 0f) yield return new WaitForSecondsRealtime(startDelay);
        yield return Fade(0f, 1f, fadeInDuration);
        if (holdDuration > 0f) yield return new WaitForSecondsRealtime(holdDuration);
        yield return Fade(1f, 0f, fadeOutDuration);

        Release();
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        _canvasGroup.alpha = to;
    }

    void Release()
    {
        IsHolding = false;
        Action pending = s_whenClear;
        s_whenClear = null;
        pending?.Invoke();
    }

    void ApplyLocaleFont()
    {
        if (titleText == null) return;
        try
        {
            TMP_FontAsset font = LocalizationSettings.AssetDatabase.GetLocalizedAsset<TMP_FontAsset>(
                TipUI.FontTableName, TipUI.FontEntryKey);
            if (font != null) titleText.font = font;
        }
        catch (Exception)
        {
            // 폰트 테이블 미생성 시 프리팹 폰트 유지
        }
    }

    string ResolveText(string key)
    {
        try
        {
            _query.TableReference = TableName;
            _query.TableEntryReference = key;
            if (!_query.IsEmpty)
            {
                string value = _query.GetLocalizedString();
                if (!string.IsNullOrEmpty(value) && value != key)
                    return value;
            }
        }
        catch (Exception)
        {
            // 테이블 미생성·미로드 시 한국어 폴백
        }

        return KoreanFallback.TryGetValue(key, out string fallback) ? fallback : key;
    }

    /// <summary>배너를 띄울 씬 목록 겸 한국어 폴백. Assets/Docs/StageTitleBanner.md §3과 맞출 것.</summary>
    static readonly Dictionary<string, string> KoreanFallback = new Dictionary<string, string>
    {
        { "Title.M.Stage1", "쉬지 않는 입" },
        { "Title.M.Stage2", "넘쳐나는 침" },
        { "Title.M.Stage3", "비좁은 통로" },
        { "Title.M.Stage4", "지하의 혀" },
        { "Title.M.Stage5", "끝없는 붕괴" },
        { "Title.M.Boss",   "마지막 탈출 기회" },
        { "Title.T.Stage1", "굴러오는 사탕" },
        { "Title.T.Stage2", "보이지 않는 길" },
        { "Title.T.Stage3", "역류하는 위액" },
        { "Title.T.Stage4", "정원 초과" },
        { "Title.T.Stage5", "문 열어" },
        { "Title.T.Boss",   "사탕이 닿기 전에" },
    };
}
