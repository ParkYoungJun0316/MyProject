using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
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
/// 솔로(ActivePlayerCount==1): Dissonance 가 오디오를 주지 않을 때만 직접 Microphone.Start fallback.
/// 멀티에서는 Dissonance가 늦어도 직접 마이크를 열지 않는다 — 동시 오픈 시 메인 스톨로
/// NGO 스폰 Deferred/유실이 발생한 전례가 있음(CheerAndTutorialDesign.md §4.3, 재발 금지).
///
/// [Owner-only]
/// NetworkPlayerSetup.SetupOwner → enabled = true
/// NetworkPlayerSetup.SetupNonOwner → enabled = false
///
/// [초기화 순서]
/// 1. VoskModelLoader.GetSharedModel() → 공유 Model (null이면 초기화 중단)
/// 2. OwnerGrammarWords(ResolveOwnerCheerName()) → [내 CheerName, TeamCheerWord] grammar 빌드
/// 3. DissonanceComms 준비 대기
/// 4. SubscribeToRecordedAudio → 5초 대기 → ResetAudioStream 으로 워커 리셋 신호
///    5초 내 오디오 없으면 직접 마이크 fallback
///
/// [키워드 감지 방식]
/// FinalResult  : 침묵 후 발화 확정 → "text" 파싱
/// PartialResult: 10 AcceptWaveform 호출마다 → "partial" 파싱 (실시간 감지)
///
/// [스레드 구조]
/// 메인 스레드 : 오디오 캡처 → float→short 변환 → _pcmQueue 에 넣기
///              _resultQueue 에서 키워드 꺼내 Cheer 제출 (Unity API 여기서만)
/// 워커 스레드 : _pcmQueue 에서 꺼내 AcceptWaveform → 결과를 _resultQueue 에 넣기
/// </summary>
[DisallowMultipleComponent]
public class CheerKeywordEngine : BaseMicrophoneSubscriber
{
    // ── Inspector ─────────────────────────────────────────────────

    [Header("말해보기 테스트 모드")]
    [Tooltip("true: ServerRpc 미제출, OnKeywordDetected 이벤트만 발행(로컬 인식 확인용).\n" +
             "Tutorial \"말해보기\" 테스트 UI가 이 컴포넌트를 쓸 때 체크. 인게임(응원 실제 제출)은 false.")]
    [SerializeField] bool _sayTestMode = false;

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

    const int   VoskFeedHz             = 16000;
    const int   SoloMicCaptureHz       = 48000;
    const int   SoloMicBufSec          = 30;
    const int   MinFeedSamples         = 1600;   // 16kHz × 100ms
    const int   PartialInterval        = 10;
    const float DissonanceWaitSec      = 5f;
    const float SoloMicWarmupSec       = 0.5f;
    const float SoloMicPositionWaitSec = 1f;
    const float KeywordCooldown        = 0.5f;

    // 0.0001은 거의 완전한 무음만 걸러내는 수준이라, 배경 잡음(peak 0.001~0.005대)까지
    // NormMaxGain(20배) 가까이 증폭되어 Vosk 입력이 잡음으로 뭉개지는 문제가 있었다.
    // LogMicLevel 로그 기준(peak>0.01="작은 소리")과 맞춰 그 아래는 잡음으로 간주하고 증폭하지 않는다.
    // 실측 후 필요하면 Inspector 노출 없이 이 값만 조정할 것(값 자체가 튜닝 포인트).
    const float NormNoiseFloor         = 0.008f;
    const float NormMaxGain            = 20f;

    // 100ms 청크마다 목표 게인이 갑자기 바뀌면(예: 조용함→발화 시작) Vosk 입력 다이내믹이
    // 뭉개진다. 이전 스무딩값과 목표값을 섞어 완만하게 따라간다. 1에 가까울수록 즉각 반응.
    const float GainSmoothingFactor    = 0.3f;

    const int   PcmQueueMax            = 60;     // 큐 최대 청크 수 (~2초분)

