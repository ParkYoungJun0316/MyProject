using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Tip_Panel에 붙는 로컬 HUD. 넘겨받은 텍스트 키만 그대로 보여 준다 (스토리 대사 아님).
///
/// [표시]
/// - String Table <c>StageTip</c> 키는 <c>Assets/Docs/StageTipLines.md</c> / StageTipTranslations.md SSOT.
/// - 어떤 씬·페이즈에 어떤 키를 보여줄지는 이 스크립트가 모른다 — 각 씬 PhaseManager의
///   PhaseData.onPhaseEnter(인스펙터)가 <see cref="ShowTip"/>을 직접 호출한다
///   (DialogueUI가 각 씬에서 인스펙터로 대사를 물려 쓰는 것과 동일한 패턴).
/// - Delay 페이즈처럼 팁을 바꾸지 않아야 하는 페이즈는 onPhaseEnter에 아무것도 연결하지
///   않으면 직전 문구가 그대로 유지된다.
/// - 로케일 변경 시 같은 키를 다시 읽고, Asset Table <c>UIFont</c> / <c>TMP.Font</c>로
///   본문·제목 폰트를 바꾼다 (라틴·키릴 = Fredoka-Bold, ko/ja/zh = Noto Static).
/// - <see cref="hideOnAllPhasesComplete"/>를 켠 인스턴스(T.Boss)는 전 Phase 완료 시 숨는다.
///   Host = PhaseManager.OnPhaseDisplayChanged + AllPhasesComplete,
///   Client = StageNetworkState.OnAllPhasesCompleteClientPulse (펄스는 Host에서 발동하지 않음).
///   그 외 씬은 보통 스테이지 전환으로 사라지므로 꺼둔다.
///
/// NGO 쓰기 없음. 표시만.
/// </summary>
public class TipUI : MonoBehaviour
{
    public const string TableName = "StageTip";
    public const string FontTableName = "UIFont";
    public const string FontEntryKey = "TMP.Font";

    [Header("연결")]
    [Tooltip("Tip_Panel/Txt.Tip")]
    [SerializeField] TextMeshProUGUI bodyText;
    [Tooltip("Txt.Tip/Txt.TipTitle — 비우면 자식에서 찾는다.")]
    [SerializeField] TextMeshProUGUI titleText;

    [Header("전 Phase 완료 시")]
    [Tooltip("전 Phase 완료 시 팁을 숨길지 (Host·Client 모두).\n" +
             "완료 후에도 씬이 남아 연출이 이어지는 경우만 체크 (예: T.Boss Bossdown).")]
    [SerializeField] bool hideOnAllPhasesComplete = false;

    readonly LocalizedString _query = new LocalizedString { TableReference = TableName };

    Image _bgImage;
    bool _locReady;
    bool _subscribedCompletePulse;
    bool _subscribedPhase;
    string _currentKey;
    Coroutine _waitSns;
    Coroutine _waitPhase;

    void Awake()
    {
        if (bodyText == null)
        {
            Transform child = transform.Find("Txt.Tip");
            if (child != null)
                bodyText = child.GetComponent<TextMeshProUGUI>();
        }

        if (titleText == null && bodyText != null)
        {
            Transform title = bodyText.transform.Find("Txt.TipTitle");
            if (title != null)
                titleText = title.GetComponent<TextMeshProUGUI>();
        }

        TryGetComponent(out _bgImage);
        SetVisible(false);
    }

