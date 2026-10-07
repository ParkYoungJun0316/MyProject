using System;
using UnityEngine;

/// <summary>
/// 팀 응원 소리의 **내 PC 전용** 상태 — CheerSystemDesign.md §14.2 등록본 규칙.
/// 씬을 넘어 살아남아야 해서 static(GameSession과 같은 수명, 네트워크 아님).
///
/// - Host 기준 소리: 버전 + 압축 클립(재생용) + 특징(틀 검사 기준). Host 본인도 여기 둔다(Host도 클라이언트).
/// - 내 등록본 나 1·나 2: 둘 다 Host 틀 검사를 통과한 것만 들어온다. 어느 버전 기준인지 같이 기억해
///   Host가 다시 녹음하면(버전 바뀜) 자동으로 무효가 된다.
/// - 세션 T키 자동 ON: 등록본 없이 게이트를 넘었거나 연습 7회 연속 실패(§14.8). PlayerPrefs는 안 건드린다.
///
/// 세션이 끝나면(타이틀 복귀) <see cref="ResetSession"/>로 전부 비운다.
/// </summary>
public static class CheerSoundLocalState
{
    public const int PracticeFailAutoTKey = 7;
    public const int InGameFailHint = 3;

    // ── Host 기준 소리 ──────────────────────────────────────────

    public static int HostVersion { get; private set; }
    public static byte[] HostClipMuLaw { get; private set; }
    public static CheerSoundTemplate HostTemplate { get; private set; }
    public static bool HasHostSound => HostVersion > 0 && HostTemplate != null && HostClipMuLaw != null;

    /// <summary>Host 기준 소리가 바뀜(새 버전 도착·무효화). UI 구독.</summary>
    public static event Action HostSoundChanged;
    /// <summary>내 등록 상태가 바뀜(나 1/나 2 저장·무효화). UI 구독.</summary>
    public static event Action EnrollmentChanged;

    static AudioClip s_hostAudioClip;
    static int s_hostAudioClipVersion;

    public static void SetHostSound(int version, byte[] clipMuLaw, byte[] featureBytes)
    {
        var tmpl = CheerSoundTemplate.Deserialize(featureBytes);
        if (tmpl == null || clipMuLaw == null || clipMuLaw.Length == 0)
        {
            Debug.LogWarning($"[CheerSoundLocalState] Host 기준 소리 v{version} 형식 오류 — 무시");
            return;
        }

        bool versionChanged = version != HostVersion;
        HostVersion = version;
        HostClipMuLaw = clipMuLaw;
        HostTemplate = tmpl;

        if (versionChanged)
        {
            // Host가 다시 녹음함 → 내 등록본은 옛 기준 → 무효(§14.2 Interlude 2차 변경)
            ClearEnrollment();
            PracticeFailStreak = 0;
        }
        HostSoundChanged?.Invoke();
    }

    /// <summary>재생용 AudioClip(8kHz μ-law 복원 + 재생용 크기 키우기). 버전이 같으면 캐시.</summary>
    public static AudioClip GetHostAudioClip()
    {
        if (!HasHostSound) return null;
        if (s_hostAudioClip != null && s_hostAudioClipVersion == HostVersion) return s_hostAudioClip;

        float[] pcm = CheerSoundPlayback.NormalizeForPlayback(CheerSoundCodec.Decode(HostClipMuLaw));
        s_hostAudioClip = AudioClip.Create($"TeamCheerSound_v{HostVersion}", pcm.Length, 1, CheerSoundCodec.EncodedRate, false);
        s_hostAudioClip.SetData(pcm, 0);
        s_hostAudioClipVersion = HostVersion;
        return s_hostAudioClip;
    }

    // ── 내 등록본 ───────────────────────────────────────────────

    public static CheerSoundTemplate MyTemplate1 { get; private set; }
    public static CheerSoundTemplate MyTemplate2 { get; private set; }

    /// <summary>1번 녹음 소리(16kHz, 앞뒤 침묵 뺀 구간) — R키로 "내 목소리"를 들려준다(10/7). 내 PC에만, 네트워크 안 씀.</summary>
    static float[] s_myClip1Pcm;
    static AudioClip s_myAudioClip;

    /// <summary>등록을 마쳤으면 내 1번 녹음, 아니면 Host 기준 소리(T키 응원자 등).</summary>
    public static AudioClip GetListenClip()
    {
        if (IsEnrolled && s_myClip1Pcm != null && s_myClip1Pcm.Length > 0)
        {
            if (s_myAudioClip == null)
            {
                var data = CheerSoundPlayback.NormalizeForPlayback(s_myClip1Pcm);
                s_myAudioClip = AudioClip.Create("TeamCheerSound_Mine", data.Length, 1, CheerSoundParams.SampleRate, false);
                s_myAudioClip.SetData(data, 0);
            }
            return s_myAudioClip;
        }
        return GetHostAudioClip();
    }

    /// <summary>나 1·나 2 둘 다 현재 Host 버전 기준으로 등록됨 — 음성 응원 가능 조건(§14.2 ①).</summary>
    public static bool IsEnrolled =>
        HasHostSound && MyTemplate1 != null && MyTemplate1.HostVersion == HostVersion
                     && MyTemplate2 != null && MyTemplate2.HostVersion == HostVersion;

