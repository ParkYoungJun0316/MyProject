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
/// [마이크 이중 오픈 금지]
/// 멀티: DissonanceComms.SubscribeToRecordedAudio 로 Dissonance 스트림 탭.
/// 솔로: Dissonance 가 오디오를 주지 않을 때 직접 Microphone.Start fallback.
///
/// [Owner-only]
/// NetworkPlayerSetup.SetupOwner → enabled = true
/// NetworkPlayerSetup.SetupNonOwner → enabled = false
///
/// [초기화 순서]
/// 1. DissonanceComms 준비 대기
/// 2. VoskModelLoader.EnsureModel() → 모델 경로
/// 3. Vosk.Model 생성
/// 4. CheerLexiconBuilder.BuildDemoGrammarJson() → grammar
/// 5. SubscribeToRecordedAudio → 5초 대기 → ResetAudioStream 으로 Recognizer 생성
///    5초 내 오디오 없으면 직접 마이크 fallback
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

    // 솔로 마이크 경로
    bool      _usingSoloMic;
    AudioClip _soloMicClip;
    int       _soloMicLastPos;
    int       _soloMicSourceHz;

    // 재사용 버퍼 (GC 절약)
    float[] _captureBuf;   // Microphone.GetData 대상 버퍼
    float[] _resampleBuf;  // 캡처 Hz → 16kHz 리샘플 결과
    float[] _accumBuf;     // 200ms 청크 누적
    int     _accumCount;
    short[] _pcmBuf;       // float → 16-bit PCM 변환 결과

    // Partial 체크 카운터
    int _feedCount;

    // 중복 제출 방지 (keyword → 마지막 감지 Time.time)
    readonly Dictionary<string, float> _lastDetected = new();

    // 진단 로그용 프레임 카운터 (30프레임마다 peak 출력)
    int _debugFrameTimer;

    // ── 생명주기 ──────────────────────────────────────────────────

    void OnEnable()  => StartCoroutine(InitCoroutine());
    void OnDisable() { StopAllCoroutines(); Shutdown(); }

    // ── 초기화 ────────────────────────────────────────────────────

    IEnumerator InitCoroutine()
    {
        string modelPath = VoskModelLoader.EnsureModel();
        if (modelPath == null)
        {
            Debug.LogError("[CheerKeywordEngine] 모델 경로 없음 — 초기화 중단");
            yield break;
        }

        Vosk.Vosk.SetLogLevel(0);
        _model       = new Model(modelPath);
        _grammarJson = CheerLexiconBuilder.BuildDemoGrammarJson();

        // DissonanceComms 준비 대기
        DissonanceComms comms = null;
        while (comms == null) { comms = DissonanceComms.GetSingleton(); yield return null; }

        comms.SubscribeToRecordedAudio(this);
        _subscribed = true;
        Debug.Log($"[CheerKeywordEngine] Init OK — grammar={_grammarJson}");

        // Dissonance 실제 오디오 수신 여부 확인
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
        _model?.Dispose();      _model       = null;
        _grammarJson = null;
    }

    // ── Dissonance 경로 콜백 ─────────────────────────────────────

    protected override void ResetAudioStream(WaveFormat waveFormat)
    {
        if (_usingSoloMic) return;

        _recognizer?.Dispose();
        _recognizer = null;
        if (_model == null || _grammarJson == null) return;

        _recognizer = new VoskRecognizer(_model, waveFormat.SampleRate, _grammarJson);
        _recognizer.SetWords(false);
        Debug.Log($"[CheerKeywordEngine] Recognizer ready — sampleRate={waveFormat.SampleRate}");
    }

    protected override void ProcessAudio(ArraySegment<float> data)
    {
        if (_usingSoloMic) return;
        // data.Offset을 반드시 전달해야 backing array의 올바른 구간을 읽음
        FeedVosk(data.Array, data.Offset, data.Count);
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

    /// <summary>캡처 Hz → 16kHz 선형 보간. 출력은 _resampleBuf, 반환값은 샘플 수.</summary>
    int ResampleTo16k(float[] input, int count)
    {
        if (_soloMicSourceHz == VoskFeedHz)
        {
            EnsureResampleCapacity(count);
            Array.Copy(input, _resampleBuf, count);
            return count;
        }

        int   outCount = Mathf.Max(1, (int)((long)count * VoskFeedHz / _soloMicSourceHz));
        float ratio    = (float)_soloMicSourceHz / VoskFeedHz;
        EnsureResampleCapacity(outCount);

        for (int i = 0; i < outCount; i++)
        {
            float srcIdx = i * ratio;
            int   idx    = (int)srcIdx;
            if (idx >= count - 1) { _resampleBuf[i] = input[count - 1]; continue; }
            float frac = srcIdx - idx;
            _resampleBuf[i] = input[idx] * (1f - frac) + input[idx + 1] * frac;
        }
        return outCount;
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

    void EnsureResampleCapacity(int needed)
    {
        if (_resampleBuf == null || _resampleBuf.Length < needed)
            _resampleBuf = new float[needed];
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
