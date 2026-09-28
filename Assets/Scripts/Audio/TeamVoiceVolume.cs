using Dissonance;
using Dissonance.Audio.Playback;
using UnityEngine;

/// <summary>
/// 팀 보이스 수신 볼륨(0~100%) 적용 SSOT.
/// 호출처: OptionsTeamVoicePanel(슬라이더), GameSettingsManager(세션 합류 시 100% 초기화).
///
/// [상한 100% — 2026-09-29]
/// 200%·300%는 AudioSource.volume 0~1 클램프, 그 뒤 증폭 필터를 달아도 Dissonance Opus 소프트 클리퍼와
/// 디지털 최대치(1.0)에 막혀 100%와 똑같이 들렸다. 목소리가 작게 들리는 문제는 증폭이 아니라
/// 팀원이 말할 때 게임 소리를 줄이는 VoiceDucking으로 푼다(Dissonance 공식 권장 방식).
///
/// VoicePlayerState.Volume은 쓰지 않고 VoicePlayback.AudioSource.volume을 직접 제어한다(기존 방식 유지).
/// 0이면 IsLocallyMuted도 켜서 디코드 단계까지 무음(AudioSource.volume=0만으로도 무음이지만 이중 안전장치, 사용자 확정 요구).
/// </summary>
public static class TeamVoiceVolume
{
    public static void Set(VoicePlayerState player, float value)
    {
        value = Mathf.Clamp01(value);
        player.IsLocallyMuted = value <= 0f;

        AudioSource src = (player.Playback as VoicePlayback)?.AudioSource;
        if (src != null) src.volume = value;
    }

    /// <summary>현재 AudioSource.volume = 슬라이더 표시값(참조 못 구하면 기본 1).</summary>
    public static float Get(VoicePlayerState player)
    {
        AudioSource src = (player.Playback as VoicePlayback)?.AudioSource;
        return src != null ? src.volume : 1f;
    }
}
