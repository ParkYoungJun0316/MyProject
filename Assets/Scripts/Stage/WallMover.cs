using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 개별 벽 이동 컴포넌트.
///
/// [동작]
///  Activate() 호출 시 현재 위치에서 moveOffset 방향으로 moveDuration 초 동안 이동.
///  Rigidbody.MovePosition 사용 → 물리 충돌 정상 작동 (플레이어 밀어냄·막힘).
///  easeCurve로 가속/감속 커스텀 가능.
///
/// [복귀·반복]
///  returnAfterMove = true : 이동 완료 후 시작 위치로 되돌아옴
///  loop            = true : 왕복을 무한 반복
///  returnDelay          : 끝에 도달 후 복귀 전 대기(초)
///  loopDelay            : 복귀 완료 후 다음 사이클 시작 전 대기(초)
///
/// [시간 경과 난이도]
///  speedPhases    : 시간이 지날수록 이동 속도 배율 상승 (moveDuration 단축)
///  distancePhases : 시간이 지날수록 이동 거리 배율 상승 (moveOffset 크기 증가)
///
/// [필수 컴포넌트]
///  Rigidbody: Is Kinematic = true, Interpolate = Interpolate
///  Collider:  Is Trigger = false
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class WallMover : MonoBehaviour
{
    [Header("이동 설정")]
    [Tooltip("시작 위치에서 이동할 로컬 오프셋. 이 오브젝트의 회전을 따르고 스케일은 무시함.\n" +
             "Y를 올려도 오브젝트가 기울어져 있으면 월드 위가 아니라 로컬 Y로 감.\n" +
             "씬 기즈모(주황 화살표+도착 메시)가 실제 도착 위치다.")]
    public Vector3 moveOffset = Vector3.zero;

    [Tooltip("이동 완료까지 걸리는 시간(초)")]
    public float moveDuration = 0f;

    [Tooltip("이동 곡선. x=시간 진행도(0~1), y=위치 진행도(0~1).\n기본: EaseInOut")]
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("복귀 설정")]
    [Tooltip("true: 이동 완료 후 시작 위치로 되돌아옴")]
    public bool returnAfterMove = false;

    [Tooltip("끝 위치에 도달한 뒤 복귀 시작까지 대기(초)")]
    public float returnDelay = 0f;

    [Tooltip("복귀 소요 시간(초). 0이면 즉시 스냅 복귀")]
    public float returnDuration = 0f;

    [Header("반복 설정")]
    [Tooltip("true: 왕복을 무한 반복 (returnAfterMove도 자동 활성)")]
    public bool loop = false;

    [Tooltip("복귀 완료 후 다음 사이클 시작까지 대기(초)")]
    public float loopDelay = 0f;

    [Header("자동 시작")]
    [Tooltip("true: 씬 시작 시 즉시 Activate() 호출\n" +
             "loop=true와 함께 사용하면 씬 시작부터 계속 왕복 이동")]
    [SerializeField] bool activateOnStart = false;

    [Header("발동 스케줄 (ArrowTrap 방식)")]
    [Tooltip("스케줄 시작 기준으로 이 초에 이동 발동. 예: [5, 13, 20]\n" +
             "비워두면 스케줄 없이 Activate()를 직접 호출해야 함")]
    [SerializeField] float[] moveAtSeconds = new float[0];

    [Tooltip("스케줄 반복 여부")]
    [SerializeField] bool loopSchedule = false;

    [Tooltip("반복 시 한 사이클 길이(초). loopSchedule=true일 때만 사용")]
    [SerializeField] float schedulePeriod = 10f;

    [Tooltip("true: 씬 시작 시 자동으로 스케줄 시작\nfalse: StartSchedule()을 외부에서 직접 호출")]
    [SerializeField] bool scheduleOnStart = true;

    [Header("이벤트")]
    public UnityEvent OnMoveStarted;
    public UnityEvent OnMoveCompleted;
    public UnityEvent OnReturnStarted;
    public UnityEvent OnReturnCompleted;

    bool _isMoving;
    bool _isReturning;

    Rigidbody _rb;
    Vector3   _startPos;
    Vector3   _endPos;
    Coroutine _moveCoroutine;
    Coroutine _scheduleCoroutine;
    float     _scheduleStartTime;

    void Awake()
    {
        _rb             = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _startPos       = transform.position;
        _endPos         = _startPos + transform.TransformDirection(moveOffset);
    }

    void Start()
    {
        if (activateOnStart)
            Activate();
        else if (scheduleOnStart && moveAtSeconds != null && moveAtSeconds.Length > 0)
            StartSchedule();
    }

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>벽 이동 시작. 이미 이동 중이면 무시.</summary>
    public void Activate()
    {
        if (_isMoving || _isReturning) return;
        _moveCoroutine = StartCoroutine(MoveRoutine());
    }

    /// <summary>스케줄 시작. scheduleOnStart=false일 때 외부에서 호출.</summary>
    public void StartSchedule()
    {
        if (_scheduleCoroutine != null) StopCoroutine(_scheduleCoroutine);
        _scheduleStartTime  = Time.time;
        _scheduleCoroutine  = StartCoroutine(ScheduleRoutine());
    }

    /// <summary>스케줄 중단.</summary>
    public void StopSchedule()
    {
        if (_scheduleCoroutine != null)
        {
            StopCoroutine(_scheduleCoroutine);
            _scheduleCoroutine = null;
        }
    }

    /// <summary>벽을 시작 위치로 즉시 복귀 + 루프 중단.</summary>
    public void ResetToStart()
    {
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            _moveCoroutine = null;
        }
        _isMoving    = false;
        _isReturning = false;
        _rb.MovePosition(_startPos);
    }

    // ── 내부 ────────────────────────────────────────────────────

    IEnumerator ScheduleRoutine()
    {
        if (moveAtSeconds == null || moveAtSeconds.Length == 0) yield break;

        var nm = NetworkManager.Singleton;

        // ── 기준 시각 결정 ─────────────────────────────────────────────
        // 스케줄이 실제로 시작된(StartSchedule 호출된) 이 순간을 기준으로 잡는다.
        // [버그 수정 2026-07-21] ArrowTrap과 동일한 이유로 StageStartServerTime 앵커 제거
        // (앞 Phase가 길어지면 moveAtSeconds가 이미 과거가 되어 한 번도 발동 안 하는 버그).
        _scheduleStartTime = nm != null ? (float)nm.ServerTime.Time : Time.time;
        float cycleOffset = 0f;

        do
        {
            foreach (float t in moveAtSeconds)
            {
                float targetTime = _scheduleStartTime + cycleOffset + t;

                if (ScheduleTimeUtil.IsPastEvent(targetTime, nm)) continue;

                while ((float)nm.ServerTime.Time < targetTime)
                    yield return null;

                Activate();
            }

            cycleOffset += schedulePeriod;

        } while (loopSchedule);
    }

    IEnumerator MoveRoutine()
    {
        do
        {
            // ── 전진 ──────────────────────────────────────────
            _isMoving    = true;
            _isReturning = false;
            OnMoveStarted?.Invoke();

            yield return Lerp(_startPos, _endPos, moveDuration);

            _rb.MovePosition(_endPos);
            OnMoveCompleted?.Invoke();

            // ── 복귀 (returnAfterMove 또는 loop이면 수행) ────
            if (returnAfterMove || loop)
            {
                if (returnDelay > 0f)
                    yield return new WaitForSeconds(returnDelay);

                _isMoving    = false;
                _isReturning = true;
                OnReturnStarted?.Invoke();

                if (returnDuration > 0f)
                    yield return Lerp(_endPos, _startPos, returnDuration);

                _rb.MovePosition(_startPos);
                _isReturning = false;
                OnReturnCompleted?.Invoke();

                if (loop && loopDelay > 0f)
                    yield return new WaitForSeconds(loopDelay);
            }

        } while (loop);

        _isMoving    = false;
        _isReturning = false;
    }

    /// <summary>from → to 사이를 duration 초 동안 이동하는 코루틴</summary>
    IEnumerator Lerp(Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = duration > 0f ? elapsed / duration : 1f;
            _rb.MovePosition(Vector3.LerpUnclamped(from, to, easeCurve.Evaluate(t)));
            elapsed += Time.deltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    // ── 에디터 지원 ──────────────────────────────────────────────

    [ContextMenu("테스트: 이동 시작")]
    void Debug_Activate() => Activate();

    [ContextMenu("테스트: 스케줄 시작")]
    void Debug_StartSchedule() => StartSchedule();

    [ContextMenu("테스트: 시작 위치로 리셋")]
    void Debug_Reset() => ResetToStart();

    void OnDrawGizmos()
    {
        if (moveOffset.sqrMagnitude < 0.0001f) return;

        Vector3 start = Application.isPlaying ? _startPos : transform.position;
        // runtime과 동일: TransformDirection (회전만, 스케일 미포함)
        Vector3 worldDelta = transform.TransformDirection(moveOffset);
        Vector3 end = start + worldDelta;
        float mark = Mathf.Clamp(worldDelta.magnitude * 0.04f, 0.8f, 8f);

        // 시작(파랑) → 도착(주황). AdvancingWall / EsophagusSqueeze와 같은 색 관례.
        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.9f);
        Gizmos.DrawWireSphere(start, mark * 0.6f);

        Gizmos.color = new Color(1f, 0.4f, 0f, 0.95f);
        DrawGizmoArrow(start, end, mark);
        Gizmos.DrawWireSphere(end, mark * 0.7f);

        // 도착 위치의 실제 메시/콜라이더 (회전·스케일 유지).
        // 예전 DrawWireCube(end, lossyScale)는 월드축 AABB라 Thron처럼
        // 기울어진 가시와 발동 위치가 전혀 다르게 보였다.
        DrawColliderGhost(end, new Color(1f, 0.4f, 0f, 0.7f));
    }

    void OnDrawGizmosSelected()
    {
        if (moveOffset.sqrMagnitude < 0.0001f) return;

        Vector3 start = Application.isPlaying ? _startPos : transform.position;
        Vector3 worldDelta = transform.TransformDirection(moveOffset);
        Vector3 end = start + worldDelta;

        DrawColliderGhost(start, new Color(0.2f, 0.7f, 1f, 0.35f));

#if UNITY_EDITOR
        UnityEditor.Handles.color = new Color(1f, 0.75f, 0.2f, 1f);
        UnityEditor.Handles.Label(
            end + Vector3.up * 0.4f,
            $"moveOffset {moveOffset}\nworld {worldDelta.magnitude:0.##}m");
#endif
    }

    void DrawColliderGhost(Vector3 position, Color color)
    {
        Gizmos.color = color;

        Mesh mesh = null;
        var meshCol = GetComponent<MeshCollider>();
        if (meshCol != null) mesh = meshCol.sharedMesh;
        if (mesh == null)
        {
            var mf = GetComponent<MeshFilter>();
            if (mf != null) mesh = mf.sharedMesh;
        }

        if (mesh != null)
        {
            Gizmos.DrawWireMesh(mesh, position, transform.rotation, transform.lossyScale);
            return;
        }

        var box = GetComponent<BoxCollider>();
        if (box == null) return;

        Matrix4x4 prev = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(position, transform.rotation, transform.lossyScale);
        Gizmos.DrawWireCube(box.center, box.size);
        Gizmos.matrix = prev;
    }

    static void DrawGizmoArrow(Vector3 from, Vector3 to, float headSize)
    {
        Vector3 dir = to - from;
        float mag = dir.magnitude;
        if (mag < 0.001f) return;
        dir /= mag;

        Gizmos.DrawLine(from, to);

        Vector3 right = Vector3.Cross(dir, Vector3.up);
        if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(dir, Vector3.right);
        right.Normalize();
        Vector3 up = Vector3.Cross(right, dir);

        float h = Mathf.Min(headSize, mag * 0.2f);
        Vector3 back = to - dir * h;
        Gizmos.DrawLine(to, back + right * h * 0.35f);
        Gizmos.DrawLine(to, back - right * h * 0.35f);
        Gizmos.DrawLine(to, back + up * h * 0.35f);
        Gizmos.DrawLine(to, back - up * h * 0.35f);
    }
}
