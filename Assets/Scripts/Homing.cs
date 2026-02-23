using UnityEngine;

public class Homing : MonoBehaviour
{
    [Header("Target (런타임에 할당)")]
    public Transform target;

    [HideInInspector] public float speed;
    [HideInInspector] public float turnSpeed;

    Rigidbody rb;
    bool usePhysics;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        usePhysics = rb != null && !rb.isKinematic;
    }

    void FixedUpdate()
    {
        if (speed <= 0f) return;

        Vector3 moveDir = transform.forward;

        if (target != null)
        {
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude > 0.001f)
            {
                toTarget.Normalize();
                float rate = turnSpeed > 0f ? turnSpeed : 90f;
                moveDir = Vector3.RotateTowards(
                    transform.forward,
                    toTarget,
                    rate * Mathf.Deg2Rad * Time.fixedDeltaTime,
                    0f);
            }
        }

        transform.forward = moveDir;

        if (usePhysics)
            rb.linearVelocity = moveDir * speed;
        else
            transform.position += moveDir * speed * Time.fixedDeltaTime;
    }
}
