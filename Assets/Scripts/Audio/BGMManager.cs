using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 배경음악 전담 싱글턴 매니저. DontDestroyOnLoad.
///
/// [씬 배치]
///   최초 씬(0.Title)에 빈 GameObject 생성 → BGMManager 추가.
///   zoneClips 에 씬 이름 접두사별 클립을 등록(예: "M." → 마을 BGM, "T." → 마을2 BGM).
///   같은 접두사 구간을 이동할 때는(M.Stage1 → M.Stage2 등) 트랙을 안 끊고 계속 재생함.
///   접두사가 여러 개 매치되면 가장 긴(구체적인) 접두사가 우선 — 나중에 특정 스테이지 전용 곡을
///   추가하고 싶으면(예: "M.Boss") 배열에 추가만 하면 됨, 코드 수정 불필요.
///
/// [구역당 여러 곡 — 재생목록 순환]
///   ZoneClip.clips 에 곡을 2개 이상 넣으면 순서대로(1→2→1→2…) 자동 순환 재생됨.
///   한 곡이 끝나면 다음 곡으로 크로스페이드 전환. 클립이 1개면 그냥 그 곡을 계속 loop.
///
/// [볼륨]
///   GameSettingsManager.Instance.MasterVolume × BgmVolume 를 매 프레임 읽어서 반영(pull 방식).
///   GameSettingsManager가 없는 씬(예: 격리 테스트)에서는 1(최대)로 폴백.
///
/// [같은 씬 안에서 구간(Phase)별 BGM 전환]
///   PhaseManager.PhaseData.onPhaseEnter(UnityEvent)에 PlayClip(AudioClip)을 연결하면
///   씬 전환 없이도(예: M.Stage2의 OX퀴즈 구간 → 화살함정 구간) 그 시점에 원하는 곡으로
///   크로스페이드 전환됨. 씬 접두사 자동 매칭(PlayForScene)과는 독립적으로 동작 — 충돌 없음.
/// </summary>
public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance { get; private set; }

    [Serializable]
    public class ZoneClip
    {
        [Tooltip("씬 이름이 이 접두사로 시작하면 이 재생목록 사용 (예: \"M.\", \"T.\", \"0.Title\")")]
        public string scenePrefix;
        [Tooltip("2개 이상이면 순서대로 순환 재생(1→2→1→2…), 1개면 그 곡만 계속 loop")]
        public AudioClip[] clips;
    }

    [Header("구역별 BGM")]
    [SerializeField] ZoneClip[] zoneClips;

    [Header("크로스페이드")]
    [Tooltip("트랙 전환 시 페이드 소요 시간(초)")]
    [SerializeField] float fadeDuration = 1.5f;

    AudioSource _sourceA;
    AudioSource _sourceB;
    AudioSource _active;
    AudioSource _inactive;

    string _currentPrefix;
    AudioClip[] _currentPlaylist; // null이면 순환 없음(단일 곡 loop 또는 PlayClip 강제 지정)
    int _playlistIndex;
    Coroutine _fadeCoroutine;

    float EffectiveVolume
    {
        get
        {
            GameSettingsManager settings = GameSettingsManager.Instance;
            return settings != null ? settings.MasterVolume * settings.BgmVolume : 1f;
        }
    }

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _sourceA = CreateSource();
        _sourceB = CreateSource();
        _active   = _sourceA;
        _inactive = _sourceB;
    }

    void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void Start() => PlayForScene(SceneManager.GetActiveScene().name);

    AudioSource CreateSource()
    {
        AudioSource src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake  = false;
        src.loop         = true;
        src.spatialBlend = 0f;
        src.volume       = 0f;
        return src;
    }

    // ── 볼륨 실시간 반영 ─────────────────────────────────────────

    void Update()
    {
        if (_fadeCoroutine != null) return; // 페이드 중엔 코루틴이 볼륨을 제어

        // 재생목록 곡이 자연스럽게 끝났으면(loop=false) 다음 곡으로 순환.
        if (_currentPlaylist != null && _active != null && !_active.isPlaying)
        {
            _playlistIndex = (_playlistIndex + 1) % _currentPlaylist.Length;
            FadeTo(_currentPlaylist[_playlistIndex], false);
            return;
        }

        if (_active != null && _active.isPlaying)
            _active.volume = EffectiveVolume;
    }

    // ── 씬 전환 대응 ─────────────────────────────────────────────

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => PlayForScene(scene.name);

    void PlayForScene(string sceneName)
    {
        ZoneClip zone = ResolveZone(sceneName, out string matchedPrefix);

        if (zone == null)
        {
            // 이 씬엔 지정된 BGM이 없음 — 현재 재생 중이면 페이드아웃.
            if (_currentPrefix != null)
            {
                _currentPrefix = null;
                _currentPlaylist = null;
                FadeTo(null, false);
            }
            return;
        }

        if (matchedPrefix == _currentPrefix && _active.isPlaying) return; // 같은 구역이면 유지

        _currentPrefix = matchedPrefix;
        _currentPlaylist = zone.clips.Length > 1 ? zone.clips : null;
        _playlistIndex = 0;
        FadeTo(zone.clips[0], zone.clips.Length <= 1);
    }

    ZoneClip ResolveZone(string sceneName, out string matchedPrefix)
    {
        matchedPrefix = null;
        ZoneClip best = null;
        int bestLength = -1;

        if (zoneClips == null) return null;

        foreach (ZoneClip zc in zoneClips)
        {
            if (zc == null || string.IsNullOrEmpty(zc.scenePrefix) || zc.clips == null || zc.clips.Length == 0) continue;
            if (!sceneName.StartsWith(zc.scenePrefix, StringComparison.Ordinal)) continue;
            if (zc.scenePrefix.Length <= bestLength) continue;

            best = zc;
            bestLength = zc.scenePrefix.Length;
            matchedPrefix = zc.scenePrefix;
        }

        return best;
    }

    // ── 수동 전환 (같은 씬 안 Phase별 BGM) ───────────────────────

    /// <summary>
    /// 씬 접두사 매칭과 무관하게 지정한 클립으로 강제 크로스페이드.
    /// PhaseManager.PhaseData.onPhaseEnter 에 연결해서 같은 씬 안 구간별 BGM 전환에 사용.
    /// 이미 그 클립이 재생 중이면 무시(불필요한 재시작/끊김 방지).
    /// </summary>
    public void PlayClip(AudioClip clip)
    {
        if (clip == null) return;
        if (_active.clip == clip && _active.isPlaying) return;
        _currentPlaylist = null; // 재생목록 순환 중단, 지정한 곡을 계속 loop
        FadeTo(clip, true);
    }

    // ── 크로스페이드 ─────────────────────────────────────────────

    void FadeTo(AudioClip clip, bool loop)
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(clip, loop));
    }

    IEnumerator FadeRoutine(AudioClip nextClip, bool loop)
    {
        AudioSource fadeOutSrc = _active;
        AudioSource fadeInSrc  = _inactive;

        if (nextClip != null)
        {
            fadeInSrc.clip = nextClip;
            fadeInSrc.loop = loop;
            fadeInSrc.volume = 0f;
            fadeInSrc.Play();
        }

        float startVolume = fadeOutSrc.isPlaying ? fadeOutSrc.volume : 0f;
        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float ratio = fadeDuration > 0f ? Mathf.Clamp01(t / fadeDuration) : 1f;
            float target = EffectiveVolume;

            fadeOutSrc.volume = Mathf.Lerp(startVolume, 0f, ratio);
            if (nextClip != null) fadeInSrc.volume = Mathf.Lerp(0f, target, ratio);

            yield return null;
        }

        fadeOutSrc.Stop();
        fadeOutSrc.volume = 0f;

        if (nextClip != null)
        {
            fadeInSrc.volume = EffectiveVolume;
            _active   = fadeInSrc;
            _inactive = fadeOutSrc;
        }

        _fadeCoroutine = null;
    }
}
