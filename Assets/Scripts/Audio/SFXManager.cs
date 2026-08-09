using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 효과음 전담 싱글턴 매니저. DontDestroyOnLoad.
///
/// [씬 배치]
///   최초 씬(예: 0.Title 또는 M.Stage1)에 빈 GameObject 생성 → SFXManager 추가.
///   library 필드에 SFXLibrary 에셋 연결.
///   source2D 는 비워 두면 자동 생성됨.
///
/// [재생 방법]
///   2D 1회 : SFXManager.Instance.Play(SFXId.Player_Hit);
///   3D 1회 : SFXManager.Instance.Play(SFXId.Breakable_Destroy, transform.position);
///   루프   : AudioSource src = SFXManager.Instance.PlayLoop(SFXId.Boulder_Roll);
///            ...
///            SFXManager.Instance.StopLoop(src);
///
/// [볼륨]
///   GameSettingsManager.Instance가 있으면 MasterVolume × SfxVolume 를 우선 사용(옵션 메뉴 연동).
///   없으면(격리 테스트 등) Inspector의 masterVolume 필드로 폴백 (0 = 미설정 취급 → 1로 처리).
/// </summary>
public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance { get; private set; }

    [Header("Library")]
    [Tooltip("SFXLibrary 에셋 연결")]
    [SerializeField] SFXLibrary library;

    [Header("2D Source (UI / Player 단발음)")]
    [Tooltip("비워두면 자동 생성. PlayOnAwake = false, SpatialBlend = 0 자동 설정")]
    [SerializeField] AudioSource source2D;

    [Header("볼륨 (GameSettingsManager 없을 때 폴백)")]
    [Tooltip("전체 SFX 볼륨 배율 (0 ~ 1). 0이면 무음.")]
    [SerializeField] [Range(0f, 1f)] float masterVolume = 0f;

    /// <summary>
    /// 현재 SFX 유효 볼륨. GameSettingsManager가 있으면 그쪽 값 우선(옵션 메뉴 실시간 반영),
    /// 없으면 Inspector의 masterVolume 폴백(0이면 1로 처리 — 기존 동작 유지).
    /// 외부(PlayerAudio 등)가 자체 AudioSource 볼륨에 곱해 쓸 때도 이 프로퍼티를 사용할 것.
    /// </summary>
    public float EffectiveVolume
    {
        get
        {
            GameSettingsManager settings = GameSettingsManager.Instance;
            if (settings != null) return settings.MasterVolume * settings.SfxVolume;
            return masterVolume > 0f ? masterVolume : 1f;
        }
    }

    readonly Dictionary<int, bool> _alternateUseSecond = new Dictionary<int, bool>();

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

        if (source2D == null)
        {
            source2D = gameObject.AddComponent<AudioSource>();
        }
        source2D.playOnAwake   = false;
        source2D.spatialBlend  = 0f;
        source2D.loop          = false;
    }

    // ── 라이브러리 참조 ───────────────────────────────────────────

    /// <summary>외부 컴포넌트(PlayerAudio 등)가 직접 클립을 꺼낼 때 사용.</summary>
    public AudioClip GetClip(SFXId id)
    {
        return library != null ? library.GetClip(id) : null;
    }

    /// <summary>
    /// 외부 컴포넌트가 자체 AudioSource(3D 세팅 등)로 재생할 때 곱해야 할 최종 볼륨.
    /// 클립별 보정(SFXLibrary.VolumeOverride) × EffectiveVolume(마스터 × SFX).
    /// SpinRoller 등 PlayLoop()의 2D 고정 세팅을 못 쓰는 루프 사운드가 사용.
    /// </summary>
    public float GetEffectiveVolume(SFXId id)
    {
        float multiplier = library != null ? library.GetVolumeMultiplier(id) : 1f;
        return EffectiveVolume * multiplier;
    }

    // ── 2D 1회 재생 ──────────────────────────────────────────────

    /// <summary>SpatialBlend = 0 (UI / 플레이어 전용 단발음).</summary>
    public void Play(SFXId id)
    {
        if (library == null || source2D == null) return;
        AudioClip clip = library.GetClip(id);
        if (clip == null) return;
        source2D.PlayOneShot(clip, EffectiveVolume * library.GetVolumeMultiplier(id));
    }

    // ── 3D 1회 재생 (월드 위치) ───────────────────────────────────

    /// <summary>함정·오브젝트처럼 월드 좌표에서 들려야 할 때 사용.</summary>
    public void Play(SFXId id, Vector3 worldPosition)
    {
        if (library == null) return;
        AudioClip clip = library.GetClip(id);
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, worldPosition, EffectiveVolume * library.GetVolumeMultiplier(id));
    }

    // ── 1/2 교차 재생 ─────────────────────────────────────────────

    static int AlternateKey(SFXId a, SFXId b) => ((int)a * 397) ^ (int)b;

    /// <summary>a ↔ b 를 호출마다 번갈아 2D 재생.</summary>
    public void PlayAlternating(SFXId a, SFXId b)
    {
        int key = AlternateKey(a, b);
        bool useSecond = _alternateUseSecond.TryGetValue(key, out bool v) && v;
        Play(useSecond ? b : a);
        _alternateUseSecond[key] = !useSecond;
    }

    /// <summary>a ↔ b 를 호출마다 번갈아 3D 재생.</summary>
    public void PlayAlternating(SFXId a, SFXId b, Vector3 worldPosition)
    {
        int key = AlternateKey(a, b);
        bool useSecond = _alternateUseSecond.TryGetValue(key, out bool v) && v;
        Play(useSecond ? b : a, worldPosition);
        _alternateUseSecond[key] = !useSecond;
    }

    public void PlayMouthTeethBreak(Vector3 worldPosition) =>
        Play(SFXId.Mouth_TeethBreak_1, worldPosition);

    public void PlayTrapArrow(Vector3 worldPosition) =>
        PlayAlternating(SFXId.Trap_Arrow_1, SFXId.Trap_Arrow_2, worldPosition);

    // ── 루프 재생 ────────────────────────────────────────────────

    /// <summary>
    /// 루프 사운드를 새 AudioSource 에 재생하고 반환.
    /// 호출자가 StopLoop() 를 직접 호출해서 정리해야 함.
    /// Boulder_Roll / Stage5_Chaser_Run 등 주로 사용.
    /// </summary>
    /// <param name="volume">개별 볼륨 배율 (0~1). masterVolume 과 곱해짐.</param>
    public AudioSource PlayLoop(SFXId id, float volume = 0f)
    {
        if (library == null) return null;
        AudioClip clip = library.GetClip(id);
        if (clip == null) return null;

        float vol = volume > 0f ? volume : 1f;

        GameObject go = new GameObject($"SFXLoop_{id}");
        DontDestroyOnLoad(go);

        AudioSource src = go.AddComponent<AudioSource>();
        src.clip          = clip;
        src.loop          = true;
        src.spatialBlend  = 0f;
        src.playOnAwake   = false;
        src.volume        = vol * EffectiveVolume * library.GetVolumeMultiplier(id);
        src.Play();
        return src;
    }

    /// <summary>PlayLoop 로 얻은 AudioSource 를 정지하고 오브젝트를 제거.</summary>
    public void StopLoop(AudioSource src)
    {
        if (src == null) return;
        src.Stop();
        Destroy(src.gameObject);
    }
}
