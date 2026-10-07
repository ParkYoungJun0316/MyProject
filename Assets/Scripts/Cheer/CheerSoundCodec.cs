using System;

/// <summary>
/// Host 기준 소리 배포용 압축 — CheerSystemDesign.md §14.5. 16kHz float → 8kHz μ-law 8bit (3초 = 24KB).
/// 들려주기(재생) 전용. 틀 검사는 Host가 원본 16kHz로 뽑은 특징(<see cref="CheerSoundTemplate"/>)을 따로 보내므로
/// 압축 열화가 검사에 섞이지 않는다.
/// </summary>
public static class CheerSoundCodec
{
    public const int EncodedRate = 8000;

    const int Taps = 31;
    static readonly float[] s_lowPass = BuildLowPass();

    /// <summary>16kHz → 저역통과(3.6kHz) → 2:1 솎기 → μ-law.</summary>
    public static byte[] Encode(float[] pcm16k, int count)
    {
        int outLen = count / 2;
        var data = new byte[outLen];
        int mid = Taps / 2;
        for (int o = 0; o < outLen; o++)
        {
            int center = o * 2;
            float y = 0f;
            for (int k = 0; k < Taps; k++)
            {
                int i = center + k - mid;
                if (i >= 0 && i < count) y += pcm16k[i] * s_lowPass[k];
            }
            data[o] = LinearToMuLaw((short)Math.Clamp((int)(y * 32767f), -32768, 32767));
        }
        return data;
    }

    /// <summary>μ-law → 8kHz float. 재생은 AudioClip.Create(..., EncodedRate, ...)로 그대로.</summary>
    public static float[] Decode(byte[] data)
    {
        var pcm = new float[data.Length];
        for (int i = 0; i < data.Length; i++) pcm[i] = MuLawToLinear(data[i]) / 32768f;
        return pcm;
    }

    // G.711 μ-law
    const int Bias = 0x84, Clip = 32635;

    static byte LinearToMuLaw(short sample)
    {
        int s = sample;
        int sign = (s >> 8) & 0x80;
        if (sign != 0) s = -s;
        if (s > Clip) s = Clip;
        s += Bias;
        int exponent = 7;
        for (int mask = 0x4000; (s & mask) == 0 && exponent > 0; mask >>= 1) exponent--;
        int mantissa = (s >> (exponent + 3)) & 0x0F;
        return (byte)~(sign | (exponent << 4) | mantissa);
    }

    static short MuLawToLinear(byte mu)
    {
        int u = ~mu & 0xFF;
        int sign = u & 0x80, exponent = (u >> 4) & 0x07, mantissa = u & 0x0F;
        int s = (((mantissa << 3) + Bias) << exponent) - Bias;
        return (short)(sign != 0 ? -s : s);
    }

    static float[] BuildLowPass()
    {
        const double cutoff = 3600.0 / 16000.0; // 정규화(샘플레이트 대비)
        var h = new float[Taps];
        int mid = Taps / 2;
        double sum = 0;
        for (int i = 0; i < Taps; i++)
        {
            int n = i - mid;
            double sinc = n == 0 ? 2 * cutoff : Math.Sin(2 * Math.PI * cutoff * n) / (Math.PI * n);
            double win = 0.54 - 0.46 * Math.Cos(2 * Math.PI * i / (Taps - 1));
            h[i] = (float)(sinc * win);
            sum += h[i];
        }
        for (int i = 0; i < Taps; i++) h[i] = (float)(h[i] / sum);
        return h;
    }
}
