using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using Dissonance;
using NAudio.Wave;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 내 마이크 소리로 팀 응원을 판정한다 — CheerSystemDesign.md §14 (2026-10-07, 구 Vosk 키워드 인식 대체).
/// 클래스 이름은 프리팹 연결 호환용으로 유지(실질 = "CheerSoundEngine").
///
/// [두 가지 일]
/// ① 판정(§14.6): 팀 응원 창이 열려 있고 내가 아직 통과 전이며 등록본(나 1·나 2)이 있을 때만 마이크를
///    CheerSoundMatcher(워커 스레드)에 넣는다. 통과 → SubmitTeamCheerServerRpc(isVoice:true).
///    외쳤는데 기준 미달(final 실패) → CheerSoundLocalState 실패 횟수(연습 7회 → T키 자동 ON, 인게임 3회 → 힌트).
/// ② 녹음(§14.2): 패널(TutorialCheerNameUI)이 BeginCapture/EndCapture로 같은 16kHz 스트림을 받아
///    Host 기준 소리·나 1·나 2를 만든다. 마이크를 따로 열지 않는다.
///
/// [마이크 이중 오픈 금지 — 그대로]
/// 멀티: DissonanceComms.MicrophoneCapture.Subscribe — Dissonance가 연 마이크의 **가공 전 원본**을 옆에서 받는다(10/7).
///   SubscribeToRecordedAudio는 rnnoise·WebRTC(잡음 억제·에코 제거) **처리 후** 소리라(BasePreprocessingPipeline.SendSamplesToSubscribers)
///   같은 사람이 같은 말을 해도 끊김 5 vs 3, 거리 7~12로 흔들렸다. 음성 채팅은 그대로 처리된 소리를 보낸다.
///   원본 캡처를 못 얻으면(드묾) 처리된 스트림으로 대신 받는다.
/// 솔로(ActivePlayerCount==1): Dissonance 가 오디오를 주지 않을 때만 직접 Microphone.Start fallback.
/// 멀티에서는 Dissonance가 늦어도 직접 마이크를 열지 않는다(§4.3, 재발 금지). 구독은 창마다 끊지 않는다.
/// Dissonance IsMuted(옵션 음소거)는 네트워크 전송만 끊고 로컬 캡처는 유지하므로 여기엔 영향 없음.
///
/// [자동 게인 삭제 — 2026-10-07]
/// Vosk용 청크별 자동 게인(NormalizeBuffer)은 음량 곡선을 뭉개 끊김 횟수를 망가뜨린다(§14.11 ② 주의점).
/// 판정은 음량 무관 특징(c0 제외 MFCC + 상대 dB)을 쓰므로 원 신호를 그대로 넣는다.
///
/// [Owner-only]
/// NetworkPlayerSetup.SetupOwner → enabled = true / SetupNonOwner → enabled = false. <see cref="Local"/>로 접근.
///
/// [스레드 구조]
/// 메인: 캡처 → 저역통과+리샘플(16kHz float) → (녹음 중이면 Recorder) / (판정 중이면 _pcmQueue)
/// 워커: _pcmQueue → CheerSoundMatcher.Feed → 통과/실패 이벤트 → _eventQueue
/// 메인: _eventQueue drain → 제출·실패 집계 (Unity API 여기서만)
/// </summary>
[DisallowMultipleComponent]
public class CheerKeywordEngine : BaseMicrophoneSubscriber
{
    public static CheerKeywordEngine Local { get; private set; }

    /// <summary>외쳤는데 기준 미달로 끝난 발화(창 안 연속 횟수, 연습 창 여부). 힌트 UI 구독.</summary>
    public static event Action<int, bool> OnVoiceAttemptFailed;

    // ── 상수 ──────────────────────────────────────────────────────

