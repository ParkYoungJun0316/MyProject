using System;

/// <summary>
/// 팀 응원 소리 매칭의 저수준 신호처리 — CheerSystemDesign.md §14.3.
/// 프레임 dB, MFCC(13) 원본 계수, YIN 간이 피치, 끊김 횟수, DTW(전체·부분 구간).
///
/// [정규화 없음 — 2026-10-07 합성 시험] 발화 단위 평균(·분산) 정규화(CMN/CMVN)는 길게 끄는 모음의 음색을
/// 지워 "오오오"·"에에에"·소음이 서로 비슷해졌다(분산 정규화: 내 소리 5.2 vs 소음 5.9). 판정은 같은 사람·같은
/// 마이크 비교라 채널 보정이 필요 없으므로 MFCC c1~c13+Δ를 그대로 쓴다(소음 거리 12~15로 벌어짐). c0(음량)은 빼서
/// 마이크 음량 차이는 영향 없음.
///
/// [스레드] 인스턴스마다 작업 버퍼를 들고 있어 **한 인스턴스는 한 스레드에서만** 쓴다.
/// 정적 함수(CountBursts·AppendDeltas·Dtw*)는 호출자가 버퍼를 넘기므로 어디서든 호출 가능.
/// Unity API를 쓰지 않는다 — 워커 스레드에서 그대로 돈다.
/// </summary>
public sealed class CheerSoundDsp
{
    const int N    = CheerSoundParams.FftSize;
    const int Bins = N / 2 + 1;

    static readonly float[]   s_hamming = BuildHamming();
    static readonly float[][] s_mel     = BuildMelBank();
    static readonly float[,]  s_dct     = BuildDct();
    static readonly int[]     s_bitRev  = BuildBitReverse();
    static readonly float[]   s_cos     = BuildTwiddle(true);
    static readonly float[]   s_sin     = BuildTwiddle(false);

    readonly float[] _re    = new float[N];
    readonly float[] _im    = new float[N];
    readonly float[] _melE  = new float[CheerSoundParams.MelBands];
    readonly float[] _yin   = new float[(int)(CheerSoundParams.SampleRate / CheerSoundParams.PitchMinHz) + 2];

    // ── 프레임 단위 ─────────────────────────────────────────────

    /// <summary>x[offset..offset+FrameLen) RMS dBFS. 범위 밖은 0으로 본다.</summary>
    public static float FrameDb(float[] x, int offset, int count)
    {
        double sum = 0;
        int end = Math.Min(offset + CheerSoundParams.FrameLen, count);
        for (int i = offset; i < end; i++) sum += x[i] * x[i];
        double rms = Math.Sqrt(sum / CheerSoundParams.FrameLen);
        return (float)(20.0 * Math.Log10(Math.Max(rms, 1e-6)));
    }

    /// <summary>MFCC c1..c13 원본(정규화 전). 프리엠퍼시스는 프레임 앞 샘플을 이어 받는다.</summary>
    public void Cepstra(float[] x, int offset, int count, float[] dst, int dstOffset)
    {
        const int L = CheerSoundParams.FrameLen;
        float a = CheerSoundParams.PreEmphasis;
        float prev = offset > 0 && offset - 1 < count ? x[offset - 1] : 0f;

        for (int i = 0; i < N; i++)
        {
            float v = 0f;
            if (i < L)
            {
                int idx = offset + i;
                float cur = idx < count ? x[idx] : 0f;
                v = (cur - a * prev) * s_hamming[i];
                prev = cur;
            }
            _re[i] = v;
            _im[i] = 0f;
        }

        Fft(_re, _im);

        for (int m = 0; m < CheerSoundParams.MelBands; m++)
        {
            float[] w = s_mel[m];
            double e = 0;
            for (int k = 0; k < Bins; k++)
            {
                if (w[k] == 0f) continue;
                e += w[k] * (_re[k] * _re[k] + _im[k] * _im[k]);
            }
            _melE[m] = (float)Math.Log(Math.Max(e, 1e-10));
        }

        for (int c = 0; c < CheerSoundParams.Ceps; c++)
        {
            double s = 0;
            for (int m = 0; m < CheerSoundParams.MelBands; m++) s += _melE[m] * s_dct[c, m];
            dst[dstOffset + c] = (float)s;
        }
    }

