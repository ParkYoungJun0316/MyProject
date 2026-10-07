#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 팀 응원 소리 매칭(CheerSystemDesign.md §14) 실측 도구 — 게임에 붙이기 전에 녹음·틀 검사·판정을
/// 에디터에서 바로 시험하고, 임계값(T_self·T_cross 등, §14.10 #6)을 정할 숫자를 모은다.
/// Menu: Tools / Cheer Sound Lab
///
/// [슬롯]
///   Host  = Host가 녹음한 기준 소리 (틀 검사 기준)
///   나 1  = 구역 2에서 내 목소리로 등록한 것 (Host와 틀 검사)
///   나 2  = 구역 3 연습에서 통과한 외침 (2번째 등록본 — T_self 보정)
/// [실시간 판정] 나 1(+나 2)을 등록본으로 마이크를 계속 듣고 매 평가의 거리 d를 보여준다.
/// [흘려보기] 슬롯 녹음을 판정기에 그대로 넣어 d를 본다 — 다른 사람 목소리·다른 소리로 잘못 통과하는지 확인용.
///
/// Play Mode가 아니어도 동작한다(에디터 Microphone 사용). 게임 런타임은 마이크를 직접 열지 않는다(§4.3) —
/// 이 도구만 예외이고, Play Mode 중 Dissonance가 마이크를 잡고 있으면 쓰지 말 것.
/// </summary>
public class CheerSoundLab : EditorWindow
{
    const int Rate = CheerSoundParams.SampleRate;

    sealed class Slot
    {
        public string Name;
        public float[] Pcm;
        public CheerSoundTemplate Tmpl;
        /// <summary>Host 틀 검사(§14.4). Host가 없거나 녹음이 없으면 null.</summary>
        public CheerSoundShapeCheck.Result? Shape;
        /// <summary>나 2 전용 — 나 1 등록본으로 실시간 판정한 결과(§14.2 ②).</summary>
        public bool  LiveChecked, LivePass;
        public float LiveDistance;
        public CheerSoundMatcher.Eval LiveEval;

        public bool HasTemplate => Tmpl != null && Tmpl.Frames > 0 && Tmpl.Issue == CheerClipIssue.None;
        public bool ShapeOk => Shape.HasValue && Shape.Value.Verdict == CheerShapeVerdict.Ok;
    }

    readonly Slot _host = new() { Name = "Host 기준 소리" };
    readonly Slot _mine1 = new() { Name = "나 1 (등록)" };
    readonly Slot _mine2 = new() { Name = "나 2 (연습 통과분)" };

    readonly CheerSoundDsp _dsp = new();
    readonly CheerSoundDsp.DtwWork _work = new();
    readonly CheerSoundRecorder _recorder = new();
    Slot _recordingSlot;

    // 마이크
    string _device;
    AudioClip _micClip;
    int _micRate;
    int _micLastPos;
    float[] _micRead = new float[0];
    float[] _resampled = new float[0];

    // 실시간 판정
    CheerSoundMatcher _matcher;
    bool _live;
    int _liveDetections;
    readonly List<string> _evalLog = new();
    readonly List<string> _feedLog = new();
    int _lastEvalStamp;

    Vector2 _scroll;

    [MenuItem("Tools/Cheer Sound Lab")]
    static void Open()
    {
        var w = GetWindow<CheerSoundLab>("Cheer Sound Lab");
        w.minSize = new Vector2(460, 600);
    }

    void OnEnable() => EditorApplication.update += Tick;

    void OnDisable()
    {
        EditorApplication.update -= Tick;
        StopMic();
    }

    // ── GUI ─────────────────────────────────────────────────────

    void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawDevice();
        EditorGUILayout.Space(6);

        DrawSlot(_host);
        DrawSlot(_mine1);
        DrawShape("① 등록 — 틀 검사: 나 1 vs Host", _mine1);
        DrawSlot(_mine2);
        DrawShape("② 등록 — 틀 검사: 나 2 vs Host", _mine2);

        EditorGUILayout.Space(6);
        DrawEligibility();
        EditorGUILayout.Space(6);
        DrawLive();
        EditorGUILayout.Space(6);
        DrawFeedTests();

