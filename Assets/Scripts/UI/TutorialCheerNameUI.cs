using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.UI;

/// <summary>
/// 팀 구호 **소리** 패널 — CheerSystemDesign.md §14.2 (2026-10-07, 구 TeamCheerWord 텍스트 입력 패널 교체).
/// 클래스 이름(TutorialCheerNameUI)은 씬·프리팹 연결 호환용으로 유지.
///
/// [한 패널에서 전부 — 사용자 결정 2026-10-07]
///   Host:  [녹음/정지] → 자동 재생 → [확정]   (= 기준 소리. 확정하면 Host 자신의 나 1이 된다)
///   전원:  [호스트 소리 듣기] → [1번 녹음/정지] → Host 틀 검사 → [2번 녹음/정지] → Host 틀 검사
///   (10/7 사용자 결정: 기준은 Host 하나 — 1·2번은 서로 비교하지 않는다. 게임 중 판정은 1·2번 중 가까운 쪽, R키는 내 1번 녹음)
///   게임에서 쓰는 등록본은 전부 Host 틀 검사를 통과한 것뿐(등록본 규칙). 거절되면 이유를 보여 주고 될 때까지 다시.
///   구역 3 연습 표지판은 이 등록본으로 **실시간 판정**만 한다(여기서 나 2를 만들지 않는다).
///
/// [녹음]
///   마이크를 직접 열지 않고 CheerKeywordEngine.Local(내 캐릭터의 엔진)의 BeginCapture/EndCapture로 같은 16kHz
///   스트림을 받는다(§4.3 이중 오픈 금지). 버튼 한 개 토글: 누르면 바로 녹음, 다시 누르면 정지.
///   1~3초 강제(10/7): RecordMinSec(1초) 전엔 정지 버튼이 잠기고, RecordMaxSec(3초)에 자동 정지.
///
/// [배치] Tutorial·Interlude 상시 HUD Canvas 자식(씬에 1개). 표지판(TutorialCheerNameSignboard)이 Open()/Close().
///   씬에는 기본 비활성으로 배치(사용자 에디터 작업). 입력 우선권·커서 공유 규칙은 이전과 동일(아래 Update/OnEnable).
/// </summary>
public class TutorialCheerNameUI : MonoBehaviour
{
    enum Target { None, Host, Mine1, Mine2 }

    [Header("닫기")]
    [Tooltip("비워도 됨 — 상호작용 표지판에서 다시 상호작용해도 닫힘(토글).")]
    [SerializeField] Button closeButton;

    [Header("표시")]
    [SerializeField] TMP_Text feedbackText;
    [SerializeField] float feedbackDisplaySeconds = 3f;

    [Header("Host 전용 섹션 (비-Host에선 통째로 숨김)")]
    [SerializeField] GameObject hostSection;
    [Tooltip("누르면 녹음 시작, 다시 누르면 정지(라벨이 바뀜).")]
    [SerializeField] Button hostRecordButton;
    [SerializeField] TMP_Text hostRecordButtonLabel;
    [Tooltip("방금 녹음한 소리 다시 듣기(확정 전).")]
    [SerializeField] Button hostPreviewButton;
    [SerializeField] Button hostConfirmButton;
    [SerializeField] TMP_Text hostStatusText;
    [Tooltip("Host 전용 \"마이크 없음 — 팀 전체 T키로 응원\" 토글(10/7). 켜면 이번 판은 음성 응원 없이 전원 T키.")]
    [SerializeField] Toggle teamNoMicToggle;

    [Header("전원 섹션")]
    [Tooltip("Host가 확정한 기준 소리 재생.")]
    [SerializeField] Button listenHostButton;
    [SerializeField] TMP_Text hostSoundStatusText;
    [SerializeField] Button record1Button;
    [SerializeField] TMP_Text record1ButtonLabel;
    [SerializeField] Button record2Button;
    [SerializeField] TMP_Text record2ButtonLabel;
    [SerializeField] TMP_Text enrollStatusText;
    [Tooltip("비-Host \"마이크가 없어요 — T키로 응원\" 토글(10/7). Host에겐 숨긴다(Host는 팀 전체 토글).")]
    [SerializeField] Toggle personalNoMicToggle;
    [Tooltip("personalNoMicToggle과 라벨을 묶은 줄 — Host면 숨김.")]
    [SerializeField] GameObject personalNoMicRow;