    /// <summary>YIN 간이형. 유성이면 true + Hz. 분석 창은 FrameLen, 지연은 PitchMaxHz~PitchMinHz.</summary>
    public bool Pitch(float[] x, int offset, int count, out float hz)
    {
        hz = 0f;
        const int W = CheerSoundParams.FrameLen;
        int minLag = (int)(CheerSoundParams.SampleRate / CheerSoundParams.PitchMaxHz);
        int maxLag = (int)(CheerSoundParams.SampleRate / CheerSoundParams.PitchMinHz);
        if (offset + W + maxLag > count) return false;

        _yin[0] = 1f;
        double running = 0;
        for (int tau = 1; tau <= maxLag; tau++)
        {
            double d = 0;
            for (int i = 0; i < W; i++)
            {
                float diff = x[offset + i] - x[offset + i + tau];
                d += diff * diff;
            }
            running += d;
            _yin[tau] = running > 0 ? (float)(d * tau / running) : 1f;
        }

        // 유성 판단과 주기 고르기를 나눈다(10/7).
        // 단일 기준(0.25)이면 "아"처럼 3배음이 센 고음에서 2/3 주기의 얕은 골을 먼저 잡아 5도 높게 읽었다(330→470Hz).
        // → 가장 깊은 골이 유성 기준보다 낮으면 유성, 주기는 "가장 깊은 골 + 여유" 안에 드는 첫 골.
        float globalMin = float.MaxValue;
        for (int tau = minLag; tau <= maxLag; tau++) if (_yin[tau] < globalMin) globalMin = _yin[tau];
        if (globalMin >= CheerSoundParams.YinVoicingThreshold) return false;
        float pick = Math.Max(CheerSoundParams.YinThreshold, globalMin + CheerSoundParams.YinPickMargin);

        for (int tau = minLag; tau <= maxLag; tau++)
        {
            if (_yin[tau] >= pick) continue;
            while (tau + 1 <= maxLag && _yin[tau + 1] < _yin[tau]) tau++;

            // 포물선 보간
            float better = tau;
            if (tau > 1 && tau < maxLag)
            {
                float s0 = _yin[tau - 1], s1 = _yin[tau], s2 = _yin[tau + 1];
                float den = s0 + s2 - 2f * s1;
                if (Math.Abs(den) > 1e-9f) better = tau + 0.5f * (s0 - s2) / den;
            }
            hz = CheerSoundParams.SampleRate / better;
            return true;
        }
        return false;
    }

    // ── 시퀀스 단위 (정적) ──────────────────────────────────────

    /// <summary>
    /// 끊김 횟수 — 3프레임 평균 dB 곡선에서 BurstDipDb만큼 내려갔다 다시 올라오면 새 덩어리.
    /// 봉우리 사이 BurstMinGapFrames 미만이면 같은 덩어리로 본다.
    /// </summary>
    public static int CountBursts(float[] db, int from, int to, float floorDb)
    {
        float speech = floorDb + CheerSoundParams.SpeechAboveFloorDb;
        int bursts = 0;
        bool inBurst = false;
        float peak = float.NegativeInfinity, valley = float.PositiveInfinity;
        int lastPeak = int.MinValue / 2;

        for (int t = from; t <= to; t++)
        {
            float v = Smooth3(db, t, from, to);
            if (!inBurst)
            {
                valley = Math.Min(valley, v);
                bool rose = bursts == 0 || v - valley >= CheerSoundParams.BurstDipDb;
                if (v >= speech && rose)
                {
                    if (bursts == 0 || t - lastPeak >= CheerSoundParams.BurstMinGapFrames) bursts++;
                    inBurst = true;
                    peak = v;
                    lastPeak = t;
                }
            }
            else
            {
                if (v > peak) { peak = v; lastPeak = t; }
                if (peak - v >= CheerSoundParams.BurstDipDb || v < speech)
                {
                    inBurst = false;
                    valley = v;
                }
            }
        }
        return bursts;
    }