    const int   FeedHz                 = CheerSoundParams.SampleRate;
    const int   SoloMicCaptureHz       = 48000;
    const int   SoloMicBufSec          = 30;
    const int   ChunkSamples           = 1600;   // 16kHz × 100ms
    const float DissonanceWaitSec      = 5f;
    const float SoloMicWarmupSec       = 0.5f;
    const float SoloMicPositionWaitSec = 1f;
    const float SubmitRetrySec         = 1.5f;
    const int   PcmQueueMax            = 60;     // ~6초분

    // ── Dissonance 경로 ───────────────────────────────────────────

    bool   _subscribed;
    Dissonance.Audio.Capture.IMicrophoneCapture _rawCapture; // null이면 처리된 스트림(SubscribeToRecordedAudio) 구독
    int    _dissonanceSampleRate;
    float[] _dissonanceResample;
    readonly StreamResampler _dissonanceResampler = new();
    volatile bool _dissonanceAudioSeen;

    // ── 솔로 마이크 경로 ─────────────────────────────────────────

    bool      _usingSoloMic;
    AudioClip _soloMicClip;
    string    _soloMicDevice;
    int       _soloMicLastPos;
    int       _soloMicSourceHz;
    float[]   _captureBuf;
    float[]   _resampleBuf;
    readonly StreamResampler _soloResampler = new();

    // ── 청크 누적 ─────────────────────────────────────────────────

    float[] _accumBuf;
    int     _accumCount;

    // ── 판정 상태 ─────────────────────────────────────────────────

    CheerService _boundService;
    int[]        _lastVoters = Array.Empty<int>();
    bool         _listening;
    float        _lastSubmitTime = float.NegativeInfinity;

    // ── 녹음 상태 ─────────────────────────────────────────────────

    readonly CheerSoundRecorder _recorder = new();

    // ── 워커 ──────────────────────────────────────────────────────

    struct MatchEvent
    {
        public int  Generation;
        public bool Detected;      // true = 통과, false = 외쳤는데 미달(final)
        public float Distance;
        public float Threshold;
    }

    readonly ConcurrentQueue<float[]>     _pcmQueue   = new();
    readonly ConcurrentQueue<MatchEvent>  _eventQueue = new();
    Thread        _workerThread;
    volatile bool _workerRunning;
    int           _resetSignal;     // Interlocked: 1 = 워커에게 등록본 재적용 + 버퍼 비우기
    int           _listenGeneration;
    CheerSoundTemplate _workerTemplate1, _workerTemplate2;

    NetworkObject _netObj;
    Player        _player;

    // ── 생명주기 ──────────────────────────────────────────────────

    void Awake()
    {
        _netObj = GetComponent<NetworkObject>();
        _player = GetComponent<Player>();
    }

    /// <summary>
    /// Owner 캐릭터만 Local이 된다 — NetworkPlayerSetup.SetupOwner에서 호출.
    /// 프리팹 기본 enabled라 남의 캐릭터도 생성 순간 OnEnable이 한 번 돈다(곧 SetupNonOwner가 끔).
    /// OnEnable에서 Local을 잡으면 그때 내 엔진을 덮어쓰고 OnDisable에서 비워 버린다(10/7 2인 녹음 버튼 비활성 버그).
    /// </summary>
    public void MarkLocal() => Local = this;

    void OnEnable()
    {
        CheerSoundLocalState.EnrollmentChanged += HandleEnrollmentChanged;
        StartWorker();
        StartCoroutine(InitCoroutine());
    }

    void OnDisable()
    {
        if (Local == this) Local = null;
        CheerSoundLocalState.EnrollmentChanged -= HandleEnrollmentChanged;
        StopAllCoroutines();
        Shutdown();
    }

    // ── 초기화 ────────────────────────────────────────────────────

