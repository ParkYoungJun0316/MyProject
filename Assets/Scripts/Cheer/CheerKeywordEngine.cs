using System;
using System.Collections;
using Dissonance;
using NAudio.Wave;
using Unity.Netcode;
using UnityEngine;
using Vosk;

/// <summary>
/// Dissonance 마이크 스트림을 Vosk에 연결해 CheerName 키워드를 감지한다.
///
/// [마이크 이중 오픈 금지]
/// Microphone.Start 를 직접 호출하지 않는다.
/// DissonanceComms.SubscribeToRecordedAudio 로 Dissonance 캡처 스트림을 탭한다.
///
/// [Owner-only]
/// NetworkPlayerSetup.SetupOwner → enabled = true
/// NetworkPlayerSetup.SetupNonOwner → enabled = false (기본값)
/// enabled 상태에서만 초기화·구독 진행.
///
/// [초기화 순서]
/// 1. DissonanceComms 준비 대기
/// 2. VoskModelLoader.EnsureModel() → 모델 경로
/// 3. Vosk.Model 생성
/// 4. CheerLexiconBuilder.BuildDemoGrammarJson() → grammar
/// 5. SubscribeToRecordedAudio → ResetAudioStream → VoskRecognizer 생성
///
/// [완료 로그]
/// [CheerKeywordEngine] Init OK — grammar=..., subscribed
/// [CheerKeywordEngine] Recognizer ready — sampleRate=16000
/// </summary>
[DisallowMultipleComponent]
public class CheerKeywordEngine : BaseMicrophoneSubscriber
{
    // ── 내부 상태 ─────────────────────────────────────────────────

    Model           _model;
    VoskRecognizer  _recognizer;
    string          _grammarJson;
    bool            _subscribed;

    // ── 생명주기 ──────────────────────────────────────────────────

    void OnEnable()
    {
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
        // DissonanceComms 준비 대기
        DissonanceComms comms = null;
        while (comms == null)
        {
            comms = DissonanceComms.GetSingleton();
            yield return null;
        }

        // 모델 압축 해제 + 경로 확보 (첫 실행 시 수 초 소요)
        string modelPath = VoskModelLoader.EnsureModel();
        if (modelPath == null)
        {
            Debug.LogError("[CheerKeywordEngine] 모델 경로 없음 — 초기화 중단");
            yield break;
        }

        // Vosk 네이티브 로그 최소화
        Vosk.Vosk.SetLogLevel(0);

        // Vosk.Model 생성 (동기 — 수백 ms 소요 가능)
        _model = new Model(modelPath);

        // grammar JSON 빌드
        _grammarJson = CheerLexiconBuilder.BuildDemoGrammarJson();

        // Dissonance 마이크 스트림 구독
        // → ResetAudioStream 이 호출되면 VoskRecognizer 생성
        comms.SubscribeToRecordedAudio(this);
        _subscribed = true;

        Debug.Log($"[CheerKeywordEngine] Init OK — grammar={_grammarJson}, subscribed");
    }

    void Shutdown()
    {
        if (_subscribed)
        {
            var comms = DissonanceComms.GetSingleton();
            comms?.UnsubscribeFromRecordedAudio(this);
            _subscribed = false;
        }

        _recognizer?.Dispose();
        _recognizer = null;

        _model?.Dispose();
        _model = null;

        _grammarJson = null;
    }

    // ── BaseMicrophoneSubscriber 콜백 ──────────────────────────────

    protected override void ResetAudioStream(WaveFormat waveFormat)
    {
        // 포맷 변경 시 recognizer 재생성
        _recognizer?.Dispose();
        _recognizer = null;

        if (_model == null || _grammarJson == null) return;

        _recognizer = new VoskRecognizer(_model, waveFormat.SampleRate, _grammarJson);
        _recognizer.SetWords(false);

        Debug.Log($"[CheerKeywordEngine] Recognizer ready — sampleRate={waveFormat.SampleRate}");
    }

    protected override void ProcessAudio(ArraySegment<float> data)
    {
        if (_recognizer == null) return;

        // BaseMicrophoneSubscriber는 항상 offset=0 으로 전달
        bool finalResult = _recognizer.AcceptWaveform(data.Array, data.Count);

        if (finalResult)
            ParseAndSubmit(_recognizer.Result());
    }

    // ── 결과 파싱 + 응원 제출 ────────────────────────────────────

    void ParseAndSubmit(string resultJson)
    {
        if (string.IsNullOrEmpty(resultJson)) return;

        // {"text": "berry"} 파싱 — grammar 제한으로 텍스트는 단어 1개
        var node = JSONNode.Parse(resultJson);
        string text = node?["text"]?.Value;
        if (string.IsNullOrEmpty(text)) return;

        text = text.Trim().ToLower();

        int colorIndex = CheerService.GetColorIndex(text);
        if (colorIndex < 0) return; // [unk] 또는 매핑 없는 단어

        Debug.Log($"[CheerKeywordEngine] 키워드 감지: '{text}' → colorIndex={colorIndex}");

        if (CheerService.Instance == null) return;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            CheerService.Instance.SubmitCheerServerRpc(colorIndex, isVoice: true);
        else
            CheerService.Instance.SubmitCheerLocal(colorIndex);
    }
}