    static float Smooth3(float[] db, int t, int from, int to)
    {
        float a = db[Math.Max(from, t - 1)], b = db[t], c = db[Math.Min(to, t + 1)];
        return (a + b + c) / 3f;
    }

    /// <summary>원본 13계수 [frames×13] → 26차원 [frames×26] (계수 + Δ, 회귀 폭 2).</summary>
    public static void AppendDeltas(float[] ceps, int frames, float[] dst)
    {
        const int C = CheerSoundParams.Ceps, D = CheerSoundParams.Dims;
        for (int t = 0; t < frames; t++)
        {
            for (int c = 0; c < C; c++)
            {
                dst[t * D + c] = ceps[t * C + c];
                float num = 0f;
                for (int n = 1; n <= 2; n++)
                {
                    int tp = Math.Min(frames - 1, t + n), tm = Math.Max(0, t - n);
                    num += n * (ceps[tp * C + c] - ceps[tm * C + c]);
                }
                dst[t * D + C + c] = num / 10f;
            }
        }
    }

    static float FrameDist(float[] a, int ia, float[] b, int ib)
    {
        const int D = CheerSoundParams.Dims;
        int oa = ia * D, ob = ib * D;
        float s = 0f;
        for (int d = 0; d < D; d++)
        {
            float diff = a[oa + d] - b[ob + d];
            s += diff * diff;
        }
        return (float)Math.Sqrt(s);
    }

    /// <summary>앞쪽 계수 k개(+그 Δ)만 쓰는 거리 — 다른 사람끼리 비교용. 뒤쪽 계수는 음높이 배음이 섞여 사람마다 크게 갈린다.</summary>
    static float FrameDistLow(float[] a, int ia, float[] b, int ib, int k)
    {
        const int D = CheerSoundParams.Dims, C = CheerSoundParams.Ceps;
        int oa = ia * D, ob = ib * D;
        float s = 0f;
        for (int c = 0; c < k; c++)
        {
            float d0 = a[oa + c] - b[ob + c];
            float d1 = a[oa + C + c] - b[ob + C + c];
            s += d0 * d0 + d1 * d1;
        }
        return (float)Math.Sqrt(s);
    }

    /// <summary>DTW 작업 버퍼 — 판정기·틀 검사가 각자 하나씩 들고 재사용(GC 없음).</summary>
    public sealed class DtwWork
    {
        public float[] PrevD = Array.Empty<float>(), CurD = Array.Empty<float>();
        public int[]   PrevL = Array.Empty<int>(),   CurL = Array.Empty<int>();
        public int[]   PrevS = Array.Empty<int>(),   CurS = Array.Empty<int>();
        /// <summary>그 칸에 들어온 마지막 이동 — 0 대각/시작, 1 세로(등록본만 전진), 2 가로(실시간만 전진).</summary>
        public byte[]  PrevM = Array.Empty<byte>(),  CurM = Array.Empty<byte>();

        public void Ensure(int m)
        {
            if (PrevD.Length >= m) return;
            PrevD = new float[m]; CurD = new float[m];
            PrevL = new int[m];   CurL = new int[m];
            PrevS = new int[m];   CurS = new int[m];
            PrevM = new byte[m];  CurM = new byte[m];
        }

        public void Swap()
        {
            (PrevD, CurD) = (CurD, PrevD);
            (PrevL, CurL) = (CurL, PrevL);
            (PrevS, CurS) = (CurS, PrevS);
            (PrevM, CurM) = (CurM, PrevM);
        }
    }

