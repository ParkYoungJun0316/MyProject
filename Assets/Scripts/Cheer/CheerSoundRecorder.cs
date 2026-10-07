using System;

/// <summary>
/// 응원 소리 녹음 버퍼 — CheerSystemDesign.md §14.2 "버튼 누르면 바로 녹음, 다시 누르면 정지".
/// 마이크를 직접 열지 않는다(§4.3 이중 오픈 금지) — 호출자가 기존 16kHz 스트림을 <see cref="Append"/>로 넘긴다.
/// 앞뒤 RecordEdgeDropSec(버튼 클릭 소리)는 버리고, RecordMaxSec에 닿으면 스스로 멈춘다(안전장치).
/// 앞뒤 침묵 자르기는 <see cref="CheerSoundTemplate.Build"/>가 한다.
/// </summary>
public sealed class CheerSoundRecorder
{
    const int Rate = CheerSoundParams.SampleRate;

    // 앞 클릭 구간(RecordEdgeDropSec)까지 합쳐 RecordMaxSec에 자동 정지되도록
    readonly float[] _buf = new float[(int)(Rate * (CheerSoundParams.RecordMaxSec - CheerSoundParams.RecordEdgeDropSec))];
    readonly int _edge = (int)(Rate * CheerSoundParams.RecordEdgeDropSec);
    int _count;
    int _skip;

    public bool IsRecording { get; private set; }
    /// <summary>상한에 닿아 자동 정지됐는지 — 호출자가 보고 End()를 부른다.</summary>
    public bool HitLimit { get; private set; }
    public float ElapsedSec => (_count + (_edge - _skip)) / (float)Rate;

    public void Begin()
    {
        _count = 0;
        _skip = _edge;
        HitLimit = false;
        IsRecording = true;
    }

    /// <summary>녹음 중이 아니거나 상한이면 무시. 상한에 닿으면 IsRecording=false, HitLimit=true.</summary>
    public void Append(float[] samples, int offset, int count)
    {
        if (!IsRecording) return;

        if (_skip > 0)
        {
            int drop = Math.Min(_skip, count);
            _skip -= drop;
            offset += drop;
            count -= drop;
        }

        int room = _buf.Length - _count;
        int n = Math.Min(room, count);
        if (n > 0)
        {
            Array.Copy(samples, offset, _buf, _count, n);
            _count += n;
        }
        if (_count >= _buf.Length)
        {
            IsRecording = false;
            HitLimit = true;
        }
    }

    /// <summary>정지 — 끝 RecordEdgeDropSec를 버린 녹음을 돌려준다(상한 자동 정지면 그대로).</summary>
    public float[] End()
    {
        bool manual = IsRecording;
        IsRecording = false;
        int len = manual ? Math.Max(0, _count - _edge) : _count;
        var outBuf = new float[len];
        Array.Copy(_buf, outBuf, len);
        return outBuf;
    }
}