    IEnumerator InitCoroutine()
    {
        DissonanceComms comms = null;
        while (comms == null) { comms = DissonanceComms.GetSingleton(); yield return null; }

        // 원본 마이크 캡처는 Dissonance 파이프라인이 시작된 뒤 생긴다 — 잠깐 기다렸다가 붙고, 끝내 없으면 처리된 스트림으로
        float rawDeadline = Time.time + DissonanceWaitSec;
        while (comms.MicrophoneCapture == null && Time.time < rawDeadline)
            yield return null;
        _rawCapture = comms.MicrophoneCapture;
        if (_rawCapture != null) _rawCapture.Subscribe(this);
        else
        {
            Debug.LogWarning("[CheerKeywordEngine] 원본 마이크 캡처 없음 — Dissonance 처리 후 스트림으로 대신 받음(인식률 낮을 수 있음)");
            comms.SubscribeToRecordedAudio(this);
        }
        _subscribed = true;
        NetLog.Transition("CheerKeywordEngine", "MicTap", _rawCapture != null ? "source=raw" : "source=processed");

        float deadline = Time.time + DissonanceWaitSec;
        while (!_dissonanceAudioSeen && Time.time < deadline)
            yield return null;

        if (!_dissonanceAudioSeen)
        {
            bool isSolo = GameSession.Instance != null && GameSession.Instance.ActivePlayerCount == 1;
            if (!isSolo)
            {
                Debug.LogWarning($"[CheerKeywordEngine] Dissonance 오디오 미수신 ({DissonanceWaitSec}s 초과) — 멀티라 마이크 직접 오픈 보류. 구독 유지, 늦게라도 오디오 오면 정상 처리됨.");
                yield break;
            }

            Debug.LogWarning($"[CheerKeywordEngine] Dissonance 오디오 미수신 ({DissonanceWaitSec}s 초과) → 직접 마이크 fallback (솔로)");
            Unsubscribe(comms);
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
        _soloMicDevice = !string.IsNullOrEmpty(preferred) && Array.IndexOf(Microphone.devices, preferred) >= 0
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
        _soloResampler.Reset();
        Debug.Log($"[CheerKeywordEngine] 직접 마이크 시작 — 캡처:{_soloMicSourceHz}Hz → {FeedHz}Hz");
    }

    void Shutdown()
    {
        Unsubscribe(DissonanceComms.GetSingleton());

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
        }

        if (_recorder.IsRecording) _recorder.End();

        BindCheerService(null);
        _listening      = false;
        _lastSubmitTime = float.NegativeInfinity;

        _accumBuf     = null;
        _accumCount   = 0;
        _dissonanceResampler.Reset();
        _soloResampler.Reset();
        _dissonanceAudioSeen = false;

        while (_pcmQueue.TryDequeue(out _)) { }
        while (_eventQueue.TryDequeue(out _)) { }
    }

    void Unsubscribe(DissonanceComms comms)
    {
        if (!_subscribed) return;
        if (_rawCapture != null) _rawCapture.Unsubscribe(this);
        else comms?.UnsubscribeFromRecordedAudio(this);
        _rawCapture = null;
        _subscribed = false;
    }

    // ── 공개: 상태·녹음 (패널용) ─────────────────────────────────

    /// <summary>마이크 소리가 실제로 들어오고 있는가(Dissonance 탭 또는 솔로 마이크).</summary>
    public bool HasAudioInput => _dissonanceAudioSeen || _usingSoloMic;
    public bool IsCapturing => _recorder.IsRecording;
    public float CaptureSeconds => _recorder.ElapsedSec;
    /// <summary>상한(RecordMaxSec)에 닿아 스스로 멈춤 — 패널은 이걸 보고 EndCapture를 부른다.</summary>
    public bool CaptureHitLimit => !_recorder.IsRecording && _recorder.HitLimit;

    /// <summary>녹음 시작. 마이크 입력이 없으면 false. 판정 중이어도 녹음은 된다(패널은 게이트 전이라 실제로는 겹치지 않음).</summary>
    public bool BeginCapture()
    {
        if (!HasAudioInput) return false;
        _accumCount = 0;
        _recorder.Begin();
        return true;
    }