    // ── 메인 스레드 전용 상태 ─────────────────────────────────────

    Model  _model;
    string _grammarJson;
    bool   _subscribed;
    int    _dissonanceSampleRate;

    // InitCoroutine의 "Dissonance 오디오 수신했는가" 판단 전용 신호. ResetAudioStream에서만 true.
    // 과거엔 _workerNextModel(grammar 재적용 시에도 채워짐)로 겸용해서, 5초 창 안에
    // ApplyOwnerLocalGrammar가 한 번이라도 불리면 오디오가 실제로는 안 왔는데도 "왔다"로
    // 오판 → 솔로 마이크 폴백이 통째로 스킵되는 버그가 있었다(인게임 진입 직후 CheerService
    // TeamCheerWord NV 복원이 이 재적용을 트리거하는 경로가 있어 인게임에서만 재현되기 쉬웠다).
    volatile bool _dissonanceAudioSeen;

    // 솔로 마이크 경로
    bool      _usingSoloMic;
    AudioClip _soloMicClip;
    int       _soloMicLastPos;
    int       _soloMicSourceHz;
    /// <summary>null = 시스템 기본. 옵션 메뉴에서 고른 장치가 있으면 그걸 씀(GameSettingsManager.MicDeviceName).</summary>
    string    _soloMicDevice;

    // 재사용 버퍼 (메인 스레드 전용)
    float[] _captureBuf;
    float[] _resampleBuf;
    float[] _dissonanceResample;
    float[] _accumBuf;
    int     _accumCount;

    // NormalizeBuffer 청크 간 게인 스무딩 상태 (메인 스레드 전용). 1f = 무증폭.
    float _smoothedGain = 1f;

    // ── 이벤트 (말해보기 테스트 모드 전용) ───────────────────────
    /// <summary>
    /// 말해보기 테스트 모드(_sayTestMode=true)에서 키워드 감지 시 발행.
    /// arg = targetColorIndex (해당 CheerName 소유자의 ColorIndex).
    /// ServerRpc 미제출. Tutorial "말해보기" 테스트 UI에서 구독.
    /// </summary>
    public event System.Action<int> OnKeywordDetected;

    // 중복 제출 방지 (keyword → 마지막 감지 Time.time)
    readonly Dictionary<string, float> _lastDetected = new();

    // 진단 로그용 프레임 카운터 (30프레임마다 peak 출력)
    int _debugFrameTimer;

    // ── 스레드 간 통신 ────────────────────────────────────────────

    readonly ConcurrentQueue<short[]> _pcmQueue    = new();
    readonly ConcurrentQueue<string>  _resultQueue = new();

    Thread        _workerThread;
    volatile bool _workerRunning;
    int           _resetSignal;        // Interlocked: 1 = 워커에게 Recognizer 리셋 요청

    // 워커가 Recognizer 생성 시 읽을 설정 (메인이 signal 전에 씀)
    volatile Model  _workerNextModel;
    volatile string _workerNextGrammar;

    // ── 생명주기 ──────────────────────────────────────────────────

    void OnEnable()
    {
        StartWorker();
        StartCoroutine(InitCoroutine());

        // 색 해석(ResolveOwnerColorIndex → PlayerSpawnCoordinator.TryGetColor) 실패로 grammar가
        // 팀워드만 남는 레이스 대비 자동 재시도. 표준 구독 패턴(PlayerSpawnCoordinator 문서 헤더) —
        // 늦은 구독 대비 IsReady 즉시 체크. ApplyOwnerLocalGrammar는 결과가 같으면 no-op이라
        // (§3.4/이 파일 ApplyOwnerLocalGrammar 참고) 여러 번 걸려도 안전하다.
        PlayerSpawnCoordinator.OnPlayersReady   += ApplyOwnerLocalGrammar;
        PlayerSpawnCoordinator.OnRosterChanged  += ApplyOwnerLocalGrammar;
        if (PlayerSpawnCoordinator.IsReady) ApplyOwnerLocalGrammar();
    }

