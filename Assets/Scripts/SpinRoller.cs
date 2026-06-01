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

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (rb == null || spinSpeed == 0f) return;

        Vector3 vel = rb.linearVelocity;
        if (vel.sqrMagnitude < 0.001f) return;

        Vector3 spinAxis = Vector3.Cross(vel.normalized, Vector3.up).normalized;
        if (spinAxis.sqrMagnitude < 0.001f) return;

        rb.angularVelocity = spinAxis * spinSpeed;
    }
}
