using UnityEngine;

/// <summary>
/// 회전 전담 컴포넌트.
/// rb.linearVelocity 방향을 매 프레임 읽어 굴림축을 계산하고 angularVelocity만 설정.
/// 이동(velocity)·데미지·lifetime·speedPhases는 담당하지 않음.
///
/// [함께 쓰는 컴포넌트]
/// - TrapProjectile : 이동 방향, 데미지, lifetime
/// - WaypointMover  : 웨이포인트 경로 이동
/// - ArrowTrap      : 발사 시 속도 주입 (rb.linearVelocity)
/// </summary>
public class SpinRoller : MonoBehaviour
{
    [Tooltip("회전 속도 (rad/s). 0이면 회전 없음")]
    public float spinSpeed = 0f;

    [Header("굴림 사운드")]
    [Tooltip("굴러가는 동안 루프 재생할 SFX. None이면 무음. 볼륨은 SFXLibrary(클립별 보정) × 옵션 메뉴 마스터/SFX 볼륨으로 일괄 결정됨.")]
    [SerializeField] SFXId rollSfxId = SFXId.None;

    [Header("3D 오디오 설정")]
    [Tooltip("0 = 완전 2D, 1 = 완전 3D. 위압감을 위해 0.7~1 권장.")]
    [SerializeField] [Range(0f, 1f)] float rollSpatialBlend = 1f;

    [Tooltip("이 거리(m) 이내에서는 최대 볼륨. 클수록 멀리서도 크게 들림. 위압감 있는 boulder는 15~25 권장.")]
    [SerializeField] float rollMinDistance = 40f;

    [Tooltip("이 거리(m) 밖에서는 완전 무음. 0이면 500으로 처리.")]
    [SerializeField] float rollMaxDistance = 200f;

    [Tooltip("Logarithmic = 기본 로그 감쇠, Linear = 선형 감쇠(균일하게 들림)")]
    [SerializeField] AudioRolloffMode rollRolloffMode = AudioRolloffMode.Logarithmic;

    Rigidbody  rb;
    AudioSource _rollSource;
    bool        _isRolling;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        Vector3 vel = rb.linearVelocity;
        bool moving = vel.sqrMagnitude >= 0.001f;

        if (moving && spinSpeed != 0f)
        {
            Vector3 spinAxis = Vector3.Cross(Vector3.up, vel.normalized).normalized;
            if (spinAxis.sqrMagnitude >= 0.001f)
                rb.angularVelocity = spinAxis * spinSpeed;
        }

        if (!_isRolling) StartRollSound();

        if (_isRolling && _rollSource != null && SFXManager.Instance != null)
            _rollSource.volume = SFXManager.Instance.GetEffectiveVolume(rollSfxId);
    }

    void StartRollSound()
    {
        if (rollSfxId == SFXId.None || SFXManager.Instance == null) return;
        AudioClip clip = SFXManager.Instance.GetClip(rollSfxId);
        if (clip == null) return;

        _rollSource                  = gameObject.AddComponent<AudioSource>();
        _rollSource.clip             = clip;
        _rollSource.loop             = true;
        _rollSource.spatialBlend     = rollSpatialBlend;
        _rollSource.volume           = SFXManager.Instance.GetEffectiveVolume(rollSfxId);
        _rollSource.rolloffMode      = rollRolloffMode;
        _rollSource.minDistance      = rollMinDistance > 0f ? rollMinDistance : 1f;
        _rollSource.maxDistance      = rollMaxDistance > 0f ? rollMaxDistance : 500f;
        _rollSource.playOnAwake      = false;
        _rollSource.Play();
        _isRolling = true;
    }

    void StopRollSound()
    {
        if (_rollSource != null)
        {
            _rollSource.Stop();
            Destroy(_rollSource);
            _rollSource = null;
        }
        _isRolling = false;
    }

    void OnDisable()
    {
        StopRollSound();
    }
}
