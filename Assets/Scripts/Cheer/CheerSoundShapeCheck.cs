using System;

/// <summary>틀 검사 결과 — 거절 이유. UI 거절 힌트와 1:1(§14.2·§14.4).</summary>
public enum CheerShapeVerdict
{
    Ok,
    TooQuiet,         // 너무 작아요
    TooLoud,          // 너무 커요(찢어짐)
    TooShort,         // 너무 짧아요 (절대 0.3초 미만 또는 기준 대비 짧음)
    TooLong,          // 너무 길어요 (절대 3초 초과 또는 기준 대비 김)
    BurstMismatch,    // 끊는 횟수가 달라요 (기준 N번, 나 M번)
    PitchMismatch,    // 높낮이가 달라요
    TimbreMismatch,   // 소리가 달라요
}

/// <summary>항목 하나의 판정 단계.</summary>
public enum CheerShapeBand { Pass, Soft, Hard, Skipped }

/// <summary>
/// 틀 검사 — 내 등록 녹음이 Host 기준 소리와 "같은 소리"인지 (CheerSystemDesign.md §14.4).
/// 목적은 같은 목소리가 아니라 같은 소리 — 길이·끊김·상대 높낮이·음색 4항목을 각각
/// 통과 / 애매(Soft) / 확실히 다름(Hard)으로 나누고, **Hard 1개 또는 Soft 3개 이상이면 거절**한다(10/7 사용자: 2개 → 3개).
/// 살짝 다른 따라하기는 보통 한 항목만 경계에 걸리고, 다른 소리는 여러 항목이 같이 틀리거나 한 항목이 크게 틀린다.
/// 모든 항목 수치를 채워서 돌려준다(Lab 튜닝용).
/// </summary>
public static class CheerSoundShapeCheck
{
    public struct Result
    {
        public CheerShapeVerdict Verdict;
        public float DurationRatio;
        public int   HostBursts, MyBursts;
        public float PitchCorr, PitchMeanAbsSemi;
        public float TimbreDistance;
        public CheerShapeBand DurationBand, BurstBand, PitchBand, TimbreBand;
        public int   SoftCount;
        public bool  PitchChecked => PitchBand != CheerShapeBand.Skipped;
    }

    public static Result Compare(CheerSoundTemplate mine, CheerSoundTemplate host, CheerSoundDsp.DtwWork work)
    {
        var r = new Result
        {
            HostBursts = host.Bursts,
            MyBursts = mine.Bursts,
            DurationRatio = host.DurationMs > 0 ? mine.DurationMs / (float)host.DurationMs : 0f,
            PitchCorr = float.NaN,
            PitchMeanAbsSemi = float.NaN,
            TimbreDistance = float.PositiveInfinity,
            PitchBand = CheerShapeBand.Skipped,
        };

        if (mine.Frames > 0 && host.Frames > 0)
            r.TimbreDistance = CheerSoundDsp.DtwFull(mine.Mfcc, mine.Frames, host.Mfcc, host.Frames, work,
                                                     CheerSoundParams.ShapeTimbreCeps);

        r.DurationBand = BandDuration(r.DurationRatio);
        r.BurstBand = BandBursts(r.MyBursts, r.HostBursts);
        if (mine.VoicedRatio >= CheerSoundParams.ShapeMinVoicedRatio && host.VoicedRatio >= CheerSoundParams.ShapeMinVoicedRatio)
        {
            ComparePitch(mine.PitchContour, host.PitchContour, out r.PitchCorr, out r.PitchMeanAbsSemi);
            r.PitchBand = BandPitch(r.PitchCorr, r.PitchMeanAbsSemi);
        }
        r.TimbreBand = r.TimbreDistance <= CheerSoundParams.ShapeTimbrePass ? CheerShapeBand.Pass
                     : r.TimbreDistance <= CheerSoundParams.ShapeTimbreSoft ? CheerShapeBand.Soft
                     : CheerShapeBand.Hard;

        r.SoftCount = (r.DurationBand == CheerShapeBand.Soft ? 1 : 0) + (r.BurstBand == CheerShapeBand.Soft ? 1 : 0)
                    + (r.PitchBand == CheerShapeBand.Soft ? 1 : 0) + (r.TimbreBand == CheerShapeBand.Soft ? 1 : 0);
        r.Verdict = Judge(mine, r);
        return r;
    }

