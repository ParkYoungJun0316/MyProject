using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Tutorial·Interlude 모이는 곳(TutorialGatherZone)의 Start 간판을 월드에 표시한다.
///
///  [대기]       Start 간판. 모이는 곳 자체는 도착 지점과 같은 발판(ZonePadVisual)이라 인원 표시는 하지 않는다.
///  [카운트다운] 전원이 모이면 간판 글씨를 숨기고 큰 숫자 3·2·1. 숫자가 바뀔 때마다 countdownSfx 1회.
///               누가 나가서 리셋되면 다시 Start 글씨.
///
/// 판정·카운트다운 진행은 TutorialNetworkManager(Tutorial) / InterludeNetworkManager(Interlude)가 Host에서
/// 소유 — 여기선 이벤트 구독과 표시만 한다. 둘 중 씬에 있는 쪽 하나만 연결한다(이벤트 3종이 동일).
/// </summary>
public class TutorialGatherDisplay : MonoBehaviour
{
    [Tooltip("Tutorial 씬: TutorialNetworkManager. Interlude 씬에선 비워 둔다.")]
    [FormerlySerializedAs("gate")]
    [SerializeField] TutorialNetworkManager tutorialGate;
    [Tooltip("Interlude 씬: InterludeNetworkManager. Tutorial 씬에선 비워 둔다.")]
    [SerializeField] InterludeNetworkManager interludeGate;

    [Header("간판")]
    [Tooltip("Start 글씨 판. 카운트다운 동안 숨김.")]
    [SerializeField] Renderer signLabel;
    [Tooltip("카운트다운 숫자(TextMeshPro 3D). 카운트다운 동안만 보임.")]
    [SerializeField] TMP_Text countdownText;

    [Header("SFX")]
    [Tooltip("3·2·1 숫자가 바뀔 때마다 1회.")]
    [SerializeField] SFXId countdownSfx = SFXId.Minigame_CountdownTick;

    [Header("팀 구호 게이트 안내 (CheerSystemDesign.md §14.2)")]
    [Tooltip("Start 간판 위 안내판(TextMeshPro 3D) — 막힘 사유 한 줄 + 사람별 상태 목록. 막힘 없으면 숨김. 비워도 동작.")]
    [SerializeField] TMP_Text gateBlockText;
    [SerializeField] UnityEngine.Localization.LocalizedString blockHostSound;   // "호스트가 팀 구호를 녹음해야 시작할 수 있어요"
    [SerializeField] UnityEngine.Localization.LocalizedString blockPractice;    // "모두 팀 구호 연습을 통과해야 시작해요 ({0}/{1})"
    [SerializeField] UnityEngine.Localization.LocalizedString headerRejected;   // "아직 준비 안 된 사람이 있어요"
    [SerializeField] UnityEngine.Localization.LocalizedString rowReady;         // "준비 완료"
    [SerializeField] UnityEngine.Localization.LocalizedString rowNeedRec12;     // "1·2번 녹음 필요"
    [SerializeField] UnityEngine.Localization.LocalizedString rowNeedRec1;      // "1번 녹음 필요"
    [SerializeField] UnityEngine.Localization.LocalizedString rowNeedRec2;      // "2번 녹음 필요"
    [SerializeField] UnityEngine.Localization.LocalizedString rowNeedPractice;  // "연습 필요"
    [SerializeField] UnityEngine.Localization.LocalizedString rowTKey;          // "T키"
    [Tooltip("연습 거절 때 준비 안 된 줄을 깜빡이는 시간(초).")]
    [SerializeField] float rejectFlashSeconds = 3f;

    // 간판 판(SignBG)이 각도·조명에 따라 어둡게 보여 밝은 글자색을 쓴다(10/7 렌더 확인)
    const string ColReady = "#7CF29A", ColPractice = "#FFFFFF", ColNotReady = "#FF6B6B";

    float _maxTick;
    int _digit;
    TutorialNetworkManager _boundTutorial;
    InterludeNetworkManager _boundInterlude;
    CheerService _boundSvc;
    float _rejectUntil = -1f;
    bool _flashOn;
    float _nextFlash;

    void Awake()
    {
        if (tutorialGate != null)
        {
            tutorialGate.OnGateCountdownTick.AddListener(HandleTick);
            tutorialGate.OnGateCountdownReset.AddListener(HandleReset);
            tutorialGate.OnGateCountdownComplete.AddListener(HandleReset);
        }
        if (interludeGate != null)
        {
            interludeGate.OnGateCountdownTick.AddListener(HandleTick);
            interludeGate.OnGateCountdownReset.AddListener(HandleReset);
            interludeGate.OnGateCountdownComplete.AddListener(HandleReset);
        }
        // 안내판은 코드가 글자를 직접 만들어서 다른 보드(LocalizeStringEvent)처럼 언어 변경을 스스로 못 따라간다(10/8)
        UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
    }

