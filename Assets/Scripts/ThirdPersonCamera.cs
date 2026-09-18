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
public class ThirdPersonCamera : MonoBehaviour
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

    // ── 벽 충돌 회피 ────────────────────────────────────────────────
    [Header("벽 충돌 회피 (SphereCast pull-in)")]
    [Tooltip("카메라가 이 레이어들에 막히면 피벗 쪽으로 당겨진다.\n" +
             "★ 정적 지형만 넣을 것. 특히 Default는 레이어를 지정 안 한 모든 오브젝트가 들어가는 " +
             "쓰레기통이라 절대 넣으면 안 된다 — 파편(FloorTileShards/RubbleShards, Rigidbody 달린 " +
             "비-트리거 MeshCollider)·식도 함정·MagicCircle이 전부 Default라, 이것들이 피벗→카메라 " +
             "구간을 스쳐 지나갈 때마다 카메라가 튄다(2026-09-16 이 기능을 삭제했던 원인).\n" +
             "BoulderStop도 제외 — 플레이어는 통과하는 배리어인데 SphereCast는 물리 매트릭스를 " +
             "무시하고 그냥 맞는다. 기본값은 Ground/Wall/BackGround.")]
    [SerializeField] LayerMask cameraObstructionLayers =
        (1 << 25) | (1 << 27) | (1 << 29); // Ground, Wall, BackGround

    [Tooltip("SphereCast 반지름(m). 근평면 폭 정도로 — 너무 작으면 벽 모서리를 못 걸러 살짝 뚫려 보인다.")]
    [SerializeField] float cameraCollisionRadius = 0.3f;

    [Tooltip("장애물 표면에서 추가로 띄워 두는 여유 거리(m). 0이면 표면에 딱 붙어 z-fighting/근평면 클리핑 위험.")]
    [SerializeField] float cameraCollisionBuffer = 0.15f;

    [Tooltip("벽에 막혀 당겨질 때의 보간 시간(초). 짧게 — 길면 당겨지는 동안 벽 뒤가 보인다.")]
    [SerializeField] float pullInSmoothTime = 0.05f;

    [Tooltip("장애물이 사라져 원래 거리로 돌아갈 때의 보간 시간(초). 당길 때보다 넉넉하게 — " +
             "짧은 오탐이 한두 프레임 생겨도 눈에 안 띄게 만드는 장치다.")]
    [SerializeField] float pullOutSmoothTime = 0.25f;

    [Tooltip("디버그: 막힘이 시작/해제될 때 어떤 콜라이더였는지 콘솔에 찍는다. 원인 추적용, 평소엔 끌 것.")]
    [SerializeField] bool logObstructionHits = false;

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
    bool _snapNextFrame; // SnapToTarget() 요청 — 다음 LateUpdate 1회만 보간 생략

    float _obstructionCut;    // 벽 때문에 desired 거리에서 깎아낸 양(m). 0 = 안 막힘
    float _obstructionCutVel; // 위 값의 SmoothDamp 속도
    bool  _wasObstructed;     // 직전 프레임 막힘 여부 (로그 전이 판정용)

    /// <summary>막혔을 때 피벗에서 유지할 최소 거리(m). 0이면 피벗과 완전히 겹쳐 근평면이 깨진다.</summary>
    const float MinObstructedDistance = 0.05f;

    /// <summary>
    /// SphereCast 시작 구가 이미 콜라이더와 겹치면 Unity는 hit.distance = 0을 돌려준다.
    /// 그걸 그대로 쓰면 카메라가 플레이어 머리 안으로 순간이동하므로 이 값 이하는 "막히지 않음"으로 본다.
    /// </summary>
    const float InitialOverlapEpsilon = 0.01f;

    // ── Public 프로퍼티 ─────────────────────────────────────────────
    public float Yaw => _yaw;
    public float PreviewBlendTime => previewBlendTime;

    /// <summary>프리뷰 중이어도 원래 따라가던 대상(로컬 플레이어)을 돌려준다. 프리뷰 중 target은 pivot이다.</summary>
    public Transform GameplayTarget => _isInPreview && _gameplayTarget != null ? _gameplayTarget : target;

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
            // 옵션 메뉴 마우스 감도 배율 — pull 방식(GameSettingsManager §1과 동일 원칙, push 아님).
            // 미준비/타이틀 부팅 이전 등 Instance가 없을 때는 배율 1.0(원래 Inspector 감도 그대로)로 폴백.
            float sensMul = GameSettingsManager.Instance != null ? GameSettingsManager.Instance.MouseSensitivity : 1f;

            _activeDist     = distance;
            _activeSensX    = sensitivityX * sensMul;
            _activeSensY    = sensitivityY * sensMul;
            _activeOffset   = targetOffset;
            _activePitchMin = minPitch;
            _activePitchMax = maxPitch;
        }

        // 마우스가 필요한 UI(Esc메뉴/이모트메뉴/치어네임패널/채팅 등)가 하나라도 떠서 커서가
        // 풀려있는 동안엔 시점 회전을 멈춘다 — 특정 UI를 여기서 하드코딩해서 체크하지 않고
        // CursorUnlockRequestUtil(커서 해제 요청의 SSOT) 하나만 보면 전부 커버된다
        // (2026-08-22 수정 — 이전엔 InGameChatUI.IsChatOpen만 체크해서 나머지 UI는 안 걸렸음).
        if (!CursorUnlockRequestUtil.IsRequested)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            _yaw   += mouseDelta.x * _activeSensX;
            _pitch  = Mathf.Clamp(_pitch - mouseDelta.y * _activeSensY, _activePitchMin, _activePitchMax);
        }

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

        // 벽 충돌 회피는 여기서 끝난다 — 아래 분기는 이미 보정된 safePos만 쓴다.
        // 특히 텔레포트 스냅도 safePos를 써야 부활 순간 한 프레임 벽 안에서 시작하지 않는다.
        Vector3 safePos = ResolveObstruction(pivot, desiredPos, immediate: _snapNextFrame);

        if (_snapNextFrame)
        {
            // 텔레포트 직후 1프레임: SmoothDamp를 건너뛰어 맵을 가로지르는 비행을 막는다(SnapToTarget 참고).
            _snapNextFrame = false;
            transform.position = safePos;
        }
        else if (positionDamping > 0f)
            transform.position = Vector3.SmoothDamp(transform.position, safePos, ref _posVelocity, positionDamping);
        else
            transform.position = safePos;

        transform.rotation = _currentRot;
    }

    // ── 외부 API ───────────────────────────────────────────────────

    /// <summary>외부에서 Yaw를 강제 설정 (리스폰, 씬 초기화 등)</summary>
    public void SetYaw(float yaw) => _yaw = yaw;

    /// <summary>
    /// 다음 LateUpdate에서 보간 없이 타겟 뒤 정위치로 즉시 이동. 플레이 중 텔레포트 전용
    /// (현재 사용처 = 자동 부활 — ReviveSystemDesign.md §3·§9.3).
    ///
    /// [왜 즉시 대입이 아니라 1프레임 플래그인가] 호출부는 부활 ClientRpc(메시지 처리 레인)라
    /// 여기서 위치를 직접 써도 **같은 프레임의 LateUpdate가 뒤이어 SmoothDamp로 덮어쓴다.**
    /// 카메라 위치의 진실은 LateUpdate 하나이므로 거기서 소비해야 한다.
    ///
    /// [회전은 건드리지 않는다] rotationDamping은 각도 보간이고 텔레포트로 각도는 바뀌지 않는다.
    /// yaw/pitch도 유지 — 부활 순간 시점 방향까지 바뀌면 방향감이 끊긴다.
    ///
    /// [프리뷰 중에는 무동작] 프리뷰/블렌드 중에는 target이 플레이어가 아니라 pivot이라
    /// (BlendToPreview) 스냅하면 탑다운 프레이밍이 튄다. 인트로 도중 사망은 실제로 존재하는
    /// 경로다(ForceGameplayViewImmediate 주석의 2026-09-14 사고). 프리뷰 종료 시
    /// BlendToGameplay가 _posVelocity를 어차피 0으로 리셋하므로 놓치는 것도 없다.
    /// </summary>
    public void SnapToTarget()
    {
        if (_isInPreview) return;
        _snapNextFrame = true;
        _posVelocity   = Vector3.zero;
    }

    /// <summary>
    /// 탑다운 프리뷰 시점으로 부드럽게 전환. Inspector에 지정된 Preview Preset 값을 사용.
    /// pivot: 카메라가 따라볼 Transform — 매 프레임 위치를 다시 읽으므로 고정 지점(Pioneer/Memory)뿐 아니라
    /// 계속 움직이는 Transform(예: 진행축만 추적하는 pivot)을 넘겨도 그대로 따라간다.
    /// </summary>
    public void EnterPreviewView(Transform pivot)
        => EnterPreviewView(pivot, previewDistance, previewPitch, previewYaw, previewTargetOffset);

    /// <summary>
    /// 탑다운 프리뷰 시점으로 부드럽게 전환 — 스테이지별 커스텀 프레이밍 지정.
    /// Inspector의 공용 Preview Preset을 덮어쓰지 않고 이 호출에서만 쓸 distance/pitch/yaw/offset을 넘긴다
    /// (한 씬에 여러 프리뷰 스테이지가 있고 서로 다른 프레이밍이 필요할 때 사용, 예: T.Stage4 MovingCorridor).
    /// </summary>
    public void EnterPreviewView(Transform pivot, float dist, float pitch, float yaw, Vector3 offset)
    {
        if (_blendCoroutine != null) StopCoroutine(_blendCoroutine);
        _blendCoroutine = StartCoroutine(BlendToPreview(pivot, dist, pitch, yaw, offset));
    }

    /// <summary>게임플레이 시점으로 부드럽게 복귀.</summary>
    public void ExitPreviewView()
    {
        if (_blendCoroutine != null) StopCoroutine(_blendCoroutine);
        _blendCoroutine = StartCoroutine(BlendToGameplay());
    }

    /// <summary>
    /// 프리뷰(또는 블렌드) 중이면 블렌드 없이 즉시 게임플레이 시점으로 되돌린다. 프리뷰가 아니면 무동작.
    /// 이 카메라는 DontDestroyOnLoad라 프리뷰 상태(씬 소속 pivot 포함)가 씬 리로드를 넘어 살아남는다 —
    /// 인트로 도중 사망/ESC Reset 리로드 시 새 씬에서 탑다운으로 고착되던 버그(2026-09-14)를
    /// 막기 위해 LocalPlayerCamera.SetTarget(씬마다 1회 re-bind)에서 호출한다.
    /// </summary>
    public void ForceGameplayViewImmediate()
    {
        if (!_isInPreview) return;

        if (_blendCoroutine != null) { StopCoroutine(_blendCoroutine); _blendCoroutine = null; }

        _isInPreview    = false;
        _gameplayTarget = null;
        _posVelocity    = Vector3.zero;

        _pitch          = initialPitch;
        _activeDist     = distance;
        _activeOffset   = targetOffset;
        _activeSensX    = sensitivityX;
        _activeSensY    = sensitivityY;
        _activePitchMin = minPitch;
        _activePitchMax = maxPitch;
        _currentRot     = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    // ── 내부 ──────────────────────────────────────────────────────

    /// <summary>
    /// 피벗 → desiredPos 구간을 SphereCast로 검사해, 장애물에 막히면 그 앞까지 당긴 위치를 돌려준다.
    /// 막히지 않으면 desiredPos 그대로.
    ///
    /// [왜 "거리"가 아니라 "깎아낸 양(_obstructionCut)"을 보간하는가]
    /// 최종 거리를 직접 보간하면 minDistanceWhenLookingUp 같은 정상적인 거리 변화까지 느려진다.
    /// 깎아낸 양만 보간하면 안 막혔을 때 cut = 0으로 수렴해 원래 거리와 정확히 일치한다.
    ///
    /// [왜 positionDamping에 기대지 않는가] 이 보정은 자체 보간(pullIn/pullOutSmoothTime)을 갖는다.
    /// LocalPlayerCamera 프리팹의 positionDamping은 0이라, 예전 구현처럼 거기에 기대면 막힘/풀림이
    /// 양쪽 다 즉시 스냅이 되어 한 프레임짜리 오탐도 그대로 튐으로 보였다(2026-09-16 삭제 원인).
    ///
    /// [프리뷰 제외] 탑다운 프리뷰는 pivot 위 수십 m에서 내려다보는 연출이라 천장·배경에 막혀
    /// 당겨지면 구도가 깨진다.
    /// </summary>
    Vector3 ResolveObstruction(Vector3 pivot, Vector3 desiredPos, bool immediate)
    {
        Vector3 toDesired = desiredPos - pivot;
        float desiredDist = toDesired.magnitude;

        if (_isInPreview || desiredDist < 0.0001f)
        {
            _obstructionCut    = 0f;
            _obstructionCutVel = 0f;
            _wasObstructed     = false;
            return desiredPos;
        }

        Vector3 dir = toDesired / desiredDist;

        bool hitAny = Physics.SphereCast(pivot, cameraCollisionRadius, dir, out RaycastHit hit, desiredDist,
                                         cameraObstructionLayers, QueryTriggerInteraction.Ignore);
        bool blocked = hitAny && hit.distance > InitialOverlapEpsilon;

        float rawCut = blocked ? Mathf.Max(desiredDist - (hit.distance - cameraCollisionBuffer), 0f) : 0f;

        if (logObstructionHits && blocked != _wasObstructed)
        {
            if (blocked)
                Debug.Log($"[ThirdPersonCamera] 막힘 ▶ {hit.collider.name} " +
                          $"(layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}) " +
                          $"hit={hit.distance:F2}m / desired={desiredDist:F2}m pitch={_pitch:F0}", hit.collider);
            else
                Debug.Log($"[ThirdPersonCamera] 막힘 해제 ◀ desired={desiredDist:F2}m pitch={_pitch:F0}");
        }
        _wasObstructed = blocked;

        // 당길 때는 빠르게(한 프레임도 벽 뒤가 보이면 안 됨), 풀릴 때는 느긋하게.
        float smoothTime = rawCut > _obstructionCut ? pullInSmoothTime : pullOutSmoothTime;
        if (immediate || smoothTime <= 0f)
        {
            _obstructionCut    = rawCut;
            _obstructionCutVel = 0f;
        }
        else
        {
            _obstructionCut = Mathf.SmoothDamp(_obstructionCut, rawCut, ref _obstructionCutVel, smoothTime);
            if (Mathf.Abs(_obstructionCut - rawCut) < 0.01f) // 꼬리 제거 — 안 막혔으면 정확히 원래 거리로
            {
                _obstructionCut    = rawCut;
                _obstructionCutVel = 0f;
            }
        }

        float finalDist = Mathf.Max(desiredDist - _obstructionCut, MinObstructedDistance);
        return pivot + dir * finalDist;
    }

    IEnumerator BlendToPreview(Transform pivot, float toDist, float toPitch, float toYaw0, Vector3 toOffset)
    {
        // 이미 프리뷰(또는 복귀 블렌드) 중이면 target이 pivot일 수 있으므로 복귀 대상을 덮어쓰지 않는다.
        if (!_isInPreview) _gameplayTarget = target;
        _isInPreview    = true;
        target          = pivot;
        _posVelocity    = Vector3.zero;

        // pitch/yaw 범위를 preview 값까지 임시 확장 (clamp 방지)
        _activePitchMax = Mathf.Max(maxPitch, toPitch);

        float fromDist  = _activeDist;
        float fromPitch = _pitch;
        float fromYaw   = _yaw;
        Vector3 fromOff = _activeOffset;
        float fromSensX = _activeSensX;
        float fromSensY = _activeSensY;

        // yaw 최단 경로 계산 (예: 350° → 10° 를 +20° 방향으로)
        float yawDelta = Mathf.DeltaAngle(fromYaw, toYaw0);
        float toYaw    = fromYaw + yawDelta;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(previewBlendTime, 0.01f);
            float ease = Mathf.SmoothStep(0f, 1f, t);

            _activeDist   = Mathf.Lerp(fromDist,  toDist,   ease);
            _pitch        = Mathf.Lerp(fromPitch, toPitch,  ease);
            _yaw          = Mathf.Lerp(fromYaw,   toYaw,    ease);
            _activeOffset = Vector3.Lerp(fromOff, toOffset, ease);
            _activeSensX  = Mathf.Lerp(fromSensX, 0f, ease);
            _activeSensY  = Mathf.Lerp(fromSensY, 0f, ease);

            yield return null;
        }

        _activeDist   = toDist;
        _pitch        = toPitch;
        _yaw          = toYaw0;
        _activeOffset = toOffset;
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