        EditorGUILayout.EndScrollView();
    }

    void DrawDevice()
    {
        string[] devices = Microphone.devices;
        if (devices.Length == 0)
        {
            EditorGUILayout.HelpBox("마이크가 없습니다.", MessageType.Warning);
            return;
        }
        int idx = Mathf.Max(0, Array.IndexOf(devices, _device));
        using (new EditorGUI.DisabledScope(_micClip != null))
            idx = EditorGUILayout.Popup("마이크", idx, devices);
        _device = devices[idx];
        EditorGUILayout.LabelField(_micClip != null ? $"마이크 켜짐 ({_micRate}Hz → 16kHz)" : "마이크 꺼짐", EditorStyles.miniLabel);
    }

    void DrawSlot(Slot slot)
    {
        EditorGUILayout.LabelField(slot.Name, EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            bool recordingThis = _recordingSlot == slot;
            using (new EditorGUI.DisabledScope(_recordingSlot != null && !recordingThis || _live))
            {
                if (GUILayout.Button(recordingThis ? $"■ 정지 ({_recorder.ElapsedSec:0.0}s)" : "● 녹음", GUILayout.Width(120)))
                {
                    if (recordingThis) StopRecording();
                    else StartRecording(slot);
                }
            }
            using (new EditorGUI.DisabledScope(slot.Pcm == null))
            {
                if (GUILayout.Button("▶ 듣기")) Play(slot.Pcm, Rate);
                if (GUILayout.Button("▶ 압축본")) PlayCompressed(slot.Pcm);
                if (GUILayout.Button("WAV 저장")) SaveWav(slot);
            }
            if (GUILayout.Button("WAV 열기")) LoadWav(slot);
        }

        var t = slot.Tmpl;
        if (t == null)
        {
            EditorGUILayout.LabelField("—", EditorStyles.miniLabel);
            return;
        }
        string issue = t.Issue == CheerClipIssue.None ? "없음" : IssueText(t.Issue);
        EditorGUILayout.LabelField(
            $"길이 {t.DurationMs}ms · 끊김 {t.Bursts} · 유성 {t.VoicedRatio:P0} · 피크 {t.PeakDb:0.0}dB · SNR {t.SnrDb:0.0}dB · 클립 {t.ClipFraction:P1}",
            EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"녹음 문제: {issue}   ·   높낮이 {PitchSketch(t.PitchContour)}", EditorStyles.miniLabel);
    }

    void DrawShape(string title, Slot mine)
    {
        if (!mine.Shape.HasValue) return;
        var r = mine.Shape.Value;

        var box = new GUIStyle(EditorStyles.helpBox) { richText = true, fontSize = 11 };
        string pitch = r.PitchChecked
            ? $"상관 {r.PitchCorr:0.00} / 평균 차 {r.PitchMeanAbsSemi:0.0}반음  (통과: ≥{CheerSoundParams.ShapePitchPassCorr} 또는 ≤{CheerSoundParams.ShapePitchPassAbs} · 애매: ≥{CheerSoundParams.ShapePitchSoftCorr} 또는 ≤{CheerSoundParams.ShapePitchSoftAbs})"
            : "생략(유성 부족)";
        string verdict = r.Verdict == CheerShapeVerdict.Ok
            ? "<color=#3c3><b>통과</b></color>"
            : $"<color=#e44><b>거절 — {VerdictText(r)}</b></color>";
        EditorGUILayout.LabelField(
            $"<b>{title}</b>  {verdict}   <color=#999>(✗ 1개 또는 △ {CheerSoundParams.ShapeMaxSoft + 1}개 이상이면 거절 · 지금 △ {r.SoftCount}개)</color>\n" +
            $"{Mark(r.DurationBand)} 길이 비율 {r.DurationRatio:0.00}  (통과 {CheerSoundParams.ShapeDurPassMin}~{CheerSoundParams.ShapeDurPassMax} · 애매 {CheerSoundParams.ShapeDurSoftMin}~{CheerSoundParams.ShapeDurSoftMax})\n" +
            $"{Mark(r.BurstBand)} 끊김 나 {r.MyBursts} / 기준 {r.HostBursts}\n" +
            $"{Mark(r.PitchBand)} 높낮이 {pitch}\n" +
            $"{Mark(r.TimbreBand)} 음색 거리 {r.TimbreDistance:0.00}  (통과 ≤{CheerSoundParams.ShapeTimbrePass} · 애매 ≤{CheerSoundParams.ShapeTimbreSoft})",
            box);
    }

    static string Mark(CheerShapeBand b) => b switch
    {
        CheerShapeBand.Pass => "<color=#3c3>✓</color>",
        CheerShapeBand.Soft => "<color=#fb3>△</color>",
        CheerShapeBand.Hard => "<color=#e44>✗</color>",
        _ => "<color=#999>–</color>",
    };

    /// <summary>게임 게이트와 같은 조건 — 나 1·나 2 둘 다 통과해야 ③(게임 중 판정). 실측용으로만 나 1 단독 허용 토글.</summary>
    bool _allowMine1Only;
    bool GateOpen => UsableMine1 && (UsableMine2 || _allowMine1Only);

    /// <summary>게임 규칙 그대로 — 실시간 판정에 쓰는 등록본은 Host 틀 검사를 통과한 것만(§14.2).</summary>
    void DrawEligibility()
    {
        EditorGUILayout.LabelField("③ 게임 중 판정에 쓰는 등록본", EditorStyles.boldLabel);
        var style = new GUIStyle(EditorStyles.miniLabel) { richText = true };
        EditorGUILayout.LabelField($"나 1: {Eligible(_mine1, false)}      나 2: {Eligible(_mine2, true)}", style);
        EditorGUILayout.LabelField(
            $"기준(전원 공통 고정): 소리 크기 ≥ 소음 +{CheerSoundParams.LiveLoudAboveFloorDb:0}dB · 길이 {CheerSoundParams.LiveMinDurationRatio}~{CheerSoundParams.LiveMaxDurationRatio}배 · " +
            $"끊김 ±{CheerSoundParams.LiveBurstTolerance} · 거리 ≤ {CheerSoundParams.SelfThreshold:0.0} 연속 {CheerSoundParams.PartialConfirmHits}회 (말 끝난 뒤 ≤ {CheerSoundParams.SelfThreshold * CheerSoundParams.FinalThresholdScale:0.0})",
            EditorStyles.wordWrappedMiniLabel);
        if (_mine1.Tmpl != null && !_mine1.ShapeOk)
            EditorGUILayout.HelpBox("나 1이 Host 틀 검사를 못 넘으면 게임에서 음성 판정이 돌지 않아요 — 될 때까지 다시 녹음. (게임은 이 상태로 게이트를 넘으면 T키 응원)", MessageType.Warning);
        else if (UsableMine1 && !UsableMine2)
            EditorGUILayout.HelpBox("게임은 1·2번 둘 다 Host 틀 검사를 통과해야 등록돼요 — 나 2를 다시 녹음하세요.", MessageType.Info);
        _allowMine1Only = EditorGUILayout.ToggleLeft("실측용: 나 1만으로도 ③ 판정 허용 (게임에는 없는 모드)", _allowMine1Only);
    }

    string Eligible(Slot slot, bool practice)
    {
        if (slot.Tmpl == null) return "<color=#999>없음</color>";
        if (!slot.HasTemplate) return "<color=#e44>✗ 녹음 문제</color>";
        if (!slot.ShapeOk) return "<color=#e44>✗ Host 틀 검사 거절 → 다시 녹음</color>";
        return "<color=#3c3>✓ 사용</color>";
    }

    bool UsableMine1 => _mine1.HasTemplate && _mine1.ShapeOk;
    // 10/7: 1·2번은 서로 비교하지 않는다 — 둘 다 Host 틀 검사만(게임과 같은 규칙)
    bool UsableMine2 => _mine2.HasTemplate && _mine2.ShapeOk;

    void DrawLive()
    {
        EditorGUILayout.LabelField("③ 실시간 판정 (사용 가능한 등록본으로 마이크 듣기)", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!GateOpen || _recordingSlot != null))
            {
                if (GUILayout.Button(_live ? "■ 판정 중지" : "▶ 판정 시작", GUILayout.Width(120)))
                {
                    if (_live) StopLive();
                    else StartLive();
                }
            }
            using (new EditorGUI.DisabledScope(!_live))
            {
                if (GUILayout.Button("창 다시 열기 (Reset)")) { _matcher.Reset(); _evalLog.Insert(0, "— Reset —"); }
            }
            if (GUILayout.Button("기록 지우기")) { _evalLog.Clear(); _liveDetections = 0; }
        }

        if (_live)
        {
            var e = _matcher.Last;
            EditorGUILayout.LabelField($"통과 {_liveDetections}회   ·   소음 바닥 {e.FloorDb:0.0}dB   ·   최대 {e.MaxDb:0.0}dB", EditorStyles.miniLabel);
        }
        DrawLog(_evalLog, 14);
    }

    void DrawFeedTests()
    {
        EditorGUILayout.LabelField("흘려보기 (슬롯 녹음 → 나 1+나 2 판정기)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Host 소리로 통과되면 = 다른 사람 목소리로도 뚫림(정상일 수 있음). 다른 소리 WAV로 통과되면 = 잘못 통과.", EditorStyles.wordWrappedMiniLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!GateOpen || _live))
            {
                foreach (var s in new[] { _host, _mine1, _mine2 })
                {
                    using (new EditorGUI.DisabledScope(s.Pcm == null))
                        if (GUILayout.Button(s.Name)) FeedClip(s);
                }
                if (GUILayout.Button("WAV 파일…")) FeedWavFile();
            }
        }
        DrawLog(_feedLog, 8);
    }

    static void DrawLog(List<string> log, int max)
    {
        var style = new GUIStyle(EditorStyles.miniLabel) { richText = true };
        for (int i = 0; i < Mathf.Min(max, log.Count); i++) EditorGUILayout.LabelField(log[i], style);
    }

    // ── 녹음 ────────────────────────────────────────────────────

    void StartRecording(Slot slot)
    {
        if (!StartMic()) return;
        _recordingSlot = slot;
        _recorder.Begin();
    }

    void StopRecording()
    {
        var pcm = _recorder.End();
        var slot = _recordingSlot;
        _recordingSlot = null;
        StopMic();
        SetSlot(slot, pcm);
    }

    void SetSlot(Slot slot, float[] pcm)
    {
        var tmpl = CheerSoundTemplate.Build(pcm, pcm.Length, _dsp);
        // 듣기·저장은 발화 구간만(게임에서 Host 클립을 앞뒤 잘라 보내는 것과 같게)
        slot.Pcm = tmpl.Mfcc.Length > 0 && tmpl.TrimEndSample > tmpl.TrimStartSample
            ? Slice(pcm, tmpl.TrimStartSample, tmpl.TrimEndSample)
            : pcm;
        slot.Tmpl = tmpl;
        _matcher = null;
        RefreshChecks();
        Repaint();
    }

    /// <summary>슬롯이 바뀔 때마다 게임과 같은 규칙으로 다시 검사 — 나 1·나 2 모두 Host 틀 검사만(10/7, 서로 비교 안 함).</summary>
    void RefreshChecks()
    {
        _mine1.Shape = ShapeOf(_mine1);
        _mine2.Shape = ShapeOf(_mine2);
    }

    CheerSoundShapeCheck.Result? ShapeOf(Slot mine)
    {
        if (mine.Tmpl == null || _host.Tmpl == null || mine.Tmpl.Frames == 0 || _host.Tmpl.Frames == 0) return null;
        return CheerSoundShapeCheck.Compare(mine.Tmpl, _host.Tmpl, _work);
    }

    // ── 실시간 판정 ─────────────────────────────────────────────

    CheerSoundMatcher EnsureMatcher()
    {
        if (_matcher != null) return _matcher;
        _matcher = new CheerSoundMatcher();
        _matcher.SetTemplates(UsableMine1 ? _mine1.Tmpl : null, UsableMine2 ? _mine2.Tmpl : null);
        return _matcher;
    }

    void StartLive()
    {
        if (!StartMic()) return;
        EnsureMatcher().Reset();
        _live = true;
        _lastEvalStamp = _matcher.EvalCount;
    }

    void StopLive()
    {
        _live = false;
        StopMic();
    }

    void FeedClip(Slot s) => RunFeed(s.Name, s.Pcm);

    void FeedWavFile()
    {
        string path = EditorUtility.OpenFilePanel("판정기에 흘려볼 WAV", "", "wav");
        if (string.IsNullOrEmpty(path)) return;
        var pcm = WavIo.Read(path, Rate);
        if (pcm == null) { EditorUtility.DisplayDialog("Cheer Sound Lab", "읽을 수 없는 WAV입니다(PCM16/float32만).", "확인"); return; }
        RunFeed(Path.GetFileName(path), pcm);
    }

    /// <summary>앞 0.5초 침묵 + 소리 + 뒤 0.5초 침묵(→ final 평가)을 실시간처럼 100ms씩 넣는다.</summary>
    void RunFeed(string name, float[] pcm)
    {
        var m = EnsureMatcher();
        var (detected, best, bestEval) = FeedOnce(m, pcm);
        string res = detected ? "<color=#3c3><b>통과</b></color>" : "<color=#e44>미통과</color>";
        _feedLog.Insert(0, $"{name}: {res}  최소 d {Fmt(best)} (기준 {m.SelfThreshold:0.0})  " +
                           $"리듬 {(bestEval.RhythmOk ? "OK" : "X")} 길이비 {bestEval.DurationRatio:0.00} 끊김 {bestEval.Bursts}/{bestEval.TemplateBursts}");
        Repaint();
    }

    /// <summary>녹음 하나를 실시간처럼 100ms씩 넣어 본다(앞뒤 0.5초 침묵). 통과면 통과 시점 값, 아니면 최소 d.</summary>
    static (bool pass, float best, CheerSoundMatcher.Eval eval) FeedOnce(CheerSoundMatcher m, float[] pcm)
    {
        m.Reset();
        int pad = Rate / 2;
        var all = new float[pad + pcm.Length + pad];
        Array.Copy(pcm, 0, all, pad, pcm.Length);
        // 완전 무음이면 소음 바닥이 -120dB로 내려가 판정이 지나치게 관대해진다 — 아주 작은 잡음을 깐다
        var rng = new System.Random(1);
        for (int i = 0; i < all.Length; i++) all[i] += (float)(rng.NextDouble() * 2 - 1) * 0.0005f;

        float best = float.PositiveInfinity;
        CheerSoundMatcher.Eval bestEval = default;
        bool detected = false;
        const int chunk = Rate / 10;
        for (int o = 0; o < all.Length && !detected; o += chunk)
        {
            detected = m.Feed(all, o, Mathf.Min(chunk, all.Length - o));
            var e = m.Last;
            if (e.Distance < best) { best = e.Distance; bestEval = e; }
        }
        if (detected) { bestEval = m.Last; best = m.Last.Distance; }
        return (detected, best, bestEval);
    }

    // ── 마이크 (에디터 전용) ────────────────────────────────────

    bool StartMic()
    {
        if (_micClip != null) return true;
        if (Microphone.devices.Length == 0) return false;
        Microphone.GetDeviceCaps(_device, out int min, out int max);
        _micRate = (min == 0 && max == 0) || (Rate >= min && Rate <= max) ? Rate : max;
        _micClip = Microphone.Start(_device, true, 10, _micRate);
        _micLastPos = 0;
        return _micClip != null;
    }

    void StopMic()
    {
        if (_micClip == null) return;
        Microphone.End(_device);
        _micClip = null;
    }

    void Tick()
    {
        if (_micClip == null) return;
        int pos = Microphone.GetPosition(_device);
        if (pos == _micLastPos) return;

        int total = _micClip.samples;
        int n = pos > _micLastPos ? pos - _micLastPos : total - _micLastPos + pos;
        if (_micRead.Length < n) _micRead = new float[n];
        ReadMic(_micLastPos, n, total);
        _micLastPos = pos;

        int m = ToRate16k(_micRead, n);

        if (_recordingSlot != null)
        {
            _recorder.Append(_resampled, 0, m);
            if (!_recorder.IsRecording) StopRecording();
            Repaint();
        }
        else if (_live)
        {
            if (_matcher.Feed(_resampled, 0, m))
            {
                _liveDetections++;
                LogEval(_matcher.Last);
                _matcher.Reset();
            }
            else LogEvalIfNew();
            Repaint();
        }
    }

    void ReadMic(int from, int n, int total)
    {
        int first = Mathf.Min(n, total - from);
        var tmp = new float[first];
        _micClip.GetData(tmp, from);
        Array.Copy(tmp, 0, _micRead, 0, first);
        if (n > first)
        {
            var tmp2 = new float[n - first];
            _micClip.GetData(tmp2, 0);
            Array.Copy(tmp2, 0, _micRead, first, n - first);
        }
    }

    /// <summary>장치가 16kHz를 못 주면 상자 평균으로 내린다(실측 도구 전용 — 게임은 기존 리샘플러 사용).</summary>
    int ToRate16k(float[] src, int n)
    {
        if (_micRate == Rate)
        {
            if (_resampled.Length < n) _resampled = new float[n];
            Array.Copy(src, _resampled, n);
            return n;
        }
        double ratio = _micRate / (double)Rate;
        int m = (int)(n / ratio);
        if (_resampled.Length < m) _resampled = new float[m];
        for (int i = 0; i < m; i++)
        {
            int a = (int)(i * ratio), b = Mathf.Min(n, (int)((i + 1) * ratio));
            float s = 0f;
            for (int k = a; k < b; k++) s += src[k];
            _resampled[i] = b > a ? s / (b - a) : 0f;
        }
        return m;
    }

    void LogEvalIfNew()
    {
        if (_matcher.EvalCount == _lastEvalStamp) return;
        _lastEvalStamp = _matcher.EvalCount;
        var e = _matcher.Last;
        if (!e.Loud) return; // 조용할 때 평가는 기록 안 함(목록이 침묵으로 밀리지 않게)
        LogEval(e);
    }

    void LogEval(CheerSoundMatcher.Eval e)
    {
        string head = e.Detected ? "<color=#3c3><b>통과</b></color>" : (e.Final ? "final" : "partial");
        string gate = !e.Loud ? "작음" : !e.EnoughSpeech ? "짧음" : "";
        _evalLog.Insert(0,
            $"{DateTime.Now:HH:mm:ss.f} {head} d {Fmt(e.Distance)} / {e.Threshold:0.00}  " +
            $"리듬 {(e.RhythmOk ? "OK" : "X")} 길이비 {e.DurationRatio:0.00} 끊김 {e.Bursts}/{e.TemplateBursts}  연속 {e.Hits} {gate}");
        if (_evalLog.Count > 200) _evalLog.RemoveRange(200, _evalLog.Count - 200);
    }

    // ── 재생 (에디터 미리듣기) ──────────────────────────────────

    void PlayCompressed(float[] pcm)
    {
        var bytes = CheerSoundCodec.Encode(pcm, pcm.Length);
        Play(CheerSoundCodec.Decode(bytes), CheerSoundCodec.EncodedRate);
        Debug.Log($"[CheerSoundLab] 압축본 {bytes.Length / 1024f:0.0}KB ({pcm.Length / (float)Rate:0.00}s)");
    }

    static void Play(float[] pcm, int rate)
    {
        if (pcm == null || pcm.Length == 0) return;
        var clip = AudioClip.Create("CheerSoundLab", pcm.Length, 1, rate, false);
        clip.SetData(pcm, 0);

        Type audioUtil = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            if ((audioUtil = asm.GetType("UnityEditor.AudioUtil")) != null) break;
        var stop = audioUtil?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public);
        var play = audioUtil?.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public,
                                        null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
        if (play == null)
        {
            Debug.LogWarning("[CheerSoundLab] 에디터 미리듣기 API를 찾지 못했습니다 — WAV 저장 후 들어보세요.");
            return;
        }
        stop?.Invoke(null, null);
        play.Invoke(null, new object[] { clip, 0, false });
    }

    // ── WAV ─────────────────────────────────────────────────────

    void SaveWav(Slot slot)
    {
        string path = EditorUtility.SaveFilePanel("WAV 저장", "", slot.Name.Split(' ')[0] + ".wav", "wav");
        if (string.IsNullOrEmpty(path)) return;
        WavIo.Write(path, slot.Pcm, Rate);
    }

    void LoadWav(Slot slot)
    {
        string path = EditorUtility.OpenFilePanel("WAV 열기", "", "wav");
        if (string.IsNullOrEmpty(path)) return;
        var pcm = WavIo.Read(path, Rate);
        if (pcm == null) { EditorUtility.DisplayDialog("Cheer Sound Lab", "읽을 수 없는 WAV입니다(PCM16/float32만).", "확인"); return; }
        SetSlot(slot, pcm);
    }

    static class WavIo
    {
        public static void Write(string path, float[] pcm, int rate)
        {
            using var w = new BinaryWriter(File.Create(path));
            int bytes = pcm.Length * 2;
            w.Write(0x46464952); w.Write(36 + bytes); w.Write(0x45564157);           // RIFF size WAVE
            w.Write(0x20746D66); w.Write(16); w.Write((short)1); w.Write((short)1);    // fmt  PCM mono
            w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(0x61746164); w.Write(bytes);                                       // data
            foreach (float s in pcm) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
        }

        /// <summary>PCM16/float32, 모노·스테레오(평균), 아무 레이트 → targetRate 모노 float. 실패 시 null.</summary>
        public static float[] Read(string path, int targetRate)
        {
            try
            {
                using var r = new BinaryReader(File.OpenRead(path));
                if (r.ReadInt32() != 0x46464952) return null;
                r.ReadInt32();
                if (r.ReadInt32() != 0x45564157) return null;

                int format = 0, channels = 0, rate = 0, bits = 0;
                while (r.BaseStream.Position + 8 <= r.BaseStream.Length)
                {
                    int id = r.ReadInt32(), size = r.ReadInt32();
                    if (id == 0x20746D66)
                    {
                        format = r.ReadInt16(); channels = r.ReadInt16(); rate = r.ReadInt32();
                        r.ReadInt32(); r.ReadInt16(); bits = r.ReadInt16();
                        if (size > 16) r.ReadBytes(size - 16);
                    }
                    else if (id == 0x61746164)
                    {
                        if (channels <= 0 || rate <= 0) return null;
                        int frameBytes = bits / 8 * channels;
                        int frames = size / frameBytes;
                        var mono = new float[frames];
                        for (int f = 0; f < frames; f++)
                        {
                            float sum = 0f;
                            for (int c = 0; c < channels; c++)
                            {
                                if (format == 1 && bits == 16) sum += r.ReadInt16() / 32768f;
                                else if (format == 3 && bits == 32) sum += r.ReadSingle();
                                else return null;
                            }
                            mono[f] = sum / channels;
                        }
                        return rate == targetRate ? mono : Resample(mono, rate, targetRate);
                    }
                    else r.ReadBytes(size + (size & 1));
                }
                return null;
            }
            catch (IOException) { return null; }
        }

        static float[] Resample(float[] src, int from, int to)
        {
            double ratio = from / (double)to;
            int n = (int)(src.Length / ratio);
            var dst = new float[n];
            for (int i = 0; i < n; i++)
            {
                int a = (int)(i * ratio), b = Mathf.Min(src.Length, Mathf.Max(a + 1, (int)((i + 1) * ratio)));
                float s = 0f;
                for (int k = a; k < b; k++) s += src[k];
                dst[i] = s / (b - a);
            }
            return dst;
        }
    }

    // ── 표시 유틸 ───────────────────────────────────────────────

    static float[] Slice(float[] a, int from, int to)
    {
        var r = new float[to - from];
        Array.Copy(a, from, r, 0, r.Length);
        return r;
    }

    static string Fmt(float d) => float.IsInfinity(d) || float.IsNaN(d) ? "—" : d.ToString("0.00");

    static string IssueText(CheerClipIssue i) => i switch
    {
        CheerClipIssue.TooQuiet => "너무 작음",
        CheerClipIssue.TooShort => "너무 짧음",
        CheerClipIssue.TooLong  => "너무 김",
        CheerClipIssue.TooLoud  => "너무 큼(찢어짐)",
        _ => i.ToString(),
    };

    static string VerdictText(CheerSoundShapeCheck.Result r) => r.Verdict switch
    {
        CheerShapeVerdict.TooQuiet       => "너무 작아요",
        CheerShapeVerdict.TooLoud        => "너무 커요",
        CheerShapeVerdict.TooShort       => "너무 짧아요",
        CheerShapeVerdict.TooLong        => "너무 길어요",
        CheerShapeVerdict.BurstMismatch  => $"끊는 횟수가 달라요 (기준 {r.HostBursts}번, 나 {r.MyBursts}번)",
        CheerShapeVerdict.PitchMismatch  => "높낮이가 달라요",
        CheerShapeVerdict.TimbreMismatch => "소리가 달라요",
        _ => r.Verdict.ToString(),
    };

    /// <summary>높낮이 곡선을 10글자 막대로 — 올라감/내려감을 눈으로 보기.</summary>
    static string PitchSketch(float[] contour)
    {
        if (contour == null || contour.Length == 0) return "";
        const string bars = "▁▂▃▄▅▆▇█";
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (float v in contour) { lo = Mathf.Min(lo, v); hi = Mathf.Max(hi, v); }
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 10; i++)
        {
            float v = contour[i * (contour.Length - 1) / 9];
            int k = hi - lo < 0.5f ? 3 : Mathf.Clamp((int)((v - lo) / (hi - lo) * 7.99f), 0, 7);
            sb.Append(bars[k]);
        }
        return $"{sb} ({hi - lo:0.0}반음 폭)";
    }
}
#endif