    void OnDisable()
    {
        PlayerSpawnCoordinator.OnPlayersReady  -= ApplyOwnerLocalGrammar;
        PlayerSpawnCoordinator.OnRosterChanged -= ApplyOwnerLocalGrammar;
        StopAllCoroutines();
        Shutdown();
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

        _grammarJson = CheerLexiconBuilder.BuildGrammarJson(OwnerGrammarWords(ResolveOwnerCheerName()));

        DissonanceComms comms = null;
        while (comms == null) { comms = DissonanceComms.GetSingleton(); yield return null; }

        comms.SubscribeToRecordedAudio(this);
        _subscribed = true;
        Debug.Log($"[CheerKeywordEngine] Init OK — grammar={_grammarJson}");

        // Dissonance 오디오 수신 여부 확인 (ResetAudioStream → _dissonanceAudioSeen 설정됨)
        float deadline = Time.time + DissonanceWaitSec;
        while (!_dissonanceAudioSeen && Time.time < deadline)
            yield return null;

        if (!_dissonanceAudioSeen)
        {
            // 마이크 이중 오픈 금지(과거 사고: CheerAndTutorialDesign.md §4.3) — 솔로(1인)일 때만
            // 직접 마이크 fallback 허용. 멀티(2인 이상)는 Dissonance가 늦어도 직접 마이크를 열지
            // 않고 구독만 유지한다 — Dissonance + Microphone.Start 동시 오픈 → 메인 스톨 → NGO
            // 스폰 Deferred/유실 재발 방지.
            bool isSolo = GameSession.Instance != null && GameSession.Instance.ActivePlayerCount == 1;

            if (!isSolo)
            {
                Debug.LogWarning($"[CheerKeywordEngine] Dissonance 오디오 미수신 ({DissonanceWaitSec}s 초과) — 멀티라 마이크 직접 오픈 보류. Dissonance 구독 유지, 늦게라도 오디오 오면 정상 처리됨.");
                yield break;
            }

            Debug.LogWarning($"[CheerKeywordEngine] Dissonance 오디오 미수신 ({DissonanceWaitSec}s 초과) → 직접 마이크 fallback (솔로)");
            comms.UnsubscribeFromRecordedAudio(this);
            _subscribed = false;
            StartSoloMic();

            if (_usingSoloMic)
            {
                float posWait = Time.time + SoloMicPositionWaitSec;
                while (Time.time < posWait && Microphone.GetPosition(_soloMicDevice) <= 0)
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

        string preferred = GameSettingsManager.Instance != null ? GameSettingsManager.Instance.MicDeviceName : "";
        _soloMicDevice = !string.IsNullOrEmpty(preferred) && System.Array.IndexOf(Microphone.devices, preferred) >= 0
            ? preferred
            : null;

        _soloMicClip = Microphone.Start(_soloMicDevice, true, SoloMicBufSec, SoloMicCaptureHz);
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
        _smoothedGain    = 1f;

        SignalWorkerReset(_model, _grammarJson);
        Debug.Log($"[CheerKeywordEngine] 직접 마이크 시작 — 캡처:{_soloMicSourceHz}Hz → Vosk:{VoskFeedHz}Hz, gain={soloMicGain:F1}, normalize={autoNormalizeMic}");
    }

    void Shutdown()
    {
        if (_subscribed)
        {
            DissonanceComms.GetSingleton()?.UnsubscribeFromRecordedAudio(this);
            _subscribed = false;
        }

        StopWorker();

        if (_usingSoloMic)
        {
            Microphone.End(_soloMicDevice);
            _usingSoloMic    = false;
            _soloMicClip     = null;
            _soloMicSourceHz = 0;
            _soloMicDevice   = null;
            _captureBuf      = null;
            _resampleBuf     = null;
            _accumBuf        = null;
            _accumCount      = 0;
            _smoothedGain    = 1f;
        }

        _model               = null;
        _grammarJson         = null;
        _workerNextModel     = null;
        _workerNextGrammar   = null;
        _dissonanceAudioSeen = false;

        while (_pcmQueue.TryDequeue(out _)) { }
        while (_resultQueue.TryDequeue(out _)) { }
    }

    // ── Dissonance 경로 콜백 ─────────────────────────────────────

    protected override void ResetAudioStream(WaveFormat waveFormat)
    {
        _dissonanceAudioSeen = true;

        if (_usingSoloMic) return;

        _dissonanceSampleRate = waveFormat.SampleRate;

        // 스테일 오디오 버리기
        while (_pcmQueue.TryDequeue(out _)) { }

        SignalWorkerReset(_model, _grammarJson);
        Debug.Log($"[CheerKeywordEngine] Recognizer 리셋 신호 — input={_dissonanceSampleRate}Hz vosk={VoskFeedHz}Hz");
    }

    protected override void ProcessAudio(ArraySegment<float> data)
    {
        if (_usingSoloMic) return;

        float[] src    = data.Array;
        int     offset = data.Offset;
        int     count  = data.Count;

        if (_dissonanceSampleRate != VoskFeedHz && _dissonanceSampleRate != 0)
        {
            count  = ResampleLinear(src, offset, count, _dissonanceSampleRate, VoskFeedHz, ref _dissonanceResample);
            src    = _dissonanceResample;
            offset = 0;
        }

        // 솔로 경로와 동일하게 누적 후 MinFeedSamples 단위로 분할 Enqueue
        EnsureAccumCapacity(_accumCount + count);
        Array.Copy(src, offset, _accumBuf, _accumCount, count);
        _accumCount += count;

        while (_accumCount >= MinFeedSamples)
        {
            EnqueuePcmChunk(_accumBuf, 0, MinFeedSamples);
            _accumCount -= MinFeedSamples;
            if (_accumCount > 0)
                Array.Copy(_accumBuf, MinFeedSamples, _accumBuf, 0, _accumCount);
        }
    }

    // ── Update ────────────────────────────────────────────────────

    public override void Update()
    {
        if (_usingSoloMic) PollSoloMic();
        else               base.Update(); // TransferBuffer → ProcessAudio

        DrainResultQueue();
    }

    // ── 솔로 마이크 폴링 ─────────────────────────────────────────

    void PollSoloMic()
    {
        if (_workerNextModel == null || _soloMicClip == null) return;
        if (!Microphone.IsRecording(_soloMicDevice)) return;

        int pos = Microphone.GetPosition(_soloMicDevice);
        if (pos < 0) return;

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

        if (autoNormalizeMic)
            NormalizeBuffer(_captureBuf, samples);
        else if (soloMicGain != 1f)
            ApplyGain(_captureBuf, samples, soloMicGain);

        LogMicLevel(_captureBuf, samples);

        int resampled = ResampleTo16k(_captureBuf, samples);
        AppendAndEnqueue(_resampleBuf, resampled);
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

        // 목표 게인 — 잡음(NormNoiseFloor 미달) 또는 이미 충분히 큰 소리(target 이상)면 1배(무증폭).
        float targetGain = (peak < NormNoiseFloor || peak >= normalizeTargetPeak)
            ? 1f
            : Mathf.Min(normalizeTargetPeak / peak, NormMaxGain);

        // 청크(100ms)마다 목표 게인이 튀는 걸 완만하게 따라간다(조용함→발화 전환 등).
        _smoothedGain = Mathf.Lerp(_smoothedGain, targetGain, GainSmoothingFactor);

        if (Mathf.Abs(_smoothedGain - 1f) < 0.01f) return; // 거의 무증폭이면 곱 연산 스킵

        for (int i = 0; i < count; i++)
            buf[i] = Mathf.Clamp(buf[i] * _smoothedGain, -1f, 1f);
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
        // peak는 게인 적용 후 값. normalize 모드에선 실제 적용 중인 스무딩 게인을 함께 보여준다
        // (soloMicGain은 그 모드에서 무시되는 Inspector 값이라 그대로 찍으면 오해를 준다).
        float appliedGain = autoNormalizeMic ? _smoothedGain : soloMicGain;
        Debug.Log($"[CheerKeywordEngine] 마이크 레벨 peak={peak:F4} {level} (gain={appliedGain:F1}, normalize={autoNormalizeMic})");
    }

    // ── 리샘플 ────────────────────────────────────────────────────

    int ResampleTo16k(float[] input, int count)
        => ResampleLinear(input, 0, count, _soloMicSourceHz, VoskFeedHz, ref _resampleBuf);

    static int ResampleLinear(float[] input, int offset, int count, int sourceHz, int targetHz, ref float[] buf)
    {
        if (sourceHz == targetHz)
        {
            EnsureCapacity(ref buf, count);
            Array.Copy(input, offset, buf, 0, count);
            return count;
        }

        int   outCount = Math.Max(1, (int)((long)count * targetHz / sourceHz));
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

    // ── 솔로 청크 누적 → 큐 ──────────────────────────────────────

    void AppendAndEnqueue(float[] buf, int count)
    {
        EnsureAccumCapacity(_accumCount + count);
        Array.Copy(buf, 0, _accumBuf, _accumCount, count);
        _accumCount += count;

        while (_accumCount >= MinFeedSamples)
        {
            EnqueuePcmChunk(_accumBuf, 0, MinFeedSamples);
            _accumCount -= MinFeedSamples;
            if (_accumCount > 0)
                Array.Copy(_accumBuf, MinFeedSamples, _accumBuf, 0, _accumCount);
        }
    }

    // ── PCM 큐 ───────────────────────────────────────────────────

    void EnqueuePcmChunk(float[] buf, int offset, int count)
    {
        if (count <= 0 || _pcmQueue.Count >= PcmQueueMax) return;

        var chunk = new short[count];
        for (int i = 0; i < count; i++)
            chunk[i] = (short)Math.Max(-32768, Math.Min(32767, (int)(buf[offset + i] * 32767f)));

        _pcmQueue.Enqueue(chunk);
    }

    // ── 워커 스레드 ──────────────────────────────────────────────

    void StartWorker()
    {
        if (_workerThread != null && _workerThread.IsAlive) return;

        _workerRunning = true;
        _workerThread  = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name         = "VoskWorker"
        };
        _workerThread.Start();
    }

    void StopWorker()
    {
        _workerRunning = false;
        if (_workerThread != null && _workerThread.IsAlive)
            _workerThread.Join(1000);
        _workerThread = null;
    }

    void SignalWorkerReset(Model model, string grammar)
    {
        _workerNextModel   = model;
        _workerNextGrammar = grammar;
        Interlocked.Exchange(ref _resetSignal, 1);
    }

    void WorkerLoop()
    {
        VoskRecognizer rec       = null;
        int            feedCount = 0;

        while (_workerRunning)
        {
            // 리셋 신호 처리 (메인 스레드가 Recognizer 재생성 요청 시)
            if (Interlocked.Exchange(ref _resetSignal, 0) == 1)
            {
                rec?.Dispose();
                rec       = null;
                feedCount = 0;

                Model  m = _workerNextModel;
                string g = _workerNextGrammar;
                if (m != null && g != null)
                {
                    rec = new VoskRecognizer(m, VoskFeedHz, g);
                    rec.SetWords(false);
                }
            }

            if (rec == null || !_pcmQueue.TryDequeue(out short[] chunk))
            {
                Thread.Sleep(5);
                continue;
            }

            // 청크는 항상 MinFeedSamples(800) 단위 — EnqueuePcmChunk에서 고정 크기 보장
            bool isFinal = rec.AcceptWaveform(chunk, chunk.Length);

            if (isFinal)
            {
                string json = rec.Result();
                if (!string.IsNullOrEmpty(json))
                    _resultQueue.Enqueue("final|" + json);
            }
            else
            {
                feedCount++;
                if (feedCount >= PartialInterval)
                {
                    feedCount = 0;
                    string json = rec.PartialResult();
                    if (!string.IsNullOrEmpty(json))
                        _resultQueue.Enqueue("partial|" + json);
                }
            }
        }

        rec?.Dispose();
    }

    // ── 결과 큐 처리 (메인 스레드) ───────────────────────────────

    void DrainResultQueue()
    {
        while (_resultQueue.TryDequeue(out string entry))
        {
            int sep = entry.IndexOf('|');
            if (sep < 0) continue;

            string kind = entry.Substring(0, sep);
            string json = entry.Substring(sep + 1);

            var node = JSONNode.Parse(json);
            if (node == null) continue;

            if (kind == "final")
            {
                if (!string.IsNullOrEmpty(node["text"]?.Value))
                    ParseAndSubmit(node, "text");
            }
            else
            {
                if (!string.IsNullOrEmpty(node["partial"]?.Value))
                    ParseAndSubmit(node, "partial");
            }
        }
    }

    // ── 결과 파싱 + 응원 제출 ────────────────────────────────────

    void ParseAndSubmit(JSONNode node, string key)
    {
        string raw = node?[key]?.Value;
        if (string.IsNullOrEmpty(raw)) return;

        foreach (string rawWord in raw.Trim().ToLower().Split(' '))
        {
            if (string.IsNullOrEmpty(rawWord) || rawWord == "[unk]") continue;

            // §5.2 B — 고정 4종(berry/guma/sook/dan) 발음 변형 대체 단어가 등록되면 원래 CheerName으로 되돌림.
            string word = CheerLexiconBuilder.ResolveVariant(rawWord);

            if (_lastDetected.TryGetValue(word, out float lastTime) &&
                Time.time - lastTime < KeywordCooldown)
                continue;

            string teamWord = ResolveTeamCheerWord();
            int myColorIndex = ResolveOwnerColorIndex();
            int colorIndex = _sayTestMode
                ? GetTutorialColorIndex(word)
                : CheerService.GetColorIndex(word);

            bool isTeam = word == teamWord;
            bool isSelf = myColorIndex >= 0 && colorIndex == myColorIndex;

            if (!isTeam && colorIndex < 0)
            {
                Debug.Log($"[CheerKeywordEngine] 인식됐으나 CheerName/TeamCheerWord 불일치: '{word}'");
                continue;
            }

            if (!isTeam && !isSelf && !_sayTestMode)
            {
                _lastDetected[word] = Time.time;
                continue;
            }

            _lastDetected[word] = Time.time;
            Debug.Log($"[CheerKeywordEngine] 키워드 감지: '{word}' team={isTeam} self={isSelf} colorIndex={colorIndex}");

            if (_sayTestMode)
            {
                if (colorIndex >= 0)
                    OnKeywordDetected?.Invoke(colorIndex);
                continue;
            }

            if (CheerService.Instance == null) continue;
            if (isSelf)
                CheerService.Instance.SubmitSelfCheerServerRpc(isVoice: true);
            else if (isTeam)
                CheerService.Instance.SubmitTeamCheerServerRpc(isVoice: true);
        }
    }

    static string ResolveTeamCheerWord()
    {
        if (CheerService.Instance != null)
            return CheerService.Instance.TeamCheerWord;
        if (GameSession.Instance != null)
            return GameSession.Instance.GetSessionTeamCheerWord();
        return GameSession.DefaultTeamCheerWord;
    }

    int ResolveOwnerColorIndex()
    {
        var netObj = GetComponent<NetworkObject>();
        if (netObj == null) return -1;
        if (!PlayerSpawnCoordinator.TryGetColor(netObj.OwnerClientId, out var color)) return -1;
        return PlayerColorUtil.ColorTypeToIndex(color);
    }

    // ── 말해보기 테스트 모드 헬퍼 ─────────────────────────────────

    /// <summary>
    /// 현재 Tutorial에 스폰된 PlayerCheerNameSync 전원의 유효 CheerName으로 colorIndex 역탐색.
    /// 구 GetLobbyColorIndex(LobbyNetworkManager.Instance 슬롯 순회)를 대체 — 로비 의존 제거
    /// (NetworkDesign.md §6B.7 "CheerKeywordEngine에 Tutorial 전용 판정 분기 신설").
    /// CheerService RPC를 부르지 않는 로컬 전용 조회라 CheerService.GetColorIndex는
    /// 쓰지 않는다(그건 실제 응원 제출 경로).
    /// </summary>
    static int GetTutorialColorIndex(string lower)
    {
        foreach (var (clientId, name) in PlayerCheerNameSync.GetAllEffectiveNames())
        {
            if (name != lower) continue;
            if (PlayerSpawnCoordinator.TryGetColor(clientId, out var color))
                return PlayerColorUtil.ColorTypeToIndex(color);
        }
        return -1;
    }

    /// <summary>
    /// 로컬 grammar를 [내 유효 CheerName, TeamCheerWord]로 재적용 (CheerSystemDesign.md §3.4).
    /// 모델 로드 전이면 무시 — InitCoroutine이 같은 헬퍼로 초기 grammar를 만든다.
    /// PlayerSpawnCoordinator.OnPlayersReady/OnRosterChanged로도 걸려 여러 번 호출될 수 있으므로
    /// 결과가 이전과 같으면(_grammarJson 비교) 워커 리셋을 스킵한다 — _workerNextModel/_workerNextGrammar는
    /// "Dissonance 오디오 수신 여부" 판단(InitCoroutine)에도 쓰여 여기서 그 값과 비교하면 안 된다.
    /// </summary>
    public void ApplyOwnerLocalGrammar()
    {
        if (_model == null) return;
        string newJson = CheerLexiconBuilder.BuildGrammarJson(OwnerGrammarWords(ResolveOwnerCheerName()));
        if (newJson == _grammarJson) return;
        _grammarJson = newJson;
        SignalWorkerReset(_model, newJson);
        Debug.Log($"[CheerKeywordEngine] owner grammar 갱신: {newJson}");
    }

    /// <summary>내 CheerName + TeamCheerWord. 남의 이름은 넣지 않는다.</summary>
    static string[] OwnerGrammarWords(string ownerName)
    {
        string team = ResolveTeamCheerWord();
        var list = new List<string>(2);
        if (!string.IsNullOrEmpty(ownerName) && !list.Contains(ownerName))
            list.Add(ownerName);
        if (!string.IsNullOrEmpty(team) && !list.Contains(team))
            list.Add(team);
        return list.Count > 0 ? list.ToArray() : new[] { GameSession.DefaultTeamCheerWord };
    }

    /// <summary>
    /// 내 CheerName. UI와 동일 SSOT(<see cref="CheerService.GetCheerName"/>) —
    /// 게이트 후 세션값, 게이트 전 PlayerCheerNameSync, 없으면 색 기본값.
    /// EffectiveCheerName을 직접 읽으면 스테이지 재스폰 후 빈 NV가 기본색 이름으로
    /// 세션값을 가려 grammar만 틀어지는 전례가 있다.
    /// </summary>
    string ResolveOwnerCheerName()
    {
        int ci = ResolveOwnerColorIndex();
        if (ci < 0)
        {
            // 이 상태로 grammar가 굳으면 TeamCheerWord만 남아 자기 응원이 이번 스테이지 내내 죽는다.
            // OnPlayersReady/OnRosterChanged 재시도(OnEnable)가 곧 다시 부를 것이므로 여기선 경고만.
            Debug.LogWarning("[CheerKeywordEngine] 색 해석 실패(PlayerSpawnCoordinator.TryGetColor) — 이번 호출은 CheerName 없이 진행, 재시도 대기");
            return "";
        }
        return CheerService.GetCheerName(ci);
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
            _accumBuf = new float[Math.Max(needed, MinFeedSamples * 2)];
    }
}
