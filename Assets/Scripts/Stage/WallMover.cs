using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 개별 벽 이동 컴포넌트.
///
/// [동작]
///  Activate() 호출 시 현재 위치에서 moveOffset 방향으로 moveDuration 초 동안 이동.
///  easeCurve로 가속/감속 커스텀 가능.
///  ResetToStart()로 초기 위치로 즉시 복귀.
///
/// [사용법]
///  1. 벽 GameObject에 이 컴포넌트 추가
///  2. moveOffset: 이동 방향과 거리 (예: (5, 0, 0) = 오른쪽으로 5m)
///  3. moveDuration: 이동 소요 시간(초)
///  4. WallMoverSequencer 또는 PhaseManager UnityEvent에서 Activate() 호출
/// </summary>
public class WallMover : MonoBehaviour
{
    [Header("이동 설정")]
    [Tooltip("시작 위치(현재 위치)에서 얼마나 어느 방향으로 이동할지 (로컬 오프셋)")]
    public Vector3 moveOffset = Vector3.zero;

    [Tooltip("이동 완료까지 걸리는 시간(초)")]
    public float moveDuration = 0f;

    [Tooltip("이동 곡선. x=시간 진행도(0~1), y=위치 진행도(0~1).\n" +
             "기본: EaseInOut (처음·끝 부드럽게)")]
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("이벤트")]
    [Tooltip("이동 시작 시 호출")]
    public UnityEvent OnMoveStarted;

    [Tooltip("이동 완료 시 호출")]
    public UnityEvent OnMoveCompleted;

    [Header("Runtime (확인용)")]
    [SerializeField] bool _isMoving;

    Vector3    _startPos;
    Vector3    _endPos;
    Coroutine  _moveCoroutine;

    void Awake()
    {
        _startPos = transform.position;
        _endPos   = _startPos + transform.TransformDirection(moveOffset);
    }

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>벽 이동 시작. 이미 이동 중이면 무시.</summary>
    public void Activate()
    {
        if (_isMoving) return;
        _moveCoroutine = StartCoroutine(MoveRoutine());
    }

    /// <summary>벽을 시작 위치로 즉시 복귀 (리셋).</summary>
    public void ResetToStart()
    {
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
            _moveCoroutine = null;
        }

        _isMoving         = false;
        transform.position = _startPos;
    }

    // ── 내부 ────────────────────────────────────────────────────

    IEnumerator MoveRoutine()
    {
        _isMoving = true;
        OnMoveStarted?.Invoke();

        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            float t          = moveDuration > 0f ? elapsed / moveDuration : 1f;
            float curveValue = easeCurve.Evaluate(t);
            transform.position = Vector3.LerpUnclamped(_startPos, _endPos, curveValue);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = _endPos;
        _isMoving          = false;
        OnMoveCompleted?.Invoke();
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
