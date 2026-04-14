using UnityEngine;

/// <summary>
/// 함정에서 발사/생성된 투사체 공통 컴포넌트 (화살, 돌 등).
/// - Player(스텔스 포함) 충돌 → 데미지 (destroyOnPlayer=true면 파괴)
/// - Wall / Floor(지형지물) 충돌 → 파괴
///
/// [SpinRoller와 함께 쓸 때]
/// SpinRoller가 매 FixedUpdate에서 rb.linearVelocity를 덮어쓰므로
/// TrapProjectile.speed / damage / lifetime 은 0으로 설정하고
/// SpinRoller.initialSpeed / damage / lifetime 을 사용할 것.
/// TrapProjectile은 벽(Wall) 충돌 파괴만 담당.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class TrapProjectile : MonoBehaviour
{
    public enum ProjectileType { Arrow, Boulder }

    [Header("Type")]
    [SerializeField] public ProjectileType type = ProjectileType.Arrow;

    [Header("Stats")]
    [Tooltip("플레이어에게 입히는 데미지")]
    [SerializeField] public int damage = 0;

    [Tooltip("발사 속도 (m/s)")]
    [SerializeField] public float speed = 0f;

    [Tooltip("자동 파괴 시간(초). 0이면 무제한")]
    [SerializeField] public float lifetime = 0f;

    [Header("Direction (발사 함정이 런타임에 덮어씀)")]
    [SerializeField] public Vector3 moveDirection = Vector3.forward;

    [Header("충돌 파괴 설정")]
    [Tooltip("Player와 충돌 시 파괴. 돌굴림은 false — 플레이어를 치고도 계속 굴러감")]
    [SerializeField] private bool destroyOnPlayer = true;

    [Tooltip("Wall 태그 오브젝트와 충돌 시 파괴")]
    [SerializeField] private bool destroyOnWall = true;

    [Tooltip("Floor 태그 오브젝트와 충돌 시 파괴 (돌굴림은 false 권장)")]
    [SerializeField] private bool destroyOnFloor = true;

    [Header("충돌 이펙트")]
    [Tooltip("충돌 파괴 시 스폰할 파티클 프리팹. 충돌 지점 + 법선 방향으로 생성됨.\n" +
             "프리팹의 Particle System > Stop Action을 Destroy로 설정할 것.")]
    [SerializeField] private GameObject hitEffectPrefab = null;

    Rigidbody rb;
    bool      isDestroyed;
    Vector3   _lastHitPoint  = Vector3.zero;
    Vector3   _lastHitNormal = Vector3.up;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        Vector3 dir = moveDirection.sqrMagnitude > 0.001f
            ? moveDirection.normalized
            : transform.forward;

        if (type == ProjectileType.Arrow)
        {
            rb.useGravity = false;
            rb.linearVelocity = dir * speed;
            transform.forward = dir;
        }
        else // Boulder
        {
            rb.useGravity = true;
            rb.linearVelocity = dir * speed;
        }

        if (lifetime > 0f)
            Destroy(gameObject, lifetime);
    }

    // 물리 충돌 (Floor 등 non-trigger 지형)
    void OnCollisionEnter(Collision collision)
    {
        if (isDestroyed) return;
        if (collision.contactCount > 0)
        {
            ContactPoint cp = collision.GetContact(0);
            _lastHitPoint  = cp.point;
            _lastHitNormal = cp.normal;
        }
        HandleContact(collision.gameObject);
    }

    // 트리거 충돌 (Wall 등 trigger 지형, 또는 Player trigger 영역)
    void OnTriggerEnter(Collider other)
    {
        if (isDestroyed) return;
        _lastHitPoint  = transform.position;
        _lastHitNormal = -transform.forward; // 날아오던 방향 반대 = 벽면 법선
        HandleContact(other.gameObject);
    }

    void HandleContact(GameObject other)
    {
        // Player 태그는 레이어(Player/PlayerStealth) 변경 후에도 유지됨
        // → 스텔스 상태여도 데미지 적용
        if (other.CompareTag("Player"))
        {
            if (damage > 0)
            {
                Player p = other.GetComponent<Player>()
                           ?? other.GetComponentInParent<Player>();
                if (p != null) p.TakeDamage(damage, false);
            }
            if (destroyOnPlayer) DestroyProjectile();
            return;
        }

        if (destroyOnWall && other.CompareTag("Wall"))
        {
            DestroyProjectile();
            return;
        }

        if (destroyOnFloor && other.CompareTag("Floor"))
        {
            DestroyProjectile();
        }
    }

    void DestroyProjectile()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        SpawnHitEffect();
        Destroy(gameObject);
    }

    void SpawnHitEffect()
    {
        if (hitEffectPrefab == null) return;
        // 충돌 법선 방향으로 파티클이 튀도록 회전 설정
        Quaternion rot = _lastHitNormal != Vector3.zero
            ? Quaternion.LookRotation(_lastHitNormal)
            : Quaternion.identity;
        Instantiate(hitEffectPrefab, _lastHitPoint, rot);
    }
}
