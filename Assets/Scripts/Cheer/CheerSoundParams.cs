/// <summary>
/// 팀 응원 소리 매칭(TeamCheerSound) 수치 SSOT — CheerSystemDesign.md §14.
/// 전부 초안값이다. 임계값(T_self·T_cross 등)은 Tools/Cheer Sound Lab 실측으로 확정한다(§14.10 #6).
/// 2026-10-07 합성 소리 시험(남/여·속도·소음 변주)으로 1차 조정: 내 소리 d 3.9~4.5, 다른 소리 4.2~7.4(리듬 게이트로
/// 대부분 걸러짐), 소음 12~15 / 다른 사람 같은 소리 음색 4.9~7.0.
/// </summary>
public static class CheerSoundParams
{
    // ── 신호 형식 (§14.3) ───────────────────────────────────────
    public const int   SampleRate  = 16000;
    public const int   FrameLen    = 400;    // 25ms
    public const int   Hop         = 160;    // 10ms
    public const int   FftSize     = 512;
    public const int   MelBands    = 26;
    public const int   Ceps        = 13;     // c1..c13 (c0 제외)
    public const int   Dims        = Ceps * 2; // + Δ
    public const float PreEmphasis = 0.97f;
    public const int   PitchPoints = 50;

    // ── 발화 구간 (앞뒤 침묵 자르기) ────────────────────────────
    public const float SpeechAboveFloorDb  = 6f;
    /// <summary>소음 바닥 = min(하위 10% 프레임, 피크 − 이 값). 녹음이 말로 꽉 차 있어도 바닥이 말 위로 올라가지 않게.</summary>
    public const float FloorBelowPeakMaxDb = 25f;
    public const int   MinSpeechRunFrames  = 3;  // 30ms 미만 튐(버튼 클릭 등)은 발화 시작으로 안 침
    public const int   TrimPadFrames       = 2;

    // ── 녹음 자체 검사 (§14.4 #1, §14.5 검증) ───────────────────
    public const float MinPeakDbfs      = -45f;
    public const float MinSnrDb         = 12f;
    /// <summary>실제 소리(앞뒤 침묵 뺀) 최소 길이 — 10/7 사용자 결정 0.5초. 짧으면 비교할 프레임이 적어 인식률이 떨어진다.</summary>
    public const float MinDurationSec   = 0.5f;
    public const float MaxDurationSec   = 3f;
    public const float ClipSampleLevel  = 0.99f;
    public const float MaxClipFraction  = 0.01f;

    // ── 끊김 횟수 (§14.3 리듬) ──────────────────────────────────
    public const float BurstDipDb        = 8f;
    public const int   BurstMinGapFrames = 12; // 120ms

    // ── 높낮이 (§14.3) ──────────────────────────────────────────
    public const float PitchMinHz   = 60f;
    public const float PitchMaxHz   = 800f; // 여성·아이 고음 "미야옹"까지
    /// <summary>유성 판단 — 가장 깊은 골이 이보다 낮으면 유성(실제 마이크 목소리는 0.15면 유성을 많이 놓침, Lab 10/7).</summary>
    public const float YinVoicingThreshold = 0.35f;
    /// <summary>주기 고르기 — max(이 값, 가장 깊은 골 + YinPickMargin)보다 낮은 첫 골. 얕은 골(5도·옥타브 위)을 건너뛴다.</summary>
    public const float YinThreshold        = 0.1f;
    public const float YinPickMargin       = 0.05f;

