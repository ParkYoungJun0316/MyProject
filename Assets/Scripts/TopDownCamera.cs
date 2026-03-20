using UnityEngine;

/// <summary>
/// 고정 각도 추적 카메라.
/// 캐릭터 위치만 따라가고, 회전은 항상 고정 (캐릭터 회전과 무관).
///
/// [폴가이즈 스타일 세팅 예시]
/// strictTopDown = false
/// pitchAngle  = 55  (상하 기울기. 클수록 더 위에서 내려다봄)
/// yawAngle    = 45  (좌우 꺾기. 0 = 정면, 45 = 대각선)
/// distance    = 20  (카메라가 타겟으로부터 떨어진 거리)
/// </summary>
public class TopDownCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("따라갈 대상 (Player 등). 비우면 이동 없음.")]
    public Transform target;

    [Header("Look Angle")]
    [Tooltip("true: 수직 아래 고정 / false: 아래 두 각도 값 사용")]
    [SerializeField] bool strictTopDown = true;

    [Tooltip("상하 기울기 (도). 0 = 수평, 90 = 수직 아래. 폴가이즈 느낌: 50~60")]
    [SerializeField] float pitchAngle = 55f;

    [Tooltip("좌우 꺾기 (도). 0 = 정면, 45 = 대각선. 아이소메트릭 느낌: 45")]
    [SerializeField] float yawAngle = 0f;

    [Header("Distance")]
    [Tooltip("카메라가 타겟으로부터 떨어지는 거리. pitchAngle/yawAngle 방향의 반대로 배치됨")]
    [SerializeField] float distance = 20f;

    [Tooltip("타겟 기준 추가 오프셋 (월드 좌표)")]
    [SerializeField] Vector3 targetOffset = Vector3.zero;

    [Header("Smooth (0 = 즉시)")]
    [SerializeField] float positionDamping = 0f;

    Vector3 _velocity;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 pivot = target.position + targetOffset;

        if (strictTopDown)
        {
            transform.position = pivot + Vector3.up * distance;
            transform.rotation = Quaternion.LookRotation(Vector3.down);
            return;
        }

        // pitch/yaw로 카메라가 바라보는 방향 계산
        Quaternion rot = Quaternion.Euler(pitchAngle, yawAngle, 0f);

        // 카메라는 바라보는 방향의 반대로 distance만큼 이동해서 배치
        Vector3 desiredPos = pivot + rot * (Vector3.back * distance);

        if (positionDamping > 0f)
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _velocity, positionDamping);
        else
            transform.position = desiredPos;

        transform.rotation = rot;
    }
}
