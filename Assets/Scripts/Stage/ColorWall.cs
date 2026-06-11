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
///  ContactDamage(같은 오브젝트) → Deactivate (일치 중 면역) → 종료 시 Activate
///
/// [색상 불일치]
///  데미지 없음. 데미지는 ContactDamage 컴포넌트가 전담.
///
/// [스케줄 색상 전환]
///  colorSchedule[] 배열에 {atSeconds, color} 를 지정하면
///  스케줄 시작 기준 해당 시각에 이 벽의 색이 변환됨.
///  벽마다 개별 설정 → 원하는 벽만, 원하는 시간에, 원하는 색으로 전환 가능.
///
/// [WallColorType.Default]
///  플레이어 어떤 색과도 일치하지 않음(일치 멈춤·면역 없음). 평상시 휴지 상태에 사용.
///
/// [머티리얼]
///  defaultMaterial: defaultColor 상태일 때 표시할 머티리얼.
///  colorMaterials[]: 그 외 논리 색일 때 교체할 색상별 머티리얼.
///  SetColor 시 color == defaultColor 면 defaultMaterial, 아니면 colorMaterials 탐색.
///
/// [연결 컴포넌트]
///  AdvancingWall / WallMover / WallWaveController 는 같은 오브젝트에서 자동 탐색.
///  Collider Is Trigger 여부에 따라 OnTrigger / OnCollision 모두 처리.
/// </summary>
public class ColorWall : MonoBehaviour
{
    public enum WallColorType
    {
        Black, White, Blue, Purple, Green, Yellow,
        /// <summary>플레이어 색과 절대 일치하지 않는 휴지 상태. Black 벽과 구분.</summary>
        Default
    }

    // ─── 색상 스케줄 이벤트 ────────────────────────────────────────
    [System.Serializable]
    public class ColorChangeEvent
    {
        [Tooltip("스케줄 시작 기준 몇 초 뒤에 색이 바뀌는지")]
        public float atSeconds;
        [Tooltip("변환될 목표 색상")]
        public WallColorType color;
    }

    // ─── 색상별 머티리얼 항목 ──────────────────────────────────────
    [System.Serializable]
    public class ColorMaterialEntry
    {
        [Tooltip("이 머티리얼이 적용될 색상")]
        public WallColorType color;
        [Tooltip("해당 색상일 때 적용할 머티리얼")]
        public Material material;
    }

    [Header("기본 색상")]
    [Tooltip("초기·복귀 시 논리 색. Default = 어떤 플레이어 색과도 일치 없음.\n" +
             "(기존 씬이 Black(0)으로 저장돼 있으면 인스펙터에서 Default로 바꿀 것)")]
    [SerializeField] WallColorType defaultColor = WallColorType.Default;

    [Header("머티리얼")]
    [Tooltip("평상시(default 상태)에 표시할 원래 머티리얼.\n" +
             "비워두면 머티리얼 교체 없음.")]
    [SerializeField] Material defaultMaterial;

    [Tooltip("색상 전환 시 교체할 머티리얼 배열.\n" +
             "color 항목에 WallColorType을, material에 해당 머티리얼을 연결.")]
    [SerializeField] ColorMaterialEntry[] colorMaterials = new ColorMaterialEntry[0];

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

    [Header("이벤트 (선택)")]
    [Tooltip("색상 일치 시 호출 (시각 피드백 등)")]
    public UnityEvent OnColorMatch;

    [Tooltip("색상이 바뀔 때 호출 (추가 시각 피드백용)")]
    public UnityEvent OnColorChanged;

    WallColorType _wallColor;
    bool      _isPaused;
    Coroutine _pauseCoroutine;
    Coroutine _scheduleCoroutine;

    Renderer[]          _renderers;
    AdvancingWall       _advancingWall;
    WallMover           _wallMover;
    WallWaveController  _waveController;
    ContactDamage       _contactDamage;

    // ── 현재 논리 색 외부 읽기용 ──────────────────────────────────
    public WallColorType CurrentColor => _wallColor;

    // ── 생명주기 ─────────────────────────────────────────────────

