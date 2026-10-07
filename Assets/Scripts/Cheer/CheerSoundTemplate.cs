using System;
using System.IO;

/// <summary>녹음 자체 문제 — 등록·Host 녹음 모두 이게 None이어야 받는다(§14.4 #1, §14.5 검증).</summary>
public enum CheerClipIssue { None, TooQuiet, TooShort, TooLong, TooLoud }

/// <summary>
/// 응원 소리 1개의 특징 묶음 — CheerSystemDesign.md §14.3 "등록본".
/// Host 기준 소리의 특징(틀 검사 기준, 네트워크로 배포)과 각자 등록본(로컬 전용)이 같은 형식이다.
///
/// <see cref="Build"/>는 녹음 전체(앞뒤 침묵 포함, 16kHz mono)를 받아 발화 구간만 잘라 특징을 뽑는다.
/// 녹음에 문제가 있어도 특징은 채워서 돌려주고 <see cref="Issue"/>로 알린다 — 판정은 호출자가 한다
/// (Cheer Sound Lab이 수치를 다 보여줘야 하므로).
/// </summary>
public sealed class CheerSoundTemplate
{
    const int Magic = 0x31545343; // "CST1"

    /// <summary>[Frames × Dims] MFCC c1~c13 + Δ (정규화 없음).</summary>
    public float[] Mfcc;
    public int     Frames;
    public int     Bursts;
    public int     DurationMs;
    /// <summary>상대 높낮이(반음, 중앙값 0) — 시간축 PitchPoints점.</summary>
    public float[] PitchContour;
    public float   VoicedRatio;
    public float   PeakDb;
    public float   SnrDb;
    /// <summary>Host 기준 소리 버전 — 등록본이 어느 기준 소리에 대해 등록됐는지(§14.2 Interlude 재녹음 무효화).</summary>
    public int     HostVersion;

    // 아래는 Build 직후에만 유효(직렬화 안 함) — 녹음 원본에서 발화 구간 위치, 클립 비율.
    public int     TrimStartSample;
    public int     TrimEndSample;
    public float   ClipFraction;
    public CheerClipIssue Issue;

    // ── 생성 ────────────────────────────────────────────────────

    public static CheerSoundTemplate Build(float[] pcm, int count, CheerSoundDsp dsp)
    {
        const int Hop = CheerSoundParams.Hop, L = CheerSoundParams.FrameLen;
        var t = new CheerSoundTemplate { PitchContour = new float[CheerSoundParams.PitchPoints] };

        int frames = count >= L ? 1 + (count - L) / Hop : 0;
        if (frames < CheerSoundParams.MinSpeechRunFrames)
        {
            t.Issue = CheerClipIssue.TooShort;
            t.Mfcc = Array.Empty<float>();
            return t;
        }

        // DC 제거 + 클립 비율
        double mean = 0;
        for (int i = 0; i < count; i++) mean += pcm[i];
        mean /= count;
        var x = new float[count];
        int clipped = 0;
        for (int i = 0; i < count; i++)
        {
            if (Math.Abs(pcm[i]) >= CheerSoundParams.ClipSampleLevel) clipped++;
            x[i] = (float)(pcm[i] - mean);
        }
        t.ClipFraction = clipped / (float)count;

        // 프레임 dB → 소음 바닥 → 발화 구간
        var db = new float[frames];
        for (int f = 0; f < frames; f++) db[f] = CheerSoundDsp.FrameDb(x, f * Hop, count);

        float peak = Max(db, 0, frames - 1);
        float p10 = Percentile(db, frames, 0.1f);
        float floor = Math.Min(p10, peak - CheerSoundParams.FloorBelowPeakMaxDb);
        t.PeakDb = peak;
        t.SnrDb = peak - p10;

        FindSpeechSpan(db, frames, floor, out int s, out int e);
        if (s < 0)
        {
            t.Issue = CheerClipIssue.TooQuiet;
            t.Mfcc = Array.Empty<float>();
            return t;
        }
        s = Math.Max(0, s - CheerSoundParams.TrimPadFrames);
        e = Math.Min(frames - 1, e + CheerSoundParams.TrimPadFrames);
        int n = e - s + 1;

        t.Frames = n;
        t.DurationMs = n * 10;
        t.TrimStartSample = s * Hop;
        t.TrimEndSample = Math.Min(count, e * Hop + L);
        t.Bursts = CheerSoundDsp.CountBursts(db, s, e, floor);

        // MFCC+Δ (정규화 없음 — CheerSoundDsp 상단 주석)
        var ceps = new float[n * CheerSoundParams.Ceps];
        for (int f = 0; f < n; f++) dsp.Cepstra(x, (s + f) * Hop, count, ceps, f * CheerSoundParams.Ceps);
        t.Mfcc = new float[n * CheerSoundParams.Dims];
        CheerSoundDsp.AppendDeltas(ceps, n, t.Mfcc);
        var speech = new bool[n];
        float speechDb = floor + CheerSoundParams.SpeechAboveFloorDb;
        for (int f = 0; f < n; f++) speech[f] = db[s + f] >= speechDb;

        // 높낮이 곡선
        BuildPitch(x, count, s, n, speech, dsp, t);

        t.Issue = Classify(t);
        return t;
    }

