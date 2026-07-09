using System;
using System.Collections;
using System.Collections.Generic;
using Dissonance;
using NAudio.Wave;
using Unity.Netcode;
using UnityEngine;
using Vosk;

/// <summary>
/// Dissonance 마이크 스트림을 Vosk에 연결해 CheerName 키워드를 감지한다.
///
/// [배치 위치 — 세션 단위 싱글턴]
/// Player 프리팹이 아니라 0.Title 씬의 NetworkManager GameObject(DissonanceComms와 동일 GO)에
/// 배치되어 NGO의 DontDestroyOnLoad를 타고 앱 실행 중 단 1개 인스턴스만 존재한다.
/// 로컬 마이크는 클라이언트 프로세스당 1개뿐이라 "누구 소유"라는 개념이 필요 없으므로,
/// 더 이상 NetworkPlayerSetup이 Owner/NonOwner에 따라 enabled를 토글하지 않는다.
/// (예전에는 Player 프리팹에 붙어 있어 스폰마다 InitCoroutine이 재실행되며 아래 마이크
///  이중 오픈 버그의 재발 빈도를 높였다 — 세션 싱글턴화로 이 재실행 자체를 제거함.)
///
/// [활성화 시점 — Title 진입 즉시 초기화하지 않음]
/// 씬에 m_Enabled=0으로 배치된다. Title 화면에서는 아직 LobbyContext.Mode가 확정되지
/// 않아(기본값 Offline) 여기서 바로 초기화하면 온라인/오프라인 분기가 틀어진다.
/// 대신 PlayerSpawnCoordinator.OnPlayersReady(스테이지 최초 스폰 완료 — 이 시점엔 Mode가
/// 이미 확정됨)를 받을 때마다 (재)초기화한다. 스테이지 진입은 플레이어 리스폰과 달리
/// 세션당 드물게 발생하므로(타이틀 복귀 후 온라인↔오프라인으로 재시작하는 경우 포함) 매번
/// 안전하게 다시 초기화해도 예전처럼 스폰마다 반복되던 마이크 경쟁은 재발하지 않는다.
///
/// [마이크 이중 오픈 금지 — 온라인/오프라인 분기 필수]
/// 온라인(멀티): Dissonance가 마이크 소유권을 항상 갖고 있다. DissonanceComms.SubscribeToRecordedAudio
///   로 그 스트림을 탭만 한다. 온라인에서는 절대 Microphone.Start로 폴백하지 않는다 —
///   Dissonance가 이미 열어둔 같은 OS 마이크 장치를 직접 캡처로 또 열면 버퍼 오버런·캡처
///   재시작 반복이 발생하고, 그 오디오 스레드 경합이 메인 스레드 프레임 스톨(0.3~0.4s급)로
///   번져 Netcode 스폰 메시지가 Deferred 타임아웃으로 유실되는 사고로 이어진다.
/// 오프라인(솔로): NGO 연결이 없어 Dissonance의 캡처 파이프라인 자체가 시작되지 않으므로,
///   그때만 직접 Microphone.Start fallback을 허용한다.
///
/// [초기화 순서]
/// 1. DissonanceComms 준비 대기
/// 2. VoskModelLoader.EnsureModel() → 모델 경로
/// 3. Vosk.Model 생성
/// 4. CheerLexiconBuilder.BuildDemoGrammarJson() → grammar
/// 5. SubscribeToRecordedAudio →
///    - 오프라인: 5초 대기 후 ResetAudioStream 콜백 없으면 직접 마이크 fallback
///    - 온라인: 폴백 없이 계속 대기(느려도 결국 Dissonance가 공급함)
///
/// [키워드 감지 방식]
/// FinalResult  : 침묵 후 발화 확정 → "text" 파싱
/// PartialResult: 10 AcceptWaveform 호출마다 → "partial" 파싱 (실시간 감지)
/// </summary>
[DisallowMultipleComponent]
public class CheerKeywordEngine : BaseMicrophoneSubscriber
{
    // ── Inspector ─────────────────────────────────────────────────

    [Header("솔로 마이크 게인")]
    [Tooltip("autoNormalizeMic=true 이면 이 값은 무시되고 자동 보정만 사용됨.\n" +
             "Dissonance 경로에서는 무시됨.")]
    [SerializeField] float soloMicGain = 5f;

    [Tooltip("true: peak를 normalizeTargetPeak 까지 자동 증폭 (soloMicGain 무시).\n" +
             "false: soloMicGain 고정 배율 적용.")]
    [SerializeField] bool autoNormalizeMic = true;

