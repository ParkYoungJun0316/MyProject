using System;

/// <summary>
/// 인게임 판정 — 내 마이크 실시간 소리 vs 내 등록본 (CheerSystemDesign.md §14.6).
///
/// 16kHz mono를 <see cref="Feed"/>로 흘려 넣으면 10ms 프레임마다 dB·MFCC 원본 계수를 4초 링버퍼에 쌓고,
/// 100ms마다(또는 발화가 끝나는 순간) 평가한다:
///   ① 사전 게이트(싸게): 버퍼 최대 dB ≥ 소음 바닥 + 12dB, 발화 프레임 수 ≥ 등록본의 0.5배
///   ② 부분 구간 DTW: 등록본 전체를 버퍼 어디에든 맞춤 → 거리 d (등록본 여러 개면 최소)
///   ③ 리듬: 맞춰진 구간 길이 0.5~2.0배, 끊김 횟수 ±1
///   partial — d ≤ T_self 가 연속 2회면 통과 / final(침묵 300ms로 발화 끝) — d ≤ T_self × 1.15 면 통과
///
/// [스레드] 한 인스턴스는 한 스레드에서만. Unity API 없음 — 워커 스레드에서 그대로 돈다.
/// [수명] 창이 열릴 때 <see cref="Reset"/>(§4.7 창 열림 순간 비우기와 같은 규칙). 통과 뒤엔 Reset 전까지 다시 안 알린다.
/// </summary>
public sealed class CheerSoundMatcher
{
    /// <summary>마지막 평가 기록 — Lab 표시·구조화 로그용(§14.6 로그).</summary>
    public struct Eval
    {
        public float Distance;     // 최소 d (리듬 통과 여부 무관)
        public float Threshold;    // 이번 평가에 쓴 기준(partial = T_self, final = ×1.15)
        public float FloorDb;
        public float MaxDb;
        public bool  Loud;
        public bool  EnoughSpeech;
        public bool  RhythmOk;
        public float DurationRatio;
        public int   Bursts;
        public int   TemplateBursts;
        public bool  Final;
        public int   Hits;
        public bool  Detected;
    }

    const int C = CheerSoundParams.Ceps, D = CheerSoundParams.Dims, R = CheerSoundParams.RingFrames;

    readonly CheerSoundDsp _dsp = new();
    readonly CheerSoundDsp.DtwWork _work = new();

    CheerSoundTemplate[] _templates = Array.Empty<CheerSoundTemplate>();
    int _maxTemplateFrames, _minTemplateFrames;

    // 샘플 → 프레임
    float[] _pcm = new float[CheerSoundParams.FrameLen * 4];
    int _pcmCount;

    // 프레임 링버퍼 (원본 13계수 + dB)
    readonly float[] _ringCeps = new float[R * C];
    readonly float[] _ringDb = new float[R];
    int _ringHead, _ringCount;

    // 평가용 선형 버퍼
    readonly float[] _winCeps = new float[R * C];
    readonly float[] _winFeat = new float[R * D];
    readonly float[] _winDb = new float[R];
    readonly float[] _sortScratch = new float[R];

    int _framesSinceEval;
    int _hits;
    bool _wasEnded = true;
    bool _detected;
    // final(말 끝남) 평가는 큰 소리가 새로 난 뒤 한 번만 — 소음 바닥이 흔들려 '끝남'이 다시 켜져도 같은 발화를 또 세지 않게
    // (10/7 로그: 같은 d가 실패로 3~4번씩 연달아 찍혀 연습 7회 실패 T키 자동이 너무 빨리 켜졌다)
    bool _loudSinceFinal;
    float _lastFloor = -200f;

    /// <summary>전 플레이어 공통 고정 기준(§14.6). 사람별 보정 없음.</summary>
    public float SelfThreshold => _threshold;

    float _threshold = CheerSoundParams.SelfThreshold;
    float _finalScale = CheerSoundParams.FinalThresholdScale;

    /// <summary>등록 검사 전용 — 게임 중 판정은 건드리지 않는다. 말 끝난 뒤 보험 배수 없이 이 값 하나로 본다.</summary>
    public void UseFixedThreshold(float threshold)
    {
        _threshold = threshold;
        _finalScale = 1f;
    }
    public Eval Last { get; private set; }
    /// <summary>평가 횟수(Reset으로 안 지워짐) — 새 평가가 났는지 확인용.</summary>
    public int EvalCount { get; private set; }
    public bool HasTemplates => _templates.Length > 0;
    public int TemplateCount => _templates.Length;

