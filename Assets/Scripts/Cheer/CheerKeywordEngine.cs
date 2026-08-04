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
/// 1. DissonanceComms 준비 대기
/// 2. VoskModelLoader.EnsureModel() → 모델 경로
/// 3. Vosk.Model 생성
/// 4. CheerLexiconBuilder.BuildDemoGrammarJson() → grammar
/// 5. SubscribeToRecordedAudio → 5초 대기 → ResetAudioStream 으로 워커 리셋 신호
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

    [Header("로비 테스트 모드")]
    [Tooltip("true: ServerRpc 미제출, OnKeywordDetected 이벤트만 발행.\n" +
             "로비 씬에 직접 배치할 때 체크. 인게임 플레이어 프리팹은 false.")]
    [SerializeField] bool _lobbyTestMode = false;

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
    const float NormNoiseFloor         = 0.0001f;
    const float NormMaxGain            = 20f;
    const int   PcmQueueMax            = 60;     // 큐 최대 청크 수 (~2초분)

    // ── 메인 스레드 전용 상태 ─────────────────────────────────────

    Model  _model;
    string _grammarJson;
    bool   _subscribed;
    int    _dissonanceSampleRate;

    // 솔로 마이크 경로
    bool      _usingSoloMic;
    AudioClip _soloMicClip;
    int       _soloMicLastPos;
    int       _soloMicSourceHz;

    // 재사용 버퍼 (메인 스레드 전용)
    float[] _captureBuf;
    float[] _resampleBuf;
    float[] _dissonanceResample;
    float[] _accumBuf;
    int     _accumCount;

    // ── 이벤트 (로비 테스트 모드 전용) ───────────────────────────
    /// <summary>
    /// 로비 테스트 모드(_lobbyTestMode=true)에서 키워드 감지 시 발행.
    /// arg = targetColorIndex (해당 CheerName 소유자의 ColorIndex).
    /// ServerRpc 미제출. LobbyMenuController 에서 구독.
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
    }

    void OnDisable()
    {
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

        _grammarJson = _lobbyTestMode
            ? BuildLobbyGrammarJson()
            : BuildInGameGrammarJson();

        DissonanceComms comms = null;
        while (comms == null) { comms = DissonanceComms.GetSingleton(); yield return null; }

        comms.SubscribeToRecordedAudio(this);
        _subscribed = true;
        Debug.Log($"[CheerKeywordEngine] Init OK — grammar={_grammarJson}");

        // Dissonance 오디오 수신 여부 확인 (ResetAudioStream → _workerNextModel 설정됨)
        float deadline = Time.time + DissonanceWaitSec;
        while (_workerNextModel == null && Time.time < deadline)
            yield return null;

        if (_workerNextModel == null)
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
            Microphone.End(null);
            _usingSoloMic    = false;
            _soloMicClip     = null;
            _soloMicSourceHz = 0;
            _captureBuf      = null;
            _resampleBuf     = null;
            _accumBuf        = null;
            _accumCount      = 0;
        }

        _model             = null;
        _grammarJson       = null;
        _workerNextModel   = null;
        _workerNextGrammar = null;

        while (_pcmQueue.TryDequeue(out _)) { }
        while (_resultQueue.TryDequeue(out _)) { }
    }

    // ── Dissonance 경로 콜백 ─────────────────────────────────────

    protected override void ResetAudioStream(WaveFormat waveFormat)
    {
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
        if (!Microphone.IsRecording(null)) return;

        int pos = Microphone.GetPosition(null);
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
                {
                    Debug.Log($"[CheerKeywordEngine] Final JSON: {json}");
                    ParseAndSubmit(node, "text");
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(node["partial"]?.Value))
                {
                    Debug.Log($"[CheerKeywordEngine] Partial JSON: {json}");
                    ParseAndSubmit(node, "partial");
                }
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

            // §5.2 B — 대체 단어(예: hobo)로 인식됐으면 원래 CheerName(hobak)으로 되돌림.
            string word = CheerLexiconBuilder.ResolveVariant(rawWord);

            if (_lastDetected.TryGetValue(word, out float lastTime) &&
                Time.time - lastTime < KeywordCooldown)
                continue;

            int colorIndex = _lobbyTestMode
                ? GetLobbyColorIndex(word)
                : CheerService.GetColorIndex(word);

            if (colorIndex < 0)
            {
                Debug.Log($"[CheerKeywordEngine] 인식됐으나 CheerName 불일치: '{word}'");
                continue;
            }

            _lastDetected[word] = Time.time;
            Debug.Log($"[CheerKeywordEngine] 키워드 감지: '{word}' → colorIndex={colorIndex}");

            // 로비 테스트 모드: 이벤트만 발행, ServerRpc 미제출
            if (_lobbyTestMode)
            {
                OnKeywordDetected?.Invoke(colorIndex);
                continue;
            }

            if (CheerService.Instance != null)
                CheerService.Instance.SubmitCheerServerRpc(colorIndex, isVoice: true);
        }
    }

    // ── 로비 모드 헬퍼 ───────────────────────────────────────────

    /// <summary>
    /// 세션 슬롯의 유효 CheerName으로 colorIndex 역탐색.
    /// CheerService 없이 로비에서 사용.
    /// </summary>
    static int GetLobbyColorIndex(string lower)
    {
        var lnm = LobbyNetworkManager.Instance;
        if (lnm != null)
        {
            for (int i = 0; i < lnm.SlotCount; i++)
            {
                var s = lnm.GetSlot(i);
                if (!s.IsOccupied) continue;
                if (LobbyNetworkManager.GetEffectiveCheerName(s) == lower)
                    return s.ColorIndex;
            }
            return -1;
        }
        // 솔로(LNM 없음): GameSession 세션 이름 → 기본값 순 fallback
        return CheerService.GetColorIndex(lower);
    }

    /// <summary>
    /// 세션 이름 배열로 Vosk grammar를 갱신.
    /// LobbyMenuController.RefreshAllSlots 에서 이름 확정 후 호출.
    /// 모델 로드 전이면 무시 (InitCoroutine에서 세션 이름으로 이미 빌드됨).
    /// </summary>
    public void ApplySessionGrammar(string[] names)
    {
        if (_model == null) return;
        string newJson = CheerLexiconBuilder.BuildGrammarJson(names);
        if (newJson == _workerNextGrammar) return;
        _workerNextModel   = _model;
        _workerNextGrammar = newJson;
        Interlocked.Exchange(ref _resetSignal, 1);
        Debug.Log($"[CheerKeywordEngine] 로비 grammar 갱신: {newJson}");
    }

    static string BuildLobbyGrammarJson()
    {
        var lnm = LobbyNetworkManager.Instance;
        if (lnm == null) return CheerLexiconBuilder.BuildDemoGrammarJson();
        int count = lnm.SlotCount;
        var names = new string[count];
        for (int i = 0; i < count; i++)
            names[i] = LobbyNetworkManager.GetEffectiveCheerName(lnm.GetSlot(i));
        return CheerLexiconBuilder.BuildGrammarJson(names);
    }

    /// <summary>인게임 Vosk grammar — GameSession 세션 이름 우선, 없으면 기본값.</summary>
    static string BuildInGameGrammarJson()
    {
        if (GameSession.Instance != null)
        {
            var names = new string[4];
            for (int i = 0; i < 4; i++)
                names[i] = GameSession.Instance.GetSessionCheerName(i);
            return CheerLexiconBuilder.BuildGrammarJson(names);
        }
        return CheerLexiconBuilder.BuildDemoGrammarJson();
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
