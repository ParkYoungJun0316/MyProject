using System.Collections;
using UnityEngine;

/// <summary>
/// AdvancingWall 출발 전 경고 연출 전담 컴포넌트.
///
/// [역할 분리]
///  AdvancingWall  → 언제·얼마나 이동하는지
///  이 컴포넌트    → 출발 전 흔들림·사운드 연출
///
/// [사용법]
///  1. 라인 루트(AdvancingWall 이 붙은 오브젝트)에 함께 부착.
///  2. AdvancingWall 인스펙터의 Telegraph 필드에 이 컴포넌트를 연결.
///  3. 각 항목을 Inspector에서 설정.
///
/// [흔들림]
///  Visual Root 에 지정한 자식 Transform만 로컬 좌표로 흔들림.
///  Rigidbody / 물리 위치는 변경하지 않음.
/// </summary>
public class AdvancingWallTelegraph : MonoBehaviour
{
    [Header("경고 시간")]
    [Tooltip("전진 시작 전 경고 구간(초). 0이면 연출 없이 즉시 출발")]
    [SerializeField] float telegraphDuration = 0f;

    [Header("흔들림 (비주얼 전용)")]
    [Tooltip("흔들릴 비주얼 자식 Transform. 비워두면 흔들림 없음")]
    [SerializeField] Transform visualRoot;
    [Tooltip("흔들림 최대 진폭(m)")]
    [SerializeField] float shakeAmplitude = 0f;
    [Tooltip("흔들림 주파수(Hz). 예) 10~15 = 빠르게 잔떨림")]
    [SerializeField] float shakeFrequency = 0f;

    [Header("사운드 (경고 루프 — 3D)")]
    [Tooltip("경고 구간 동안 재생할 SFX. 기본값(Trap_AdvancingWall_Telegraph)은 일반 벽용 — 천장 낙하 등\n" +
             "이 컴포넌트를 재사용하는 다른 트랩은 여기서 다른 SFXId(예: Trap_Ceiling)로 지정할 것.")]
    [SerializeField] SFXId warnSfxId = SFXId.Trap_AdvancingWall_Telegraph;
    [Tooltip("경고 구간(telegraphDuration) 동안 재생되는 루프. 시작~종료에 맞춰 자동으로 켜고 끔.\n0 = 완전 2D, 1 = 완전 3D")]
    [SerializeField] [Range(0f, 1f)] float warnSpatialBlend = 1f;
    [Tooltip("이 거리(m) 이내에서는 최대 볼륨")]
    [SerializeField] float warnMinDistance = 1f;
    [Tooltip("이 거리(m) 밖에서는 완전 무음. 0이면 500으로 처리")]
    [SerializeField] float warnMaxDistance = 0f;
    [SerializeField] AudioRolloffMode warnRolloffMode = AudioRolloffMode.Logarithmic;

    /// <summary>외부(AdvancingWall)에서 대기 시간 계산용으로 읽는 경고 구간(초).</summary>
    public float Duration => telegraphDuration;

    Coroutine   _routine;
    Vector3     _visualOrigin;
    AudioSource _warnLoopSource;

    void Awake()
    {
        _visualOrigin = visualRoot != null ? visualRoot.localPosition : Vector3.zero;
    }

    void Update()
    {
        // 볼륨 실시간 반영(옵션 메뉴 마스터/SFX 슬라이더).
        if (_warnLoopSource != null && _warnLoopSource.isPlaying && SFXManager.Instance != null)
            _warnLoopSource.volume = SFXManager.Instance.GetEffectiveVolume(warnSfxId);
    }

    void OnDisable()
    {
        StopWarnLoop();
    }

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>
    /// 경고 연출 시작 (흔들림 + 경고음).
    /// 완료 대기는 Duration 만큼 WaitForSeconds 후 Cancel() 호출로 마무리.
    /// </summary>
    public void Play()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(TelegraphRoutine());
    }

    /// <summary>연출 중단 + 비주얼 원상복귀. 색상 일치 중단 / Deactivate 시 호출.</summary>
    public void Cancel()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        ResetVisual();
        StopWarnLoop();
    }

    // ── 내부 ────────────────────────────────────────────────────

    IEnumerator TelegraphRoutine()
    {
        StartWarnLoop();

        float elapsed = 0f;
        while (elapsed < telegraphDuration)
        {
            if (visualRoot != null && shakeAmplitude > 0f && shakeFrequency > 0f)
            {
                float ox = Mathf.Sin(elapsed * shakeFrequency * Mathf.PI * 2f) * shakeAmplitude;
                float oy = Mathf.Sin(elapsed * shakeFrequency * Mathf.PI * 2f * 0.73f + 1.1f) * shakeAmplitude * 0.4f;
                visualRoot.localPosition = _visualOrigin + new Vector3(ox, oy, 0f);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        ResetVisual();
        StopWarnLoop();
        _routine = null;
    }

    void ResetVisual()
    {
        if (visualRoot != null)
            visualRoot.localPosition = _visualOrigin;
    }

    // ── 사운드 (경고 루프) ────────────────────────────────────────

    void StartWarnLoop()
    {
        if (_warnLoopSource != null && _warnLoopSource.isPlaying) return;
        if (SFXManager.Instance == null) return;

        AudioClip clip = SFXManager.Instance.GetClip(warnSfxId);
        if (clip == null) return;

        if (_warnLoopSource == null)
        {
            _warnLoopSource              = gameObject.AddComponent<AudioSource>();
            _warnLoopSource.loop         = true;
            _warnLoopSource.playOnAwake  = false;
            _warnLoopSource.spatialBlend = warnSpatialBlend;
            _warnLoopSource.rolloffMode  = warnRolloffMode;
            _warnLoopSource.minDistance  = warnMinDistance > 0f ? warnMinDistance : 1f;
            _warnLoopSource.maxDistance  = warnMaxDistance > 0f ? warnMaxDistance : 500f;
        }

        _warnLoopSource.clip   = clip;
        _warnLoopSource.volume = SFXManager.Instance.GetEffectiveVolume(warnSfxId);
        _warnLoopSource.Play();
    }

    void StopWarnLoop()
    {
        if (_warnLoopSource != null && _warnLoopSource.isPlaying)
            _warnLoopSource.Stop();
    }
}