    /// <summary>
    /// 등록본 설정 — 둘 다 **Host 틀 검사를 통과한 것만** 넘길 것(§14.2 규칙: 게임에서 쓰는 등록본은 전부 Host 틀 검사 통과분).
    /// primary = 1번 녹음, secondary = 2번 녹음(둘 다 Host 틀 검사 통과분, 10/7). 판정은 둘 중 하나라도 기준 안이면 통과.
    /// </summary>
    public void SetTemplates(CheerSoundTemplate primary, CheerSoundTemplate secondary)
    {
        if (primary == null || primary.Frames <= 0)
        {
            _templates = Array.Empty<CheerSoundTemplate>();
            Reset();
            return;
        }

        _templates = secondary != null && secondary.Frames > 0
            ? new[] { primary, secondary }
            : new[] { primary };

        _maxTemplateFrames = 0;
        _minTemplateFrames = int.MaxValue;
        foreach (var t in _templates)
        {
            _maxTemplateFrames = Math.Max(_maxTemplateFrames, t.Frames);
            _minTemplateFrames = Math.Min(_minTemplateFrames, t.Frames);
        }
        Reset();
    }

    /// <summary>
    /// 녹음 하나를 실시간처럼 100ms씩 흘려 넣어 판정 — Lab "흘려보기"용(10/7부터 등록은 Host 틀 검사만 쓴다).
    /// 앞뒤 0.5초 침묵을 붙이고(final 평가가 나도록) 아주 작은 잡음을 깐다(완전 무음이면 소음 바닥이 -120dB로 내려가
    /// 판정이 지나치게 관대해짐). 통과면 통과 시점 Eval, 아니면 최소 d의 Eval.
    /// </summary>
    public static bool OfflineCheck(CheerSoundTemplate primary, CheerSoundTemplate secondary, float[] pcm, int count, out Eval result,
                                    float fixedThreshold = -1f)
    {
        var m = new CheerSoundMatcher();
        m.SetTemplates(primary, secondary);
        if (fixedThreshold > 0f) m.UseFixedThreshold(fixedThreshold);
        int pad = CheerSoundParams.SampleRate / 2;
        var all = new float[pad + count + pad];
        Array.Copy(pcm, 0, all, pad, count);
        var rng = new Random(1);
        for (int i = 0; i < all.Length; i++) all[i] += (float)(rng.NextDouble() * 2 - 1) * 0.0005f;

        result = new Eval { Distance = float.PositiveInfinity };
        bool detected = false;
        const int chunk = CheerSoundParams.SampleRate / 10;
        for (int o = 0; o < all.Length && !detected; o += chunk)
        {
            detected = m.Feed(all, o, Math.Min(chunk, all.Length - o));
            if (m.Last.Distance < result.Distance) result = m.Last;
        }
        if (detected) result = m.Last;
        return detected;
    }

    /// <summary>창 열림 순간 — 버퍼·연속 횟수·통과 상태 비우기.</summary>
    public void Reset()
    {
        _pcmCount = 0;
        _ringHead = _ringCount = 0;
        _framesSinceEval = 0;
        _hits = 0;
        _wasEnded = true;
        _detected = false;
        _loudSinceFinal = false;
        _lastFloor = -200f;
        Last = new Eval { Distance = float.PositiveInfinity };
    }

    /// <summary>16kHz mono 샘플 투입. 이번 호출 중 통과가 확정되면 true(Reset 전까지 한 번만).</summary>
    public bool Feed(float[] samples, int offset, int count)
    {
        if (_templates.Length == 0 || _detected) return false;

        EnsurePcm(_pcmCount + count);
        Array.Copy(samples, offset, _pcm, _pcmCount, count);
        _pcmCount += count;

        bool detected = false;
        int consumed = 0;
        // 프리엠퍼시스가 앞 샘플 1개를 읽으므로 프레임 시작은 1부터(버퍼 맨 앞 1샘플은 이월분)
        while (consumed + 1 + CheerSoundParams.FrameLen <= _pcmCount)
        {
            int at = consumed + 1;
            PushFrame(at);
            consumed += CheerSoundParams.Hop;

            if (!detected && EvaluateIfDue()) detected = true;
        }

        if (consumed > 0)
        {
            Array.Copy(_pcm, consumed, _pcm, 0, _pcmCount - consumed);
            _pcmCount -= consumed;
        }
        return detected;
    }

    void PushFrame(int at)
    {
        int slot = _ringHead;
        _ringDb[slot] = CheerSoundDsp.FrameDb(_pcm, at, _pcmCount);
        _dsp.Cepstra(_pcm, at, _pcmCount, _ringCeps, slot * C);
        if (_ringDb[slot] >= _lastFloor + CheerSoundParams.LiveLoudAboveFloorDb) _loudSinceFinal = true;
        _ringHead = (_ringHead + 1) % R;
        if (_ringCount < R) _ringCount++;
        _framesSinceEval++;
    }

