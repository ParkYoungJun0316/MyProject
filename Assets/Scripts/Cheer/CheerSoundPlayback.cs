using UnityEngine;

/// <summary>
/// Host 기준 소리 재생(2D, 로컬 전용) — 패널 [듣기]·HUD R키(§14.7). 필요할 때 스스로 생기는 DDOL 싱글톤.
/// 볼륨은 SFX 파이프라인(마스터 × SFX)을 따른다. 응원 판정과 무관 — 스피커로 나간 소리가 내 마이크에
/// 들어갈 수는 있으나 피해는 "함정이 조금 쉬워짐"뿐(§14.11 ② 주의점).
///
/// [2026-10-08 너무 작게 들림] 녹음은 마이크 원본(크기 보정 없음)이라 작다 → 재생용 사본만 <see cref="NormalizeForPlayback"/>로
/// 키운다(판정용 원본·특징은 그대로). 재생 중엔 VoiceDucking이 게임 소리를 줄이고, 이 소리는 ignoreListenerVolume으로 빠진다.
/// </summary>
public class CheerSoundPlayback : MonoBehaviour
{
    /// <summary>재생용 최대치 목표(가장 큰 지점).</summary>
    const float NormalizePeak    = 0.9f;
    /// <summary>최대 키움 배율 — 잡음뿐인 녹음이 폭발하지 않게(≈ +30dB).</summary>
    const float NormalizeMaxGain = 30f;

    static CheerSoundPlayback s_instance;
    AudioSource _source;

    /// <summary>응원 소리가 재생 중인가 — VoiceDucking이 게임 소리를 줄이는 데 쓴다(메인 스레드).</summary>
    public static bool IsPlaying => s_instance != null && s_instance._source != null && s_instance._source.isPlaying;

    public static void PlayHostClip()
    {
        var clip = CheerSoundLocalState.GetHostAudioClip();
        if (clip == null) return;
        Play(clip);
    }

    /// <summary>HUD R키 — 등록했으면 내 1번 녹음(판정 기준이 내 녹음이므로), 아니면 Host 소리(10/7 사용자 결정).</summary>
    public static void PlayListenClip()
    {
        var clip = CheerSoundLocalState.GetListenClip();
        if (clip == null) return;
        Play(clip);
    }

    /// <summary>녹음 직후 자기 소리 확인용(16kHz float).</summary>
    public static void PlayPcm(float[] pcm, int rate)
    {
        if (pcm == null || pcm.Length == 0) return;
        var data = NormalizeForPlayback(pcm);
        var clip = AudioClip.Create("CheerSoundPreview", data.Length, 1, rate, false);
        clip.SetData(data, 0);
        Play(clip);
    }

    /// <summary>
    /// 재생용 사본 — 가장 큰 지점이 <see cref="NormalizePeak"/>가 되게 전체를 같은 배율로 키운다(최대 <see cref="NormalizeMaxGain"/>배).
    /// 원본은 건드리지 않는다(판정·틀 검사는 원본 기준, §14 정규화 없음).
    /// </summary>
    public static float[] NormalizeForPlayback(float[] pcm)
    {
        var outBuf = (float[])pcm.Clone();
        float peak = 0f;
        for (int i = 0; i < outBuf.Length; i++)
        {
            float a = Mathf.Abs(outBuf[i]);
            if (a > peak) peak = a;
        }
        if (peak <= 0f) return outBuf;

        float gain = Mathf.Min(NormalizePeak / peak, NormalizeMaxGain);
        if (gain <= 1f) return outBuf;
        for (int i = 0; i < outBuf.Length; i++) outBuf[i] *= gain;
        return outBuf;
    }

    public static void Stop()
    {
        if (s_instance != null && s_instance._source != null) s_instance._source.Stop();
    }

    static void Play(AudioClip clip)
    {
        if (s_instance == null)
        {
            var go = new GameObject("CheerSoundPlayback");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<CheerSoundPlayback>();
            s_instance._source = go.AddComponent<AudioSource>();
            s_instance._source.spatialBlend = 0f;
            s_instance._source.playOnAwake = false;
            s_instance._source.ignoreListenerVolume = true; // 재생 중 VoiceDucking이 줄이는 건 게임 소리뿐
        }
        var src = s_instance._source;
        src.Stop();
        src.clip = clip;
        src.volume = SFXManager.Instance != null ? SFXManager.Instance.EffectiveVolume : 1f;
        src.Play();
    }
}