    public static bool HasTemplate1 => HasHostSound && MyTemplate1 != null && MyTemplate1.HostVersion == HostVersion;
    public static bool HasTemplate2 => HasHostSound && MyTemplate2 != null && MyTemplate2.HostVersion == HostVersion;

    /// <summary>
    /// Host 틀 검사를 통과한 등록본만 넣을 것. pcm = R키로 들려줄 1번 녹음 소리(발화 구간). 1·2번은 서로 비교하지 않으므로(10/7)
    /// 1번을 다시 녹음해도 2번은 그대로 둔다.
    /// </summary>
    public static void SetTemplate1(CheerSoundTemplate t, float[] pcm)
    {
        t.HostVersion = HostVersion;
        MyTemplate1 = t;
        s_myClip1Pcm = pcm;
        s_myAudioClip = null;
        PracticeFailStreak = 0;
        EnrollmentChanged?.Invoke();
    }

    public static void SetTemplate2(CheerSoundTemplate t)
    {
        t.HostVersion = HostVersion;
        MyTemplate2 = t;
        EnrollmentChanged?.Invoke();
    }

    public static void ClearEnrollment()
    {
        if (MyTemplate1 == null && MyTemplate2 == null) return;
        MyTemplate1 = null;
        MyTemplate2 = null;
        s_myClip1Pcm = null;
        s_myAudioClip = null;
        EnrollmentChanged?.Invoke();
    }

    // ── 마이크 없음 토글 (10/7) ─────────────────────────────────

    /// <summary>내 패널 "마이크가 없어요" 토글 — 이 세션 동안 나는 T키로 응원(준비 완료로 친다).</summary>
    public static bool PersonalNoMic { get; private set; }

    /// <summary>Host의 "마이크 없음 — 팀 전체 T키" 토글 사본(CheerService NV를 전 머신이 따라 적음). 씬을 넘어 유지.</summary>
    public static bool TeamNoMic { get; private set; }

    public static void SetPersonalNoMic(bool on)
    {
        if (PersonalNoMic == on) return;
        PersonalNoMic = on;
        EnrollmentChanged?.Invoke();
    }

    public static void SetTeamNoMic(bool on)
    {
        if (TeamNoMic == on) return;
        TeamNoMic = on;
        EnrollmentChanged?.Invoke();
    }

    /// <summary>Host에 보고하는 내 상태 비트 — 1: 1번 녹음, 2: 2번 녹음, 4: 마이크 없음(개인).</summary>
    public static byte LocalStatusFlags =>
        (byte)((HasTemplate1 ? 1 : 0) | (HasTemplate2 ? 2 : 0) | (PersonalNoMic ? 4 : 0));

    /// <summary>연습을 열 수 있는 상태인가(상태 비트 기준) — 팀 전체 T키, 등록 완료(1·2번), 개인 마이크 없음 중 하나.</summary>
    public static bool IsReadyFlags(byte flags, bool teamNoMic) =>
        teamNoMic || (flags & 3) == 3 || (flags & 4) != 0;

    // ── 폴백 (§14.8) ────────────────────────────────────────────

    /// <summary>이번 세션만 T키 응원 허용(옵션 저장값과 별개). CheerDigitInput이 OR로 본다.</summary>
    public static bool SessionTKeyAutoOn { get; private set; }

    /// <summary>연습 창(Tutorial·Interlude 구역 3)에서 연속 실패 횟수. 통과·재등록 시 0.</summary>
    public static int PracticeFailStreak { get; private set; }

    /// <summary>창 안 연속 실패 횟수(인게임 힌트용). 창이 열릴 때 0.</summary>
    public static int WindowFailStreak { get; private set; }

    public static void EnableSessionTKey(string reason)
    {
        if (SessionTKeyAutoOn) return;
        SessionTKeyAutoOn = true;
        Debug.Log($"[CheerSoundLocalState] 이번 세션 T키 응원 자동 ON — {reason}");
        EnrollmentChanged?.Invoke();
    }

    public static void OnWindowOpened() => WindowFailStreak = 0;

    /// <summary>외쳤는데(사전 게이트 통과) 기준을 못 넘은 발화 1회. practice면 7회 연속 시 T키 자동 ON.</summary>
    public static void OnVoiceAttemptFailed(bool practiceWindow)
    {
        WindowFailStreak++;
        if (!practiceWindow) return;
        PracticeFailStreak++;
        if (PracticeFailStreak >= PracticeFailAutoTKey)
            EnableSessionTKey($"연습 {PracticeFailStreak}회 연속 실패");
    }

    public static void OnVoicePassed()
    {
        WindowFailStreak = 0;
        PracticeFailStreak = 0;
    }

    /// <summary>타이틀 복귀 등 세션 종료 시(TitleReturnFlow). 다음 방은 처음부터.</summary>
    public static void ResetSession()
    {
        HostVersion = 0;
        HostClipMuLaw = null;
        HostTemplate = null;
        MyTemplate1 = null;
        MyTemplate2 = null;
        s_myClip1Pcm = null;
        s_myAudioClip = null;
        PersonalNoMic = false;
        TeamNoMic = false;
        SessionTKeyAutoOn = false;
        PracticeFailStreak = 0;
        WindowFailStreak = 0;
        s_hostAudioClip = null;
        s_hostAudioClipVersion = 0;
        HostSoundChanged?.Invoke();
        EnrollmentChanged?.Invoke();
    }
}
