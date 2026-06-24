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
///   masterVolume 을 Inspector 에서 설정 (0 = 무음, 1 = 최대).
///   기본값 0 → Inspector 에서 반드시 설정할 것.
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

    [Header("볼륨")]
    [Tooltip("전체 SFX 볼륨 배율 (0 ~ 1). 0이면 무음.")]
    [SerializeField] [Range(0f, 1f)] float masterVolume = 0f;

    // masterVolume 이 설정되지 않았을 때(0)를 최대 볼륨으로 처리
    float EffectiveVolume => masterVolume > 0f ? masterVolume : 1f;

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

    // ── 2D 1회 재생 ──────────────────────────────────────────────

    /// <summary>SpatialBlend = 0 (UI / 플레이어 전용 단발음).</summary>
    public void Play(SFXId id)
    {
        if (library == null || source2D == null) return;
        AudioClip clip = library.GetClip(id);
        if (clip == null) return;
        source2D.PlayOneShot(clip, EffectiveVolume);
    }

    // ── 3D 1회 재생 (월드 위치) ───────────────────────────────────

    /// <summary>함정·오브젝트처럼 월드 좌표에서 들려야 할 때 사용.</summary>
    public void Play(SFXId id, Vector3 worldPosition)
    {
        if (library == null) return;
        AudioClip clip = library.GetClip(id);
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, worldPosition, EffectiveVolume);
    }

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
        src.volume        = vol * EffectiveVolume;
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
