using UnityEngine;

/// <summary>
/// 탑다운 카메라. 캐릭터(타겟) 위치만 따라가고, 회전은 고정(항상 수직 아래).
/// 캐릭터 회전/정면은 Player에서 마우스 방향으로 처리.
/// </summary>
public class TopDownCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("따라갈 대상 (Player 등). 비우면 이동 없음.")]
    public Transform target;

    [Header("Position")]
    [SerializeField] float height = 0f;
    [SerializeField] Vector3 flatOffset = Vector3.zero;

    [Header("Look")]
    [Tooltip("true: 수직 아래, false: forwardAngle만큼 기울여서 아래")]
    [SerializeField] bool strictTopDown = true;
    [SerializeField] float forwardAngle = 0f;

    [Header("Smooth (0 = 즉시)")]
    [SerializeField] float positionDamping = 0f;

    Vector3 _velocity;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPos = target.position + flatOffset + Vector3.up * height;

        if (positionDamping > 0f)
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _velocity, positionDamping);
        else
            transform.position = desiredPos;

        // 회전 고정: 항상 바닥을 봄 (카메라 자체는 회전 안 함)
        transform.rotation = strictTopDown
            ? Quaternion.LookRotation(Vector3.down)
            : Quaternion.Euler(forwardAngle, 0f, 0f);
    }
}