    [Tooltip("autoNormalizeMic 목표 peak (0.01~1). peak가 이 값 이상이면 증폭하지 않음.")]
    [SerializeField, Range(0.01f, 1f)] float normalizeTargetPeak = 0.35f;

    // ── 상수 ──────────────────────────────────────────────────────

    const int   VoskFeedHz           = 16000;
    const int   SoloMicCaptureHz     = 48000; // Windows 네이티브 캡처 Hz → 16kHz 리샘플
    const int   SoloMicBufSec        = 30;
    const int   MinFeedSamples       = 3200;  // 16kHz × 200ms
    const int   PartialInterval      = 10;    // AcceptWaveform 호출 N회마다 Partial 체크
    const float DissonanceWaitSec    = 5f;
    const float SoloMicWarmupSec     = 0.5f;
    const float SoloMicPositionWaitSec = 1f; // Microphone.Start 후 position > 0 대기 한계
    const float KeywordCooldown      = 2f;
    const float NormNoiseFloor       = 0.0001f; // 이 이하 peak는 무음으로 간주해 normalize 스킵
    const float NormMaxGain          = 20f;

    // ── 내부 상태 ─────────────────────────────────────────────────

    Model          _model;
    VoskRecognizer _recognizer;
    string         _grammarJson;
    bool           _subscribed;
    int            _dissonanceSampleRate; // Dissonance 실제 입력 Hz (보통 48000)

    // 솔로 마이크 경로
    bool      _usingSoloMic;
    AudioClip _soloMicClip;
    int       _soloMicLastPos;
    int       _soloMicSourceHz;

    // 재사용 버퍼 (GC 절약)
    float[] _captureBuf;          // Microphone.GetData 대상 버퍼
    float[] _resampleBuf;         // 솔로 캡처 Hz → 16kHz 리샘플 결과
    float[] _dissonanceResample;  // Dissonance 48kHz → 16kHz 리샘플 결과
    float[] _accumBuf;            // 200ms 청크 누적
    int     _accumCount;
    short[] _pcmBuf;              // float → 16-bit PCM 변환 결과

    // Partial 체크 카운터
    int _feedCount;

    // 중복 제출 방지 (keyword → 마지막 감지 Time.time)
    readonly Dictionary<string, float> _lastDetected = new();

    // 진단 로그용 프레임 카운터 (30프레임마다 peak 출력)
    int _debugFrameTimer;

    // ── 생명주기 ──────────────────────────────────────────────────

    // Title 진입 시점엔 LobbyContext.Mode가 아직 미확정이므로, 스테이지 최초 스폰 완료
    // 신호(OnPlayersReady)를 받을 때마다 명시적으로 (재)초기화한다.
    // OnEnable/OnDisable에 Init/Shutdown을 걸지 않는 이유: enabled 토글과 무관하게
    // 이 지점에서만 초기화를 트리거해야 최초 활성화 시 이중 초기화 경쟁을 피할 수 있다.
    void Awake()
    {
        PlayerSpawnCoordinator.OnPlayersReady += HandlePlayersReady;
        if (PlayerSpawnCoordinator.IsReady) HandlePlayersReady();
    }