    /// <summary>녹음 정지 → 16kHz mono. 앞뒤 침묵 자르기·특징 추출은 CheerSoundTemplate.Build가 한다.</summary>
    public float[] EndCapture() => _recorder.End();

    // ── Dissonance 경로 콜백 ─────────────────────────────────────

    protected override void ResetAudioStream(WaveFormat waveFormat)
    {
        _dissonanceAudioSeen = true;
        if (_usingSoloMic) return;

        _dissonanceSampleRate = waveFormat.SampleRate;
        while (_pcmQueue.TryDequeue(out _)) { }
        _accumCount = 0;
        _dissonanceResampler.Reset();
        Debug.Log($"[CheerKeywordEngine] 오디오 스트림 리셋 — input={_dissonanceSampleRate}Hz → {FeedHz}Hz");
    }

    protected override void ProcessAudio(ArraySegment<float> data)
    {
        // 창 밖·녹음 아님이면 버린다 — base.Update는 계속 돌아 Dissonance 전달 버퍼는 넘치지 않는다.
        if (_usingSoloMic || _dissonanceSampleRate <= 0) return;
        if (!_listening && !_recorder.IsRecording) return;

        int count = _dissonanceResampler.Process(data.Array, data.Offset, data.Count,
                                                 _dissonanceSampleRate, ref _dissonanceResample);
        Consume(_dissonanceResample, count);
    }

    // ── Update ────────────────────────────────────────────────────

    public override void Update()
    {
        UpdateListening();

        if (_usingSoloMic) PollSoloMic();
        else               base.Update(); // TransferBuffer → ProcessAudio

        DrainEventQueue();
    }

    // ── 듣는 구간 게이팅 ─────────────────────────────────────────

    void UpdateListening()
    {
        var svc = CheerService.Instance;
        if (!ReferenceEquals(svc, _boundService))
            BindCheerService(svc);

        bool windowOpen = svc != null && svc.IsHazardWindowActive && !HasLocalPassed();
        bool enrolled   = CheerSoundLocalState.IsEnrolled;

        // 등록본 없이 인게임 창을 만남 = 게이트 전에 등록을 못 한 사람(§14.8) → 이번 세션 T키 자동 ON.
        // Tutorial/Interlude 연습 창에서는 켜지 않는다(등록 전에 E를 눌러 봤을 뿐일 수 있음).
        if (windowOpen && !enrolled && !CheerSoundLocalState.SessionTKeyAutoOn && !IsPreGateScene())
            CheerSoundLocalState.EnableSessionTKey("등록본 없음");

        bool shouldListen = windowOpen && enrolled;
        if (shouldListen == _listening) return;

        _listening = shouldListen;
        if (shouldListen) BeginListening();
        Debug.Log($"[CheerKeywordEngine] 청취 {(shouldListen ? "시작" : "중지")}");
    }

    static bool IsPreGateScene() =>
        TutorialNetworkManager.Instance != null || InterludeNetworkManager.Instance != null;

    void BindCheerService(CheerService svc)
    {
        if (!ReferenceEquals(_boundService, null))
            _boundService.OnTeamVoteChanged -= HandleTeamVoteChanged;

        _boundService = svc;
        _lastVoters   = Array.Empty<int>();

        if (svc != null)
            svc.OnTeamVoteChanged += HandleTeamVoteChanged;
    }

    void HandleTeamVoteChanged(int current, int required, int[] voterColorIndices)
        => _lastVoters = voterColorIndices ?? Array.Empty<int>();

    void HandleEnrollmentChanged()
    {
        // 등록본이 바뀌면 다음 창부터 새 등록본 — 듣는 중이면 즉시 재적용
        if (_listening) SignalWorkerReset();
    }