    void OnDestroy()
    {
        if (tutorialGate != null)
        {
            tutorialGate.OnGateCountdownTick.RemoveListener(HandleTick);
            tutorialGate.OnGateCountdownReset.RemoveListener(HandleReset);
            tutorialGate.OnGateCountdownComplete.RemoveListener(HandleReset);
        }
        if (interludeGate != null)
        {
            interludeGate.OnGateCountdownTick.RemoveListener(HandleTick);
            interludeGate.OnGateCountdownReset.RemoveListener(HandleReset);
            interludeGate.OnGateCountdownComplete.RemoveListener(HandleReset);
        }
        UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
        Unbind();
    }

    void HandleLocaleChanged(UnityEngine.Localization.Locale _) => RefreshBlockText();

    void Start()
    {
        HandleReset();
        RefreshBlockText();
    }

    void Update()
    {
        // 게이트 매니저·CheerService는 스폰 시점이 늦을 수 있어 Update에서 붙는다(같은 객체면 아무 일 없음).
        Bind();

        // 거절 깜빡임 — 0.25초마다 준비 안 된 줄만 켰다 껐다
        if (_rejectUntil > 0f)
        {
            if (Time.time >= _rejectUntil) { _rejectUntil = -1f; _flashOn = false; RefreshBlockText(); }
            else if (Time.time >= _nextFlash) { _nextFlash = Time.time + 0.25f; _flashOn = !_flashOn; RefreshBlockText(); }
        }
    }

    void Bind()
    {
        if (tutorialGate != null && tutorialGate.IsSpawned && !ReferenceEquals(_boundTutorial, tutorialGate))
        {
            _boundTutorial = tutorialGate;
            tutorialGate.OnGateBlockChanged += HandleBlockChanged;
            RefreshBlockText();
        }
        if (interludeGate != null && interludeGate.IsSpawned && !ReferenceEquals(_boundInterlude, interludeGate))
        {
            _boundInterlude = interludeGate;
            interludeGate.OnGateBlockChanged += HandleBlockChanged;
            RefreshBlockText();
        }
        var svc = CheerService.Instance;
        if (!ReferenceEquals(svc, _boundSvc))
        {
            if (!ReferenceEquals(_boundSvc, null))
            {
                _boundSvc.OnReadyListChanged -= RefreshBlockText;
                _boundSvc.OnPracticeRejected -= HandleRejected;
                _boundSvc.OnPracticePassedCountChanged -= HandlePassedCount;
            }
            _boundSvc = svc;
            if (svc != null)
            {
                svc.OnReadyListChanged += RefreshBlockText;
                svc.OnPracticeRejected += HandleRejected;
                svc.OnPracticePassedCountChanged += HandlePassedCount;
            }
            RefreshBlockText();
        }
    }

    void Unbind()
    {
        if (!ReferenceEquals(_boundTutorial, null)) _boundTutorial.OnGateBlockChanged -= HandleBlockChanged;
        if (!ReferenceEquals(_boundInterlude, null)) _boundInterlude.OnGateBlockChanged -= HandleBlockChanged;
        if (!ReferenceEquals(_boundSvc, null))
        {
            _boundSvc.OnReadyListChanged -= RefreshBlockText;
            _boundSvc.OnPracticeRejected -= HandleRejected;
            _boundSvc.OnPracticePassedCountChanged -= HandlePassedCount;
        }
    }

    void HandleBlockChanged(TutorialNetworkManager.GateBlock _) => RefreshBlockText();
    void HandleBlockChanged(InterludeNetworkManager.GateBlock _) => RefreshBlockText();
    void HandlePassedCount(int _) => RefreshBlockText();

    void HandleRejected()
    {
        _rejectUntil = Time.time + rejectFlashSeconds;
        _flashOn = true;
        _nextFlash = Time.time + 0.25f;
        RefreshBlockText();
    }