    static CheerShapeVerdict Judge(CheerSoundTemplate mine, in Result r)
    {
        switch (mine.Issue)
        {
            case CheerClipIssue.TooQuiet: return CheerShapeVerdict.TooQuiet;
            case CheerClipIssue.TooLoud:  return CheerShapeVerdict.TooLoud;
            case CheerClipIssue.TooShort: return CheerShapeVerdict.TooShort;
            case CheerClipIssue.TooLong:  return CheerShapeVerdict.TooLong;
        }

        // Hard 우선, 없으면 Soft가 ShapeMaxSoft 넘을 때 첫 Soft 항목을 이유로 — 순서 = 플레이어가 고치기 쉬운 순
        foreach (var band in new[] { CheerShapeBand.Hard, CheerShapeBand.Soft })
        {
            if (band == CheerShapeBand.Soft && r.SoftCount <= CheerSoundParams.ShapeMaxSoft) break;
            if (r.DurationBand == band) return r.DurationRatio < 1f ? CheerShapeVerdict.TooShort : CheerShapeVerdict.TooLong;
            if (r.BurstBand == band)    return CheerShapeVerdict.BurstMismatch;
            if (r.PitchBand == band)    return CheerShapeVerdict.PitchMismatch;
            if (r.TimbreBand == band)   return CheerShapeVerdict.TimbreMismatch;
        }
        return CheerShapeVerdict.Ok;
    }

    static CheerShapeBand BandDuration(float ratio)
    {
        if (ratio >= CheerSoundParams.ShapeDurPassMin && ratio <= CheerSoundParams.ShapeDurPassMax) return CheerShapeBand.Pass;
        if (ratio >= CheerSoundParams.ShapeDurSoftMin && ratio <= CheerSoundParams.ShapeDurSoftMax) return CheerShapeBand.Soft;
        return CheerShapeBand.Hard;
    }

    /// <summary>
    /// 기준 1~5번: 같으면 통과, 다르면 애매. 기준 6번+(빠른 음절): ±1 통과, 그 밖 애매. 확실히 다름(✗)은 없다.
    /// </summary>
    static CheerShapeBand BandBursts(int mine, int host)
    {
        // 10/7 사용자 결정: 끊김은 최대 △(✗ 없음). 같은 사람이 같은 말("what's going on")을 해도 5 vs 3이 나왔다 —
        // 음절 사이 약한 틈은 붙었다 떨어졌다 한다. 다른 소리는 음색·높낮이에서 걸러진다.
        int diff = Math.Abs(mine - host);
        int passTol = host >= CheerSoundParams.ShapeBurstLooseFrom ? 1 : 0;
        return diff <= passTol ? CheerShapeBand.Pass : CheerShapeBand.Soft;
    }

    /// <summary>상관이 높거나 평균 차가 작으면 통과(평평한 소리끼리는 상관이 무의미해서 평균 차로 본다).</summary>
    static CheerShapeBand BandPitch(float corr, float meanAbs)
    {
        if (corr >= CheerSoundParams.ShapePitchPassCorr || meanAbs <= CheerSoundParams.ShapePitchPassAbs) return CheerShapeBand.Pass;
        if (corr >= CheerSoundParams.ShapePitchSoftCorr || meanAbs <= CheerSoundParams.ShapePitchSoftAbs) return CheerShapeBand.Soft;
        return CheerShapeBand.Hard;
    }

    /// <summary>시간 정규화된 두 곡선(각 PitchPoints점) 직접 비교. 한쪽이 완전히 평평하면 상관은 0.</summary>
    static void ComparePitch(float[] a, float[] b, out float corr, out float meanAbs)
    {
        int n = Math.Min(a.Length, b.Length);
        double ma = 0, mb = 0;
        for (int i = 0; i < n; i++) { ma += a[i]; mb += b[i]; }
        ma /= n; mb /= n;

        double sab = 0, saa = 0, sbb = 0, abs = 0;
        for (int i = 0; i < n; i++)
        {
            double da = a[i] - ma, db = b[i] - mb;
            sab += da * db; saa += da * da; sbb += db * db;
            abs += Math.Abs(a[i] - b[i]);
        }
        corr = saa > 1e-6 && sbb > 1e-6 ? (float)(sab / Math.Sqrt(saa * sbb)) : 0f;
        meanAbs = (float)(abs / n);
    }
}