    /// <summary>이번 창에서 Host가 내 통과를 확정했는지(명단은 창이 닫힐 때 Host가 비워서 보낸다).</summary>
    bool HasLocalPassed()
    {
        if (_lastVoters.Length == 0) return false;
        int myColorIndex = ResolveColorIndex();
        return myColorIndex >= 0 && Array.IndexOf(_lastVoters, myColorIndex) >= 0;
    }

    int ResolveColorIndex()
    {
        if (_netObj != null && PlayerSpawnCoordinator.TryGetColor(_netObj.OwnerClientId, out var sessionColor))
            return Array.IndexOf(PlayerColorUtil.ColorOrder, sessionColor);
        return _player != null ? Array.IndexOf(PlayerColorUtil.ColorOrder, _player.playerColorType) : -1;
    }

    /// <summary>창이 열리는 순간 — 창 밖 소리가 섞이지 않게 파이프라인과 매처를 비운다.</summary>
    void BeginListening()
    {
        while (_pcmQueue.TryDequeue(out _)) { }
        while (_eventQueue.TryDequeue(out _)) { }
        _accumCount = 0;
        _dissonanceResampler.Reset();
        _soloResampler.Reset();
        CheerSoundLocalState.OnWindowOpened();
        SignalWorkerReset();
    }

    // ── 솔로 마이크 폴링 ─────────────────────────────────────────

    void PollSoloMic()
    {
        if (_soloMicClip == null || !Microphone.IsRecording(_soloMicDevice)) return;

        int pos = Microphone.GetPosition(_soloMicDevice);
        if (pos < 0) return;

        bool wanted = _listening || _recorder.IsRecording;
        if (!wanted || pos <= _soloMicLastPos) { _soloMicLastPos = pos; return; }

        int samples = pos - _soloMicLastPos;
        EnsureCapacity(ref _captureBuf, samples);
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

        int resampled = _soloResampler.Process(_captureBuf, 0, samples, _soloMicSourceHz, ref _resampleBuf);
        Consume(_resampleBuf, resampled);
    }

    // ── 16kHz 신호 소비: 녹음 / 판정 ────────────────────────────

    void Consume(float[] buf, int count)
    {
        if (count <= 0) return;
        if (_recorder.IsRecording) _recorder.Append(buf, 0, count);
        if (_listening) AppendAndEnqueue(buf, count);
    }

    void AppendAndEnqueue(float[] buf, int count)
    {
        EnsureAccumCapacity(_accumCount + count);
        Array.Copy(buf, 0, _accumBuf, _accumCount, count);
        _accumCount += count;

        int consumed = 0;
        while (_accumCount - consumed >= ChunkSamples)
        {
            if (_pcmQueue.Count < PcmQueueMax)
            {
                var chunk = new float[ChunkSamples];
                Array.Copy(_accumBuf, consumed, chunk, 0, ChunkSamples);
                _pcmQueue.Enqueue(chunk);
            }
            consumed += ChunkSamples;
        }

        if (consumed > 0)
        {
            _accumCount -= consumed;
            if (_accumCount > 0)
                Array.Copy(_accumBuf, consumed, _accumBuf, 0, _accumCount);
        }
    }

    // ── 워커 스레드 ──────────────────────────────────────────────

    void StartWorker()
    {
        if (_workerThread != null && _workerThread.IsAlive) return;
        _workerRunning = true;
        _workerThread  = new Thread(WorkerLoop) { IsBackground = true, Name = "CheerSoundMatcher" };
        _workerThread.Start();
    }

    void StopWorker()
    {
        _workerRunning = false;
        if (_workerThread != null && _workerThread.IsAlive)
            _workerThread.Join(1000);
        _workerThread = null;
    }

    /// <summary>등록본 재적용 + 매처 비우기. 등록본 참조는 메인에서 잡아 워커가 읽는다(불변 객체).</summary>
    void SignalWorkerReset()
    {
        _workerTemplate1 = CheerSoundLocalState.MyTemplate1;
        _workerTemplate2 = CheerSoundLocalState.MyTemplate2;
        Interlocked.Increment(ref _listenGeneration);
        Interlocked.Exchange(ref _resetSignal, 1);
    }

