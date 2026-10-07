using System;
using Dissonance.Audio.Playback;
using UnityEngine;

/// <summary>
/// 팀원 목소리 **받는 쪽** 자동 음량 키우기(AGC + 리미터) — 2026-10-08.
///
/// [왜 단순 증폭이 아닌가]
/// 9/29 증폭 필터(×2·×3)는 Dissonance 재생 경로 끝의 Opus 소프트 클리퍼·디지털 최대치(1.0)에 큰 소리가 막혀
/// 100%와 똑같이 들렸다. 이 컴포넌트는 최대치는 그대로 두고 **평균 크기**를 올린다:
///   ① 말하는 동안의 평균 크기(RMS)를 재서 목표(TargetDb)까지 모자란 만큼만 키운다(최대 MaxBoostDb).
///   ② 키운 뒤 최대치(Ceiling)를 넘는 순간은 리미터가 즉시 눌러 잘림이 없다.
///   ③ 조용한 구간(GateDb 미만)에서는 크기를 다시 재지 않는다 — 말 사이 잡음을 끌어올리지 않게.
///
/// [위치 — Dissonance 소스 확인]
/// VoicePlayback GameObject = AudioSource + SamplePlaybackComponent(OnAudioFilterRead로 디코드한 목소리를 써 넣음).
/// 이 컴포넌트는 런타임 AddComponent라 그 뒤에 붙고, Unity는 필터를 컴포넌트 순서대로 돌리므로 Dissonance 출력을 받는다.
///
/// [팀 보이스 슬라이더] TeamVoiceVolume이 AudioSource.volume 대신 <see cref="Volume"/>을 쓴다(키운 뒤 곱함).
/// AudioSource.volume이 필터 앞/뒤 어디서 적용되든 키우기가 슬라이더를 되돌리지 않게 하기 위함.
///
/// 오디오 스레드에서 돈다 — 할당·Unity API 금지.
/// </summary>
[DisallowMultipleComponent]
public class VoiceLoudnessBoost : MonoBehaviour
{
    const float TargetDb      = -20f;  // 말하는 동안 평균 크기 목표(dBFS RMS)
    const float MaxBoostDb    = 15f;   // 최대 키움(≈ 5.6배)
    const float GateDb        = -45f;  // 이보다 작은 블록은 "말 아님" — 크기 측정 보류
    const float LevelTauSec   = 0.3f;  // 평균 크기 추적 속도
    const float RiseDbPerSec  = 10f;   // 키움이 커지는 속도(천천히 — 숨소리 펌핑 방지)
    const float FallDbPerSec  = 40f;   // 키움이 줄어드는 속도(빠르게 — 갑자기 큰 소리 대비)
    const float Ceiling       = 0.9f;  // 리미터 최대치
    const float LimiterReleaseSec = 0.08f;

    /// <summary>팀 보이스 슬라이더 값(0~1). 메인 스레드가 쓰고 오디오 스레드가 읽는다.</summary>
    public float Volume
    {
        get => _volume;
        set => _volume = Mathf.Clamp01(value);
    }

    volatile float _volume = 1f;
    int   _sampleRate;
    float _levelDb = float.NaN;
    float _gainDb;
    float _appliedGain = 1f;
    float _limiterEnv;
    float _limiterRelease;

    /// <summary>원격 목소리 재생 오브젝트에 붙어 있게 보장(풀 재사용 시 이미 있으면 그대로).</summary>
    public static VoiceLoudnessBoost Ensure(VoicePlayback playback)
    {
        if (playback == null) return null;
        var boost = playback.GetComponent<VoiceLoudnessBoost>();
        return boost != null ? boost : playback.gameObject.AddComponent<VoiceLoudnessBoost>();
    }

    void Awake()
    {
        _sampleRate = AudioSettings.outputSampleRate;
        _limiterRelease = (float)Math.Exp(-1.0 / (LimiterReleaseSec * Math.Max(1, _sampleRate)));
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (_sampleRate <= 0 || channels <= 0) return;
        int frames = data.Length / channels;
        if (frames == 0) return;

        // ① 평균 크기 — 1채널 기준(Dissonance는 모노를 전 채널에 복사)
        double sum = 0;
        for (int i = 0; i < data.Length; i += channels) sum += data[i] * data[i];
        float blockDb = 10f * (float)Math.Log10(sum / frames + 1e-12);
        float blockSec = frames / (float)_sampleRate;

        if (blockDb > GateDb)
        {
            if (float.IsNaN(_levelDb))
            {
                _levelDb = blockDb;
                _gainDb = Math.Clamp(TargetDb - _levelDb, 0f, MaxBoostDb); // 첫 마디부터 바로 키움(리미터가 보호)
            }
            else
            {
                _levelDb += (blockDb - _levelDb) * (1f - (float)Math.Exp(-blockSec / LevelTauSec));
            }
        }

        float wantDb = float.IsNaN(_levelDb) ? 0f : Math.Clamp(TargetDb - _levelDb, 0f, MaxBoostDb);
        float maxStep = (wantDb > _gainDb ? RiseDbPerSec : FallDbPerSec) * blockSec;
        _gainDb += Math.Clamp(wantDb - _gainDb, -maxStep, maxStep);

        // ② 키움(블록 안에서 부드럽게) → 리미터 → 슬라이더
        float g0 = _appliedGain;
        float g1 = (float)Math.Pow(10.0, _gainDb / 20.0);
        float vol = _volume;
        float env = _limiterEnv;

        for (int f = 0; f < frames; f++)
        {
            float g = g0 + (g1 - g0) * (f + 1) / frames;
            int idx = f * channels;

            float peak = 0f;
            for (int c = 0; c < channels; c++)
            {
                float a = Math.Abs(data[idx + c] * g);
                if (a > peak) peak = a;
            }
            env = peak > env ? peak : env * _limiterRelease;
            float scale = env > Ceiling ? g * Ceiling / env : g;

            for (int c = 0; c < channels; c++)
                data[idx + c] *= scale * vol;
        }

        _limiterEnv = env;
        _appliedGain = g1;
    }
}