    /// <summary>
    /// 간판 위 안내판 — 맨 위 한 줄(막힘 사유 또는 "아직 준비 안 된 사람이 있어요") + 사람별 줄.
    /// 줄 색: 준비 완료 초록 / 연습만 남음 진한 자주 / 준비 안 됨(녹음 필요) 빨강. 거절 직후엔 빨간 줄이 깜빡인다.
    /// </summary>
    void RefreshBlockText()
    {
        if (gateBlockText == null) return;

        int block = tutorialGate != null ? (int)tutorialGate.CurrentGateBlock
                  : interludeGate != null ? (int)interludeGate.CurrentGateBlock : 0;
        bool rejecting = _rejectUntil > 0f;
        if (block == 0 && !rejecting)
        {
            if (gateBlockText.gameObject.activeSelf) gateBlockText.gameObject.SetActive(false);
            return;
        }

        var svc = CheerService.Instance;
        var sb = new System.Text.StringBuilder();
        if (rejecting)
            sb.Append("<color=").Append(ColNotReady).Append('>')
              .Append(Localized(headerRejected, null, "아직 준비 안 된 사람이 있어요")).Append("</color>");
        else if (block == 1)
            sb.Append(Localized(blockHostSound, null, "호스트가 팀 구호를 녹음해야 시작할 수 있어요"));
        else
        {
            int passed = svc != null ? svc.PracticePassedCount : 0;
            int total = PlayerSpawnCoordinator.EntryCount;
            sb.Append(Localized(blockPractice, new object[] { passed, total },
                                $"모두 팀 구호 연습을 통과해야 시작해요 ({passed}/{total})"));
        }

        if (svc != null)
        {
            bool teamNoMic = svc.TeamNoMic;
            string tKey = Localized(rowTKey, null, "T키");
            for (int i = 0; i < svc.ReadyCount; i++)
            {
                var e = svc.GetReadyEntry(i);
                bool ready = CheerSoundLocalState.IsReadyFlags(e.Flags, teamNoMic);
                bool noMic = teamNoMic || (e.Flags & 4) != 0;
                string status;
                string color;
                if (!ready)
                {
                    bool has1 = (e.Flags & 1) != 0, has2 = (e.Flags & 2) != 0;
                    status = has1 ? Localized(rowNeedRec2, null, "2번 녹음 필요")
                           : has2 ? Localized(rowNeedRec1, null, "1번 녹음 필요")
                           : Localized(rowNeedRec12, null, "1·2번 녹음 필요");
                    color = ColNotReady;
                }
                else if (e.Passed == 0)
                {
                    status = Localized(rowNeedPractice, null, "연습 필요");
                    color = ColPractice;
                }
                else
                {
                    status = Localized(rowReady, null, "준비 완료");
                    color = ColReady;
                }
                if (noMic) status = tKey + " · " + status;

                sb.Append('\n');
                bool dim = rejecting && !ready && !_flashOn;
                string alphaHex = dim ? "30" : "FF";
                // 이름은 다른 HUD(TeamStatus·이름표·채팅)와 같이 대문자(ToUpperInvariant — 터키어 i→İ 방지),
                // 색은 자기 캐릭터 계열의 밝은 톤(PlayerColorUtil.GetNameColorOnDark — 간판이 어두워서). 상태 문구만 준비 상태 색.
                sb.Append("<color=#").Append(NameColorHex(e.ColorIndex)).Append(alphaHex).Append('>')
                  .Append(CheerService.GetCheerName(e.ColorIndex).ToUpperInvariant()).Append("</color>  ");
                sb.Append("<color=").Append(color).Append('>');
                if (dim) sb.Append("<alpha=#30>");
                sb.Append(status);
                sb.Append("</color>");
            }
        }

        if (!gateBlockText.gameObject.activeSelf) gateBlockText.gameObject.SetActive(true);
        gateBlockText.text = sb.ToString();
    }

    static string NameColorHex(int colorIndex) =>
        ColorUtility.ToHtmlStringRGB(PlayerColorUtil.GetNameColorOnDark(colorIndex));

    static string Localized(UnityEngine.Localization.LocalizedString ls, object[] args, string fallback)
    {
        if (ls == null || ls.IsEmpty) return fallback;
        string s = args == null ? ls.GetLocalizedString() : ls.GetLocalizedString(args);
        return string.IsNullOrEmpty(s) ? fallback : s;
    }

    // ── 카운트다운 ───────────────────────────────────────────────

    /// <summary>대기 중에도 duration 값으로 Tick이 오므로(TutorialNetworkManager), 지금까지 본 최댓값보다
    /// 줄어든 Tick부터 카운트다운으로 본다.</summary>
    void HandleTick(float remaining)
    {
        if (remaining > _maxTick) _maxTick = remaining;
        if (remaining >= _maxTick - 0.001f || remaining <= 0f) return;

        int digit = Mathf.CeilToInt(remaining);
        if (digit == _digit) return;
        _digit = digit;

        if (signLabel != null) signLabel.enabled = false;
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = digit.ToString();
        }
        if (SFXManager.Instance != null) SFXManager.Instance.Play(countdownSfx);
    }

    void HandleReset()
    {
        _digit = 0;
        if (signLabel != null) signLabel.enabled = true;
        if (countdownText != null) countdownText.gameObject.SetActive(false);
    }
}
