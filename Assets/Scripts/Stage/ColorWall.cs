using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 색상 벽 컴포넌트.
/// 플레이어 현재 색(흑/백/고유색)과 벽 색상을 비교해 반응을 다르게 함.
///
/// [색상 일치]
///  WallMover → ResetToStart() 후 pauseDuration 뒤 Activate() (밀려남 + 잠시 멈춤)
///  WallWaveController → Stop() 후 pauseDuration 뒤 Play()
///
/// [색상 불일치]
///  플레이어에게 damage 적용 (damageInterval마다)
///
/// [스케줄 색상 전환]
///  colorSchedule[] 배열에 {atSeconds, color} 를 지정하면
///  스케줄 시작 기준 해당 시각에 이 벽의 색이 변환됨.
///  벽마다 개별 설정 → 원하는 벽만, 원하는 시간에, 원하는 색으로 전환 가능.
///
/// [설정]
///  wallMover / waveController: 비워두면 같은 오브젝트에서 자동 탐색.
///  Collider Is Trigger 여부에 따라 OnTrigger / OnCollision 모두 처리.
/// </summary>
public class ColorWall : MonoBehaviour
{
    public enum WallColorType { Black, White, Blue, Red, Green, Yellow }

    // ─── 색상 스케줄 이벤트 ────────────────────────────────────────
    [System.Serializable]
    public class ColorChangeEvent
    {
        [Tooltip("스케줄 시작 기준 몇 초 뒤에 색이 바뀌는지")]
        public float atSeconds;
        [Tooltip("변환될 목표 색상")]
        public WallColorType color;
    }

    [Header("기본 색상")]
    [Tooltip("초기 색상 (스케줄 시작 전 사용)")]
    [SerializeField] WallColorType defaultColor = WallColorType.Black;

    [Header("스케줄 색상 전환")]
    [Tooltip("시간(초)에 따라 색이 바뀌는 스케줄.\n" +
             "atSeconds: 시작 기준 경과 시간 / color: 바뀔 색상")]
    [SerializeField] ColorChangeEvent[] colorSchedule = new ColorChangeEvent[0];

    [Tooltip("스케줄를 반복할지 여부")]
    [SerializeField] bool loopSchedule = false;

    [Tooltip("loopSchedule = true 일 때 반복 주기(초). 마지막 이벤트 atSeconds보다 크게 설정")]
    [SerializeField] float schedulePeriod = 60f;

    [Tooltip("씬 시작 시 자동으로 스케줄 시작")]
    [SerializeField] bool scheduleOnStart = true;

    [Header("색상 일치 — 멈춤")]
    [Tooltip("색상이 같으면 이 시간(초) 동안 벽 이동 정지")]
    [SerializeField] float pauseDuration = 2f;

    [Header("색상 불일치 — 데미지")]
    [Tooltip("불일치 시 플레이어에게 입히는 데미지")]
    [SerializeField] int damage = 1;

    [Tooltip("연속 데미지 간격(초)")]
    [SerializeField] float damageInterval = 0.5f;

    [Header("연결 컴포넌트 (비워두면 같은 오브젝트에서 자동 탐색)")]
    [Tooltip("점진적 전진 벽 컴포넌트 (AdvancingWall 사용 시 등록)")]
    [SerializeField] AdvancingWall advancingWall;

    [Tooltip("개별 벽 이동 컴포넌트")]
    [SerializeField] WallMover wallMover;

    [Tooltip("파형 벽 이동 컴포넌트")]
    [SerializeField] WallWaveController waveController;

    [Header("이벤트 (선택)")]
    [Tooltip("색상 일치 시 호출 (시각 피드백 등)")]
    public UnityEvent OnColorMatch;

    [Tooltip("색상 불일치 데미지 발생 시 호출")]
    public UnityEvent OnColorMismatch;

    [Tooltip("색상이 바뀔 때 호출 (머티리얼 변경 등 시각 피드백)")]
    public UnityEvent OnColorChanged;

    [Header("Runtime (확인용)")]
    [SerializeField] WallColorType _currentColor;

    WallColorType _wallColor;   // 실제 적용 중인 색 (wallColor 대신 사용)
    float     _nextDamageTime;
    bool      _isPaused;
    Coroutine _pauseCoroutine;
    Coroutine _scheduleCoroutine;

    // ── 생명주기 ─────────────────────────────────────────────────

    void Awake()
    {
        if (advancingWall == null)
            advancingWall = GetComponent<AdvancingWall>();
        if (wallMover == null)
            wallMover = GetComponent<WallMover>();
        if (waveController == null)
            waveController = GetComponent<WallWaveController>();

        _wallColor    = defaultColor;
        _currentColor = defaultColor;
    }

    void Start()
    {
        if (scheduleOnStart && colorSchedule != null && colorSchedule.Length > 0)
            StartSchedule();
    }

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>색상 스케줄을 지금 시점 기준으로 시작.</summary>
    public void StartSchedule()
    {
        if (_scheduleCoroutine != null) StopCoroutine(_scheduleCoroutine);
        _scheduleCoroutine = StartCoroutine(ScheduleRoutine());
    }

