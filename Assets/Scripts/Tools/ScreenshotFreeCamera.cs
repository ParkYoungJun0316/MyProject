using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 스크린샷 촬영용 자유 이동 카메라 (디버그/마케팅 툴 — 게임플레이 로직과 무관).
/// 토글 키로 켜면 그 순간 활성 카메라(LocalPlayerCamera 우선, 없으면 Camera.main)의
/// 위치/회전을 그대로 이어받아 자연스럽게 시점을 넘겨받고, 원래 카메라는 잠시 꺼둔다.
/// 다시 토글하면 원래 카메라가 즉시 복귀한다.
///
/// [사용법]
/// 1) 빈 GameObject에 Camera + ScreenshotFreeCamera 컴포넌트를 붙여 씬에 배치.
/// 2) Play 모드에서 toggleKey(기본 F9)로 자유 카메라 On/Off.
/// 3) 우클릭을 누른 채 마우스로 시점 회전, WASD 이동, Q/E 상하 이동, Shift 가속, 마우스 휠로 이동 속도 조절.
/// 4) Time.timeScale로 게임을 멈춰도(추후 Pause 기능) unscaledDeltaTime 기반이라 카메라는 계속 움직임.
///
/// [주의]
/// - 자유 카메라 켜져 있는 동안 ThirdPersonCamera는 enabled=false로 꺼서 마우스 입력이 겹치지 않게 함.
/// - 순수 디버그 툴. 게임플레이 카메라 SSOT(ThirdPersonCamera)를 대체하지 않고 스왑만 한다.
/// </summary>
[RequireComponent(typeof(Camera))]
public class ScreenshotFreeCamera : MonoBehaviour
{
    [Header("Toggle")]
    [Tooltip("자유 카메라 켜기/끄기 키")]
    [SerializeField] Key toggleKey = Key.F9;

    [Header("Move")]
    [SerializeField] float moveSpeed = 8f;
    [SerializeField] float boostMultiplier = 4f;
    [SerializeField] float minSpeed = 0.5f;
    [SerializeField] float maxSpeed = 100f;
    [SerializeField] float scrollSpeedStep = 2f;

    [Header("Look")]
    [SerializeField] float sensitivityX = 0.15f;
    [SerializeField] float sensitivityY = 0.15f;

    Camera _cam;
    Camera _prevCam;
    ThirdPersonCamera _prevThirdPersonCam;
    float _yaw;
    float _pitch;

    public bool IsActive { get; private set; }

    void Awake()
    {
        _cam = GetComponent<Camera>();
        _cam.enabled = false;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            if (IsActive) Deactivate();
            else Activate();
        }

        if (!IsActive) return;

        HandleLook();
        HandleMove();
    }

    void Activate()
    {
        _prevCam = null;
        _prevThirdPersonCam = null;

        if (LocalPlayerCamera.Instance != null)
        {
            _prevCam = LocalPlayerCamera.Instance.Cam;
            _prevThirdPersonCam = LocalPlayerCamera.Instance.ThirdPersonCam;
        }
        else if (Camera.main != null)
        {
            _prevCam = Camera.main;
        }

        if (_prevCam != null)
        {
            transform.SetPositionAndRotation(_prevCam.transform.position, _prevCam.transform.rotation);
            _prevCam.enabled = false;
        }
        if (_prevThirdPersonCam != null)
            _prevThirdPersonCam.enabled = false;

        Vector3 e = transform.eulerAngles;
        _pitch = e.x > 180f ? e.x - 360f : e.x;
        _yaw = e.y;

        _cam.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        IsActive = true;

        Debug.Log("[ScreenshotFreeCamera] 자유 카메라 활성화");
    }

    void Deactivate()
    {
        _cam.enabled = false;

        if (_prevCam != null) _prevCam.enabled = true;
        if (_prevThirdPersonCam != null) _prevThirdPersonCam.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        IsActive = false;

        Debug.Log("[ScreenshotFreeCamera] 자유 카메라 비활성화 — 원래 카메라로 복귀");
    }

    void HandleLook()
    {
        if (Mouse.current == null || !Mouse.current.rightButton.isPressed) return;

        Vector2 delta = Mouse.current.delta.ReadValue();
        _yaw += delta.x * sensitivityX;
        _pitch = Mathf.Clamp(_pitch - delta.y * sensitivityY, -89f, 89f);

        transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    void HandleMove()
    {
        if (Keyboard.current == null) return;

        Vector3 dir = Vector3.zero;
        if (Keyboard.current.wKey.isPressed) dir += Vector3.forward;
        if (Keyboard.current.sKey.isPressed) dir += Vector3.back;
        if (Keyboard.current.aKey.isPressed) dir += Vector3.left;
        if (Keyboard.current.dKey.isPressed) dir += Vector3.right;
        if (Keyboard.current.eKey.isPressed) dir += Vector3.up;
        if (Keyboard.current.qKey.isPressed) dir += Vector3.down;

        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                moveSpeed = Mathf.Clamp(moveSpeed + Mathf.Sign(scroll) * scrollSpeedStep, minSpeed, maxSpeed);
        }

        float speed = moveSpeed * (Keyboard.current.leftShiftKey.isPressed ? boostMultiplier : 1f);
        transform.position += transform.TransformDirection(dir.normalized) * speed * Time.unscaledDeltaTime;
    }
}
