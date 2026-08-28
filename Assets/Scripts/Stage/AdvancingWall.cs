using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 점진적 전진 벽 컴포넌트.
/// 스케줄 항목마다 전진 거리·후퇴 거리를 개별 설정 가능.
///
/// [동작 원리]
///  retreatDistance = advanceDistance → 제자리 복귀 (순 전진 0)
///  retreatDistance &lt; advanceDistance → 순 전진 (원점이 앞으로 이동)
///  retreatDistance = 0               → 전진만, 후퇴 없음
///
/// [스케줄 예시]
///  Entry 0: atSeconds=10, advance=8, retreat=8  → 10초에 8 전진 후 제자리 복귀
///  Entry 1: atSeconds=12, advance=5, retreat=3  → 12초에 5 전진, 3 후퇴 (순 +2)
///  Entry 2: atSeconds=15, advance=2, retreat=2  → 15초에 2 전진 후 제자리 복귀
///
/// [필수 컴포넌트]
///  Rigidbody: Is Kinematic = true, Interpolate = Interpolate
///  Collider:  Is Trigger = false
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class AdvancingWall : MonoBehaviour
{
    // ─── 스케줄 항목 ─────────────────────────────────────────────

    [System.Serializable]
    public class AdvanceEntry
    {
        [Tooltip("씬 시작 기준 발동 시각(초)")]
        public float atSeconds = 0f;

        [Tooltip("전진 거리(m)")]
        public float advanceDistance = 5f;

        [Tooltip("후퇴 거리(m).\n" +
                 "= advanceDistance → 제자리 복귀\n" +
                 "< advanceDistance → 순전진 (원점 이동)\n" +
                 "= 0               → 전진만 (후퇴 없음)")]
        public float retreatDistance = 5f;

        [Tooltip("이 항목에서 반복할 사이클 수 (0 = 1회)")]
        public int cycles = 0;

        [Tooltip("전진 소요 시간(초). 0이면 기본값(moveDuration) 사용")]
        public float overrideMoveDuration = 0f;

        [Tooltip("후퇴 소요 시간(초). 0이면 기본값(returnDuration) 사용")]
        public float overrideReturnDuration = 0f;
    }

    [Header("이동 방향")]
    [Tooltip("전진 방향 (로컬 좌표). 예: (1,0,0) = 로컬 오른쪽")]
    [SerializeField] Vector3 moveDirection = Vector3.right;

    [Header("기본 속도 (항목별 override가 0이면 이 값 사용)")]
    [Tooltip("전진 소요 시간(초)")]
    [SerializeField] float moveDuration = 1f;

    [Tooltip("후퇴 소요 시간(초)")]
    [SerializeField] float returnDuration = 0.8f;

    [Header("기본 대기 시간")]
    [Tooltip("전진 완료 후 후퇴 시작까지 대기(초)")]
    [SerializeField] float returnDelay = 0.5f;

    [Tooltip("한 사이클 완료 후 다음 사이클 시작까지 대기(초)")]
    [SerializeField] float loopDelay = 0.3f;

    [Header("타임라인 스케줄")]
    [Tooltip("발동 시각과 이동 거리를 항목마다 개별 설정.\n" +
             "atSeconds 오름차순으로 입력 권장.")]
    [SerializeField] AdvanceEntry[] schedule = new AdvanceEntry[0];

    [Tooltip("스케줄 전체를 주기적으로 반복")]
    [SerializeField] bool loopSchedule = false;

    [Tooltip("loopSchedule = true 일 때 반복 주기(초).\n" +
             "마지막 항목의 atSeconds 보다 크게 설정할 것")]
    [SerializeField] float schedulePeriod = 60f;

    [Tooltip("씬 시작 시 자동으로 스케줄 시작")]
    [SerializeField] bool scheduleOnStart = true;

    [Header("즉시 시작 (스케줄 미사용 시)")]
    [Tooltip("schedule 이 비어있을 때만 적용. 아래 기본 거리로 즉시 무한 반복.")]
    [SerializeField] bool activateOnStart = false;

    [Tooltip("activateOnStart 전용 전진 거리(m)")]
    [SerializeField] float defaultAdvanceDistance = 5f;

    [Tooltip("activateOnStart 전용 후퇴 거리(m)")]
    [SerializeField] float defaultRetreatDistance = 3f;

    [Header("한계 / 게임 실패")]
    [Tooltip("최대 순전진 허용 거리(m). 이 거리를 초과하면 OnMaxReached 이벤트 발생 → 게임 실패 연결.\n" +
             "0이면 무제한.\n" +
             "예) 100×100 방, 상하 벽 쌍이면 각 35 설정 시 최소 공간 10m 확보")]
    [SerializeField] float maxTotalAdvance = 0f;

    [Header("패널티 전진")]
    [Tooltip("PermanentAdvance() 호출 시 이동 소요 시간(초). 스케줄 이동과 별개로 움직임")]
    [SerializeField] float penaltyMoveDuration = 0.6f;

    [Header("ColorWall 연동 — 일시정지")]
    [Tooltip("색상 일치로 일시정지 시, 누적 원점(_currentOrigin)으로 복귀하는 소요 시간(초).\n" +
             "예) 30 전진 → 20 후퇴 → 원점이 +10. 정지 시 +10 위치로 복귀.")]
    [SerializeField] float pauseReturnDuration = 0.5f;

    [Header("텔레그래프 연동")]
    [Tooltip("출발 전 경고 연출 컴포넌트. 비워두면 경고 없이 즉시 출발")]
    [SerializeField] AdvancingWallTelegraph telegraph;

    [Header("사운드 (이동 루프 — 3D)")]
    [Tooltip("전진·후퇴 이동 중 재생할 SFX. 기본값(Trap_AdvancingWall_Move)은 일반 벽용 —\n" +
             "이 컴포넌트를 재사용하는 다른 트랩은 여기서 다른 SFXId로 지정할 것.")]
    [SerializeField] SFXId moveSfxId = SFXId.Trap_AdvancingWall_Move;
    [Tooltip("전진·후퇴 이동 중에만 재생되는 루프. 이동 시작~종료에 맞춰 자동으로 켜고 끔.\n0 = 완전 2D, 1 = 완전 3D")]
    [SerializeField] [Range(0f, 1f)] float moveSpatialBlend = 1f;
    [Tooltip("이 거리(m) 이내에서는 최대 볼륨")]
    [SerializeField] float moveMinDistance = 30f;
    [Tooltip("이 거리(m) 밖에서는 완전 무음. 0이면 500으로 처리")]
    [SerializeField] float moveMaxDistance = 100f;
    [SerializeField] AudioRolloffMode moveRolloffMode = AudioRolloffMode.Logarithmic;

    [Header("이벤트")]
    public UnityEvent OnAdvanceStarted;
    public UnityEvent OnAdvanceCompleted;
    public UnityEvent OnRetreatStarted;
    public UnityEvent OnRetreatCompleted;
    /// <summary>maxTotalAdvance 초과 시 발생. GameManager.GameFail() 등을 연결.</summary>
    public UnityEvent OnMaxReached;
    /// <summary>PermanentAdvance() 완료 시 발생.</summary>
    public UnityEvent OnPermanentAdvance;

    bool    _isActive;
    bool    _isPausedByColor;
    float   _totalAdvanced;
    Vector3 _currentOrigin;

    Rigidbody _rb;
    Coroutine _advanceCoroutine;
    Coroutine _scheduleCoroutine;
    Coroutine _pauseCoroutine;

    AudioSource _moveLoopSource;

    // ── 생명주기 ─────────────────────────────────────────────────

    void Awake()
    {
        _rb             = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _currentOrigin  = transform.position;
    }

    void Update()
    {
        // 볼륨 실시간 반영(옵션 메뉴 마스터/SFX 슬라이더).
        if (_moveLoopSource != null && _moveLoopSource.isPlaying && SFXManager.Instance != null)
            _moveLoopSource.volume = SFXManager.Instance.GetEffectiveVolume(moveSfxId);
    }

    void OnDisable()
    {
        StopMoveLoop();
    }

    void Start()
    {
        bool hasSchedule = schedule != null && schedule.Length > 0;

        if (hasSchedule && scheduleOnStart)
            StartSchedule();
        else if (!hasSchedule && activateOnStart)
            StartCoroutine(RunEntry(
                new AdvanceEntry
                {
                    advanceDistance = defaultAdvanceDistance,
                    retreatDistance = defaultRetreatDistance,
                    cycles          = 0
                }));
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

    /// <summary>현 위치에서 정지.</summary>
    public void Deactivate()
    {
        _isActive = false;
        if (_advanceCoroutine != null)
        {
            StopCoroutine(_advanceCoroutine);
            _advanceCoroutine = null;
        }
        telegraph?.Cancel();
        StopMoveLoop();
    }

    /// <summary>시작 위치로 완전 초기화.</summary>
    public void ResetToStart()
    {
        Deactivate();
        _totalAdvanced = 0f;
        _currentOrigin = transform.position;
        _rb.MovePosition(_currentOrigin);
    }

    /// <summary>
    /// 패널티 전진 — 스케줄과 완전히 별개로 distance 만큼 영구 접근.
    /// 현재 스케줄 이동이 진행 중이면 완료 후 실행.
    /// 이후 스케줄 이동은 새 원점 기준으로 적용됨.
    /// ex) 스케줄 10 전진 예정 + 패널티 5 → 패널티 먼저 이동 후 스케줄 10 추가 = 총 15
    /// </summary>
    public void PermanentAdvance(float distance)
    {
        if (distance <= 0f) return;
        StartCoroutine(PenaltyRoutine(distance));
    }

    /// <summary>현재까지 순전진한 총 거리 (패널티 포함).</summary>
    public float TotalAdvanced => _totalAdvanced;

    /// <summary>현재 전진·후퇴 이동 중인지. WallLineRandomizer 등 외부 스케줄러가 완료 대기에 사용.</summary>
    public bool IsMoving => _isActive;

    /// <summary>
    /// 한 번 전진·후퇴 실행 (WallLineRandomizer 등 외부 스케줄 전용).
    /// 이미 이동 중이거나 색 일시정지 상태이면 무시.
    /// </summary>
    /// <param name="advanceDistance">전진 거리(m)</param>
    /// <param name="retreatRatio">0~1. 후퇴 거리 = 전진 × 비율 (1이면 전진만큼 후퇴)</param>
    /// <param name="advanceMoveDuration">전진에 걸리는 시간(초). 0이면 moveDuration 사용</param>
    /// <param name="returnMoveDuration">후퇴에 걸리는 시간(초). 0이면 returnDuration 사용</param>
    public void RunOnce(float advanceDistance, float retreatRatio, float advanceMoveDuration, float returnMoveDuration)
    {
        if (_isActive || _isPausedByColor) return;
        float ratio = Mathf.Clamp01(retreatRatio);
        float retreatDist = advanceDistance * ratio;
        _advanceCoroutine = StartCoroutine(RunEntry(new AdvanceEntry
        {
            advanceDistance        = advanceDistance,
            retreatDistance        = retreatDist,
            cycles                 = 0,
            overrideMoveDuration   = advanceMoveDuration,
            overrideReturnDuration = returnMoveDuration
        }));
    }

    /// <summary>
    /// ColorWall 색상 일치 시 호출.
    /// 현재 전진을 중단하고 누적 원점(_currentOrigin)으로 부드럽게 복귀 후
    /// duration 만큼 멈춘 뒤 스케줄 재개.
    ///
    /// "자기 자리"란 마지막으로 확정된 후퇴 완료 위치 (_currentOrigin).
    /// 예) 30 전진 → 20 후퇴 → _currentOrigin = +10. 멈추면 +10 위치로 복귀.
    /// </summary>
    public void PauseTemporarily(float duration)
    {
        if (_pauseCoroutine != null) StopCoroutine(_pauseCoroutine);
        _pauseCoroutine = StartCoroutine(PauseByColorRoutine(duration));
    }

    // ── 내부 ────────────────────────────────────────────────────

    IEnumerator ScheduleRoutine()
    {
        while (true)
        {
            float startTime = NetTime();

            foreach (AdvanceEntry entry in schedule)
            {
                float remaining = (startTime + entry.atSeconds) - NetTime();
                if (remaining > 0f)
                    yield return new WaitForSeconds(remaining);

                // 이전 이동 완료 + ColorWall 일시정지 해제까지 대기
                while (_isActive || _isPausedByColor) yield return null;

                _advanceCoroutine = StartCoroutine(RunEntry(entry));

                // 이동 완료 + ColorWall 일시정지 해제까지 대기
                while (_isActive || _isPausedByColor) yield return null;
            }

            if (!loopSchedule) yield break;

            float periodEnd = startTime + schedulePeriod;
            float waitLeft  = periodEnd - NetTime();
            if (waitLeft > 0f)
                yield return new WaitForSeconds(waitLeft);
        }
    }

    /// <summary>
    /// 자유런(트리거 없이 씬 시작 즉시 재생) 스케줄용 시간 소스.
    /// 각 머신이 ServerTime만 폴링하면 결정론적 (WallMover.ScheduleRoutine / WallWaveController와 동일 원칙).
    /// </summary>
    static float NetTime()
    {
        var nm = NetworkManager.Singleton;
        return nm != null ? (float)nm.ServerTime.Time : Time.time;
    }

    /// <summary>단일 AdvanceEntry 실행 (지정 사이클 수만큼 전진·후퇴 반복).</summary>
    IEnumerator RunEntry(AdvanceEntry entry)
    {
        _isActive = true;

        float advDist = entry.advanceDistance;
        float retDist = entry.retreatDistance;
        float advDur  = entry.overrideMoveDuration   > 0f ? entry.overrideMoveDuration   : moveDuration;
        float retDur  = entry.overrideReturnDuration > 0f ? entry.overrideReturnDuration : returnDuration;
        int   total   = Mathf.Max(entry.cycles, 1); // 0도 최소 1회 실행

        int cyclesDone = 0;

        while (_isActive && cyclesDone < total)
        {
            // 최대 거리 도달 확인
            if (maxTotalAdvance > 0f && _totalAdvanced >= maxTotalAdvance)
            {
                OnMaxReached?.Invoke();
                _isActive = false;
                yield break;
            }

            Vector3 worldDir      = transform.TransformDirection(moveDirection.normalized);
            Vector3 advanceTarget = _currentOrigin + worldDir * advDist;
            float   net           = advDist - retDist;
            Vector3 newOrigin     = _currentOrigin + worldDir * net;

            // 텔레그래프 — 경고 연출 위임
            if (telegraph != null && telegraph.Duration > 0f)
            {
                telegraph.Play();
                yield return new WaitForSeconds(telegraph.Duration);
                telegraph.Cancel();
            }

            // 전진
            OnAdvanceStarted?.Invoke();
            StartMoveLoop();
            yield return LerpTo(_currentOrigin, advanceTarget, advDur);
            StopMoveLoop();
            _rb.MovePosition(advanceTarget);
            OnAdvanceCompleted?.Invoke();

            if (returnDelay > 0f)
                yield return new WaitForSeconds(returnDelay);

            // 후퇴 (retreatDistance = 0이면 후퇴 생략)
            if (retDist > 0f)
            {
                OnRetreatStarted?.Invoke();
                StartMoveLoop();
                yield return LerpTo(advanceTarget, newOrigin, retDur);
                StopMoveLoop();
                _rb.MovePosition(newOrigin);
                OnRetreatCompleted?.Invoke();

                _currentOrigin  = newOrigin;
                _totalAdvanced += Mathf.Max(net, 0f);
            }
            else
            {
                _currentOrigin  = advanceTarget;
                _totalAdvanced += advDist;
            }

            cyclesDone++;

            if (loopDelay > 0f && cyclesDone < total)
                yield return new WaitForSeconds(loopDelay);
        }

        _isActive = false;
    }

    IEnumerator PenaltyRoutine(float distance)
    {
        // 현재 스케줄 이동이 실행 중이면 완료 대기
        while (_isActive) yield return null;

        Vector3 worldDir = transform.TransformDirection(moveDirection.normalized);
        Vector3 from     = _rb.position;
        Vector3 target   = _currentOrigin + worldDir * distance;

        yield return LerpTo(from, target, Mathf.Max(penaltyMoveDuration, 0.05f));
        _rb.MovePosition(target);

        _currentOrigin  = target;
        _totalAdvanced += distance;

        OnPermanentAdvance?.Invoke();

        // 패널티로 인해 최대 거리 초과 확인
        if (maxTotalAdvance > 0f && _totalAdvanced >= maxTotalAdvance)
            OnMaxReached?.Invoke();
    }

    IEnumerator PauseByColorRoutine(float duration)
    {
        // 현재 전진 중지
        if (_advanceCoroutine != null)
        {
            StopCoroutine(_advanceCoroutine);
            _advanceCoroutine = null;
        }
        telegraph?.Cancel();
        StopMoveLoop();
        _isActive        = false;
        _isPausedByColor = true;

        // _currentOrigin 으로 부드럽게 복귀 (= 마지막 확정 후퇴 위치)
        Vector3 from = _rb.position;
        if (Vector3.Distance(from, _currentOrigin) > 0.01f)
            yield return LerpTo(from, _currentOrigin, Mathf.Max(pauseReturnDuration, 0.05f));
        _rb.MovePosition(_currentOrigin);

        // 일시정지 유지
        yield return new WaitForSeconds(Mathf.Max(duration, 0f));

        _isPausedByColor = false;
        // ScheduleRoutine이 _isPausedByColor == false 를 확인 후 다음 항목 자동 실행
    }

    // ── 사운드 (이동 루프) ────────────────────────────────────────
    // 전진/후퇴 공용. LerpTo() 시작 직전에 켜고, 완료 직후(또는 강제 중단 시 Deactivate/
    // PauseByColorRoutine에서) 꺼서 "소리 나는 시간 = 실제 이동 시간"을 보장한다.

    void StartMoveLoop()
    {
        if (_moveLoopSource != null && _moveLoopSource.isPlaying) return;
        if (SFXManager.Instance == null) return;

        AudioClip clip = SFXManager.Instance.GetClip(moveSfxId);
        if (clip == null) return;

        if (_moveLoopSource == null)
        {
            _moveLoopSource              = gameObject.AddComponent<AudioSource>();
            _moveLoopSource.loop         = true;
            _moveLoopSource.playOnAwake  = false;
            _moveLoopSource.spatialBlend = moveSpatialBlend;
            _moveLoopSource.rolloffMode  = moveRolloffMode;
            _moveLoopSource.minDistance  = moveMinDistance > 0f ? moveMinDistance : 1f;
            _moveLoopSource.maxDistance  = moveMaxDistance > 0f ? moveMaxDistance : 500f;
        }

        _moveLoopSource.clip   = clip;
        _moveLoopSource.volume = SFXManager.Instance.GetEffectiveVolume(moveSfxId);
        _moveLoopSource.Play();
    }

    void StopMoveLoop()
    {
        if (_moveLoopSource != null && _moveLoopSource.isPlaying)
            _moveLoopSource.Stop();
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

        if (schedule != null && schedule.Length > 0)
        {
            // 모든 스케줄 항목의 전진/후퇴 위치를 시뮬레이션해서 표시
            Vector3 simOrigin = origin;
            for (int i = 0; i < schedule.Length; i++)
            {
                AdvanceEntry e = schedule[i];
                float alpha = 1f - (float)i / schedule.Length * 0.5f;

                Vector3 advTarget  = simOrigin + worldDir * e.advanceDistance;
                float   net        = e.advanceDistance - e.retreatDistance;
                Vector3 newOrigin  = simOrigin + worldDir * net;

                // 전진선 (주황)
                Gizmos.color = new Color(1f, 0.4f, 0f, alpha);
                Gizmos.DrawLine(simOrigin, advTarget);
                Gizmos.DrawWireSphere(advTarget, 0.18f);

                // 후퇴 후 원점 (파랑)
                Gizmos.color = new Color(0.2f, 0.7f, 1f, alpha);
                Gizmos.DrawWireSphere(newOrigin, 0.13f);

                simOrigin = newOrigin;
            }
        }
        else
        {
            // 기본 거리 표시
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.9f);
            Gizmos.DrawLine(origin, origin + worldDir * defaultAdvanceDistance);
            Gizmos.DrawWireSphere(origin + worldDir * defaultAdvanceDistance, 0.18f);

            float net = defaultAdvanceDistance - defaultRetreatDistance;
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.9f);
            Gizmos.DrawWireSphere(origin + worldDir * net, 0.13f);
        }

        // 현재 원점 (흰색)
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(origin, 0.1f);
    }
}
