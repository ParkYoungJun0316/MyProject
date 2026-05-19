using System.Collections;
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
///
/// [Preview 전환 API]
/// EnterPreviewView(pivot) : 탑다운 시점으로 부드럽게 전환, pivot 고정
/// ExitPreviewView()       : 게임플레이 시점으로 복귀
/// </summary>
public class TopDownCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("따라갈 대상 (Player 등). 비우면 이동 없음.")]
    public Transform target;

    [Header("Camera Distance & Offset")]
    [Tooltip("카메라가 타겟으로부터 떨어진 거리")]
    [SerializeField] float distance = 0f;

    [Tooltip("천장(위) 방향을 볼 때 줄어드는 최소 거리. 0이면 distance 그대로 유지")]
    [SerializeField] float minDistanceWhenLookingUp = 0f;

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

    // ── Preview Preset ──────────────────────────────────────────────
    [Header("Preview Preset (Inspector에서 직접 지정)")]
    [Tooltip("탑다운 프리뷰 시 카메라 거리. 경로 발판 전체가 화면에 들어오도록 조정.")]
    [SerializeField] float previewDistance = 40f;

    [Tooltip("탑다운 프리뷰 시 pitch 각도(도). 85° 권장 — 90°에 너무 가까우면 이동 방향 계산이 불안정해짐.")]
    [SerializeField] float previewPitch = 82f;

    [Tooltip("탑다운 프리뷰 시 yaw(좌우) 각도(도). 경로 정면 방향으로 고정. 0 = 월드 북쪽 정면.")]
    [SerializeField] float previewYaw = 0f;

    [Tooltip("프리뷰 pivot 기준 추가 오프셋. 경로 중심을 화면 가운데에 맞추려면 Y값 조정.")]
    [SerializeField] Vector3 previewTargetOffset = Vector3.zero;

    [Tooltip("게임플레이 ↔ 프리뷰 시점 전환에 걸리는 시간(초). 1.5~2 권장.")]
    [SerializeField] float previewBlendTime = 1.5f;

    // ── Runtime ─────────────────────────────────────────────────────
    float _yaw;
    float _pitch;
    Vector3 _posVelocity;
    Quaternion _currentRot;

    // LateUpdate에서 실제로 사용하는 active 값 (blend 중 보간됨)
    float _activeDist;
    float _activeSensX;
    float _activeSensY;
    Vector3 _activeOffset;
    float _activePitchMin;
    float _activePitchMax;

    Transform _gameplayTarget; // 게임플레이 follow 대상 저장용
    Coroutine _blendCoroutine;
    bool _isInPreview; // 프리뷰(또는 블렌드) 진행 중 여부

    // ── Public 프로퍼티 ─────────────────────────────────────────────
    public float Yaw => _yaw;
    public float PreviewBlendTime => previewBlendTime;

    // ── Unity 라이프사이클 ──────────────────────────────────────────

    void Start()
    {
        _yaw   = transform.eulerAngles.y;
        _pitch = initialPitch;
        _currentRot = Quaternion.Euler(_pitch, _yaw, 0f);

        _activeDist    = distance;
        _activeSensX   = sensitivityX;
        _activeSensY   = sensitivityY;
        _activeOffset  = targetOffset;
        _activePitchMin = minPitch;
        _activePitchMax = maxPitch;

        _gameplayTarget = target;

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 게임플레이 중(프리뷰/블렌드 아닐 때)에는 Inspector 값을 _active*에 실시간 반영
        if (!_isInPreview)
        {
            _activeDist     = distance;
            _activeSensX    = sensitivityX;
            _activeSensY    = sensitivityY;
            _activeOffset   = targetOffset;
            _activePitchMin = minPitch;
            _activePitchMax = maxPitch;
        }

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        _yaw   += mouseDelta.x * _activeSensX;
        _pitch  = Mathf.Clamp(_pitch - mouseDelta.y * _activeSensY, _activePitchMin, _activePitchMax);

        Quaternion targetRot = Quaternion.Euler(_pitch, _yaw, 0f);

        if (rotationDamping > 0f)
            _currentRot = Quaternion.Slerp(_currentRot, targetRot, Time.deltaTime / rotationDamping);
        else
            _currentRot = targetRot;

        float currentDistance = _activeDist;
        if (minDistanceWhenLookingUp > 0f && _pitch < 0f)
        {
            float t = Mathf.Clamp01(-_pitch / Mathf.Abs(_activePitchMin < 0f ? _activePitchMin : -1f));
            currentDistance = Mathf.Lerp(_activeDist, minDistanceWhenLookingUp, t);
        }

        Vector3 pivot    = target.position + _activeOffset;
        Vector3 desiredPos = pivot + _currentRot * (Vector3.back * currentDistance);

        if (positionDamping > 0f)
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _posVelocity, positionDamping);
        else
            transform.position = desiredPos;

        transform.rotation = _currentRot;
    }

    // ── 외부 API ───────────────────────────────────────────────────

    /// <summary>외부에서 Yaw를 강제 설정 (리스폰, 씬 초기화 등)</summary>
    public void SetYaw(float yaw) => _yaw = yaw;

    /// <summary>
    /// 탑다운 프리뷰 시점으로 부드럽게 전환.
    /// pivot: 경로 중앙 Transform — 카메라가 이 지점을 고정으로 바라봄.
    /// </summary>
    public void EnterPreviewView(Transform pivot)
    {
        if (_blendCoroutine != null) StopCoroutine(_blendCoroutine);
        _blendCoroutine = StartCoroutine(BlendToPreview(pivot));
    }

    /// <summary>게임플레이 시점으로 부드럽게 복귀.</summary>
    public void ExitPreviewView()
    {
        if (_blendCoroutine != null) StopCoroutine(_blendCoroutine);
        _blendCoroutine = StartCoroutine(BlendToGameplay());
    }

    // ── 내부 ──────────────────────────────────────────────────────

    IEnumerator BlendToPreview(Transform pivot)
    {
        _isInPreview    = true;
        _gameplayTarget = target;
        target          = pivot;
        _posVelocity    = Vector3.zero;

        // pitch/yaw 범위를 preview 값까지 임시 확장 (clamp 방지)
        _activePitchMax = Mathf.Max(maxPitch, previewPitch);

        float fromDist  = _activeDist;
        float fromPitch = _pitch;
        float fromYaw   = _yaw;
        Vector3 fromOff = _activeOffset;
        float fromSensX = _activeSensX;
        float fromSensY = _activeSensY;

        // yaw 최단 경로 계산 (예: 350° → 10° 를 +20° 방향으로)
        float yawDelta = Mathf.DeltaAngle(fromYaw, previewYaw);
        float toYaw    = fromYaw + yawDelta;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(previewBlendTime, 0.01f);
            float ease = Mathf.SmoothStep(0f, 1f, t);

            _activeDist   = Mathf.Lerp(fromDist,  previewDistance,     ease);
            _pitch        = Mathf.Lerp(fromPitch, previewPitch,        ease);
            _yaw          = Mathf.Lerp(fromYaw,   toYaw,              ease);
            _activeOffset = Vector3.Lerp(fromOff, previewTargetOffset, ease);
            _activeSensX  = Mathf.Lerp(fromSensX, 0f, ease);
            _activeSensY  = Mathf.Lerp(fromSensY, 0f, ease);

            yield return null;
        }

        _activeDist   = previewDistance;
        _pitch        = previewPitch;
        _yaw          = previewYaw;
        _activeOffset = previewTargetOffset;
        _activeSensX  = 0f;
        _activeSensY  = 0f;
        _blendCoroutine = null;
    }

    IEnumerator BlendToGameplay()
    {
        // 즉시 gameplay follow 대상으로 복귀하고 damping이 위치를 부드럽게 처리
        if (_gameplayTarget != null) target = _gameplayTarget;
        _posVelocity = Vector3.zero;

        float fromDist  = _activeDist;
        float fromPitch = _pitch;
        Vector3 fromOff = _activeOffset;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(previewBlendTime, 0.01f);
            float ease = Mathf.SmoothStep(0f, 1f, t);

            _activeDist   = Mathf.Lerp(fromDist,  distance,      ease);
            _pitch        = Mathf.Lerp(fromPitch, initialPitch,  ease);
            _activeOffset = Vector3.Lerp(fromOff, targetOffset,  ease);
            _activeSensX  = Mathf.Lerp(0f, sensitivityX, ease);
            _activeSensY  = Mathf.Lerp(0f, sensitivityY, ease);

            yield return null;
        }

        _activeDist     = distance;
        _pitch          = initialPitch;
        _activeOffset   = targetOffset;
        _activeSensX    = sensitivityX;
        _activeSensY    = sensitivityY;
        _activePitchMin = minPitch;
        _activePitchMax = maxPitch;
        _isInPreview    = false;
        _blendCoroutine = null;
    }

    // ── 에디터 테스트 ──────────────────────────────────────────────

    [ContextMenu("테스트: 프리뷰 시점으로 전환")]
    void Debug_EnterPreview() => EnterPreviewView(target);

    [ContextMenu("테스트: 게임플레이 시점으로 복귀")]
    void Debug_ExitPreview() => ExitPreviewView();
}