    /// <summary>색상 스케줄 정지.</summary>
    public void StopSchedule()
    {
        if (_scheduleCoroutine != null)
        {
            StopCoroutine(_scheduleCoroutine);
            _scheduleCoroutine = null;
        }
    }

    /// <summary>런타임에서 직접 색상 변경.</summary>
    public void SetColor(WallColorType color)
    {
        _wallColor    = color;
        _currentColor = color;
        OnColorChanged?.Invoke();
    }

    // ── 충돌 감지 (Trigger / Collider 모두 처리) ─────────────────

    void OnTriggerEnter(Collider other)  => HandleContact(other);
    void OnTriggerStay(Collider other)   => HandleContact(other);
    void OnCollisionEnter(Collision col) => HandleContact(col.collider);
    void OnCollisionStay(Collision col)  => HandleContact(col.collider);

    // ── 내부 ────────────────────────────────────────────────────

    void HandleContact(Collider other)
    {
        Player p = other.GetComponentInParent<Player>();
        if (p == null || p.IsDead) return;

        if (IsColorMatch(p))
        {
            if (!_isPaused)
            {
                if (_pauseCoroutine != null) StopCoroutine(_pauseCoroutine);
                _pauseCoroutine = StartCoroutine(PauseRoutine());
                OnColorMatch?.Invoke();
            }
        }
        else
        {
            if (damage > 0 && Time.time >= _nextDamageTime)
            {
                p.TakeDamage(damage, false);
                _nextDamageTime = Time.time + Mathf.Max(damageInterval, 0.05f);
                OnColorMismatch?.Invoke();
            }
        }
    }

    bool IsColorMatch(Player p)
    {
        switch (_wallColor)
        {
            case WallColorType.Black:
                return !p.isUniqueColor && p.isBlack;
            case WallColorType.White:
                return !p.isUniqueColor && !p.isBlack;
            case WallColorType.Blue:
                return p.isUniqueColor && p.playerColorType == PlayerColorType.Blue;
            case WallColorType.Red:
                return p.isUniqueColor && p.playerColorType == PlayerColorType.Red;
            case WallColorType.Green:
                return p.isUniqueColor && p.playerColorType == PlayerColorType.Green;
            case WallColorType.Yellow:
                return p.isUniqueColor && p.playerColorType == PlayerColorType.Yellow;
            default:
                return false;
        }
    }

    IEnumerator PauseRoutine()
    {
        _isPaused = true;

        // AdvancingWall: 현재 누적 원점으로 복귀 후 pauseDuration 동안 정지, 이후 스케줄 자동 재개
        advancingWall?.PauseTemporarily(pauseDuration);

        // WallMover / WaveController: 기존 방식
        wallMover?.ResetToStart();
        waveController?.Stop();

        yield return new WaitForSeconds(pauseDuration);

        wallMover?.Activate();
        waveController?.Play();

        _isPaused = false;
    }

    /// <summary>
    /// colorSchedule 배열 순서대로 해당 시각에 색상 변경.
    /// loopSchedule = true면 schedulePeriod 주기로 반복.
    /// </summary>
    IEnumerator ScheduleRoutine()
    {
        while (true)
        {
            float startTime = Time.time;

            // 배열을 순서대로 순회 (atSeconds 오름차순 권장)
            foreach (var evt in colorSchedule)
            {
                float waitUntil = startTime + evt.atSeconds;
                float remaining = waitUntil - Time.time;
                if (remaining > 0f)
                    yield return new WaitForSeconds(remaining);

                SetColor(evt.color);
            }

            if (!loopSchedule) yield break;

            // 다음 주기 시작까지 대기
            float periodEnd = startTime + schedulePeriod;
            float waitForPeriod = periodEnd - Time.time;
            if (waitForPeriod > 0f)
                yield return new WaitForSeconds(waitForPeriod);

            // 주기 시작 시 defaultColor로 복귀
            SetColor(defaultColor);
        }
    }

    // ── 에디터 ──────────────────────────────────────────────────

    [ContextMenu("테스트: 스케줄 시작")]
    void Debug_StartSchedule() => StartSchedule();

    [ContextMenu("테스트: 스케줄 정지")]
    void Debug_StopSchedule() => StopSchedule();

    [ContextMenu("테스트: 기본색 복귀")]
    void Debug_ResetColor() => SetColor(defaultColor);

    void OnDrawGizmos()
    {
        WallColorType showColor = Application.isPlaying ? _wallColor : defaultColor;
        Color c = showColor switch
        {
            WallColorType.Black  => Color.black,
            WallColorType.White  => Color.white,
            WallColorType.Blue   => Color.blue,
            WallColorType.Red    => Color.red,
            WallColorType.Green  => Color.green,
            WallColorType.Yellow => Color.yellow,
            _                    => Color.gray
        };
        c.a = 0.4f;
        Gizmos.color = c;
        Gizmos.DrawWireCube(transform.position, transform.lossyScale);
    }
}
