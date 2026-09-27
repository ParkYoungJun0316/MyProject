using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 색상 벽 컴포넌트.
/// 플레이어 현재 색(흑/백/고유색)과 벽 색상을 비교해 반응을 다르게 함.
///
/// [색상 일치]
///  WallMover → ResetToStart() 후 pauseDuration 뒤 Activate() (밀려남 + 잠시 멈춤)
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
///  AdvancingWall / WallMover 는 같은 오브젝트(또는 부모)에서 자동 탐색.
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

    [Tooltip("플레이어 색 슬롯 배정을 결정론적으로 섞기 위한 salt.\n" +
             "같은 씬의 여러 ColorWall이 서로 다른 색 배정을 갖길 원하면 벽마다 다른 값 지정.\n" +
             "0(기본값)이면 슬롯 수가 같은 벽끼리 동일한 배정 — 그래도 Host/Client는 항상 일치함.")]
    [SerializeField] int colorSeedSalt = 0;

    [Header("색상 일치 — 멈춤")]
    [Tooltip("색상이 같으면 이 시간(초) 동안 벽 이동 정지")]
    [SerializeField] float pauseDuration = 2f;

    [Header("이벤트 (선택)")]
    [Tooltip("색상 일치 시 호출 (시각 피드백 등)")]
    public UnityEvent OnColorMatch;

    [Tooltip("색상이 바뀔 때 호출 (추가 시각 피드백용)")]
    public UnityEvent OnColorChanged;

    // MovingCorridor.cs 주석의 salt 목록과 겹치지 않는 ColorWall 전용 salt
    // (다른 파일의 salt: 0x050AD5E7, 0x43484153, 0x5716D000, 0x4D4F5554, 0x5B1DE000, 0x52554E52)
    const int ColorSeedBaseSalt = unchecked((int)0x434F4C57);

    // Owner 보고 RPC가 멈춤 배포보다 먼저 여러 번 나가지 않게 — 왕복 한 번보다 넉넉하면 된다.
    const float MatchReportCooldown = 0.25f;

    WallColorType _wallColor;
    bool      _isPaused;
    Coroutine _pauseCoroutine;
    Coroutine _scheduleCoroutine;

    // ── 멈춤 네트워크 동기 ────────────────────────────────────────
    // StageNetworkState 릴레이가 벽을 int ID로 지목한다(ArrowTrap·DropTrap과 같은 레지스트리).
    static readonly SceneStableRegistry<ColorWall> _registry = new SceneStableRegistry<ColorWall>();
    int    _netIndex = -1;
    float  _nextMatchReportTime;
    double _hostPauseUntil;   // Host 레인: 이 서버 시각 전까지는 멈춤을 다시 배포하지 않는다
    double _hostLastPauseStart = double.NegativeInfinity;   // Host 레인: 마지막으로 확정한 멈춤의 시작 서버 시각

    // 끼임 신고 시각보다 이만큼 앞서 시작한 멈춤까지는 "Client에 아직 도착 안 한 멈춤"으로 보고 끼임을 무효로 한다.
    // Client의 ServerTime은 Host보다 뒤처져 있어 멈춤 도착 전 끼임이 멈춤 시각 근처(앞뒤)로 찍힌다 — 그 오차 여유.
    // 멈춤 직후엔 벽이 제자리로 빠지는 중(AdvancingWall.pauseReturnDuration 0.5초)이라 그 벽으로 진짜 끼일 수 없으므로
    // 0.5초 안쪽이면 진짜 끼임을 잘못 살리지 않는다.
    const double CrushPauseWindow = 0.3;

    Renderer[]          _renderers;
    AdvancingWall       _advancingWall;
    WallMover           _wallMover;
    ContactDamage       _contactDamage;

    // ── 현재 논리 색 외부 읽기용 ──────────────────────────────────
    public WallColorType CurrentColor => _wallColor;

    /// <summary>StageNetworkState 릴레이·끼임 신고가 이 벽을 지목하는 ID. 씬 배치가 아니면 -1.</summary>
    public int NetId => _netIndex;

    // ── 생명주기 ─────────────────────────────────────────────────

    void Awake()
    {
        _renderers      = GetComponentsInChildren<Renderer>(true);
        _advancingWall  = GetComponent<AdvancingWall>() ?? GetComponentInParent<AdvancingWall>();
        _wallMover      = GetComponent<WallMover>()     ?? GetComponentInParent<WallMover>();
        _contactDamage  = GetComponent<ContactDamage>();
        _netIndex       = _registry.Register(this);

        _wallColor = defaultColor;
        ApplyMaterial(defaultColor);
    }

    void OnDestroy() => _registry.Unregister(this, _netIndex);

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
        // 플레이어 색 슬롯을 GameSession 활성색으로 재매핑 (GameSession 없으면 원본 그대로).
        // NetworkSessionData.Seed 기반 rng를 넘겨 여분 슬롯 배정까지 Host/Client 동일하게 고정
        // (안 넘기면 GameSessionColorDistribution이 UnityEngine.Random을 써서 머신마다 갈라짐).
        var rng = new System.Random(NetworkSessionData.Seed ^ ColorSeedBaseSalt ^ colorSeedSalt);
        ColorChangeEvent[] effective = GameSessionWallColorRemap.RemapSchedule(colorSchedule, rng);
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

    // [멈춤 동기 2026-09-28] 멈춤은 Host가 정해 전 머신에 같은 서버 시각으로 배포한다.
    // 예전엔 머신마다 로컬 접촉으로 멈췄는데, Client에서는 Host 캐릭터 사본(kinematic)과 벽(kinematic)이
    // 닿아도 충돌 이벤트가 오지 않아(ContactPairsMode 기본값) Host가 맞춘 멈춤이 Client에서만 안 일어났다
    // → Client 화면에서만 벽이 계속 밀고 와 WallCrushKill(Owner 판정)로 죽었다.
    // Host: 자기 화면의 모든 캐릭터 접촉으로 바로 확정(예전 Host 타이밍 그대로).
    // Client: 자기 캐릭터 접촉만 Host에 보고 — 남의 캐릭터는 그 캐릭터의 Owner가 보고한다.
    void HandleContact(Collider other)
    {
        Player p = other.GetComponent<Player>();
        if (p == null || p.IsDead) return;
        if (!IsColorMatch(p)) return;

        // 멈춤 경로는 Host 배포 하나뿐 — 릴레이가 아직 준비 안 됐으면 이 머신에서만 멈추지 않고 무시한다
        // (로컬로 멈추면 그 머신만 멈추는 원래 버그가 재현된다).
        var nm  = NetworkManager.Singleton;
        var sns = StageNetworkState.Instance;
        if (nm == null || !nm.IsListening || sns == null || !sns.IsSpawned || _netIndex < 0) return;

        if (nm.IsServer)
        {
            HostTryPause(p);
            return;
        }

        if (!p.isOwnerControlled) return;
        if (_isPaused || Time.time < _nextMatchReportTime) return;

        var netObj = p.GetComponent<NetworkObject>();
        if (netObj == null) return;

        _nextMatchReportTime = Time.time + MatchReportCooldown;
        sns.ReportColorWallMatchServerRpc(_netIndex, netObj.NetworkObjectId);
    }

    /// <summary>StageNetworkState 릴레이(Host 레인): Client Owner의 색 일치 보고를 받는다.</summary>
    public static void OnMatchReportedOnHost(int wallId, Player p)
    {
        ColorWall w = _registry.Get(wallId);
        if (w == null || p == null || p.IsDead) return;
        // Host 화면 기준 지금 이 벽 색이 그 플레이어 색일 때만 — 색이 바뀐 뒤 늦게 도착한 보고는 버린다.
        if (!w.IsColorMatch(p)) return;
        w.HostTryPause(p);
    }

    void HostTryPause(Player p)
    {
        double now = NetTimeDouble();
        if (now < _hostPauseUntil) return;   // 이미 멈춤을 배포했다(보고·접촉이 겹쳐 들어온 것)
        _hostPauseUntil      = now + Mathf.Max(pauseDuration, 0f);
        _hostLastPauseStart  = now;

        NetLog.Transition(nameof(ColorWall), "ColorWallPause",
            $"wall={name} id={_netIndex} color={_wallColor} player={p.playerColorType} start={now:F2}");
        StageNetworkState.Instance.BroadcastColorWallPause(_netIndex, now);
    }

    /// <summary>StageNetworkState.PauseColorWallClientRpc 수신(Host 포함 전 머신). 늦게 받은 만큼 멈춤을 줄여 끝나는 시각을 맞춘다.</summary>
    public static void ApplyPauseById(int wallId, double startServerTime)
    {
        ColorWall w = _registry.Get(wallId);
        if (w == null) return;

        float late = Mathf.Max(0f, (float)(NetTimeDouble() - startServerTime));
        w.StartPause(Mathf.Max(0f, w.pauseDuration - late));
    }

    /// <summary>
    /// Host 레인(NetworkPlayerSetup.ReportCrushDeathServerRpc): 이 벽이 끼임 시각 무렵 이미 멈춤 확정이었는지.
    /// true면 그 끼임은 "Host가 멈춘 벽이 Client에서 아직 안 멈춰 보인 사이"에 난 것이라 무효다.
    /// 콜라이더를 다시 계산하지 않는다 — Host는 자기가 확정한 멈춤 시각과 신고 시각만 비교한다.
    /// </summary>
    public static bool IsCrushVoidedByPause(int wallId, double crushServerTime)
    {
        ColorWall w = _registry.Get(wallId);
        return w != null && w._hostLastPauseStart >= crushServerTime - CrushPauseWindow;
    }

    void StartPause(float duration)
    {
        if (_pauseCoroutine != null) StopCoroutine(_pauseCoroutine);
        _pauseCoroutine = StartCoroutine(PauseRoutine(duration));
        OnColorMatch?.Invoke();
    }

    /// <summary>이 플레이어 색과 지금 벽 색이 일치하는지. Default(휴지 회색)는 항상 false.
    /// WallCrushKill이 "두 벽 모두 내 색 아님" 판정에 쓴다.</summary>
    public bool IsColorMatch(Player p)
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

    IEnumerator PauseRoutine(float duration)
    {
        _isPaused = true;
        _contactDamage?.Deactivate();

        _advancingWall?.PauseTemporarily(duration);
        _wallMover?.ResetToStart();

        yield return new WaitForSeconds(duration);

        _wallMover?.Activate();

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
            float startTime = NetTime();

            // 배열을 순서대로 순회 (atSeconds 오름차순 권장)
            foreach (var evt in events)
            {
                float waitUntil = startTime + evt.atSeconds;
                float remaining = waitUntil - NetTime();
                if (remaining > 0f)
                    yield return new WaitForSeconds(remaining);

                SetColor(evt.color);
            }

            if (!loopSchedule) yield break;

            // 다음 주기 시작까지 대기
            float periodEnd = startTime + schedulePeriod;
            float waitForPeriod = periodEnd - NetTime();
            if (waitForPeriod > 0f)
                yield return new WaitForSeconds(waitForPeriod);

            // 주기 시작 시 defaultColor로 복귀
            SetColor(defaultColor);
        }
    }

    /// <summary>
    /// 자유런(트리거 없이 씬 시작 즉시 재생) 스케줄용 시간 소스.
    /// 각 머신이 ServerTime만 폴링하면 결정론적 (WallMover.ScheduleRoutine과 동일 원칙).
    /// </summary>
    static float NetTime()
    {
        var nm = NetworkManager.Singleton;
        return nm != null ? (float)nm.ServerTime.Time : Time.time;
    }

    static double NetTimeDouble()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening ? nm.ServerTime.Time : Time.timeAsDouble;
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
            WallColorType.Purple => PlayerColorUtil.GetUniqueColor(PlayerColorType.Purple),
            WallColorType.Green  => Color.green,
            WallColorType.Yellow => Color.yellow,
            _                    => Color.gray
        };
        c.a = 0.4f;
        Gizmos.color = c;
        Gizmos.DrawWireCube(transform.position, transform.lossyScale);
    }
}