    static CheerClipIssue Classify(CheerSoundTemplate t)
    {
        if (t.PeakDb < CheerSoundParams.MinPeakDbfs || t.SnrDb < CheerSoundParams.MinSnrDb) return CheerClipIssue.TooQuiet;
        if (t.ClipFraction > CheerSoundParams.MaxClipFraction) return CheerClipIssue.TooLoud;
        if (t.DurationMs < CheerSoundParams.MinDurationSec * 1000f) return CheerClipIssue.TooShort;
        if (t.DurationMs > CheerSoundParams.MaxDurationSec * 1000f) return CheerClipIssue.TooLong;
        return CheerClipIssue.None;
    }

    /// <summary>MinSpeechRunFrames 이상 이어진 발화의 첫 시작~마지막 끝. 없으면 s=-1.</summary>
    static void FindSpeechSpan(float[] db, int frames, float floor, out int s, out int e)
    {
        float th = floor + CheerSoundParams.SpeechAboveFloorDb;
        s = e = -1;
        int run = 0;
        for (int f = 0; f < frames; f++)
        {
            run = db[f] >= th ? run + 1 : 0;
            if (run < CheerSoundParams.MinSpeechRunFrames) continue;
            if (s < 0) s = f - run + 1;
            e = f;
        }
    }

    static void BuildPitch(float[] x, int count, int s, int n, bool[] speech, CheerSoundDsp dsp, CheerSoundTemplate t)
    {
        var semi = new float[n];
        var voiced = new bool[n];
        int voicedCount = 0, speechCount = 0;
        for (int f = 0; f < n; f++)
        {
            if (!speech[f]) continue;
            speechCount++;
            if (!dsp.Pitch(x, (s + f) * CheerSoundParams.Hop, count, out float hz)) continue;
            semi[f] = (float)(12.0 * Math.Log(hz / 100.0, 2.0));
            voiced[f] = true;
            voicedCount++;
        }
        // 끊김 사이 틈은 분모에서 뺀다 — "우가 우가"처럼 끊는 소리가 유성 부족으로 높낮이 검사를 건너뛰지 않게
        t.VoicedRatio = speechCount > 0 ? voicedCount / (float)speechCount : 0f;
        if (voicedCount == 0) return;

        // 중앙값 빼기 → 절대 음높이(남녀) 차이 제거
        float median = VoicedMedian(semi, voiced, n, voicedCount);
        FixOctaveErrors(semi, voiced, n, median);
        median = VoicedMedian(semi, voiced, n, voicedCount);

        // 무성 프레임은 양옆 유성값으로 선형 보간(끝은 유지)
        int prev = -1;
        for (int f = 0; f < n; f++)
        {
            if (!voiced[f]) continue;
            semi[f] -= median;
            if (prev < 0) for (int k = 0; k < f; k++) semi[k] = semi[f];
            else for (int k = prev + 1; k < f; k++) semi[k] = semi[prev] + (semi[f] - semi[prev]) * (k - prev) / (f - prev);
            prev = f;
        }
        for (int k = prev + 1; k < n; k++) semi[k] = semi[prev];

        int P = CheerSoundParams.PitchPoints;
        for (int p = 0; p < P; p++)
        {
            float pos = n == 1 ? 0f : p * (n - 1) / (float)(P - 1);
            int i0 = (int)pos, i1 = Math.Min(n - 1, i0 + 1);
            t.PitchContour[p] = semi[i0] + (semi[i1] - semi[i0]) * (pos - i0);
        }
    }

    static float VoicedMedian(float[] semi, bool[] voiced, int n, int voicedCount)
    {
        var vals = new float[voicedCount];
        for (int f = 0, k = 0; f < n; f++) if (voiced[f]) vals[k++] = semi[f];
        Array.Sort(vals);
        return vals[voicedCount / 2];
    }

