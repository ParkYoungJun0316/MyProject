using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 트레일러/스크린샷 촬영 전용 자유 비행 카메라 (비행 로직만).
/// 씬/프리팹 수정 없이 <see cref="TrailerFlyCameraBootstrap"/>이 런타임에 자동 생성·부착함.
/// 이 컴포넌트를 직접 씬에 배치할 필요 없음 — F9 토글로만 사용.
/// </summary>
public class TrailerFlyCamera : MonoBehaviour
{
    [Header("이동")]
    [Tooltip("기본 이동 속도 (m/s)")]
    [SerializeField] float moveSpeed = 8f;

    [Tooltip("Shift 누를 때 배속")]
    [SerializeField] float fastMultiplier = 3f;

    [Tooltip("마우스 휠로 조절 가능한 속도 범위")]
    [SerializeField] float minSpeed = 1f;
    [SerializeField] float maxSpeed = 60f;

    [Header("시점 회전 (우클릭 드래그)")]
    [SerializeField] float lookSensitivity = 0.15f;

    [Header("부드러움")]
    [Tooltip("이동/회전 스무딩 (0 = 즉시)")]
    [SerializeField] float smoothing = 0.05f;

    float _yaw;
    float _pitch;
    Vector3 _velocity;

    void OnEnable()
    {
        Vector3 e = transform.eulerAngles;
        _yaw = e.y;
        _pitch = e.x;
        _velocity = Vector3.zero;
    }

    void Update()
    {
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        if (mouse == null || keyboard == null) return;

        if (mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            _yaw += delta.x * lookSensitivity;
            _pitch -= delta.y * lookSensitivity;
            _pitch = Mathf.Clamp(_pitch, -89f, 89f);
        }

        Quaternion targetRot = Quaternion.Euler(_pitch, _yaw, 0f);
        transform.rotation = smoothing > 0f
            ? Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime / smoothing)
            : targetRot;

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
            moveSpeed = Mathf.Clamp(moveSpeed + scroll * 0.05f, minSpeed, maxSpeed);

        Vector3 input = Vector3.zero;
        if (keyboard.wKey.isPressed) input += Vector3.forward;
        if (keyboard.sKey.isPressed) input += Vector3.back;
        if (keyboard.aKey.isPressed) input += Vector3.left;
        if (keyboard.dKey.isPressed) input += Vector3.right;
        if (keyboard.eKey.isPressed) input += Vector3.up;
        if (keyboard.qKey.isPressed) input += Vector3.down;

        float speed = moveSpeed * (keyboard.leftShiftKey.isPressed ? fastMultiplier : 1f);
        Vector3 targetVelocity = transform.TransformDirection(input.normalized) * speed;

        _velocity = smoothing > 0f
            ? Vector3.Lerp(_velocity, targetVelocity, Time.deltaTime / smoothing)
            : targetVelocity;

        transform.position += _velocity * Time.deltaTime;
    }
}