    /// <summary>
    /// 전체 DTW — 두 발화 처음~끝 정렬. 대각선 기준 Sakoe-Chiba 띠. 반환 = 누적 거리 / 경로 길이.
    /// 등록본 2개 사이 거리(T_self 보정)·틀 검사 음색에 쓴다. cepsUsed &lt; 13이면 앞쪽 계수만(틀 검사 — 다른 사람끼리).
    /// </summary>
    public static float DtwFull(float[] a, int na, float[] b, int nb, DtwWork w,
                                int cepsUsed = CheerSoundParams.Ceps)
    {
        if (na <= 0 || nb <= 0) return float.PositiveInfinity;
        w.Ensure(nb);
        int band = (int)Math.Ceiling(CheerSoundParams.DtwBandRatio * Math.Max(na, nb)) + 2;
        const float Inf = float.PositiveInfinity;

        for (int i = 0; i < na; i++)
        {
            int center = na == 1 ? 0 : (int)Math.Round(i * (nb - 1) / (double)(na - 1));
            int lo = Math.Max(0, center - band), hi = Math.Min(nb - 1, center + band);
            for (int j = 0; j < nb; j++) { w.CurD[j] = Inf; w.CurL[j] = 0; }

            for (int j = lo; j <= hi; j++)
            {
                float c = cepsUsed >= CheerSoundParams.Ceps ? FrameDist(a, i, b, j) : FrameDistLow(a, i, b, j, cepsUsed);
                if (i == 0 && j == 0) { w.CurD[j] = c; w.CurL[j] = 1; continue; }

                float best = Inf; int bestL = 0;
                if (i > 0 && j > 0 && w.PrevD[j - 1] < best) { best = w.PrevD[j - 1]; bestL = w.PrevL[j - 1]; }
                if (i > 0 && w.PrevD[j] < best)              { best = w.PrevD[j];     bestL = w.PrevL[j]; }
                if (j > 0 && w.CurD[j - 1] < best)           { best = w.CurD[j - 1];  bestL = w.CurL[j - 1]; }
                if (float.IsInfinity(best)) continue;

                w.CurD[j] = best + c;
                w.CurL[j] = bestL + 1;
            }
            w.Swap();
        }

        float d = w.PrevD[nb - 1];
        return float.IsInfinity(d) ? Inf : d / w.PrevL[nb - 1];
    }

    /// <summary>
    /// 부분 구간 DTW — 등록본 전체를 실시간 버퍼의 어느 구간에든 맞춘다(시작·끝 자유, §14.6).
    /// 반환 = 최소 (누적 거리 / 경로 길이). start/end는 버퍼 안에서 맞춰진 구간(프레임, 포함).
    ///
    /// [기울기 제한 — 10/7] 세로·가로 이동은 두 번 연달아 못 한다(Itakura식) → 맞춰지는 구간 길이가 등록본의
    /// 0.5~2배로 묶인다(리듬 검사와 같은 범위). 제한이 없을 때 "오오오"처럼 처음부터 끝까지 같은 소리는 등록본 전체를
    /// 아주 짧은 구간에 몰아 맞춰(Lab 길이비 0.28) 리듬 검사에서 떨어졌다.
    /// </summary>
    public static float DtwSubsequence(float[] tmpl, int n, float[] live, int m, DtwWork w,
                                       out int start, out int end)
    {
        start = end = -1;
        if (n <= 0 || m <= 0) return float.PositiveInfinity;
        w.Ensure(m);
        const float Inf = float.PositiveInfinity;

        for (int j = 0; j < m; j++)
        {
            w.PrevD[j] = FrameDist(tmpl, 0, live, j);
            w.PrevL[j] = 1;
            w.PrevS[j] = j;
            w.PrevM[j] = 0;
        }

        for (int i = 1; i < n; i++)
        {
            for (int j = 0; j < m; j++)
            {
                float best = Inf; int bestL = 0, bestS = 0; byte move = 0;
                if (j > 0 && w.PrevD[j - 1] < best)
                { best = w.PrevD[j - 1]; bestL = w.PrevL[j - 1]; bestS = w.PrevS[j - 1]; move = 0; }
                if (w.PrevM[j] != 1 && w.PrevD[j] < best)
                { best = w.PrevD[j];     bestL = w.PrevL[j];     bestS = w.PrevS[j];     move = 1; }
                if (j > 0 && w.CurM[j - 1] != 2 && w.CurD[j - 1] < best)
                { best = w.CurD[j - 1];  bestL = w.CurL[j - 1];  bestS = w.CurS[j - 1];  move = 2; }

                if (float.IsInfinity(best)) { w.CurD[j] = Inf; w.CurL[j] = 1; w.CurS[j] = j; w.CurM[j] = 0; continue; }
                w.CurD[j] = best + FrameDist(tmpl, i, live, j);
                w.CurL[j] = bestL + 1;
                w.CurS[j] = bestS;
                w.CurM[j] = move;
            }
            w.Swap();
        }

        float bestNorm = Inf;
        for (int j = 0; j < m; j++)
        {
            if (float.IsInfinity(w.PrevD[j])) continue;
            float norm = w.PrevD[j] / w.PrevL[j];
            if (norm < bestNorm) { bestNorm = norm; start = w.PrevS[j]; end = j; }
        }
        return bestNorm;
    }