    /// <summary>
    /// 옥타브 오류 보정 — YIN이 음을 한 옥타브 위/아래로 잡은 프레임(Lab 10/7: 한 번 외침인데 "반음 폭" 25~30)을 되돌린다.
    /// ① 중앙값에서 PitchOctaveFoldSemi 넘게 벗어나면 12반음씩 중앙값 쪽으로 접기
    /// ② 유성 프레임끼리 PitchMedianFrames 중앙값 필터 — 한두 프레임 튐 제거
    /// </summary>
    static void FixOctaveErrors(float[] semi, bool[] voiced, int n, float median)
    {
        float fold = CheerSoundParams.PitchOctaveFoldSemi;
        for (int f = 0; f < n; f++)
        {
            if (!voiced[f]) continue;
            while (semi[f] - median > fold) semi[f] -= 12f;
            while (median - semi[f] > fold) semi[f] += 12f;
        }

        int voicedCount = 0;
        for (int f = 0; f < n; f++) if (voiced[f]) voicedCount++;
        if (voicedCount < CheerSoundParams.PitchMedianFrames) return;

        var idx = new int[voicedCount];
        var src = new float[voicedCount];
        for (int f = 0, k = 0; f < n; f++) if (voiced[f]) { idx[k] = f; src[k] = semi[f]; k++; }

        int half = CheerSoundParams.PitchMedianFrames / 2;
        var win = new float[CheerSoundParams.PitchMedianFrames];
        for (int k = 0; k < voicedCount; k++)
        {
            int lo = Math.Max(0, k - half), hi = Math.Min(voicedCount - 1, k + half), m = hi - lo + 1;
            Array.Copy(src, lo, win, 0, m);
            Array.Sort(win, 0, m);
            semi[idx[k]] = win[m / 2];
        }
    }

    // ── 직렬화 (Host 특징 배포·로컬 보관) ───────────────────────

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms))
        {
            w.Write(Magic);
            w.Write(CheerSoundParams.Dims);
            w.Write(Frames);
            for (int i = 0; i < Frames * CheerSoundParams.Dims; i++) w.Write(Mfcc[i]);
            w.Write(Bursts);
            w.Write(DurationMs);
            w.Write(VoicedRatio);
            w.Write(PeakDb);
            w.Write(SnrDb);
            w.Write(HostVersion);
            for (int i = 0; i < CheerSoundParams.PitchPoints; i++) w.Write(PitchContour[i]);
        }
        return ms.ToArray();
    }

    /// <summary>형식이 안 맞으면 null.</summary>
    public static CheerSoundTemplate Deserialize(byte[] data)
    {
        if (data == null || data.Length < 12) return null;
        try
        {
            using var r = new BinaryReader(new MemoryStream(data));
            if (r.ReadInt32() != Magic) return null;
            if (r.ReadInt32() != CheerSoundParams.Dims) return null;
            int frames = r.ReadInt32();
            if (frames <= 0 || frames > 1000) return null;

            var t = new CheerSoundTemplate
            {
                Frames = frames,
                Mfcc = new float[frames * CheerSoundParams.Dims],
                PitchContour = new float[CheerSoundParams.PitchPoints],
            };
            for (int i = 0; i < t.Mfcc.Length; i++) t.Mfcc[i] = r.ReadSingle();
            t.Bursts = r.ReadInt32();
            t.DurationMs = r.ReadInt32();
            t.VoicedRatio = r.ReadSingle();
            t.PeakDb = r.ReadSingle();
            t.SnrDb = r.ReadSingle();
            t.HostVersion = r.ReadInt32();
            for (int i = 0; i < CheerSoundParams.PitchPoints; i++) t.PitchContour[i] = r.ReadSingle();
            return t;
        }
        catch (EndOfStreamException)
        {
            return null;
        }
    }

    // ── 공용 유틸 ───────────────────────────────────────────────

    internal static float Max(float[] a, int from, int to)
    {
        float m = float.NegativeInfinity;
        for (int i = from; i <= to; i++) if (a[i] > m) m = a[i];
        return m;
    }

    /// <summary>a[0..count)의 q 분위수(복사 후 정렬 — 프레임 수백 개라 충분히 싸다). scratch를 주면 할당 없음.</summary>
    internal static float Percentile(float[] a, int count, float q, float[] scratch = null)
    {
        var tmp = scratch != null && scratch.Length >= count ? scratch : new float[count];
        Array.Copy(a, tmp, count);
        Array.Sort(tmp, 0, count);
        return tmp[Math.Clamp((int)(q * (count - 1)), 0, count - 1)];
    }
}