    // ── 틀 검사: 내 등록 vs Host 기준 소리 (§14.4) ──────────────
    // 항목마다 통과 / 애매 / 확실히 다름 3단계. 확실히 다름 1개 또는 애매가 ShapeMaxSoft 넘으면 거절.
    /// <summary>애매 허용 개수 — 10/7 사용자 결정 2(= 3개부터 거절). Lab 2차: 애매 2개로 거절된 건 전부 같은 소리였음.</summary>
    public const int   ShapeMaxSoft        = 2;
    public const float ShapeDurPassMin     = 0.6f,  ShapeDurPassMax    = 1.7f;
    public const float ShapeDurSoftMin     = 0.5f,  ShapeDurSoftMax    = 2.0f;
    /// <summary>기준 끊김이 이 값 이상이면 ±1까지 통과(빠른 음절은 사람마다 한 번쯤 붙거나 갈라짐 — Lab 같은 소리 9 vs 8).</summary>
    public const int   ShapeBurstLooseFrom = 6;
    /// <summary>유성 비율 = 발화 프레임 중 유성 프레임(끊김 사이 틈 제외).</summary>
    public const float ShapeMinVoicedRatio = 0.4f;
    // 10/7 Lab 2차 후 살짝 완화(사용자 결정): 0.5/1.5 → 0.4/2.0, 0.2/2.5 → 0.1/3.0
    public const float ShapePitchPassCorr  = 0.4f,  ShapePitchPassAbs  = 2.0f;
    public const float ShapePitchSoftCorr  = 0.1f,  ShapePitchSoftAbs  = 3.0f;
    /// <summary>옥타브 오류 접기 — 중앙값에서 이 반음 이상 벗어난 프레임은 12반음씩 당겨 온다(한 번 외침은 보통 ±6 안).</summary>
    public const float PitchOctaveFoldSemi = 9f;
    /// <summary>튀는 프레임 제거용 중앙값 필터 폭(유성 프레임 기준, 홀수).</summary>
    public const int   PitchMedianFrames   = 5;
    /// <summary>
    /// 틀 검사 음색은 MFCC 앞 8계수(+Δ)만 — 뒤쪽은 음높이 배음이 섞여 남녀·고음에서 크게 갈린다.
    /// 합성 시험 10/7: 13계수면 다른 사람 같은 소리 6개 중 고음 "미야옹"이 11.1로 거절, 8계수면 6/6 통과(잘못 통과 수는 같음).
    /// </summary>
    public const int   ShapeTimbreCeps     = 8;
    /// <summary>
    /// T_cross 음색 거리(8계수 기준). Lab 10/7 2차 실측: 같은 목소리 같은 소리 5.61·6.33 / 목소리 바꿔 같은 소리 7.73·8.12 /
    /// 다른 소리 9.09·10.25. 끊김을 빡빡하게 두므로(±1이 자주 △) 통과선을 8까지 올려야 따라하기가 통과한다.
    /// 다른 소리 측정이 2개뿐이고 9.09가 선에 가까움 — 표본 늘면 재확인.
    /// </summary>
    public const float ShapeTimbrePass     = 8.0f,  ShapeTimbreSoft    = 9.0f;

    // ── 인게임 판정: 내 마이크 vs 내 등록본 (§14.6) ─────────────
    /// <summary>
    /// T_self — **전 플레이어 공통 고정값**(10/7 사용자 결정: 사람마다 바뀌는 보정 삭제 — 실제 목소리에선 두 등록본 거리가
    /// 3.5~7.1이라 보정이 항상 상한에 붙어 작동하지 않았다). Lab 2~3차 실측: 통과 d 4.7~5.8, 다른 소리 6.4+.
    /// </summary>
    public const float SelfThreshold              = 6.0f;
    public const float FinalThresholdScale        = 1.15f;
    public const int   PartialConfirmHits         = 2;
    public const float LiveLoudAboveFloorDb       = 12f;
    public const int   LiveBurstTolerance         = 1;
    public const float LiveMinDurationRatio       = 0.5f;
    public const float LiveMaxDurationRatio       = 2.0f;
    /// <summary>등록본 최대 3초 × 길이비 2.0 + 끝 침묵 — 느리게 외친 긴 소리도 버퍼에 다 들어가게.</summary>
    public const int   RingFrames                 = 630;
    public const int   EvalEveryFrames            = 10;  // 100ms
    public const int   EndSilenceFrames           = 30;  // 300ms
    public const float DtwBandRatio               = 0.2f;

    // ── 녹음기 (§14.2) ──────────────────────────────────────────
    /// <summary>녹음 버튼 1~3초 강제(10/7 사용자 결정): 1초 전엔 정지 불가, 3초에 자동 정지 → "너무 길어요"는 사실상 없다.</summary>
    public const float RecordMinSec      = 1f;
    public const float RecordMaxSec      = 3f;
    public const float RecordEdgeDropSec = 0.1f; // 버튼 클릭 소리 제거
}