    [Header("커서")]
    [Tooltip("패널 닫을 때 커서를 다시 잠글지 여부. ThirdPersonCamera.lockCursor 설정과 일치시키세요.")]
    [SerializeField] bool lockCursorOnClose = true;

    // ── Localization (Tutorial 테이블 — 키는 TutorialTranslations.md §CheerNamePanel에 추가 예정) ──
    // 비어 있거나 로드 전이면 한국어 폴백(OptionsMenuController.LocalizedOrFallback 패턴).

    [Header("Localization — 버튼 라벨")]
    [SerializeField] LocalizedString labelRecord;        // "● 녹음"
    [SerializeField] LocalizedString labelStop;          // "■ 정지 ({0:0.0}초)"
    [SerializeField] LocalizedString labelRecord1;       // "● 나 1 녹음"
    [SerializeField] LocalizedString labelRecord2;       // "● 나 2 녹음"

    [Header("Localization — 상태")]
    [SerializeField] LocalizedString statusHostNone;     // "호스트가 아직 팀 구호를 녹음하지 않았어요"
    [SerializeField] LocalizedString statusHostReady;    // "팀 구호 v{0} · {1:0.0}초"
    [SerializeField] LocalizedString statusHostPending;  // "녹음됨 — 들어보고 [확정]"
    [SerializeField] LocalizedString statusEnrollNeed1;  // "① 호스트 소리를 듣고 똑같이 '나 1'을 녹음하세요"
    [SerializeField] LocalizedString statusEnrollNeed2;  // "② 한 번 더 똑같이 '나 2'를 녹음하세요"
    [SerializeField] LocalizedString statusEnrolled;     // "등록 완료 ✓ — 이제 연습 표지판에서 외쳐 보세요"
    [SerializeField] LocalizedString statusInvalidated;  // "호스트가 다시 녹음했어요 — 다시 등록하세요"
    [SerializeField] LocalizedString statusTeamNoMic;    // "이번 판은 마이크 없이 T키로 응원해요"
    [SerializeField] LocalizedString statusPersonalNoMic; // "T키로 응원해요. 연습 때 T키를 누르세요"

    [Header("Localization — 거절 이유")]
    [SerializeField] LocalizedString feedbackNoMic;      // "마이크 소리가 들어오지 않아요"
    [SerializeField] LocalizedString feedbackTooQuiet;   // "너무 작아요 — 크게 외쳐 주세요"
    [SerializeField] LocalizedString feedbackTooLoud;    // "너무 커요(소리가 찢어져요)"
    [SerializeField] LocalizedString feedbackTooShort;   // "너무 짧아요"
    [SerializeField] LocalizedString feedbackTooLong;    // "너무 길어요 (최대 3초)"
    [SerializeField] LocalizedString feedbackBursts;     // "끊는 횟수가 달라요 (호스트 {0}번, 나 {1}번)"
    [SerializeField] LocalizedString feedbackPitch;      // "높낮이가 달라요"
    [SerializeField] LocalizedString feedbackTimbre;     // "소리가 달라요 — 호스트 소리를 다시 듣고 따라 하세요"
    [SerializeField] LocalizedString feedbackLiveFail;   // "나 1과 다르게 들려요 — 나 1을 녹음했을 때처럼 외쳐 주세요"
    [SerializeField] LocalizedString feedbackAccepted;   // "좋아요!"
    [SerializeField] LocalizedString feedbackHostSet;    // "팀 구호 확정! 팀원들에게 전달했어요"
    [SerializeField] LocalizedString feedbackNotServer;  // "호스트만 팀 구호를 정할 수 있어요"

    /// <summary>패널이 열려있는 동안 true — Player.cs가 이동 입력을 잠그는 데 사용.</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>이번 프레임에 Esc로 이 패널이 막 닫혔는지 — EscMenuController 확인용.</summary>
    public static bool ConsumedEscThisFrame => s_escClosedFrame == Time.frameCount;
    static int s_escClosedFrame = -1;

    /// <summary>텍스트 입력이 사라져 더 이상 Enter를 쓰지 않는다 — InGameChatUI 호환용으로 항상 false.</summary>
    public static bool ConsumedEnterThisFrame => false;

    readonly CheerSoundDsp _dsp = new();
    readonly CheerSoundDsp.DtwWork _work = new();

