using UnityEngine;

// 범용 유도 컴포넌트 — 어떤 투사체에도 부착 가능
// target/speed/turnSpeed/damage/lifetime은 Inspector 또는 코드에서 주입
public class Homing : MonoBehaviour
{
    [Tooltip("추적할 대상 Transform")]
    public Transform target;

    [Tooltip("비행 속도 (m/s)")]
    public float speed = 0;

    [Tooltip("꺾임 각도 (도/초) — 낮을수록 느리게 유도")]
    public float turnSpeed = 0;

    public int damage = 0;

    [Tooltip("0 = 벽 충돌 전까지 유지")]
    public float lifetime = 0;

    Rigidbody rb;

    void Awake() => rb = GetComponent<Rigidbody>();

    // Start에서 lifetime 체크 → Instantiate 후 코드 주입이 반영됨
    void Start()
    {
        if (lifetime > 0f) Destroy(gameObject, lifetime);
    }

    void FixedUpdate()
    {
        if (target == null || speed <= 0f) return;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.001f) return;
        toTarget.Normalize();

        Vector3 newDir = Vector3.RotateTowards(
            transform.forward,
            toTarget,
            turnSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime,
            0f);

        transform.forward = newDir;

        if (rb != null)
            rb.linearVelocity = newDir * speed;
        else
            transform.position += newDir * speed * Time.fixedDeltaTime;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Player p = other.GetComponent<Player>();
            if (p != null) p.TakeDamage(damage, true);
            Destroy(gameObject);
        }
        else if (other.CompareTag("Wall"))
        {
            Destroy(gameObject);
        }
    }
}