    void OnDestroy()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= HandlePlayersReady;
        Shutdown();
    }

    void HandlePlayersReady()
    {
        StopAllCoroutines();
        Shutdown();
        enabled = true; // Update()(솔로 마이크 폴링/버퍼 드레인)가 돌 수 있도록 보장
        StartCoroutine(InitCoroutine());
    }

    // ── 초기화 ────────────────────────────────────────────────────

    IEnumerator InitCoroutine()
    {
        _model = VoskModelLoader.GetSharedModel();
        if (_model == null)
        {
            Debug.LogError("[CheerKeywordEngine] 공유 Model 없음 — 초기화 중단");
            yield break;
        }

        _grammarJson = CheerLexiconBuilder.BuildDemoGrammarJson();

        // DissonanceComms 준비 대기
        DissonanceComms comms = null;
        while (comms == null) { comms = DissonanceComms.GetSingleton(); yield return null; }

        comms.SubscribeToRecordedAudio(this);
        _subscribed = true;
        Debug.Log($"[CheerKeywordEngine] Init OK — grammar={_grammarJson}");

        // 온라인(멀티): Dissonance가 마이크를 항상 소유한다. 늦게 도착해도 결국 오므로
        // 절대 직접 캡처로 폴백하지 않는다 — 이중 오픈은 마이크 장치 경합을 일으킨다.
        if (LobbyContext.IsOnline)
        {
            float nextWarn = Time.time + DissonanceWaitSec;
            while (_recognizer == null)
            {
                if (Time.time >= nextWarn)
                {
                    Debug.LogWarning("[CheerKeywordEngine] 온라인 — Dissonance 오디오 대기 중 (폴백 없음)");
                    nextWarn = Time.time + DissonanceWaitSec;
                }
                yield return null;
            }
            yield break;
        }

        // 오프라인(솔로): NGO 연결이 없어 Dissonance 캡처 파이프라인이 시작되지 않을 수 있으므로
        // 일정 시간 대기 후 오디오가 없으면 직접 마이크로 전환한다.
        float deadline = Time.time + DissonanceWaitSec;
        while (_recognizer == null && Time.time < deadline)
            yield return null;

        if (_recognizer == null)
        {
            Debug.LogWarning($"[CheerKeywordEngine] Dissonance 오디오 미수신 ({DissonanceWaitSec}s 초과) → 직접 마이크 fallback");
            comms.UnsubscribeFromRecordedAudio(this);
            _subscribed = false;
            StartSoloMic();

            if (_usingSoloMic)
            {
                float posWait = Time.time + SoloMicPositionWaitSec;
                while (Time.time < posWait && Microphone.GetPosition(null) <= 0)
                    yield return null;
                yield return new WaitForSeconds(SoloMicWarmupSec);
            }
        }
    }

    void StartSoloMic()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("[CheerKeywordEngine] 마이크 없음 — 직접 캡처 불가");
            return;
        }

        _soloMicClip = Microphone.Start(null, true, SoloMicBufSec, SoloMicCaptureHz);
        if (_soloMicClip == null)
        {
            Debug.LogError("[CheerKeywordEngine] Microphone.Start 실패");
            return;
        }

        _soloMicSourceHz = _soloMicClip.frequency;
        _soloMicLastPos  = 0;
        _accumCount      = 0;
        _usingSoloMic    = true;
        _debugFrameTimer = 0;

        _recognizer?.Dispose();
        _recognizer = new VoskRecognizer(_model, VoskFeedHz, _grammarJson);
        _recognizer.SetWords(false);

        Debug.Log($"[CheerKeywordEngine] 직접 마이크 시작 — 캡처:{_soloMicSourceHz}Hz → Vosk:{VoskFeedHz}Hz, gain={soloMicGain:F1}, normalize={autoNormalizeMic}");
    }

    void Shutdown()
    {
        if (_subscribed)
        {
            DissonanceComms.GetSingleton()?.UnsubscribeFromRecordedAudio(this);
            _subscribed = false;
        }
        if (_usingSoloMic)
        {
            Microphone.End(null);
            _usingSoloMic    = false;
            _soloMicClip     = null;
            _soloMicSourceHz = 0;
            _captureBuf      = null;
            _resampleBuf     = null;
            _accumBuf        = null;
            _accumCount      = 0;
            _pcmBuf          = null;
        }
        _recognizer?.Dispose(); _recognizer = null;
        _model       = null;  // 공유 인스턴스이므로 Dispose 하지 않음
        _grammarJson = null;
    }

    // ── Dissonance 경로 콜백 ─────────────────────────────────────

    protected override void ResetAudioStream(WaveFormat waveFormat)
    {
        if (_usingSoloMic) return;

        _dissonanceSampleRate = waveFormat.SampleRate;

        _recognizer?.Dispose();
        _recognizer = null;
        if (_model == null || _grammarJson == null) return;

        // Vosk는 16kHz 기준. Dissonance가 48kHz로 줘도 Recognizer는 항상 16kHz로 생성.
        _recognizer = new VoskRecognizer(_model, VoskFeedHz, _grammarJson);
        _recognizer.SetWords(false);
        Debug.Log($"[CheerKeywordEngine] Recognizer ready — input={_dissonanceSampleRate}Hz vosk={VoskFeedHz}Hz");
    }

    protected override void ProcessAudio(ArraySegment<float> data)
    {
        if (_usingSoloMic) return;

        // Dissonance 입력이 16kHz가 아니면 리샘플 후 전달
        if (_dissonanceSampleRate == VoskFeedHz || _dissonanceSampleRate == 0)
        {
            FeedVosk(data.Array, data.Offset, data.Count);
        }
        else
        {
            int resampled = ResampleLinear(
                data.Array, data.Offset, data.Count,
                _dissonanceSampleRate, VoskFeedHz,
                ref _dissonanceResample);
            FeedVosk(_dissonanceResample, 0, resampled);
        }
    }

    // ── Update ────────────────────────────────────────────────────

    public override void Update()
    {
        if (_usingSoloMic) PollSoloMic();
        else               base.Update(); // TransferBuffer → ProcessAudio → FeedVosk
    }

    // ── 솔로 마이크 폴링 ─────────────────────────────────────────

    void PollSoloMic()
    {
        if (_recognizer == null || _soloMicClip == null) return;
        if (!Microphone.IsRecording(null)) return;

        int pos = Microphone.GetPosition(null);
        if (pos < 0) return;

        // wrap-around 또는 위치 미변화: 데이터 손실 감수하고 스킵 (30초 버퍼라 실사용에 무해)
        if (pos <= _soloMicLastPos) { _soloMicLastPos = pos; return; }

        int samples = pos - _soloMicLastPos;
        if (samples <= 0) return;

        EnsureCaptureCapacity(samples);
        try
        {
            _soloMicClip.GetData(_captureBuf, _soloMicLastPos);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CheerKeywordEngine] GetData 실패 — 위치 리셋: {ex.Message}");
            _soloMicLastPos = pos;
            return;
        }
        _soloMicLastPos = pos;

        // 게인 또는 자동 노멀라이즈 (둘 중 하나만 적용)
        if (autoNormalizeMic)
            NormalizeBuffer(_captureBuf, samples);
        else if (soloMicGain != 1f)
            ApplyGain(_captureBuf, samples, soloMicGain);

        LogMicLevel(_captureBuf, samples);

        int resampled = ResampleTo16k(_captureBuf, samples);
        AppendAndFeedVosk(_resampleBuf, resampled);
    }

    // ── 오디오 처리 헬퍼 ─────────────────────────────────────────

    void ApplyGain(float[] buf, int count, float gain)
    {
        for (int i = 0; i < count; i++)
            buf[i] = Mathf.Clamp(buf[i] * gain, -1f, 1f);
    }

    void NormalizeBuffer(float[] buf, int count)
    {
        if (count <= 0) return;

        float peak = 0f;
        for (int i = 0; i < count; i++) peak = Mathf.Max(peak, Mathf.Abs(buf[i]));

        if (peak < NormNoiseFloor || peak >= normalizeTargetPeak) return;

        float gain = Mathf.Min(normalizeTargetPeak / peak, NormMaxGain);
        for (int i = 0; i < count; i++)
            buf[i] = Mathf.Clamp(buf[i] * gain, -1f, 1f);
    }

    void LogMicLevel(float[] buf, int count)
    {
        _debugFrameTimer++;
        if (_debugFrameTimer < 30) return;
        _debugFrameTimer = 0;

        float peak = 0f;
        for (int i = 0; i < count; i++) peak = Mathf.Max(peak, Mathf.Abs(buf[i]));
        if (peak <= 0.003f) return;

        string level = peak > 0.05f ? "← 발화 감지됨"
                     : peak > 0.01f ? "← 작은 소리"
                     :                "← 소음/매우 조용함";
        Debug.Log($"[CheerKeywordEngine] 마이크 레벨 peak={peak:F4} {level} (gain={soloMicGain:F1}, normalize={autoNormalizeMic})");
    }

    // ── 리샘플 ────────────────────────────────────────────────────

    /// <summary>솔로 마이크 → 16kHz 변환 (ResampleLinear 래퍼).</summary>
    int ResampleTo16k(float[] input, int count)
        => ResampleLinear(input, 0, count, _soloMicSourceHz, VoskFeedHz, ref _resampleBuf);

    /// <summary>sourceHz → targetHz 선형 보간 리샘플. buf 배열에 결과를 쓰고 샘플 수 반환.</summary>
    static int ResampleLinear(float[] input, int offset, int count, int sourceHz, int targetHz, ref float[] buf)
    {
        if (sourceHz == targetHz)
        {
            EnsureCapacity(ref buf, count);
            Array.Copy(input, offset, buf, 0, count);
            return count;
        }

        int   outCount = Mathf.Max(1, (int)((long)count * targetHz / sourceHz));
        float ratio    = (float)sourceHz / targetHz;
        EnsureCapacity(ref buf, outCount);

        for (int i = 0; i < outCount; i++)
        {
            float srcIdx = i * ratio;
            int   idx    = (int)srcIdx;
            if (idx >= count - 1) { buf[i] = input[offset + count - 1]; continue; }
            float frac = srcIdx - idx;
            buf[i] = input[offset + idx] * (1f - frac) + input[offset + idx + 1] * frac;
        }
        return outCount;
    }

    static void EnsureCapacity(ref float[] arr, int needed)
    {
        if (arr == null || arr.Length < needed)
            arr = new float[needed];
    }

    // ── 청크 누적 → Vosk 전달 ────────────────────────────────────

    /// <summary>솔로 경로: MinFeedSamples(200ms) 단위로 묶어서 Vosk에 전달.</summary>
    void AppendAndFeedVosk(float[] buf, int count)
    {
        EnsureAccumCapacity(_accumCount + count);
        Array.Copy(buf, 0, _accumBuf, _accumCount, count);
        _accumCount += count;

        while (_accumCount >= MinFeedSamples)
        {
            FeedVosk(_accumBuf, 0, MinFeedSamples);
            _accumCount -= MinFeedSamples;
            if (_accumCount > 0)
                Array.Copy(_accumBuf, MinFeedSamples, _accumBuf, 0, _accumCount);
        }
    }

    // ── Vosk 공통 입력 ────────────────────────────────────────────

    void FeedVosk(float[] buf, int offset, int count)
    {
        if (_recognizer == null || count <= 0) return;

        EnsurePcmCapacity(count);
        for (int i = 0; i < count; i++)
            _pcmBuf[i] = (short)Mathf.Clamp(buf[offset + i] * 32767f, -32768f, 32767f);

        bool isFinal = _recognizer.AcceptWaveform(_pcmBuf, count);

        if (isFinal)
        {
            string json = _recognizer.Result();
            var    node = JSONNode.Parse(json);
            if (!string.IsNullOrEmpty(node?["text"]?.Value))
            {
                Debug.Log($"[CheerKeywordEngine] Final JSON: {json}");
                ParseAndSubmit(node, "text");
            }
        }
        else
        {
            _feedCount++;
            if (_feedCount < PartialInterval) return;
            _feedCount = 0;

            string json = _recognizer.PartialResult();
            if (string.IsNullOrEmpty(json)) return;

            // Vosk partial 포맷이 "partial" : "" 처럼 공백 포함이라 파싱으로 걸러냄
            var    node        = JSONNode.Parse(json);
            string partialText = node?["partial"]?.Value;
            if (string.IsNullOrEmpty(partialText)) return;

            Debug.Log($"[CheerKeywordEngine] Partial JSON: {json}");
            ParseAndSubmit(node, "partial");
        }
    }

    // ── 결과 파싱 + 응원 제출 ────────────────────────────────────

    void ParseAndSubmit(JSONNode node, string key)
    {
        string raw = node?[key]?.Value;
        if (string.IsNullOrEmpty(raw)) return;

        foreach (string word in raw.Trim().ToLower().Split(' '))
        {
            if (string.IsNullOrEmpty(word) || word == "[unk]") continue;

            if (_lastDetected.TryGetValue(word, out float lastTime) &&
                Time.time - lastTime < KeywordCooldown)
                continue;

            int colorIndex = CheerService.GetColorIndex(word);
            if (colorIndex < 0)
            {
                Debug.Log($"[CheerKeywordEngine] 인식됐으나 CheerName 불일치: '{word}'");
                continue;
            }

            _lastDetected[word] = Time.time;
            Debug.Log($"[CheerKeywordEngine] 키워드 감지: '{word}' → colorIndex={colorIndex}");

            if (CheerService.Instance == null) continue;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                CheerService.Instance.SubmitCheerServerRpc(colorIndex, isVoice: true);
            else
                CheerService.Instance.SubmitCheerLocal(colorIndex);
        }
    }

    // ── 버퍼 용량 보장 ────────────────────────────────────────────

    void EnsureCaptureCapacity(int needed)
    {
        if (_captureBuf == null || _captureBuf.Length < needed)
            _captureBuf = new float[needed];
    }

    void EnsureAccumCapacity(int needed)
    {
        if (_accumBuf == null || _accumBuf.Length < needed)
            _accumBuf = new float[Mathf.Max(needed, MinFeedSamples * 2)];
    }

    void EnsurePcmCapacity(int needed)
    {
        if (_pcmBuf == null || _pcmBuf.Length < needed)
            _pcmBuf = new short[needed];
    }
}