    void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        if (hideOnAllPhasesComplete)
        {
            TrySubscribeCompletePulse();
            TrySubscribePhase();
        }
    }

    void Start()
    {
        StartCoroutine(WaitLocalizationThenApply());
    }

    void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        UnsubscribeCompletePulse();
        UnsubscribePhase();
        if (_waitSns != null)
        {
            StopCoroutine(_waitSns);
            _waitSns = null;
        }
        if (_waitPhase != null)
        {
            StopCoroutine(_waitPhase);
            _waitPhase = null;
        }
    }

    IEnumerator WaitLocalizationThenApply()
    {
        yield return LocalizationSettings.InitializationOperation;
        _locReady = true;
        if (!isActiveAndEnabled) yield break;
        ApplyLocaleFont();
        if (!string.IsNullOrEmpty(_currentKey))
            ApplyKey(_currentKey);
    }

    void OnSelectedLocaleChanged(Locale _)
    {
        if (!_locReady) return;
        ApplyLocaleFont();
        if (!string.IsNullOrEmpty(_currentKey))
            ApplyKey(_currentKey);
    }

    // ── 외부 호출 (PhaseData.onPhaseEnter 등에서 인스펙터로 연결) ────────

    /// <summary>이 키로 팁을 표시. 씬의 PhaseData.onPhaseEnter에 인스펙터로 연결해서 쓴다.</summary>
    public void ShowTip(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        SetVisible(true);
        if (_locReady) ApplyLocaleFont();
        ApplyKey(key);
    }

    /// <summary>팁을 숨긴다. 완료 후에도 씬이 남는 연출 등에서 인스펙터로 연결.</summary>
    public void HideTip()
    {
        _currentKey = null;
        SetVisible(false);
    }

    // ── 구독 ──────────────────────────────────────────────────────

    void TrySubscribeCompletePulse()
    {
        if (StageNetworkState.Instance != null)
        {
            SubscribeCompletePulse();
            return;
        }
        if (_waitSns != null) return;
        _waitSns = StartCoroutine(WaitAndSubscribeCompletePulse());
    }

    IEnumerator WaitAndSubscribeCompletePulse()
    {
        while (StageNetworkState.Instance == null)
            yield return null;
        _waitSns = null;
        if (isActiveAndEnabled) SubscribeCompletePulse();
    }

    void SubscribeCompletePulse()
    {
        var state = StageNetworkState.Instance;
        if (state == null || _subscribedCompletePulse) return;
        state.OnAllPhasesCompleteClientPulse -= OnAllPhasesCompleteClient;
        state.OnAllPhasesCompleteClientPulse += OnAllPhasesCompleteClient;
        _subscribedCompletePulse = true;
    }

    void UnsubscribeCompletePulse()
    {
        if (!_subscribedCompletePulse) return;
        if (StageNetworkState.Instance != null)
            StageNetworkState.Instance.OnAllPhasesCompleteClientPulse -= OnAllPhasesCompleteClient;
        _subscribedCompletePulse = false;
    }

    void OnAllPhasesCompleteClient()
    {
        HideTip();
    }

    void TrySubscribePhase()
    {
        if (PhaseManager.Instance != null)
        {
            SubscribePhase();
            return;
        }
        if (_waitPhase != null) return;
        _waitPhase = StartCoroutine(WaitAndSubscribePhase());
    }

    IEnumerator WaitAndSubscribePhase()
    {
        while (PhaseManager.Instance == null)
            yield return null;
        _waitPhase = null;
        if (isActiveAndEnabled) SubscribePhase();
    }

    void SubscribePhase()
    {
        var pm = PhaseManager.Instance;
        if (pm == null || _subscribedPhase) return;
        pm.OnPhaseDisplayChanged += OnPhaseDisplayChanged;
        _subscribedPhase = true;
        OnPhaseDisplayChanged();
    }

    void UnsubscribePhase()
    {
        if (!_subscribedPhase) return;
        if (PhaseManager.Instance != null)
            PhaseManager.Instance.OnPhaseDisplayChanged -= OnPhaseDisplayChanged;
        _subscribedPhase = false;
    }

    // Host 레인: onAllPhasesComplete 직후 PhaseComplete()가 이 이벤트를 발동한다.
    void OnPhaseDisplayChanged()
    {
        var pm = PhaseManager.Instance;
        if (pm != null && pm.AllPhasesComplete) HideTip();
    }

    // ── 표시 ──────────────────────────────────────────────────────

    void ApplyKey(string key)
    {
        _currentKey = key;
        if (bodyText == null) return;
        bodyText.text = ResolveText(key);
    }

    void ApplyLocaleFont()
    {
        TMP_FontAsset font = null;
        try
        {
            font = LocalizationSettings.AssetDatabase.GetLocalizedAsset<TMP_FontAsset>(
                FontTableName, FontEntryKey);
        }
        catch (System.Exception)
        {
            return;
        }

        if (font == null) return;
        if (bodyText != null) bodyText.font = font;
        if (titleText != null) titleText.font = font;
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
                    return WithLineBullets(value);
            }
        }
        catch (System.Exception)
        {
            // 테이블 미생성·미로드 시 한국어 폴백
        }

        return KoreanFallback.TryGetValue(key, out string fallback)
            ? WithLineBullets(fallback)
            : key;
    }

    /// <summary>
    /// 비어 있지 않은 줄 앞에 <c>•</c> 을 붙인다. 이미 있으면 그대로.
    /// T.Boss 사탕 줄 앞 빈 줄에는 점을 넣지 않는다.
    /// </summary>
    public static string WithLineBullets(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd('\r');
            if (line.Length == 0)
            {
                lines[i] = line;
                continue;
            }

            if (line.StartsWith("• ", System.StringComparison.Ordinal) ||
                line.StartsWith("•", System.StringComparison.Ordinal))
                lines[i] = line;
            else
                lines[i] = "• " + line;
        }

        return string.Join("\n", lines);
    }

    void SetVisible(bool visible)
    {
        if (_bgImage == null)
            TryGetComponent(out _bgImage);
        if (_bgImage != null)
            _bgImage.enabled = visible;
        if (bodyText != null)
            bodyText.gameObject.SetActive(visible);
    }

    static readonly Dictionary<string, string> KoreanFallback = new Dictionary<string, string>
    {
        { "Tip.M.Stage1", "한 번에 한 색의 입만 올라옵니다.\n흑백 발판은 누구나 색을 맞춰 밟을 수 있습니다.\n상단에 \"TEAMCHEER\" 경고가 뜨면 팀 응원 이름을 외치세요." },
        { "Tip.M.Stage2.1", "지정된 색은 그 구역에 반드시 들어가야 합니다.\n상단에 \"TEAMCHEER\" 경고가 뜨면 팀 응원 이름을 외치세요." },
        { "Tip.M.Stage2.2", "Ctrl로 흑/백 바닥 색과 캐릭터 색을 맞춰 조준을 피하세요." },
        { "Tip.M.Stage3", "타일을 2초 동안 밟아야 점수가 올라갑니다.\n고유색 타일은 그 색만, 흑백 타일은 누구든 밟을 수 있습니다." },
        { "Tip.M.Stage4.1", "자기 색이 뜨면 Space를 누르세요.\n흰색은 아무나 눌러도 되고, 검은색은 1초 뒤 자동으로 넘어갑니다.\n미니게임 중에는 Space 버프를 쓸 수 없습니다." },
        { "Tip.M.Stage4.2", "한 칸 앞의 바닥만 보여 줍니다.\n누를 칸을 미리 외워 두세요." },
        { "Tip.M.Stage4.3", "Ctrl로 바닥 색과 캐릭터 색을 맞춰 조준을 피하세요.\n\"TEAMCHEER\" 경고가 뜰 때 팀 응원 이름을 외치면 바닥이 복구됩니다.\n이미 부서진 뒤에는 다음 경고까지 버티세요." },
        { "Tip.M.Stage5", "고유색 칸이 나오면 그 칸 위에 서야 합니다.\n고유색이 없으면 흑백 칸 위에서 버티세요.\n바닥 색에 맞춰 캐릭터 색도 바꾸세요." },
        { "Tip.T.Stage1", "내 색이 뜬 양옆 벽에 부딪히면 벽이 뒤로 물러납니다." },
        { "Tip.T.Stage2.1", "길을 외워 두세요." },
        { "Tip.T.Stage2.2", "자기 색 칸만 밟으세요.\n칸 색이 맞아도 캐릭터가 흑백이면 안 됩니다." },
        { "Tip.T.Stage2.3", "담당 색이 먼저 지나가야 다른 팀원도 그 바닥을 밟을 수 있습니다." },
        { "Tip.T.Stage3", "양옆 벽에 색을 맞춰 부딪히면 벽이 뒤로 물러납니다." },
        { "Tip.T.Stage4", "앞뒤 벽과 부종에 닿으면 튕겨 나갑니다." },
        { "Tip.T.Stage5.1", "어디서 패드를 밟든, 문을 열고 닫을 수 있습니다.\n흑색 문이 열리면 백색 문이 닫히고, 백색 문이 열리면 흑색 문이 닫힙니다.\n2층으로 가는 길을 먼저 찾으세요." },
        { "Tip.T.Boss.1", "지정된 색이 길을 연 뒤 목표까지 도달하세요.\n\n사탕이 땅에 닿기 전에 이 구간을 끝내세요." },
        { "Tip.T.Boss.2", "발판을 눌러 길을 만들고 목표까지 도달하세요.\n\n사탕이 땅에 닿기 전에 이 구간을 끝내세요." },
        { "Tip.T.Boss.3", "가운데 발판을 밟아 튕겨 올라가 사탕과 색을 맞춰 부딪히세요.\n\n사탕이 땅에 닿기 전에 이 구간을 끝내세요." },
        { "Tip.T.Boss.4", "색을 맞춰 벽에 부딪혀 밀어내세요." },
    };
}
