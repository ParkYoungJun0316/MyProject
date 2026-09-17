using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using Dissonance;
using NAudio.Wave;
using Unity.Netcode;
using UnityEngine;
using Vosk;

/// <summary>
/// Dissonance 마이크 스트림을 Vosk에 연결해 TeamCheerWord를 감지한다.
///
/// [마이크 이중 오픈 금지]
/// 멀티: DissonanceComms.SubscribeToRecordedAudio 로 Dissonance 스트림 탭.
/// 솔로(ActivePlayerCount==1): Dissonance 가 오디오를 주지 않을 때만 직접 Microphone.Start fallback.
/// 멀티에서는 Dissonance가 늦어도 직접 마이크를 열지 않는다 — 동시 오픈 시 메인 스톨로
/// NGO 스폰 Deferred/유실이 발생한 전례가 있음(CheerSystemDesign.md §4.3, 재발 금지).
///
/// [Owner-only]
/// NetworkPlayerSetup.SetupOwner → enabled = true
/// NetworkPlayerSetup.SetupNonOwner → enabled = false
///
/// [초기화 순서]
/// 1. VoskModelLoader 백그라운드 로드 완료 대기 → GetSharedModel() (null이면 초기화 중단)
/// 2. OwnerGrammarWords() → [TeamCheerWord] 1단어 grammar 빌드
/// 3. DissonanceComms 준비 대기
/// 4. SubscribeToRecordedAudio → 5초 대기 → ResetAudioStream 으로 워커 리셋 신호
///    5초 내 오디오 없으면 직접 마이크 fallback
///
/// [듣는 구간 — 2026-09-15, CheerSystemDesign.md §4.7]
/// 팀 응원 창(CheerService.IsHazardWindowActive)이 열려 있고, 이번 창에서 내가 아직 통과
/// (OnTeamVoteChanged 명단)하지 않았을 때만 Vosk에 음성을 넣는다. 창 밖에서도 캡처 버퍼는 매 프레임
/// 비우되 버린다. Dissonance 구독은 창마다 끊지 않는다(구독/해제 반복 금지 — 마이크 이중 오픈 사고와 같은 계열).
/// 창이 열리는 순간 큐·리샘플러·Recognizer 발화 상태를 비워 창 밖 잡담이 첫 외침에 섞이지 않게 한다.
///
/// [키워드 감지 방식 — 2026-09-15 변경]
/// PartialResult: 매 청크(100ms)마다 계산. TeamCheerWord가 연속 PartialConfirmHits번 들리면 즉시 제출.
///   한 번 튀었다가 정정되는 추측은 연속 조건에서 걸러진다. (2026-09-10 "partial 금지"는 grammar가 여러
///   단어이고 게임 내내 듣던 시절의 오탐 대응이었다 — 1단어 + 창 게이팅 + 연속 확인으로 대체.)
/// FinalResult  : 침묵으로 확정된 결과에 TeamCheerWord가 있으면 즉시 제출(partial을 놓친 경우의 보험).
///
/// [스레드 구조]
/// 메인 스레드 : 오디오 캡처 → 저역통과+리샘플(16kHz) → float→short → _pcmQueue
///              _resultQueue 에서 결과 꺼내 판정·제출 (Unity API 여기서만)
/// 워커 스레드 : _pcmQueue 에서 꺼내 AcceptWaveform → Result/PartialResult → _resultQueue
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

    const int   VoskFeedHz             = 16000;
    const int   SoloMicCaptureHz       = 48000;
    const int   SoloMicBufSec          = 30;
    const int   MinFeedSamples         = 1600;   // 16kHz × 100ms
    const float DissonanceWaitSec      = 5f;
    const float SoloMicWarmupSec       = 0.5f;
    const float SoloMicPositionWaitSec = 1f;

    // partial에서 TeamCheerWord가 연속 이만큼 들려야 제출. 청크 100ms라 2 = 약 0.1~0.2초 동안 추측이 유지된 것.
    const int   PartialConfirmHits     = 2;

    // 제출 후 Host 통과 명단(OnTeamVoteChanged)이 오기 전까지 같은 외침으로 RPC를 연타하지 않는 간격.
    // Host가 거절한 경우(창 열림 시점이 머신마다 살짝 달라 생기는 경합)에는 이 시간이 지나면 다시 제출할 수 있다.
    const float SubmitRetrySec         = 1.5f;

    // 0.0001은 거의 완전한 무음만 걸러내는 수준이라, 배경 잡음(peak 0.001~0.005대)까지
    // NormMaxGain(20배) 가까이 증폭되어 Vosk 입력이 잡음으로 뭉개지는 문제가 있었다.
    // LogMicLevel 로그 기준(peak>0.01="작은 소리")과 맞춰 그 아래는 잡음으로 간주하고 증폭하지 않는다.
    // 실측 후 필요하면 Inspector 노출 없이 이 값만 조정할 것(값 자체가 튜닝 포인트).
    const float NormNoiseFloor         = 0.008f;
    const float NormMaxGain            = 20f;

    // 100ms 청크마다 목표 게인이 갑자기 바뀌면(예: 조용함→발화 시작) Vosk 입력 다이내믹이
    // 뭉개진다. 이전 스무딩값과 목표값을 섞어 완만하게 따라간다. 1에 가까울수록 즉각 반응.
    const float GainSmoothingFactor    = 0.3f;

    const int   PcmQueueMax            = 60;     // 큐 최대 청크 수 (~6초분)

    // ── 메인 스레드 전용 상태 ─────────────────────────────────────

    Model  _model;
    string _grammarJson;
    bool   _subscribed;
    int    _dissonanceSampleRate;

    NetworkObject _netObj;
    Player        _player;

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

    // 경로별 리샘플러 — 청크 경계를 넘어 필터·위상 상태를 이어가야 해서 경로마다 따로 둔다.
    readonly StreamResampler _dissonanceResampler = new();
    readonly StreamResampler _soloResampler       = new();

    // NormalizeBuffer 청크 간 게인 스무딩 상태 (메인 스레드 전용). 1f = 무증폭.
    float _smoothedGain = 1f;

    // 듣는 구간 게이팅
    bool         _listening;
    CheerService _boundService;
    int[]        _lastVoters = Array.Empty<int>();

    // 판정
    int   _partialHits;
    float _lastSubmitTime = float.NegativeInfinity;

    // 진단 로그용 프레임 카운터 (30프레임마다 peak 출력)
    int _debugFrameTimer;

    // ── 스레드 간 통신 ────────────────────────────────────────────

    readonly struct VoskResult
    {
        public readonly int    Generation;
        public readonly bool   IsFinal;
        public readonly string Json;

        public VoskResult(int generation, bool isFinal, string json)
        {
            Generation = generation;
            IsFinal    = isFinal;
            Json       = json;
        }
    }

    readonly ConcurrentQueue<short[]>    _pcmQueue    = new();
    readonly ConcurrentQueue<VoskResult> _resultQueue = new();

    Thread        _workerThread;
    volatile bool _workerRunning;
    int           _resetSignal;        // Interlocked: 1 = 워커에게 Recognizer 재생성 요청 (모델/grammar/스트림 변경)

    // Interlocked: 창이 열릴 때·제출 직후 증가. 워커는 값이 바뀌면 Recognizer.FinalResult()로 발화 상태를 비우고,
    // 메인은 세대가 다른 결과를 버린다(리셋 전에 계산된 낡은 결과가 새 창에서 제출되지 않게).
    int           _listenGeneration;

    // 워커가 Recognizer 생성 시 읽을 설정 (메인이 signal 전에 씀)
    volatile Model  _workerNextModel;
    volatile string _workerNextGrammar;

    // ── 생명주기 ──────────────────────────────────────────────────

    void Awake()
    {
        _netObj = GetComponent<NetworkObject>();
        _player = GetComponent<Player>();
    }

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
        VoskModelLoader.BeginLoad();
        while (VoskModelLoader.IsLoading)
            yield return null;

        _model = VoskModelLoader.GetSharedModel();
        if (_model == null)
        {
            Debug.LogError("[CheerKeywordEngine] 공유 Model 없음 — 초기화 중단");
            yield break;
        }

        _grammarJson = CheerLexiconBuilder.BuildGrammarJson(OwnerGrammarWords());

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
            // 마이크 이중 오픈 금지(과거 사고: CheerSystemDesign.md §4.3) — 솔로(1인)일 때만
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
        _soloResampler.Reset();

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
        }

        BindCheerService(null);
        _listening      = false;
        _partialHits    = 0;
        _lastSubmitTime = float.NegativeInfinity;

        _accumBuf     = null;
        _accumCount   = 0;
        _smoothedGain = 1f;
        _dissonanceResampler.Reset();
        _soloResampler.Reset();

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
        _accumCount = 0;
        _dissonanceResampler.Reset();

        SignalWorkerReset(_model, _grammarJson);
        Debug.Log($"[CheerKeywordEngine] Recognizer 리셋 신호 — input={_dissonanceSampleRate}Hz vosk={VoskFeedHz}Hz");
    }

    protected override void ProcessAudio(ArraySegment<float> data)
    {
        // 창 밖이면 버린다 — base.Update는 계속 돌아 Dissonance 전달 버퍼는 넘치지 않는다.
        if (_usingSoloMic || !_listening || _dissonanceSampleRate <= 0) return;

        int count = _dissonanceResampler.Process(data.Array, data.Offset, data.Count,
                                                 _dissonanceSampleRate, ref _dissonanceResample);
        AppendAndEnqueue(_dissonanceResample, count);
    }

    // ── Update ────────────────────────────────────────────────────

    public override void Update()
    {
        UpdateListening();

        if (_usingSoloMic) PollSoloMic();
        else               base.Update(); // TransferBuffer → ProcessAudio

        DrainResultQueue();
    }

    // ── 듣는 구간 게이팅 ─────────────────────────────────────────

    void UpdateListening()
    {
        var svc = CheerService.Instance;
        if (!ReferenceEquals(svc, _boundService))
            BindCheerService(svc);

        bool shouldListen = _model != null
                         && svc != null
                         && svc.IsHazardWindowActive
                         && !HasLocalPassed();

        if (shouldListen == _listening) return;

        _listening = shouldListen;
        if (shouldListen) BeginListening();
        Debug.Log($"[CheerKeywordEngine] 청취 {(shouldListen ? "시작" : "중지")}");
    }

    void BindCheerService(CheerService svc)
    {
        // 파괴된 CheerService도 C# 객체로는 남아 있으므로 Unity null 비교가 아니라 참조로 해제한다.
        if (!ReferenceEquals(_boundService, null))
            _boundService.OnTeamVoteChanged -= HandleTeamVoteChanged;

        _boundService = svc;
        _lastVoters   = Array.Empty<int>();

        if (svc != null)
            svc.OnTeamVoteChanged += HandleTeamVoteChanged;
    }

    void HandleTeamVoteChanged(int current, int required, int[] voterColorIndices)
        => _lastVoters = voterColorIndices ?? Array.Empty<int>();

    /// <summary>이번 창에서 Host가 내 통과를 확정했는지. 창 열림과 명단 도착 순서는 머신마다 다를 수 있어
    /// PlayerCheerHeartsUI처럼 마지막 명단을 기억해 판정한다(명단은 창이 닫힐 때 Host가 비워서 보낸다).</summary>
    bool HasLocalPassed()
    {
        if (_lastVoters.Length == 0) return false;
        int myColorIndex = ResolveColorIndex();
        return myColorIndex >= 0 && Array.IndexOf(_lastVoters, myColorIndex) >= 0;
    }

    /// <summary>ColorOrder 인덱스. PlayerCheerHeartsUI.ResolveColorIndex와 동일 — 색 확정 레이스 때문에 캐시하지 않는다.</summary>
    int ResolveColorIndex()
    {
        if (_netObj != null && PlayerSpawnCoordinator.TryGetColor(_netObj.OwnerClientId, out var sessionColor))
            return Array.IndexOf(PlayerColorUtil.ColorOrder, sessionColor);
        return _player != null ? Array.IndexOf(PlayerColorUtil.ColorOrder, _player.playerColorType) : -1;
    }

    /// <summary>창이 열리는 순간 — 창 밖 소리가 섞이지 않게 입력 파이프라인과 Recognizer 발화 상태를 비운다.</summary>
    void BeginListening()
    {
        while (_pcmQueue.TryDequeue(out _)) { }
        while (_resultQueue.TryDequeue(out _)) { }

        _accumCount      = 0;
        _smoothedGain    = 1f;
        _partialHits     = 0;
        _debugFrameTimer = 0;
        _dissonanceResampler.Reset();
        _soloResampler.Reset();

        Interlocked.Increment(ref _listenGeneration);
    }

    // ── 솔로 마이크 폴링 ─────────────────────────────────────────

    void PollSoloMic()
    {
        if (_workerNextModel == null || _soloMicClip == null) return;
        if (!Microphone.IsRecording(_soloMicDevice)) return;

        int pos = Microphone.GetPosition(_soloMicDevice);
        if (pos < 0) return;

        // 창 밖이면 읽기 위치만 따라가고 버린다(창이 열렸을 때 쌓인 옛 소리를 한꺼번에 먹지 않게).
        if (!_listening || pos <= _soloMicLastPos) { _soloMicLastPos = pos; return; }

        int samples = pos - _soloMicLastPos;

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

        int resampled = _soloResampler.Process(_captureBuf, 0, samples, _soloMicSourceHz, ref _resampleBuf);
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

    static void EnsureCapacity(ref float[] arr, int needed)
    {
        if (arr == null || arr.Length < needed)
            arr = new float[needed];
    }

    // ── 리샘플 ────────────────────────────────────────────────────

    /// <summary>
    /// 임의 입력 레이트 → 16kHz 스트리밍 리샘플러. 저역통과 FIR(윈도우드 싱크) 후 선형 보간.
    /// [2026-09-15] 이전 ResampleLinear는 필터 없이 48k→16k 보간만 해서 8kHz 이상 성분이 음성 대역으로
    /// 접혀 들어왔고(에일리어싱), 청크마다 보간 위상을 0으로 리셋해 경계마다 샘플이 튀었다. 둘 다 인식률을 깎는다.
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
                _taps     = sourceHz > VoskFeedHz ? BuildLowPass(sourceHz) : null;
                Reset();
            }

            if (sourceHz == VoskFeedHz)
            {
                EnsureCapacity(ref output, count);
                Array.Copy(input, offset, output, 0, count);
                return count;
            }

            EnsureCapacity(ref _filtered, count);
            if (_taps != null) Filter(input, offset, count);
            else               Array.Copy(input, offset, _filtered, 0, count);

            double step = (double)sourceHz / VoskFeedHz;
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

    // ── 청크 누적 → 큐 ───────────────────────────────────────────

    void AppendAndEnqueue(float[] buf, int count)
    {
        if (count <= 0) return;

        EnsureAccumCapacity(_accumCount + count);
        Array.Copy(buf, 0, _accumBuf, _accumCount, count);
        _accumCount += count;

        int consumed = 0;
        while (_accumCount - consumed >= MinFeedSamples)
        {
            EnqueuePcmChunk(_accumBuf, consumed, MinFeedSamples);
            consumed += MinFeedSamples;
        }

        if (consumed > 0)
        {
            _accumCount -= consumed;
            if (_accumCount > 0)
                Array.Copy(_accumBuf, consumed, _accumBuf, 0, _accumCount);
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
        VoskRecognizer rec        = null;
        int            generation = Volatile.Read(ref _listenGeneration);

        while (_workerRunning)
        {
            // 재생성 신호 처리 (모델/grammar/스트림 변경)
            if (Interlocked.Exchange(ref _resetSignal, 0) == 1)
            {
                rec?.Dispose();
                rec = null;

                Model  m = _workerNextModel;
                string g = _workerNextGrammar;
                if (m != null && g != null)
                {
                    rec = new VoskRecognizer(m, VoskFeedHz, g);
                    rec.SetWords(false);
                }
                generation = Volatile.Read(ref _listenGeneration);
            }

            // 창 열림·제출 직후 — 이전 발화 상태를 비운다(재생성보다 가볍다).
            // Reset()은 쓰지 않는다: Vosk Reset은 디코더만 마감하고 특징 파이프라인에 남은(아직 디코딩 안 된)
            // 외침 꼬리를 버리지 않아, 다음 창 첫 청크에서 그 꼬리가 디코딩되어 말 안 해도 제출됐다(2026-09-18).
            // FinalResult()는 남은 프레임을 전부 소진하고 FINALIZED로 만들어 다음 AcceptWaveform에서
            // 파이프라인까지 새로 만든다. 반환값은 이전 발화라 버린다.
            int currentGeneration = Volatile.Read(ref _listenGeneration);
            if (currentGeneration != generation)
            {
                rec?.FinalResult();
                generation = currentGeneration;
            }

            if (rec == null || !_pcmQueue.TryDequeue(out short[] chunk))
            {
                Thread.Sleep(5);
                continue;
            }

            // 청크는 항상 MinFeedSamples(1600) 단위 — AppendAndEnqueue에서 고정 크기 보장
            bool   isFinal = rec.AcceptWaveform(chunk, chunk.Length);
            string json    = isFinal ? rec.Result() : rec.PartialResult();
            if (!string.IsNullOrEmpty(json))
                _resultQueue.Enqueue(new VoskResult(generation, isFinal, json));
        }

        rec?.Dispose();
    }

    // ── 결과 판정 (메인 스레드) ──────────────────────────────────

    void DrainResultQueue()
    {
        while (_resultQueue.TryDequeue(out VoskResult result))
        {
            // 창 밖이거나 리셋 이전 세대의 결과는 버린다.
            if (!_listening || result.Generation != Volatile.Read(ref _listenGeneration)) continue;

            var node = JSONNode.Parse(result.Json);
            if (node == null) continue;

            string text = node[result.IsFinal ? "text" : "partial"]?.Value;
            bool   hit  = ContainsTeamWord(text);

            if (result.IsFinal)
            {
                _partialHits = 0;
                if (hit) TrySubmit("final", text);
                else if (HasRecognizedWord(text))
                    Debug.Log($"[CheerKeywordEngine] 확정 결과에 TeamCheerWord 없음: '{text}'");
                continue;
            }

            // 같은 추측이 연속으로 유지될 때만 믿는다 — 한 번 튀었다 정정되는 오탐 차단.
            _partialHits = hit ? _partialHits + 1 : 0;
            if (_partialHits >= PartialConfirmHits)
                TrySubmit("partial", text);
        }
    }

    void TrySubmit(string source, string text)
    {
        if (Time.time - _lastSubmitTime < SubmitRetrySec) return;

        var svc = CheerService.Instance;
        if (svc == null) return;

        _lastSubmitTime = Time.time;
        _partialHits    = 0;

        // 같은 발화의 남은 partial/final로 다시 제출하지 않도록 발화 상태를 비운다.
        Interlocked.Increment(ref _listenGeneration);

        Debug.Log($"[CheerKeywordEngine] 키워드 감지 ({source}): '{text}'");
        svc.SubmitTeamCheerServerRpc(isVoice: true);
    }

    static bool ContainsTeamWord(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        string teamWord = CheerService.ResolveTeamCheerWord();
        foreach (string word in text.Split(' '))
            if (word == teamWord) return true;
        return false;
    }

    static bool HasRecognizedWord(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (string word in text.Split(' '))
            if (word.Length > 0 && word != "[unk]") return true;
        return false;
    }

    // ── grammar ───────────────────────────────────────────────────

    /// <summary>
    /// 로컬 grammar를 [TeamCheerWord]로 재적용 (CheerSystemDesign.md §3.4).
    /// 모델 로드 전이면 무시 — InitCoroutine이 같은 헬퍼로 초기 grammar를 만든다.
    /// <see cref="RebuildOwnerLocalGrammar"/>(CheerService.TeamCheerWord NV 변경 경로)로 여러 번
    /// 호출될 수 있으므로 결과가 이전과 같으면(_grammarJson 비교) 워커 리셋을 스킵한다 —
    /// _workerNextModel/_workerNextGrammar는 "Dissonance 오디오 수신 여부" 판단(InitCoroutine)에도
    /// 쓰여 여기서 그 값과 비교하면 안 된다.
    /// </summary>
    public void ApplyOwnerLocalGrammar()
    {
        if (_model == null) return;
        string newJson = CheerLexiconBuilder.BuildGrammarJson(OwnerGrammarWords());
        if (newJson == _grammarJson) return;
        _grammarJson = newJson;
        SignalWorkerReset(_model, newJson);
        Debug.Log($"[CheerKeywordEngine] owner grammar 갱신: {newJson}");
    }

    /// <summary>
    /// 로컬 오너의 CheerKeywordEngine을 찾아 grammar를 재적용 — CheerService의
    /// TeamCheerWord NV 변경 경로(OnNetworkSpawn/HandleTeamCheerWordNv)에서 호출된다.
    /// </summary>
    public static void RebuildOwnerLocalGrammar()
    {
        var all = FindObjectsByType<CheerKeywordEngine>(FindObjectsSortMode.None);
        foreach (var engine in all)
        {
            var netObj = engine.GetComponent<NetworkObject>();
            if (netObj == null || !netObj.IsOwner) continue;
            if (!engine.enabled) return;
            engine.ApplyOwnerLocalGrammar();
            return;
        }
    }

    /// <summary>TeamCheerWord 1개뿐 — 개인 CheerName은 음성 인식 대상이 아니다(2026-09-14).</summary>
    static string[] OwnerGrammarWords() => new[] { CheerService.ResolveTeamCheerWord() };

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