    // ── FFT / 테이블 ────────────────────────────────────────────

    static void Fft(float[] re, float[] im)
    {
        for (int i = 0; i < N; i++)
        {
            int j = s_bitRev[i];
            if (j > i) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); }
        }
        for (int size = 2; size <= N; size <<= 1)
        {
            int half = size >> 1, step = N / size;
            for (int start = 0; start < N; start += size)
            {
                for (int k = 0; k < half; k++)
                {
                    float wr = s_cos[k * step], wi = s_sin[k * step];
                    int a = start + k, b = a + half;
                    float tr = re[b] * wr - im[b] * wi;
                    float ti = re[b] * wi + im[b] * wr;
                    re[b] = re[a] - tr; im[b] = im[a] - ti;
                    re[a] += tr;        im[a] += ti;
                }
            }
        }
    }

    static int[] BuildBitReverse()
    {
        int bits = 0;
        while ((1 << bits) < N) bits++;
        var r = new int[N];
        for (int i = 0; i < N; i++)
        {
            int x = i, y = 0;
            for (int b = 0; b < bits; b++) { y = (y << 1) | (x & 1); x >>= 1; }
            r[i] = y;
        }
        return r;
    }

    static float[] BuildTwiddle(bool cos)
    {
        var t = new float[N / 2];
        for (int k = 0; k < N / 2; k++)
        {
            double ang = -2.0 * Math.PI * k / N;
            t[k] = (float)(cos ? Math.Cos(ang) : Math.Sin(ang));
        }
        return t;
    }

    static float[] BuildHamming()
    {
        const int L = CheerSoundParams.FrameLen;
        var w = new float[L];
        for (int i = 0; i < L; i++) w[i] = (float)(0.54 - 0.46 * Math.Cos(2.0 * Math.PI * i / (L - 1)));
        return w;
    }

    static double HzToMel(double hz) => 2595.0 * Math.Log10(1.0 + hz / 700.0);
    static double MelToHz(double mel) => 700.0 * (Math.Pow(10.0, mel / 2595.0) - 1.0);

    static float[][] BuildMelBank()
    {
        const int M = CheerSoundParams.MelBands;
        double lo = HzToMel(0), hi = HzToMel(CheerSoundParams.SampleRate / 2.0);
        var edges = new double[M + 2];
        for (int i = 0; i < M + 2; i++)
            edges[i] = MelToHz(lo + (hi - lo) * i / (M + 1)) * N / CheerSoundParams.SampleRate; // bin 단위(실수)

        var bank = new float[M][];
        for (int m = 0; m < M; m++)
        {
            bank[m] = new float[Bins];
            double l = edges[m], c = edges[m + 1], r = edges[m + 2];
            for (int k = 0; k < Bins; k++)
            {
                double v = 0;
                if (k > l && k <= c) v = (k - l) / (c - l);
                else if (k > c && k < r) v = (r - k) / (r - c);
                bank[m][k] = (float)v;
            }
        }
        return bank;
    }

    static float[,] BuildDct()
    {
        const int M = CheerSoundParams.MelBands, C = CheerSoundParams.Ceps;
        var t = new float[C, M];
        double scale = Math.Sqrt(2.0 / M);
        for (int c = 0; c < C; c++)
            for (int m = 0; m < M; m++)
                t[c, m] = (float)(scale * Math.Cos(Math.PI * (c + 1) * (m + 0.5) / M)); // c1부터
        return t;
    }
}
