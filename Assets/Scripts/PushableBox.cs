using UnityEngine;

/// <summary>
/// 상자 컴포넌트.
/// BoxType.Fixed  : 고정 상자 - 이동 불가
/// BoxType.Movable: 이동 가능 상자 - BoxInteraction이 MovePosition으로 이동 제어
/// Rigidbody의 isKinematic 및 Freeze Rotation은 Inspector에서 직접 설정
/// </summary>
public class PushableBox : MonoBehaviour
{
    public enum BoxType { Fixed, Movable }

    [Header("박스 타입")]
    [Tooltip("Fixed: 고정(이동 불가) / Movable: 이동 가능")]
    public BoxType boxType = BoxType.Movable;

    [Header("Movable 전용 설정")]
    [Tooltip("무게. 높을수록 플레이어 Strength가 많이 필요하고 이동 속도가 느려짐")]
    public float weight = 0f;

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// 목표 위치 방향으로 speed(m/s)로 이동.
    /// MovePosition 사용: 벽·장애물 충돌 감지 유지.
    /// </summary>
    public void MoveTo(Vector3 targetPos, float speed)
    {
        if (boxType != BoxType.Movable || rb == null) return;
        if (speed <= 0f) return;

        Vector3 newPos = Vector3.MoveTowards(rb.position, targetPos, speed * Time.fixedDeltaTime);
        rb.MovePosition(newPos);
    }
}