    void WorkerLoop()
    {
        var matcher = new CheerSoundMatcher();
        int generation = Volatile.Read(ref _listenGeneration);
        int lastEval = 0;

        while (_workerRunning)
        {
            if (Interlocked.Exchange(ref _resetSignal, 0) == 1)
            {
                matcher.SetTemplates(_workerTemplate1, _workerTemplate2);
                while (_pcmQueue.TryDequeue(out _)) { }
                generation = Volatile.Read(ref _listenGeneration);
                lastEval = matcher.EvalCount;
            }

            int currentGeneration = Volatile.Read(ref _listenGeneration);
            if (currentGeneration != generation)
            {
                matcher.Reset();
                generation = currentGeneration;
                lastEval = matcher.EvalCount;
            }

            if (!matcher.HasTemplates || !_pcmQueue.TryDequeue(out float[] chunk))
            {
                Thread.Sleep(5);
                continue;
            }

            bool detected = matcher.Feed(chunk, 0, chunk.Length);
            var last = matcher.Last;
            if (detected)
            {
                _eventQueue.Enqueue(new MatchEvent { Generation = generation, Detected = true, Distance = last.Distance, Threshold = last.Threshold });
                continue;
            }
            // 외쳤는데(사전 게이트 통과) 말이 끝났을 때 기준 미달 → 실패 1회
            if (matcher.EvalCount != lastEval && last.Final && last.Loud && last.EnoughSpeech && !last.Detected)
                _eventQueue.Enqueue(new MatchEvent { Generation = generation, Detected = false, Distance = last.Distance, Threshold = last.Threshold });
            lastEval = matcher.EvalCount;
        }
    }

    // ── 결과 처리 (메인 스레드) ──────────────────────────────────

    void DrainEventQueue()
    {
        while (_eventQueue.TryDequeue(out MatchEvent e))
        {
            if (!_listening || e.Generation != Volatile.Read(ref _listenGeneration)) continue;

            if (e.Detected)
            {
                TrySubmit(e);
                continue;
            }

            bool practice = CheerService.Instance != null && CheerService.Instance.IsPracticeWindow;
            CheerSoundLocalState.OnVoiceAttemptFailed(practice);
            NetLog.Transition("CheerKeywordEngine", "VoiceAttemptFailed",
                $"d={e.Distance:0.00} th={e.Threshold:0.00} windowStreak={CheerSoundLocalState.WindowFailStreak} practice={practice} practiceStreak={CheerSoundLocalState.PracticeFailStreak}");
            OnVoiceAttemptFailed?.Invoke(CheerSoundLocalState.WindowFailStreak, practice);
        }
    }

    void TrySubmit(MatchEvent e)
    {
        if (Time.time - _lastSubmitTime < SubmitRetrySec) return;

        var svc = CheerService.Instance;
        if (svc == null) return;

        _lastSubmitTime = Time.time;
        // 같은 발화의 남은 평가로 다시 제출하지 않도록 매처를 비운다.
        Interlocked.Increment(ref _listenGeneration);

        CheerSoundLocalState.OnVoicePassed();
        NetLog.Transition("CheerKeywordEngine", "VoiceDetected", $"d={e.Distance:0.00} th={e.Threshold:0.00}");
        svc.SubmitTeamCheerServerRpc(isVoice: true);
    }

    // ── 리샘플 ────────────────────────────────────────────────────

    /// <summary>
    /// 임의 입력 레이트 → 16kHz 스트리밍 리샘플러. 저역통과 FIR(윈도우드 싱크) 후 선형 보간.
    /// 필터 이력과 보간 위상을 청크 사이에 이어가므로 경로(Dissonance/솔로)마다 인스턴스를 따로 쓴다.
    /// </summary>
    sealed class StreamResampler
    {
        const int   TapCount = 63;
        const float CutoffHz = 7000f;   // 16kHz 나이퀴스트(8k) 아래로 여유 — 전이 대역이 8k를 넘기 전에 충분히 감쇠

