using System.Collections;
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
/// [필수 컴포넌트]
///  Rigidbody: Is Kinematic = true, Interpolate = Interpolate
///  Collider:  Is Trigger = false
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class WallMover : MonoBehaviour
{
    [Header("이동 설정")]
    [Tooltip("시작 위치(현재 위치)에서 얼마나 어느 방향으로 이동할지 (로컬 오프셋)")]
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

    [Header("이벤트")]
    public UnityEvent OnMoveStarted;
    public UnityEvent OnMoveCompleted;
    public UnityEvent OnReturnStarted;
    public UnityEvent OnReturnCompleted;

    [Header("Runtime (확인용)")]
    [SerializeField] bool _isMoving;
    [SerializeField] bool _isReturning;

    Rigidbody _rb;
    Vector3   _startPos;
    Vector3   _endPos;
    Coroutine _moveCoroutine;

    void Awake()
    {
        _rb             = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _startPos       = transform.position;
        _endPos         = _startPos + transform.TransformDirection(moveOffset);
    }

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>벽 이동 시작. 이미 이동 중이면 무시.</summary>
    public void Activate()
    {
        if (_isMoving || _isReturning) return;
        _moveCoroutine = StartCoroutine(MoveRoutine());
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

    [ContextMenu("테스트: 시작 위치로 리셋")]
    void Debug_Reset() => ResetToStart();

    void OnDrawGizmos()
    {
        Vector3 start = Application.isPlaying ? _startPos : transform.position;
        Vector3 end   = start + transform.TransformDirection(moveOffset);

        Gizmos.color = new Color(1f, 0.4f, 0f, 0.8f);
        Gizmos.DrawLine(start, end);

        Gizmos.color = new Color(1f, 0.4f, 0f, 0.35f);
        Gizmos.DrawWireCube(end, transform.lossyScale);
    }
}