    bool EvaluateIfDue()
    {
        if (_ringCount < CheerSoundParams.EndSilenceFrames) return false;

        // 소음 바닥 = 버퍼 하위 10%
        CopyRecentDb(_ringCount);
        float floor = CheerSoundTemplate.Percentile(_winDb, _ringCount, 0.1f, _sortScratch);
        float speechDb = floor + CheerSoundParams.SpeechAboveFloorDb;
        _lastFloor = floor;

        bool ended = IsTailSilent(_ringCount, speechDb);
        bool finalNow = ended && !_wasEnded && _loudSinceFinal;
        _wasEnded = ended;
        if (finalNow) _loudSinceFinal = false;

        if (!finalNow && _framesSinceEval < CheerSoundParams.EvalEveryFrames) return false;
        _framesSinceEval = 0;

        return Evaluate(floor, speechDb, finalNow);
    }

    bool Evaluate(float floor, float speechDb, bool final)
    {
        var e = new Eval { FloorDb = floor, Final = final, Distance = float.PositiveInfinity };
        e.Threshold = final ? _threshold * _finalScale : _threshold;

        int w = Math.Min(_ringCount,
            (int)Math.Ceiling(CheerSoundParams.LiveMaxDurationRatio * _maxTemplateFrames) + CheerSoundParams.EndSilenceFrames);
        CopyRecent(w);

        int speechFrames = 0;
        float maxDb = float.NegativeInfinity;
        for (int f = 0; f < w; f++)
        {
            if (_winDb[f] >= speechDb) speechFrames++;
            if (_winDb[f] > maxDb) maxDb = _winDb[f];
        }
        e.MaxDb = maxDb;
        e.Loud = maxDb >= floor + CheerSoundParams.LiveLoudAboveFloorDb;
        e.EnoughSpeech = speechFrames >= CheerSoundParams.LiveMinDurationRatio * _minTemplateFrames;

        if (!e.Loud || !e.EnoughSpeech)
            return Finish(ref e, false);

        CheerSoundDsp.AppendDeltas(_winCeps, w, _winFeat);

        bool pass = false;
        foreach (var t in _templates)
        {
            float d = CheerSoundDsp.DtwSubsequence(t.Mfcc, t.Frames, _winFeat, w, _work, out int s, out int end);
            int len = end - s + 1;
            float ratio = len / (float)t.Frames;
            int bursts = s >= 0 ? CheerSoundDsp.CountBursts(_winDb, s, end, floor) : 0;
            bool rhythm = ratio >= CheerSoundParams.LiveMinDurationRatio
                       && ratio <= CheerSoundParams.LiveMaxDurationRatio
                       && Math.Abs(bursts - t.Bursts) <= CheerSoundParams.LiveBurstTolerance;

            // 표시용: 통과한 등록본이 있으면 그쪽, 없으면 최소 d 쪽 — "통과인데 리듬 X"로 보이지 않게
            bool passThis = rhythm && d <= e.Threshold;
            if (passThis && !pass || !pass && d < e.Distance)
            {
                e.Distance = d;
                e.RhythmOk = rhythm;
                e.DurationRatio = ratio;
                e.Bursts = bursts;
                e.TemplateBursts = t.Bursts;
            }
            if (rhythm && d <= e.Threshold) pass = true;
        }

        return Finish(ref e, pass);
    }

    bool Finish(ref Eval e, bool pass)
    {
        if (e.Final)
        {
            _detected = pass;
            _hits = 0;
        }
        else
        {
            _hits = pass ? _hits + 1 : 0;
            _detected = _hits >= CheerSoundParams.PartialConfirmHits;
        }
        e.Hits = _hits;
        e.Detected = _detected;
        Last = e;
        EvalCount++;
        return _detected;
    }

    bool IsTailSilent(int n, float speechDb)
    {
        int tail = CheerSoundParams.EndSilenceFrames;
        bool anySpeechBefore = false;
        for (int f = 0; f < n - tail; f++) if (_winDb[f] >= speechDb) { anySpeechBefore = true; break; }
        if (!anySpeechBefore) return true;
        for (int f = n - tail; f < n; f++) if (_winDb[f] >= speechDb) return false;
        return true;
    }

    /// <summary>링버퍼 최근 n프레임 dB → _winDb[0..n) (오래된 것부터).</summary>
    void CopyRecentDb(int n)
    {
        int start = (_ringHead - n + R) % R;
        for (int f = 0; f < n; f++) _winDb[f] = _ringDb[(start + f) % R];
    }

    /// <summary>링버퍼 최근 n프레임 dB·계수 → 선형 버퍼.</summary>
    void CopyRecent(int n)
    {
        int start = (_ringHead - n + R) % R;
        for (int f = 0; f < n; f++)
        {
            int slot = (start + f) % R;
            _winDb[f] = _ringDb[slot];
            Array.Copy(_ringCeps, slot * C, _winCeps, f * C, C);
        }
    }

    void EnsurePcm(int needed)
    {
        if (_pcm.Length >= needed) return;
        int size = _pcm.Length;
        while (size < needed) size *= 2;
        Array.Resize(ref _pcm, size);
    }
}