    Target _recording = Target.None;
    float[] _hostPendingPcm;
    CheerSoundTemplate _hostPendingTemplate;
    float _feedbackHideAt = -1f;
    bool? _hostSectionVisible;

    void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (hostRecordButton != null) hostRecordButton.onClick.AddListener(() => ToggleRecord(Target.Host));
        if (hostPreviewButton != null) hostPreviewButton.onClick.AddListener(PreviewHostPending);
        if (hostConfirmButton != null) hostConfirmButton.onClick.AddListener(ConfirmHost);
        if (listenHostButton != null) listenHostButton.onClick.AddListener(CheerSoundPlayback.PlayHostClip);
        if (record1Button != null) record1Button.onClick.AddListener(() => ToggleRecord(Target.Mine1));
        if (record2Button != null) record2Button.onClick.AddListener(() => ToggleRecord(Target.Mine2));
        if (personalNoMicToggle != null) personalNoMicToggle.onValueChanged.AddListener(CheerSoundLocalState.SetPersonalNoMic);
        if (teamNoMicToggle != null) teamNoMicToggle.onValueChanged.AddListener(OnTeamNoMicToggled);
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        IsOpen = true;
        CursorUnlockRequestUtil.Request(this);
        _hostSectionVisible = null;
        CheerSoundLocalState.HostSoundChanged += RefreshAll;
        CheerSoundLocalState.EnrollmentChanged += RefreshAll;
        HideFeedback();
        RefreshAll();
    }

    void OnDisable()
    {
        IsOpen = false;
        CheerSoundLocalState.HostSoundChanged -= RefreshAll;
        CheerSoundLocalState.EnrollmentChanged -= RefreshAll;
        CancelRecording();

        if (!gameObject.scene.isLoaded)
        {
            CursorUnlockRequestUtil.Forget(this);
            return;
        }
        CursorUnlockRequestUtil.Release(this, lockCursorOnClose);
    }

    // ── 표지판에서 호출 ──────────────────────────────────────────

    public void Open()
    {
        if (gameObject.activeSelf) return;
        gameObject.SetActive(true);
    }

    public void Close()
    {
        if (!gameObject.activeSelf) return;
        gameObject.SetActive(false);
    }

    public void Toggle()
    {
        if (gameObject.activeSelf) Close();
        else Open();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            s_escClosedFrame = Time.frameCount;
            Close();
            return;
        }

        ApplyRole();
        UpdateRecording();

        if (_feedbackHideAt >= 0f && Time.time >= _feedbackHideAt && feedbackText != null)
        {
            feedbackText.gameObject.SetActive(false);
            _feedbackHideAt = -1f;
        }
    }

    // ── 녹음 ────────────────────────────────────────────────────

    void ToggleRecord(Target target)
    {
        var engine = CheerKeywordEngine.Local;
        if (engine == null) { ShowFeedback(L(feedbackNoMic, "마이크 소리가 들어오지 않아요")); return; }

        if (_recording == target)
        {
            if (engine.CaptureSeconds < CheerSoundParams.RecordMinSec) return; // 1초 전엔 정지 불가
            FinishRecording(engine.EndCapture());
            return;
        }
        if (_recording != Target.None) return; // 다른 녹음 진행 중

        if (CheerSoundLocalState.TeamNoMic) return; // 이번 판은 T키 응원 — 녹음 없음
        if (target == Target.Host && !IsLocalServer()) { ShowFeedback(L(feedbackNotServer, "호스트만 팀 구호를 정할 수 있어요")); return; }
        if (target == Target.Mine1 && !CheerSoundLocalState.HasHostSound) { ShowFeedback(L(statusHostNone, "호스트가 아직 팀 구호를 녹음하지 않았어요")); return; }
        if (target == Target.Mine2 && !CheerSoundLocalState.HasHostSound) { ShowFeedback(L(statusHostNone, "호스트가 아직 팀 구호를 녹음하지 않았어요")); return; }

        CheerSoundPlayback.Stop();
        if (!engine.BeginCapture()) { ShowFeedback(L(feedbackNoMic, "마이크 소리가 들어오지 않아요")); return; }
        _recording = target;
        HideFeedback();
        RefreshButtons();
    }

    void UpdateRecording()
    {
        if (_recording == Target.None) return;
        var engine = CheerKeywordEngine.Local;
        if (engine == null) { CancelRecording(); return; }

        if (engine.CaptureHitLimit) { FinishRecording(engine.EndCapture()); return; }
        RefreshButtons(); // 경과 초 라벨
    }

    void CancelRecording()
    {
        if (_recording == Target.None) return;
        CheerKeywordEngine.Local?.EndCapture();
        _recording = Target.None;
    }

    void FinishRecording(float[] pcm)
    {
        var target = _recording;
        _recording = Target.None;
        if (pcm == null || pcm.Length == 0) { ShowFeedback(L(feedbackTooShort, "너무 짧아요")); RefreshAll(); return; }

        var tmpl = CheerSoundTemplate.Build(pcm, pcm.Length, _dsp);
        switch (target)
        {
            case Target.Host:  ProcessHost(pcm, tmpl); break;
            case Target.Mine1: ProcessMine(pcm, tmpl, first: true); break;
            case Target.Mine2: ProcessMine(pcm, tmpl, first: false); break;
        }
        RefreshAll();
    }

    void ProcessHost(float[] pcm, CheerSoundTemplate tmpl)
    {
        if (tmpl.Issue != CheerClipIssue.None)
        {
            ShowFeedback(IssueText(tmpl.Issue));
            return;
        }
        _hostPendingPcm = pcm;
        _hostPendingTemplate = tmpl;
        CheerSoundPlayback.PlayPcm(Slice(pcm, tmpl.TrimStartSample, tmpl.TrimEndSample), CheerSoundParams.SampleRate);
    }

    void PreviewHostPending()
    {
        if (_hostPendingPcm == null || _hostPendingTemplate == null) return;
        CheerSoundPlayback.PlayPcm(Slice(_hostPendingPcm, _hostPendingTemplate.TrimStartSample, _hostPendingTemplate.TrimEndSample),
                                   CheerSoundParams.SampleRate);
    }

    void ConfirmHost()
    {
        if (_hostPendingPcm == null || _hostPendingTemplate == null) return;
        var svc = CheerService.Instance;
        if (svc == null || !svc.IsSpawned || !IsLocalServer())
        {
            ShowFeedback(L(feedbackNotServer, "호스트만 팀 구호를 정할 수 있어요"));
            return;
        }
        if (!svc.TrySetTeamCheerSound(_hostPendingPcm, _hostPendingPcm.Length, _hostPendingTemplate, out var issue))
        {
            ShowFeedback(IssueText(issue));
            return;
        }
        _hostPendingPcm = null;
        _hostPendingTemplate = null;
        ShowFeedback(L(feedbackHostSet, "팀 구호 확정! 팀원들에게 전달했어요"));
    }

    /// <summary>1번·2번 모두 Host 틀 검사만(10/7 — 기준은 Host 하나). 1번은 R키로 들려줄 소리도 보관.</summary>
    void ProcessMine(float[] pcm, CheerSoundTemplate tmpl, bool first)
    {
        var host = CheerSoundLocalState.HostTemplate;
        if (host == null) { ShowFeedback(L(statusHostNone, "호스트가 아직 팀 구호를 녹음하지 않았어요")); return; }

        var r = CheerSoundShapeCheck.Compare(tmpl, host, _work);
        NetLog.Transition("TutorialCheerNameUI", first ? "Enroll1Shape" : "Enroll2Shape",
            $"verdict={r.Verdict} dur={r.DurationRatio:0.00} bursts={r.MyBursts}/{r.HostBursts} pitch={r.PitchCorr:0.00}/{r.PitchMeanAbsSemi:0.0} timbre={r.TimbreDistance:0.00} soft={r.SoftCount}");
        if (r.Verdict != CheerShapeVerdict.Ok)
        {
            ShowFeedback(VerdictText(r));
            return;
        }

        if (first) CheerSoundLocalState.SetTemplate1(tmpl, Slice(pcm, tmpl.TrimStartSample, tmpl.TrimEndSample));
        else       CheerSoundLocalState.SetTemplate2(tmpl);
        ShowFeedback(L(feedbackAccepted, "좋아요!"));
    }

    // ── 표시 ────────────────────────────────────────────────────

    void ApplyRole()
    {
        bool isServer = IsLocalServer();
        if (_hostSectionVisible == isServer) return;
        _hostSectionVisible = isServer;
        if (hostSection != null) hostSection.SetActive(isServer);
        if (personalNoMicRow != null) personalNoMicRow.SetActive(!isServer);
    }

    void OnTeamNoMicToggled(bool on)
    {
        var svc = CheerService.Instance;
        if (svc == null || !svc.IsSpawned || !IsLocalServer()) { RefreshAll(); return; }
        CancelRecording();
        svc.SetTeamNoMic(on);
        RefreshAll();
    }

    void RefreshAll()
    {
        if (personalNoMicToggle != null)
        {
            personalNoMicToggle.SetIsOnWithoutNotify(CheerSoundLocalState.PersonalNoMic);
            personalNoMicToggle.interactable = !CheerSoundLocalState.TeamNoMic;
        }
        if (teamNoMicToggle != null) teamNoMicToggle.SetIsOnWithoutNotify(CheerSoundLocalState.TeamNoMic);
        RefreshButtons();
        RefreshStatus();
    }

    void RefreshButtons()
    {
        var engine = CheerKeywordEngine.Local;
        bool micOk = engine != null && engine.HasAudioInput && !CheerSoundLocalState.TeamNoMic;
        bool idle = _recording == Target.None;
        float sec = engine != null ? engine.CaptureSeconds : 0f;
        bool canStop = sec >= CheerSoundParams.RecordMinSec;

        SetButton(hostRecordButton, hostRecordButtonLabel,
            micOk && (idle || (_recording == Target.Host && canStop)),
            _recording == Target.Host ? Stop(sec) : L(labelRecord, "● 녹음"));
        bool voiceOn = !CheerSoundLocalState.TeamNoMic;
        if (hostPreviewButton != null) hostPreviewButton.interactable = voiceOn && idle && _hostPendingPcm != null;
        if (hostConfirmButton != null) hostConfirmButton.interactable = voiceOn && idle && _hostPendingPcm != null && CheerServiceReady();

        if (listenHostButton != null) listenHostButton.interactable = voiceOn && idle && CheerSoundLocalState.HasHostSound;
        SetButton(record1Button, record1ButtonLabel,
            micOk && CheerSoundLocalState.HasHostSound && (idle || (_recording == Target.Mine1 && canStop)),
            _recording == Target.Mine1 ? Stop(sec) : L(labelRecord1, "1번 녹음"));
        SetButton(record2Button, record2ButtonLabel,
            micOk && CheerSoundLocalState.HasHostSound && (idle || (_recording == Target.Mine2 && canStop)),
            _recording == Target.Mine2 ? Stop(sec) : L(labelRecord2, "2번 녹음"));
    }

    string Stop(float sec)
    {
        if (labelStop != null && !labelStop.IsEmpty)
        {
            string s = labelStop.GetLocalizedString(sec);
            if (!string.IsNullOrEmpty(s)) return s;
        }
        return $"■ 정지 ({sec:0.0}초)";
    }

    static void SetButton(Button b, TMP_Text label, bool interactable, string text)
    {
        if (b != null) b.interactable = interactable;
        if (label != null && label.text != text) label.text = text;
    }

    void RefreshStatus()
    {
        string teamNoMic = L(statusTeamNoMic, "이번 판은 마이크 없이 T키로 응원해요");
        if (hostStatusText != null)
        {
            hostStatusText.text = CheerSoundLocalState.TeamNoMic ? teamNoMic
                : _hostPendingPcm != null
                ? L(statusHostPending, "녹음됨 — 들어보고 [확정]")
                : HostSoundText();
        }
        if (hostSoundStatusText != null) hostSoundStatusText.text = CheerSoundLocalState.TeamNoMic ? teamNoMic : HostSoundText();

        if (enrollStatusText != null)
        {
            string s;
            if (CheerSoundLocalState.TeamNoMic) s = teamNoMic;
            else if (CheerSoundLocalState.PersonalNoMic && !CheerSoundLocalState.IsEnrolled)
                s = L(statusPersonalNoMic, "T키로 응원해요. 연습 때 T키를 누르세요");
            else if (!CheerSoundLocalState.HasHostSound) s = L(statusHostNone, "호스트가 아직 팀 구호를 녹음하지 않았어요");
            else if (CheerSoundLocalState.IsEnrolled) s = L(statusEnrolled, "등록 완료 ✓ — 이제 연습 표지판에서 외쳐 보세요");
            else if (CheerSoundLocalState.HasTemplate1) s = L(statusEnrollNeed2, "한 번 더 똑같이 [2번 녹음]을 하세요");
            else if (CheerSoundLocalState.HasTemplate2) s = L(statusEnrollNeed1, "호스트 소리를 듣고, 똑같이 [1번 녹음]을 하세요");
            else if (CheerSoundLocalState.MyTemplate1 != null || CheerSoundLocalState.MyTemplate2 != null)
                s = L(statusInvalidated, "호스트가 다시 녹음했어요. 다시 등록하세요");
            else s = L(statusEnrollNeed1, "호스트 소리를 듣고, 똑같이 [1번 녹음]을 하세요");
            enrollStatusText.text = s;
        }
    }

    string HostSoundText()
    {
        if (!CheerSoundLocalState.HasHostSound) return L(statusHostNone, "호스트가 아직 팀 구호를 녹음하지 않았어요");
        int v = CheerSoundLocalState.HostVersion;
        float sec = CheerSoundLocalState.HostTemplate.DurationMs / 1000f;
        if (statusHostReady != null && !statusHostReady.IsEmpty)
        {
            string s = statusHostReady.GetLocalizedString(v, sec);
            if (!string.IsNullOrEmpty(s)) return s;
        }
        return $"팀 구호 v{v} · {sec:0.0}초";
    }

    string IssueText(CheerClipIssue issue) => issue switch
    {
        CheerClipIssue.TooQuiet => L(feedbackTooQuiet, "너무 작아요 — 크게 외쳐 주세요"),
        CheerClipIssue.TooLoud  => L(feedbackTooLoud, "너무 커요(소리가 찢어져요)"),
        CheerClipIssue.TooLong  => L(feedbackTooLong, "너무 길어요 (최대 3초)"),
        _                       => L(feedbackTooShort, "너무 짧아요. 최소 0.5초 이상 소리를 내 주세요"),
    };

    string VerdictText(CheerSoundShapeCheck.Result r)
    {
        switch (r.Verdict)
        {
            case CheerShapeVerdict.TooQuiet: return L(feedbackTooQuiet, "너무 작아요 — 크게 외쳐 주세요");
            case CheerShapeVerdict.TooLoud:  return L(feedbackTooLoud, "너무 커요(소리가 찢어져요)");
            case CheerShapeVerdict.TooShort: return L(feedbackTooShort, "너무 짧아요. 최소 0.5초 이상 소리를 내 주세요");
            case CheerShapeVerdict.TooLong:  return L(feedbackTooLong, "너무 길어요 (최대 3초)");
            case CheerShapeVerdict.BurstMismatch:
                if (feedbackBursts != null && !feedbackBursts.IsEmpty)
                {
                    string s = feedbackBursts.GetLocalizedString(r.HostBursts, r.MyBursts);
                    if (!string.IsNullOrEmpty(s)) return s;
                }
                return $"끊는 횟수가 달라요 (호스트 {r.HostBursts}번, 나 {r.MyBursts}번)";
            case CheerShapeVerdict.PitchMismatch:  return L(feedbackPitch, "높낮이가 달라요");
            case CheerShapeVerdict.TimbreMismatch: return L(feedbackTimbre, "소리가 달라요 — 호스트 소리를 다시 듣고 따라 하세요");
            default: return L(feedbackAccepted, "좋아요!");
        }
    }

    static string L(LocalizedString localized, string fallback)
    {
        if (localized == null || localized.IsEmpty) return fallback;
        string value = localized.GetLocalizedString();
        return string.IsNullOrEmpty(value) ? fallback : value;
    }

    static float[] Slice(float[] a, int from, int to)
    {
        if (to <= from) return a;
        var r = new float[to - from];
        System.Array.Copy(a, from, r, 0, r.Length);
        return r;
    }

    static bool IsLocalServer()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && nm.IsServer;
    }

    static bool CheerServiceReady()
    {
        var svc = CheerService.Instance;
        return svc != null && svc.IsSpawned;
    }

    void ShowFeedback(string message)
    {
        if (feedbackText == null) return;
        feedbackText.gameObject.SetActive(true);
        feedbackText.text = message;
        _feedbackHideAt = Time.time + feedbackDisplaySeconds;
    }

    void HideFeedback()
    {
        _feedbackHideAt = -1f;
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
    }
}