        int     _sourceHz;
        float[] _taps;
        readonly float[] _delay = new float[TapCount * 2];
        int     _delayPos;
        double  _phase;          // 다음 출력 샘플의 입력 위치(현재 청크 시작 기준). [-1,0)이면 직전 청크 마지막 샘플과 첫 샘플 사이
        float   _lastFiltered;
        float[] _filtered;

        public void Reset()
        {
            Array.Clear(_delay, 0, _delay.Length);
            _delayPos     = 0;
            _phase        = 0d;
            _lastFiltered = 0f;
        }

        public int Process(float[] input, int offset, int count, int sourceHz, ref float[] output)
        {
            if (count <= 0) return 0;

            if (sourceHz != _sourceHz)
            {
                _sourceHz = sourceHz;
                _taps     = sourceHz > FeedHz ? BuildLowPass(sourceHz) : null;
                Reset();
            }

            if (sourceHz == FeedHz)
            {
                EnsureCapacity(ref output, count);
                Array.Copy(input, offset, output, 0, count);
                return count;
            }

            EnsureCapacity(ref _filtered, count);
            if (_taps != null) Filter(input, offset, count);
            else               Array.Copy(input, offset, _filtered, 0, count);

            double step = (double)sourceHz / FeedHz;
            EnsureCapacity(ref output, (int)(count / step) + 2);

            int written = 0;
            while (_phase < count - 1)
            {
                int   idx  = (int)Math.Floor(_phase);
                float frac = (float)(_phase - idx);
                float a    = idx < 0 ? _lastFiltered : _filtered[idx];
                float b    = _filtered[idx + 1];
                output[written++] = a + (b - a) * frac;
                _phase += step;
            }

            _phase       -= count;
            _lastFiltered = _filtered[count - 1];
            return written;
        }

        void Filter(float[] input, int offset, int count)
        {
            // 같은 샘플을 _delay[p]와 _delay[p+TapCount] 두 곳에 써서 [p, p+TapCount) 구간을 항상 모듈로 없이 읽는다.
            for (int i = 0; i < count; i++)
            {
                _delayPos = (_delayPos == 0 ? TapCount : _delayPos) - 1;
                float x = input[offset + i];
                _delay[_delayPos] = x;
                _delay[_delayPos + TapCount] = x;

                float y = 0f;
                for (int k = 0; k < TapCount; k++)
                    y += _taps[k] * _delay[_delayPos + k];
                _filtered[i] = y;
            }
        }

        static float[] BuildLowPass(int sourceHz)
        {
            var    taps = new float[TapCount];
            double fc   = CutoffHz / sourceHz;   // 정규화 차단 주파수 (cycles/sample)
            int    mid  = TapCount / 2;
            double sum  = 0d;

            for (int i = 0; i < TapCount; i++)
            {
                int    n      = i - mid;
                double sinc   = n == 0 ? 2d * fc : Math.Sin(2d * Math.PI * fc * n) / (Math.PI * n);
                double window = 0.54d - 0.46d * Math.Cos(2d * Math.PI * i / (TapCount - 1));
                taps[i] = (float)(sinc * window);
                sum    += taps[i];
            }

            for (int i = 0; i < TapCount; i++)
                taps[i] = (float)(taps[i] / sum);   // DC 게인 1
            return taps;
        }
    }

    // ── 버퍼 용량 보장 ────────────────────────────────────────────

    static void EnsureCapacity(ref float[] arr, int needed)
    {
        if (arr == null || arr.Length < needed)
            arr = new float[needed];
    }

    void EnsureAccumCapacity(int needed)
    {
        if (_accumBuf == null || _accumBuf.Length < needed)
            _accumBuf = new float[Math.Max(needed, ChunkSamples * 2)];
    }
}
