using UnityEngine;

/// <summary>
/// Host 기준 소리 재생(2D, 로컬 전용) — 패널 [듣기]·HUD R키(§14.7). 필요할 때 스스로 생기는 DDOL 싱글톤.
/// 볼륨은 SFX 파이프라인(마스터 × SFX)을 따른다. 응원 판정과 무관 — 스피커로 나간 소리가 내 마이크에
/// 들어갈 수는 있으나 피해는 "함정이 조금 쉬워짐"뿐(§14.11 ② 주의점).
/// </summary>
public class CheerSoundPlayback : MonoBehaviour
{
    static CheerSoundPlayback s_instance;
    AudioSource _source;

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
        var clip = AudioClip.Create("CheerSoundPreview", pcm.Length, 1, rate, false);
        clip.SetData(pcm, 0);
        Play(clip);
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
        }
        var src = s_instance._source;
        src.Stop();
        src.clip = clip;
        src.volume = SFXManager.Instance != null ? SFXManager.Instance.EffectiveVolume : 1f;
        src.Play();
    }
}
