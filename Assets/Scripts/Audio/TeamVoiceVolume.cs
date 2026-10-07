using Dissonance;
using Dissonance.Audio.Playback;
using UnityEngine;

/// <summary>
/// 팀 보이스 수신 볼륨(0~100%) 적용 SSOT.
/// 호출처: OptionsTeamVoicePanel(슬라이더), GameSettingsManager(세션 합류 시 100% 초기화).
///
/// [상한 100% — 2026-09-29]
/// 200%·300%는 AudioSource.volume 0~1 클램프, 그 뒤 증폭 필터를 달아도 Dissonance Opus 소프트 클리퍼와
/// 디지털 최대치(1.0)에 막혀 100%와 똑같이 들렸다. 게임 소리는 팀원이 말할 때 VoiceDucking이 줄인다.
///
/// [2026-10-08] 목소리 평균 크기는 VoiceLoudnessBoost(AGC+리미터)가 키운다. 슬라이더는 그 뒤에 곱해야
/// 키우기가 슬라이더를 되돌리지 않으므로 AudioSource.volume은 1로 두고 VoiceLoudnessBoost.Volume에 쓴다.
/// 0이면 IsLocallyMuted도 켜서 디코드 단계까지 무음(이중 안전장치, 사용자 확정 요구).
/// </summary>
public static class TeamVoiceVolume
{
    public static void Set(VoicePlayerState player, float value)
    {
        value = Mathf.Clamp01(value);
        player.IsLocallyMuted = value <= 0f;

        var playback = player.Playback as VoicePlayback;
        if (playback == null) return;
        if (playback.AudioSource != null) playback.AudioSource.volume = 1f;
        var boost = VoiceLoudnessBoost.Ensure(playback);
        if (boost != null) boost.Volume = value;
    }

    /// <summary>현재 슬라이더 값(참조 못 구하면 기본 1).</summary>
    public static float Get(VoicePlayerState player)
    {
        var playback = player.Playback as VoicePlayback;
        if (playback == null) return 1f;
        var boost = playback.GetComponent<VoiceLoudnessBoost>();
        return boost != null ? boost.Volume : 1f;
    }
}