    void Awake()
    {
        _renderers      = GetComponentsInChildren<Renderer>(true);
        _advancingWall  = GetComponent<AdvancingWall>() ?? GetComponentInParent<AdvancingWall>();
        _wallMover      = GetComponent<WallMover>()     ?? GetComponentInParent<WallMover>();
        _waveController = GetComponent<WallWaveController>() ?? GetComponentInParent<WallWaveController>();
        _contactDamage  = GetComponent<ContactDamage>();

        _wallColor = defaultColor;
        ApplyMaterial(defaultColor);
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
        // 플레이어 색 슬롯을 GameSession 활성색으로 재매핑 (GameSession 없으면 원본 그대로)
        ColorChangeEvent[] effective = GameSessionWallColorRemap.RemapSchedule(colorSchedule);
        _scheduleCoroutine = StartCoroutine(ScheduleRoutine(effective));
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

    /// <summary>런타임에서 직접 색상 변경. 머티리얼도 즉시 교체.</summary>
    public void SetColor(WallColorType color)
    {
        _wallColor = color;
        ApplyMaterial(color);
        OnColorChanged?.Invoke();
    }

    /// <summary>defaultColor로 복귀 + defaultMaterial 적용.</summary>
    public void ResetToDefault()
    {
        SetColor(defaultColor);
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
    }

    bool IsColorMatch(Player p)
    {
        switch (_wallColor)
        {
            case WallColorType.Default:
                return false;
            case WallColorType.Black:
                return !p.isUniqueColor && p.isBlack;
            case WallColorType.White:
                return !p.isUniqueColor && !p.isBlack;
            case WallColorType.Blue:
                return p.isUniqueColor && p.playerColorType == PlayerColorType.Blue;
            case WallColorType.Purple:
                return p.isUniqueColor && p.playerColorType == PlayerColorType.Purple;
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
        _contactDamage?.Deactivate();

        _advancingWall?.PauseTemporarily(pauseDuration);
        _wallMover?.ResetToStart();
        _waveController?.Stop();

        yield return new WaitForSeconds(pauseDuration);

        _wallMover?.Activate();
        _waveController?.Play();

        _contactDamage?.Activate();
        _isPaused = false;
    }

    /// <summary>
    /// color == defaultColor 면 defaultMaterial, 아니면 colorMaterials 배열에서 탐색.
    /// 일치하는 항목이 없거나 material이 null이면 교체하지 않음.
    /// </summary>
    void ApplyMaterial(WallColorType color)
    {
        Material mat = null;

        if (color == defaultColor)
        {
            mat = defaultMaterial;
        }
        else
        {
            foreach (ColorMaterialEntry entry in colorMaterials)
            {
                if (entry.color == color)
                {
                    mat = entry.material;
                    break;
                }
            }
        }

        if (mat == null) return;

        foreach (Renderer r in _renderers)
        {
            if (r == null) continue;
            // 첫 번째 슬롯만 교체 (서브메쉬가 여럿이어도 원본 배열 길이 유지)
            Material[] mats = r.sharedMaterials;
            if (mats.Length == 0) continue;
            mats[0] = mat;
            r.sharedMaterials = mats;
        }
    }

    /// <summary>
    /// <summary>
    /// events 배열 순서대로 해당 시각에 색상 변경.
    /// loopSchedule = true면 schedulePeriod 주기로 반복.
    /// events는 StartSchedule()에서 GameSessionWallColorRemap으로 재매핑된 배열.
    /// Inspector 원본 colorSchedule은 그대로 유지됨.
    /// </summary>
    IEnumerator ScheduleRoutine(ColorChangeEvent[] events)
    {
        while (true)
        {
            float startTime = Time.time;

            // 배열을 순서대로 순회 (atSeconds 오름차순 권장)
            foreach (var evt in events)
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
    void Debug_ResetColor() => ResetToDefault();

    void OnDrawGizmos()
    {
        WallColorType showColor = Application.isPlaying ? _wallColor : defaultColor;
        Color c = showColor switch
        {
            WallColorType.Default => new Color(0.45f, 0.45f, 0.48f),
            WallColorType.Black  => Color.black,
            WallColorType.White  => Color.white,
            WallColorType.Blue   => Color.blue,
            WallColorType.Purple => new Color(0.55f, 0.2f, 0.95f),
            WallColorType.Green  => Color.green,
            WallColorType.Yellow => Color.yellow,
            _                    => Color.gray
        };
        c.a = 0.4f;
        Gizmos.color = c;
        Gizmos.DrawWireCube(transform.position, transform.lossyScale);
    }
}
