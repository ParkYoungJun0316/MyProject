using UnityEngine;

// 범용 회전·가속 컴포넌트 — 이동 거리에 따라 속도가 증가하며 회전
// 어떤 투사체/오브젝트에도 부착 가능
public class SpinRoller : MonoBehaviour
{
    [Tooltip("이동 방향 (정규화된 벡터)")]
    public Vector3 moveDir;

    [Tooltip("초기 속도 (m/s)")]
    public float initialSpeed = 0;

    [Tooltip("이동 거리 1m당 추가 속도 (m/s)")]
    public float acceleration = 0;

    [Tooltip("회전 배율 — 속도 1일 때 rad/s")]
    public float spinSpeed = 0;

    public int damage = 0;

    [Tooltip("0 = 무제한")]
    public float lifetime = 0;

    Rigidbody rb;
    Vector3 origin;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        origin = transform.position;
    }

    void Start()
    {
        if (lifetime > 0f) Destroy(gameObject, lifetime);
    }

    void FixedUpdate()
    {
        float dist         = Vector3.Distance(transform.position, origin);
        float currentSpeed = initialSpeed + acceleration * dist;

        // moveDir × up = 굴러가는 회전축
        Vector3 spinAxis = Vector3.Cross(moveDir, Vector3.up).normalized;

        if (rb != null)
        {
            rb.linearVelocity  = moveDir * currentSpeed;
            rb.angularVelocity = spinAxis * spinSpeed * currentSpeed;
        }
        else
        {
            transform.position += moveDir * currentSpeed * Time.fixedDeltaTime;
            if (spinAxis.sqrMagnitude > 0.001f)
                transform.Rotate(spinAxis, spinSpeed * currentSpeed * Mathf.Rad2Deg * Time.fixedDeltaTime, Space.World);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        Player p = other.GetComponent<Player>();
        if (p != null) p.TakeDamage(damage, false);
        // 계속 굴러감 (파괴 없음)
    }
}
