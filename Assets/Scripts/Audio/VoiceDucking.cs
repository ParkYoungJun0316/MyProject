using Dissonance;
using Dissonance.Audio.Playback;
using UnityEngine;

/// <summary>
/// 팀원이 말하는 동안 게임 소리(BGM·SFX 전부)를 줄이는 더킹. DDOL NetworkManager GameObject에 부착
/// (GameSettingsManager·DissonanceComms와 같은 자리).
///
/// [왜 — 2026-09-29]
/// 팀원 목소리가 BGM/SFX에 묻혀 100%에서도 작게 들렸고, 100% 초과 증폭은 디지털 최대치에 막혀 효과가 없었다.
/// Dissonance 공식 문서(Audio Mixing For Voice)가 권장하는 방식대로 목소리를 키우지 않고 게임 소리를 낮춘다.
///
/// [방식 — AudioListener.volume]
/// SFX는 재생 순간에 볼륨이 정해지고(PlayOneShot·PlayClipAtPoint·각자 AudioSource) 경로가 흩어져 있어
/// 소비처마다 배율을 곱하면 이미 재생 중인 소리는 안 줄어든다. 그래서 전체 출력인 AudioListener.volume을
/// 줄이고, 원격 팀원 목소리 AudioSource만 ignoreListenerVolume=true로 빼서 목소리는 그대로 둔다.
/// AudioListener.volume은 이 컴포넌트 외에 아무도 쓰지 않는다(마스터 볼륨은 BGMManager/SFXManager의 EffectiveVolume 담당).
///
/// 팀원(원격)이 말할 때만 줄인다 — 내 목소리·솔로 플레이는 해당 없음. 팀 보이스 0%로 끈 팀원은 제외.
///
/// [2026-10-08] 응원 소리 재생(CheerSoundPlayback — 패널 듣기·R키) 중에도 같은 비율로 줄인다(그 소리는
/// ignoreListenerVolume으로 빠짐). 원격 목소리 재생 오브젝트엔 VoiceLoudnessBoost(받는 쪽 자동 음량)를 붙인다.
/// 줄이는 비율 0.5는 사용자 고정값 — 0.3·0.4는 이질감으로 기각됨.
/// </summary>
public class VoiceDucking : MonoBehaviour
{
    [Tooltip("팀원이 말하는 동안 게임 소리 배율. 0.5 ≈ -6dB (Xbox 파티 채팅 기본값과 같음).")]
    [SerializeField, Range(0f, 1f)] float duckedVolume = 0.5f;

    [Tooltip("말 시작 시 줄어드는 데 걸리는 시간(초).")]
    [SerializeField] float fadeDownSeconds = 0.15f;

    [Tooltip("말이 끊긴 뒤 복구를 시작하기 전 기다리는 시간(초). 문장 사이 짧은 쉼마다 줄었다 커졌다 하는 것을 막는다.")]
    [SerializeField] float releaseHoldSeconds = 0.4f;

    [Tooltip("대기 후 원래대로 돌아오는 데 걸리는 시간(초).")]
    [SerializeField] float fadeUpSeconds = 0.6f;

    float _current = 1f;
    float _lastSpeakingTime = float.NegativeInfinity;

    void Update()
    {
        if (ScanRemoteVoices() | CheerSoundPlayback.IsPlaying) _lastSpeakingTime = Time.unscaledTime;
        bool teammateSpeaking = Time.unscaledTime - _lastSpeakingTime < releaseHoldSeconds;

        float target = teammateSpeaking ? duckedVolume : 1f;
        float seconds = teammateSpeaking ? fadeDownSeconds : fadeUpSeconds;
        float speed = (1f - duckedVolume) / Mathf.Max(0.01f, seconds);

        _current = Mathf.MoveTowards(_current, target, speed * Time.unscaledDeltaTime);
        AudioListener.volume = _current;
    }

    void OnDisable()
    {
        _current = 1f;
        _lastSpeakingTime = float.NegativeInfinity;
        AudioListener.volume = 1f;
    }

    /// <summary>원격 목소리 AudioSource를 리스너 볼륨에서 빼면서, 말하고 있는 팀원이 있는지 반환.</summary>
    static bool ScanRemoteVoices()
    {
        DissonanceComms comms = DissonanceComms.GetSingleton();
        if (comms == null) return false;

        bool speaking = false;
        var players = comms.Players;
        for (int i = 0; i < players.Count; i++)
        {
            VoicePlayerState player = players[i];
            if (player == null || player.IsLocalPlayer) continue;

            // 재생 GameObject는 풀에서 재사용되므로 새 팀원이 붙을 때마다 확인해 둔다(이미 있으면 건드리지 않음).
            var playback = player.Playback as VoicePlayback;
            AudioSource src = playback != null ? playback.AudioSource : null;
            if (src != null && !src.ignoreListenerVolume) src.ignoreListenerVolume = true;
            VoiceLoudnessBoost.Ensure(playback);

            if (player.IsSpeaking && !player.IsLocallyMuted) speaking = true;
        }
        return speaking;
    }
}
