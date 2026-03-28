using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 점진적 전진 벽 컴포넌트.
/// 매 사이클마다 전진 후 일부 후퇴하며 원점을 갱신 → 결과적으로 벽이 서서히 전진.
///
/// [동작 원리]
///  사이클 1: origin(0) → origin+advance(10) → origin+advance-retreat(3)  ← 새 원점
///  사이클 2: origin(3) → origin+advance( 9) → origin+advance-retreat(5)  ← 새 원점
///  순 전진 = advanceDistance - retreatDistance
///
/// [스케줄]
///  activateAtSeconds[] 에 원하는 시간(초)을 입력하면 해당 시각에만 전진 시작.
///  cyclesPerActivation: 한 번 발동 시 실행할 사이클 수 (0 = 무한 반복)
///  loopSchedule: true 면 schedulePeriod 주기로 스케줄 전체 반복.
///
/// [필수 컴포넌트]
///  Rigidbody: Is Kinematic = true, Interpolate = Interpolate
///  Collider:  Is Trigger = false
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class AdvancingWall : MonoBehaviour
{
    [Header("이동 방향")]
    [Tooltip("전진 방향 (로컬 좌표). 예: (1,0,0) = 로컬 오른쪽\n" +
             "Gizmo의 주황선이 전진, 파란 구가 새 원점 위치")]
    [SerializeField] Vector3 moveDirection = Vector3.right;

    [Header("거리 설정")]
    [Tooltip("매 사이클 전진 거리(m)")]
    [SerializeField] float advanceDistance = 5f;

    [Tooltip("매 사이클 후퇴 거리(m). advanceDistance보다 작아야 순전진")]
    [SerializeField] float retreatDistance = 3f;

    [Header("속도 설정")]
    [Tooltip("전진 소요 시간(초)")]
    [SerializeField] float moveDuration = 1f;

    [Tooltip("후퇴 소요 시간(초)")]
    [SerializeField] float returnDuration = 0.8f;

    [Header("대기 시간")]
    [Tooltip("전진 완료 후 후퇴 시작까지 대기(초)")]
    [SerializeField] float returnDelay = 0.5f;

    [Tooltip("후퇴 완료 후 다음 사이클 시작까지 대기(초).\n" +
             "스케줄 모드에서는 다음 스케줄까지 정지하므로 큰 의미 없음")]
    [SerializeField] float loopDelay = 0.3f;

    [Header("한계")]
    [Tooltip("최대 순전진 거리(m). 이 거리에 도달하면 정지. 0이면 무제한")]
    [SerializeField] float maxTotalAdvance = 0f;

    // ─── 타임라인 스케줄 ──────────────────────────────────────────

    [Header("타임라인 스케줄")]
    [Tooltip("씬 시작 기준 몇 초에 전진을 시작할지 입력.\n" +
             "ex) [5, 20, 40] → 5초, 20초, 40초에 각각 발동.\n" +
             "비워두면 activateOnStart 로 제어.")]
    [SerializeField] float[] activateAtSeconds = new float[0];

    [Tooltip("한 번 스케줄이 발동될 때 실행할 사이클 수.\n" +
             "0 = 무한 반복 (Deactivate() 또는 maxTotalAdvance 도달까지)")]
    [SerializeField] int cyclesPerActivation = 1;

    [Tooltip("스케줄 전체를 주기적으로 반복")]
    [SerializeField] bool loopSchedule = false;

    [Tooltip("loopSchedule = true 일 때 반복 주기(초).\n" +
             "마지막 activateAtSeconds 값보다 크게 설정할 것")]
    [SerializeField] float schedulePeriod = 60f;

    [Tooltip("스케줄을 씬 시작 시 자동 시작")]
    [SerializeField] bool scheduleOnStart = true;

    // ─── 단순 즉시 시작 (스케줄 미사용 시) ──────────────────────

    [Header("즉시 시작 (스케줄 미사용 시)")]
    [Tooltip("activateAtSeconds 가 비어있을 때만 적용.\n" +
             "true면 씬 시작 즉시 무한 반복")]
    [SerializeField] bool activateOnStart = false;

    [Header("이벤트")]
    public UnityEvent OnAdvanceStarted;
    public UnityEvent OnAdvanceCompleted;
    public UnityEvent OnRetreatStarted;
    public UnityEvent OnRetreatCompleted;
    public UnityEvent OnMaxReached;

    [Header("Runtime (확인용)")]
    [SerializeField] bool    _isActive;
    [SerializeField] int     _cyclesRemaining;
    [SerializeField] float   _totalAdvanced;
    [SerializeField] Vector3 _currentOrigin;

    Rigidbody _rb;
    Coroutine _advanceCoroutine;
    Coroutine _scheduleCoroutine;

    // ── 생명주기 ─────────────────────────────────────────────────

    void Awake()
    {
        _rb             = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _currentOrigin  = transform.position;
    }

    void Start()
    {
        bool hasSchedule = activateAtSeconds != null && activateAtSeconds.Length > 0;

        if (hasSchedule && scheduleOnStart)
            StartSchedule();
        else if (!hasSchedule && activateOnStart)
            Activate(0); // 0 = 무한
    }

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>스케줄을 지금 시점 기준으로 시작.</summary>
    public void StartSchedule()
    {
        if (_scheduleCoroutine != null) StopCoroutine(_scheduleCoroutine);
        _scheduleCoroutine = StartCoroutine(ScheduleRoutine());
    }

    /// <summary>스케줄 정지.</summary>
    public void StopSchedule()
    {
        if (_scheduleCoroutine != null)
        {
            StopCoroutine(_scheduleCoroutine);
            _scheduleCoroutine = null;
        }
    }

    /// <summary>
    /// 전진 즉시 시작.
    /// cycles: 실행할 사이클 수 (0 = 무한).
    /// </summary>
    public void Activate(int cycles = -1)
    {
        if (_isActive) return;

        // cycles < 0 이면 Inspector cyclesPerActivation 사용
        int count = cycles < 0 ? cyclesPerActivation : cycles;

        _isActive        = true;
        _cyclesRemaining = count; // 0 = 무한

        if (_advanceCoroutine != null) StopCoroutine(_advanceCoroutine);
        _advanceCoroutine = StartCoroutine(AdvanceRoutine());
    }

    /// <summary>전진 중단 (현 위치에서 정지).</summary>
    public void Deactivate()
    {
        _isActive = false;
        if (_advanceCoroutine != null)
        {
            StopCoroutine(_advanceCoroutine);
            _advanceCoroutine = null;
        }
    }

    /// <summary>시작 위치로 완전 초기화.</summary>
    public void ResetToStart()
    {
        Deactivate();
        _totalAdvanced   = 0f;
        _cyclesRemaining = 0;
        _currentOrigin   = transform.position;
        _rb.MovePosition(_currentOrigin);
    }

    // ── 내부 ────────────────────────────────────────────────────

    IEnumerator ScheduleRoutine()
    {
        while (true)
        {
            float startTime = Time.time;

            foreach (float sec in activateAtSeconds)
            {
                float remaining = (startTime + sec) - Time.time;
                if (remaining > 0f)
                    yield return new WaitForSeconds(remaining);

                // 이미 이동 중이면 완료 후 발동 (겹침 방지)
                while (_isActive) yield return null;

                Activate(); // Inspector의 cyclesPerActivation 사용

                // 이동이 끝날 때까지 대기 (다음 스케줄 항목으로 넘어가기 위해)
                while (_isActive) yield return null;
            }

            if (!loopSchedule) yield break;

            float periodEnd = startTime + schedulePeriod;
            float waitLeft  = periodEnd - Time.time;
            if (waitLeft > 0f)
                yield return new WaitForSeconds(waitLeft);
        }
    }

    IEnumerator AdvanceRoutine()
    {
        int cyclesDone = 0;

        while (_isActive)
        {
            // 최대 거리 도달 확인
            if (maxTotalAdvance > 0f && _totalAdvanced >= maxTotalAdvance)
            {
                OnMaxReached?.Invoke();
                _isActive = false;
                yield break;
            }

            // 사이클 수 제한 확인 (0 = 무한)
            if (_cyclesRemaining > 0 && cyclesDone >= _cyclesRemaining)
            {
                _isActive = false;
                yield break;
            }

            Vector3 worldDir      = transform.TransformDirection(moveDirection.normalized);
            Vector3 advanceTarget = _currentOrigin + worldDir * advanceDistance;
            float   netAdvance    = advanceDistance - retreatDistance;
            Vector3 newOrigin     = _currentOrigin + worldDir * netAdvance;

            // 전진
            OnAdvanceStarted?.Invoke();
            yield return LerpTo(_currentOrigin, advanceTarget, moveDuration);
            _rb.MovePosition(advanceTarget);
            OnAdvanceCompleted?.Invoke();

            if (returnDelay > 0f)
                yield return new WaitForSeconds(returnDelay);

            // 후퇴
            OnRetreatStarted?.Invoke();
            yield return LerpTo(advanceTarget, newOrigin, returnDuration);
            _rb.MovePosition(newOrigin);
            OnRetreatCompleted?.Invoke();

            // 원점 갱신
            _currentOrigin  = newOrigin;
            _totalAdvanced += netAdvance;
            cyclesDone++;

            if (loopDelay > 0f && (_cyclesRemaining == 0 || cyclesDone < _cyclesRemaining))
                yield return new WaitForSeconds(loopDelay);
        }

        _isActive = false;
    }

    IEnumerator LerpTo(Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = duration > 0f ? elapsed / duration : 1f;
            _rb.MovePosition(Vector3.Lerp(from, to, t));
            elapsed += Time.deltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    // ── 에디터 ──────────────────────────────────────────────────

    [ContextMenu("테스트: 즉시 시작 (1 사이클)")]
    void Debug_Activate1() => Activate(1);

    [ContextMenu("테스트: 즉시 시작 (무한)")]
    void Debug_ActivateInf() => Activate(0);

    [ContextMenu("테스트: 스케줄 시작")]
    void Debug_StartSchedule() => StartSchedule();

    [ContextMenu("테스트: 중단")]
    void Debug_Deactivate() => Deactivate();

    [ContextMenu("테스트: 리셋")]
    void Debug_Reset() => ResetToStart();

    void OnDrawGizmos()
    {
        Vector3 origin   = Application.isPlaying ? _currentOrigin : transform.position;
        Vector3 worldDir = transform.TransformDirection(moveDirection.normalized);

        // 전진 목표 (주황)
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.9f);
        Gizmos.DrawLine(origin, origin + worldDir * advanceDistance);
        Gizmos.DrawWireSphere(origin + worldDir * advanceDistance, 0.18f);

        // 새 원점 (파랑)
        float net = advanceDistance - retreatDistance;
        Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.9f);
        Gizmos.DrawWireSphere(origin + worldDir * net, 0.13f);

        // 현재 원점 (흰색)
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(origin, 0.1f);
    }
}
