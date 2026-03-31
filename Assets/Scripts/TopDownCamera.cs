using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 폴가이즈 스타일 3인칭 추적 카메라.
/// 마우스 Delta X/Y로 Yaw(좌우)/Pitch(상하) 회전.
/// 캐릭터 뒤쪽 위에서 일정 거리를 유지하며 추적.
///
/// [폴가이즈 느낌 세팅 예시]
/// distance        = 15~20
/// initialPitch    = 40~55  (위에서 내려다보는 각도)
/// minPitch        = 10     (최대한 수평 시점)
/// maxPitch        = 80     (최대한 수직 아래 시점)
/// sensitivityX    = 0.15
/// sensitivityY    = 0.10
/// positionDamping = 0.1
/// </summary>
public class TopDownCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("따라갈 대상 (Player 등). 비우면 이동 없음.")]
    public Transform target;

    [Header("Camera Distance & Offset")]
    [Tooltip("카메라가 타겟으로부터 떨어진 거리")]
    [SerializeField] float distance = 0f;

    [Tooltip("타겟 기준 추가 오프셋 (월드 좌표). Y값으로 카메라 기준점 높이 조정 가능")]
    [SerializeField] Vector3 targetOffset = Vector3.zero;

    [Header("Pitch (상하 각도)")]
    [Tooltip("Pitch 초기값 (도). 0=수평, 90=수직 아래. 폴가이즈 느낌: 40~55")]
    [SerializeField] float initialPitch = 0f;

    [Tooltip("최소 Pitch 한계 (도). 위쪽 시점 한계. 예: 5~15")]
    [SerializeField] float minPitch = 0f;

    [Tooltip("최대 Pitch 한계 (도). 아래쪽 시점 한계. 예: 70~85")]
    [SerializeField] float maxPitch = 0f;

    [Header("Mouse Sensitivity")]
    [Tooltip("마우스 좌우 감도. 폴가이즈 느낌: 0.1~0.2")]
    [SerializeField] float sensitivityX = 0f;

    [Tooltip("마우스 상하 감도. 폴가이즈 느낌: 0.08~0.15")]
    [SerializeField] float sensitivityY = 0f;

    [Header("Smooth (0 = 즉시)")]
    [Tooltip("위치 스무딩 딜레이(초). 폴가이즈 느낌: 0.05~0.15")]
    [SerializeField] float positionDamping = 0f;

    [Tooltip("회전 스무딩 딜레이(초). 0이면 즉시 반영")]
    [SerializeField] float rotationDamping = 0f;

    [Header("Cursor")]
    [Tooltip("게임 시작 시 커서를 화면 중앙에 고정. 마우스 델타 입력에 필수")]
    [SerializeField] bool lockCursor = true;

    float _yaw;
    float _pitch;
    Vector3 _posVelocity;
    Quaternion _currentRot;

    void Start()
    {
        // 현재 카메라 방향에서 초기 Yaw 추출, Pitch는 initialPitch 사용
        _yaw = transform.eulerAngles.y;
        _pitch = initialPitch;
        _currentRot = Quaternion.Euler(_pitch, _yaw, 0f);

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        _yaw   += mouseDelta.x * sensitivityX;
        _pitch  = Mathf.Clamp(_pitch - mouseDelta.y * sensitivityY, minPitch, maxPitch);

        Quaternion targetRot = Quaternion.Euler(_pitch, _yaw, 0f);

        if (rotationDamping > 0f)
            _currentRot = Quaternion.Slerp(_currentRot, targetRot, Time.deltaTime / rotationDamping);
        else
            _currentRot = targetRot;

        Vector3 pivot = target.position + targetOffset;
        Vector3 desiredPos = pivot + _currentRot * (Vector3.back * distance);

        if (positionDamping > 0f)
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _posVelocity, positionDamping);
        else
            transform.position = desiredPos;

        transform.rotation = _currentRot;
    }

    /// <summary>외부에서 Yaw를 강제 설정 (리스폰, 씬 초기화 등)</summary>
    public void SetYaw(float yaw) => _yaw = yaw;

    /// <summary>현재 카메라 Yaw (도)</summary>
    public float Yaw => _yaw;
}
