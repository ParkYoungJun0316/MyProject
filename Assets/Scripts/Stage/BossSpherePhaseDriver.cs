using Unity.Netcode;
using UnityEngine;

/// <summary>
/// T.Boss 전용 — 식도 끝 Sphere(천장)의 페이즈별 하강 시계를 구동한다.
/// CoopStageAudit.T.md §H.4 "T.Boss Sphere 컨트롤러" / §6 참고.
///
/// UnityEvent는 인자 4개짜리 메서드를 Inspector에서 직접 못 물기 때문에
/// AdvancingWall.RunOnce(4 float)를 감싸는 래퍼가 필요하다 —
/// 다만 인자 1개(int)는 Inspector에서 값 지정이 되므로, 페이즈 인덱스는 씬에서 명시한다.
///
/// [왜 내부 카운터를 쓰지 않는가 — 2026-09-10 리뷰]
///  "호출될 때마다 ++" 방식은 Host/Client에서 호출 횟수가 갈라지면 그대로 desync가 된다:
///   ① PhaseManager.EnterPhaseOnClient()는 건너뛴 중간 페이즈의 onPhaseEnter를 재생하지 않는다.
///   ② RunOnce()는 이동 중·색 정지 중이면 조용히 무시되는데 카운터는 그대로 올라간다.
///  그래서 인덱스를 씬에서 못 박고(AdvanceToCheckpoint(int)), 상태는 "지금 어느 칸인지"만 든다.
///
/// [왜 onPhaseComplete에 스냅을 걸지 않는가 — 2026-09-10 리뷰]
///  PhaseData.onPhaseComplete는 Host 레인에서만 발동한다(PhaseManager.PhaseComplete).
///  Client는 그 이벤트를 못 받으므로 이전 엔트리가 계속 살아 있고, 그 상태에서 다음 RunOnce()가
///  무시돼 Sphere 위치가 영구히 어긋났다. 그래서 스냅을 이 컴포넌트가 "다음 페이즈 진입 시"
///  직접 수행한다 — onPhaseEnter는 Host/Client 양쪽에서 발동하므로 두 머신이 같은 경로를 탄다.
///
/// [흐름]
///  1. P1 시작: StageStartGate.OnCountdownComplete → AdvanceToCheckpoint(0)
///     (P1을 phase 0의 onPhaseEnter에 물면 PhaseManager.Start()가 씬 로드 즉시 EnterPhase(0)을
///      호출하므로 카운트다운 전에 시계가 돌아버린다 — BossFightObjective 주석과 동일한 이유)
///  2. P2~P4: 각 PhaseData.onPhaseEnter → AdvanceToCheckpoint(1 / 2 / 3)
///     : 진입 시 이 칸이 시작하는 고정 앵커(체크포인트)로 스냅한 뒤, 그 앵커에서 바닥까지
///       남은 거리를 새로 하강시킨다(2026-09-11 재설계, 아래 [체크포인트 모델] 참고).
///  3. 페이즈 시간 초과 = 하강이 바닥까지 자연 완료 → sphereWall.OnAdvanceCompleted
///     → HandlePhaseTimeout() → 전원 즉사(§11 사망→씬 리로드 문으로 병합).
///  4. 보스 격파: PhaseManager.onAllPhasesComplete → StopClock() (제자리 정지)
///
/// [체크포인트 모델 — 2026-09-11 재설계, "거의 안 움직인다" 체감 문제 대응]
///  이전엔 각 페이즈가 checkpointDistances[i]만큼(예: 전체÷4)만 내려가고, 클리어하면 그 작은
///  구간의 끝점으로 스냅했다 — 거대한 Sphere가 90초에 전체의 25%만 움직이니 체감상 거의 정지.
///  지금은 각 페이즈의 목적지가 항상 "바닥"(=전체 하강거리, 닿으면 전멸)이고, 클리어하면 그 순간
///  위치가 아니라 "그 페이즈가 시작한 체크포인트"(=이전 체크포인트들의 누적 거리)로 위로 스냅한다.
///  그래서 체크포인트 절대 위치는 예전과 동일(전체÷4 지점들)하지만, 페이즈 진행 중 실제로 움직이는
///  거리는 "그 체크포인트부터 바닥까지 남은 전체" — P1은 전체 거리를, P4는 마지막 1/4만(그전과 동일,
///  마지막 칸은 원래도 남은 전체=1/4였으므로). CompleteCurrentEntryNow()(엔트리 끝점 스냅)로는
///  이걸 못 하므로(끝점=바닥이라 클리어해도 바닥으로 스냅됨) AdvancingWall.SnapToDistance(절대 거리)를
///  새로 추가해 씀. HandlePhaseTimeout()도 이제 실제로 Sphere가 바닥/플레이어 높이에 닿는 순간과
///  일치한다(이전엔 25%만 내려온 채로 "시간 초과=전멸"이 떠서 연출과 판정이 어긋났었다).
///
/// [P3 색 히트 → 정지 → 재개]
///  히트가 확정되면 AdvancingWall.PauseTemporarily()가 진행 중인 하강 엔트리를
///  죽이고 이번 페이즈 시작 지점(=이전 체크포인트)까지 밀어 올린 뒤 멈춘다. 재개해 줄 주체가
///  없으면(Sphere는 scheduleOnStart=false라 ScheduleRoutine이 안 돈다) 그 페이즈 시계가 영구히
///  멈춘다 — 그래서 Update()가 정지가 끝난 뒤 Host가 정한 재개 시각으로 같은 하강을 다시 발행한다.
///  결과: 색을 맞출 때마다 그 페이즈 하강이(2026-09-11부터는 "바닥까지 전체"가) 처음부터 다시
///  = 시간을 버는 행위(§6 "반복해 버틴다"). P3의 실제 종료는 누적 히트 횟수
///  (requiredHitCounts[2])가 담당하므로 무한정 늘어지지 않는다.
///
/// [히트 네트워크 모델 — 2026-09-17 Steam 2~4인 실기 버그 수정]
///  예전엔 ColorWall.HandleContact가 머신마다 로컬 물리로 정지·색 전환을 독자 실행하고, Sphere 색은
///  같은 오브젝트의 WallLineRandomizer가 씬 로드 시점 기준 로컬 타이머로 칠했다. 그 결과
///   ① 색이 P1 초반에 한 번 정해진 뒤 바뀌지 않았고(하강이 끊김 없이 이어져 Randomizer가 복귀 시점을 못 봄),
///      그 색도 머신마다 달랐다.
///   ② Client는 원격 플레이어가 kinematic이라 남의 히트를 못 봤고, 히트 카운트는 Host 물리가 본 충돌만 셌다.
///  지금은:
///   · 색 = requiredHitCounts[칸] > 0인 칸에서만, (시드, 칸, 히트 번호)로 전 머신이 같은 값을 계산.
///     그 외 칸은 Default(일치 불가).
///   · 맞힌 본인(Owner)의 ColorWall만 OnLocalOwnerColorMatch()로 보고 → StageNetworkState.ReportBossSphereHit
///     → Host TryAcceptHitFromHost()가 칸·번호·색을 검증해 번호 +1 → NV로 전원이 같은 정지·색을 재생.
///   · 정지 중엔 Default 색(맞힐 수 없음), 정지가 끝나 하강을 재발행할 때 다음 색을 칠한다.
///
/// [하강 시계 동기화 — 2026-09-18, MovingCorridor와 같은 버그 클래스]
///  예전 하강은 AdvancingWall.RunOnce(로컬 경과 시간 누적)였고 시작도 각 머신이 이벤트를 받은 시각이라,
///  Client는 늦게 출발하고 프레임 히치로 버려진 시간이 100~200초 하강 내내 남았다. 히트 후 재개도
///  각자 로컬 정지가 끝난 시각이었다. 지금은 Host가 하강 시작 서버 시각(칸 진입 = 그 순간, 히트 후 =
///  지금 + 복귀 시간 + 정지 시간)을 BossSphereHitState.descentStartServerTime에 실어 보내고,
///  전 머신이 AdvancingWall.RunOnceSynced(진행도 = 서버 시각 경과 / 시간제한)로 하강한다.
///  시간 초과(바닥 도달) 판정 시각도 Host/Client가 같다.
///
/// [Inspector 연결]
///  sphereWall            : Sphere에 붙은 AdvancingWall (같은 오브젝트의 ColorWall을 자동 탐색해 네트워크 히트 모드로 전환.
///                          Sphere에 WallLineRandomizer를 두지 말 것 — 색을 이 컴포넌트가 전담한다)
///                          (moveDirection=로컬 하강 방향, scheduleOnStart=false,
///                           activateOnStart=false, maxTotalAdvance=0 — 0이 아니면 RunEntry가
///                           OnMaxReached로 빠져 하강 자체가 무효가 된다)
///  checkpointDistances[] : 4칸, 전체 하강거리 ÷ 4를 동일하게 (4등분 확정).
///                          누적합이 체크포인트 절대 위치가 된다 — 위 [체크포인트 모델] 참고.
///  phaseTimeLimits[]     : 4칸, 페이즈별 시간제한(개별 튜닝).
///                          P3는 시간제한이 아니라 누적 히트 횟수(requiredHitCounts[2])로 클리어하므로
///                          시간 안에 못 채우면 자연히 바닥까지 하강해 HandlePhaseTimeout()으로 전멸한다.
///
/// [씬 배선]
///  StageStartGate.OnCountdownComplete → AdvanceToCheckpoint(0)
///  P2/P3/P4 각 PhaseData.onPhaseEnter → AdvanceToCheckpoint(1 / 2 / 3)
///  PhaseManager.onAllPhasesComplete   → StopClock()
///  sphereWall.OnAdvanceCompleted      → HandlePhaseTimeout()
///  ※ onPhaseComplete에는 아무것도 걸지 않는다 (Host 전용 레인 — 위 리뷰 참고)
///  ※ Sphere ColorWall.OnColorMatch에도 아무것도 걸지 않는다 (네트워크 히트 모드에선 발동 안 함)
/// </summary>
public class BossSpherePhaseDriver : MonoBehaviour
{
    public static BossSpherePhaseDriver Instance { get; private set; }

    // 다른 파일의 salt: 0x050AD5E7, 0x43484153, 0x5716D000, 0x4D4F5554, 0x5B1DE000, 0x52554E52, 0x434F4C57(ColorWall), 0x574C525A(WallLineRandomizer)
    const int HitColorSalt = unchecked((int)0x53504852);

    /// <summary>같은 히트 번호로 재보고하기까지의 최소 간격(초). OnCollisionStay 매 프레임 스팸 방지 +
    /// Host가 색 변경 직후 한두 틱 늦게 본 경우의 재시도.</summary>
    const float ReportRetryInterval = 0.3f;

    [Header("연결")]
    [Tooltip("식도 끝 Sphere에 붙은 AdvancingWall. moveDirection=로컬 하강 방향,\n" +
             "scheduleOnStart=false, activateOnStart=false, maxTotalAdvance=0으로 설정할 것 —\n" +
             "이 컴포넌트가 RunOnceSynced로만 구동한다.")]
    [SerializeField] AdvancingWall sphereWall;

    [Header("체크포인트 (전체 하강거리를 4등분, 확정)")]
    [Tooltip("체크포인트 구간 폭(m). 4칸 = 전체 하강거리 ÷ 4를 동일하게.\n" +
             "이 값들의 누적합이 각 페이즈가 시작하는 절대 거리(체크포인트 앵커)가 된다.\n" +
             "실제 그 페이즈에서 하강하는 거리는 '전체 - 누적'(바닥까지 남은 전체) — " +
             "값 자체가 그 페이즈의 하강 거리는 아니다(2026-09-11).")]
    [SerializeField] float[] checkpointDistances = new float[4];

    [Tooltip("페이즈별 시간제한(초). 페이즈마다 다르게 개별 튜닝.\n" +
             "이 시간 안에 페이즈를 못 깨면 Sphere가 바닥까지 다 내려와\n" +
             "OnAdvanceCompleted → HandlePhaseTimeout()으로 전멸한다(2026-09-11 — 예전엔 다음\n" +
             "체크포인트까지만 내려왔지만, 지금은 목적지가 항상 바닥).")]
    [SerializeField] float[] phaseTimeLimits = new float[4];

    [Header("승리조건 — 색 히트 횟수 (2026-09-10)")]
    [Tooltip("페이즈별 클리어에 필요한 ColorWall 색 일치 횟수. 0이면 이 페이즈는 히트 카운트로 안 깸\n" +
             "(다른 방식 — ReachZoneObjective / PhaseSurviveChallenge 등 — 로 클리어).\n" +
             "지금은 인원수 무관 고정값 하나로 임시 배선. 1~4인 난이도 분리는 나중에.")]
    [SerializeField] int[] requiredHitCounts = new int[4];

    [Tooltip("히트 카운트 목표 도달 시 NotifyPhaseCleared()를 보낼 보스 오브젝티브.")]
    [SerializeField] BossFightObjective bossFightObjective;

    [Tooltip("히트 칸(requiredHitCounts > 0)에서 Sphere에 칠할 색 후보. 비활성 플레이어 색은 자동 제외.\n" +
             "직전 색과 같은 색은 연속으로 나오지 않는다.")]
    [SerializeField] ColorWall.WallColorType[] hitColorPool =
    {
        ColorWall.WallColorType.Black,  ColorWall.WallColorType.White,
        ColorWall.WallColorType.Blue,   ColorWall.WallColorType.Purple,
        ColorWall.WallColorType.Green,  ColorWall.WallColorType.Yellow,
    };

    /// <summary>지금 시계가 돌고 있는 칸. -1이면 시계 정지 상태.</summary>
    int  _activeIndex = -1;

    /// <summary>[전 머신] 현재 번호의 하강 시작 서버 시각(Host 확정값). -1 = 아직 수신 전.</summary>
    double _descentStart = -1d;
    /// <summary>현재 번호의 하강을 이미 발행했는지 — 자연 완료(시간 초과) 뒤 재발행 방지.</summary>
    bool   _descentIssued;

    /// <summary>[Host] 현재 활성 칸에서 확정된 색 히트 수. AdvanceToCheckpoint()가 칸이 바뀔 때마다 0으로 리셋.</summary>
    int _hitCount;

    /// <summary>[전 머신] 이 머신이 재생까지 끝낸 히트 번호. 색 계산과 보고 번호의 기준.</summary>
    int _appliedSerial;

    int   _reportedSerial = -1;
    float _nextReportTime;

    ColorWall         _sphereColor;
    StageNetworkState _netState;

    /// <summary>전체 하강거리 = 체크포인트 거리의 합. UI 진행도 분모의 SSOT.</summary>
    public float TotalDistance
    {
        get
        {
            if (checkpointDistances == null) return 0f;
            float sum = 0f;
            foreach (float d in checkpointDistances) sum += d;
            return sum;
        }
    }

    /// <summary>Sphere 진행도(0~1). UI가 이 값을 읽는다.</summary>
    public float Progress01
    {
        get
        {
            float total = TotalDistance;
            if (sphereWall == null || total <= 0f) return 0f;
            return Mathf.Clamp01(sphereWall.LiveAdvancedDistance / total);
        }
    }

    // ── Unity ────────────────────────────────────────────────────

    void Awake()
    {
        Instance = this;

        if (sphereWall != null)
        {
            _sphereColor = sphereWall.GetComponent<ColorWall>();
            _sphereColor?.SetNetworkHitHandler(OnLocalOwnerColorMatch);

            var randomizer = sphereWall.GetComponent<WallLineRandomizer>();
            if (randomizer != null && randomizer.enabled)
                Debug.LogError(
                    $"[BossSpherePhaseDriver] '{sphereWall.name}'에 WallLineRandomizer가 켜져 있음 — " +
                    "Sphere 색·하강을 로컬 타이머로 덮어써 Host/Client가 갈라진다. 컴포넌트를 제거할 것.", this);
        }

        if (checkpointDistances != null && phaseTimeLimits != null
            && checkpointDistances.Length != phaseTimeLimits.Length)
            Debug.LogError(
                $"[BossSpherePhaseDriver] '{name}' 배열 길이 불일치 — " +
                $"checkpointDistances({checkpointDistances.Length}) != phaseTimeLimits({phaseTimeLimits.Length}). " +
                "두 배열을 페이즈 수만큼 같은 길이로 채울 것.", this);
    }

    void OnDestroy()
    {
        if (_netState != null) _netState.OnBossSphereHitChanged -= HandleBossSphereHitChanged;
        _sphereColor?.SetNetworkHitHandler(null);
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        EnsureNetSubscribed();

        // 시작 시각을 받았고 정지가 끝났으면 이번 번호의 하강을 한 번만 발행한다.
        // 번호당 1회(_descentIssued)라 자연 완료(=시간 초과) 뒤 무한 재발행되지 않는다.
        TryIssueDescent();
    }

    /// <summary>StageNetworkState는 씬 NetworkObject라 Awake 순서가 보장되지 않아 늦게 구독한다.
    /// 사망 리로드로 인스턴스가 바뀌면 새 인스턴스로 다시 붙는다.</summary>
    void EnsureNetSubscribed()
    {
        var sns = StageNetworkState.Instance;
        if (sns == _netState) return;
        if (_netState != null) _netState.OnBossSphereHitChanged -= HandleBossSphereHitChanged;
        _netState = sns;
        if (_netState != null) _netState.OnBossSphereHitChanged += HandleBossSphereHitChanged;
    }

    // ── 씬 배선용 (Inspector) ────────────────────────────────────

    /// <summary>
    /// checkpointIndex 칸의 하강을 시작. 이 칸이 시작하는 고정 앵커(= 이전 체크포인트들의 누적 거리)로
    /// 즉시 스냅한 뒤, 그 앵커에서 바닥까지 남은 거리를 새로 하강시킨다(2026-09-11 재설계 — 목적지가
    /// 항상 바닥이라 이전 엔트리의 끝점(CompleteCurrentEntryNow)이 아니라 임의 절대 거리 스냅
    /// (SnapToDistance)을 쓴다. Host·Client 모두 이 경로를 타므로 onPhaseComplete 배선이 필요 없다).
    ///
    /// P1은 StageStartGate.OnCountdownComplete → AdvanceToCheckpoint(0),
    /// P2~P4는 각 PhaseData.onPhaseEnter → AdvanceToCheckpoint(1 / 2 / 3)에 연결.
    /// </summary>
    public void AdvanceToCheckpoint(int checkpointIndex)
    {
        if (sphereWall == null) return;

        if (checkpointDistances == null
            || checkpointIndex < 0 || checkpointIndex >= checkpointDistances.Length)
        {
            Debug.LogError(
                $"[BossSpherePhaseDriver] '{name}' 체크포인트 인덱스 {checkpointIndex} 범위 밖 " +
                $"(checkpointDistances 길이 {checkpointDistances?.Length ?? 0}). 씬 배선의 인자 값을 확인할 것.",
                this);
            return;
        }

        // 이 칸이 시작하는 고정 앵커 = 이전 체크포인트들의 누적 거리. 진행 중이던 낙하가 어디까지
        // 왔었든(=바닥에 얼마나 가까워졌든) 이 앵커로 위로 되돌린다 — "클리어하면 다음 체크포인트로
        // 즉시 스냅"(§H.4) 규칙 그대로, 스냅 대상만 엔트리 끝점이 아니라 이 앵커로 바뀐 것.
        float anchorDistance = CumulativeBefore(checkpointIndex);
        sphereWall.SnapToDistance(anchorDistance);

        _activeIndex    = checkpointIndex;
        _hitCount       = 0; // 새 칸 = 히트 카운트 승리조건도 처음부터
        _appliedSerial  = 0;
        _reportedSerial = -1;
        _descentStart   = -1d;
        _descentIssued  = false;

        ApplyActiveColor();

        EnsureNetSubscribed();
        if (!IsClientOnly())
        {
            // Host: 이 칸의 하강 시작 시각 = 지금. 로컬 콜백(HandleBossSphereHitChanged)으로 Host도 같은 경로.
            _netState?.SetBossSphereHit(checkpointIndex, 0, NetTime());
        }
        else if (_netState != null)
        {
            // Client: 이 칸 상태가 onPhaseEnter보다 먼저 도착해 있었을 수 있다(NV 간 도착 순서 무보장).
            ApplyNetState(_netState.BossSphereHit);
        }

        TryIssueDescent();
    }

    /// <summary>
    /// [전 머신] 이 머신의 Owner 캐릭터가 Sphere에 색을 맞춰 닿았을 때 ColorWall이 호출.
    /// 로컬에선 아무것도 재생하지 않고 Host에 보고만 한다 — 정지·색 전환은 Host 확정 후
    /// HandleBossSphereHitChanged()에서 전 머신이 같이 재생한다.
    /// </summary>
    void OnLocalOwnerColorMatch(Player p)
    {
        if (_activeIndex < 0 || !IsHitCheckpoint(_activeIndex)) return;
        if (sphereWall == null || sphereWall.IsPausedByColor) return;
        if (_reportedSerial == _appliedSerial && Time.time < _nextReportTime) return;

        EnsureNetSubscribed();
        var netObj = p.GetComponent<NetworkObject>();
        if (_netState == null || netObj == null || !netObj.IsSpawned) return;

        _reportedSerial = _appliedSerial;
        _nextReportTime = Time.time + ReportRetryInterval;
        _netState.ReportBossSphereHit(netObj.NetworkObjectId, _activeIndex, _appliedSerial);
    }

    /// <summary>
    /// [Host 전용] Owner 보고 검증 후 히트 확정. StageNetworkState.ReportBossSphereHitServerRpc에서 호출.
    /// 칸·번호가 현재와 다르면(이미 다른 사람 히트가 확정됨 / 이전 칸의 늦은 보고) 버리고,
    /// 정지 중이거나 Host가 보는 Sphere 색과 그 플레이어 색이 다르면 버린다.
    /// 목표를 채우면 BossFightObjective.NotifyPhaseCleared() — 이미 채운 뒤의 보고는 무시(페이즈 스킵 방지).
    /// </summary>
    public void TryAcceptHitFromHost(Player p, int checkpointIndex, int hitSerial)
    {
        if (IsClientOnly()) return;
        if (p == null || p.IsDead) return;
        if (_activeIndex < 0 || checkpointIndex != _activeIndex || hitSerial != _hitCount) return;

        int required = RequiredHits(_activeIndex);
        if (required <= 0 || _hitCount >= required) return;
        if (sphereWall == null || sphereWall.IsPausedByColor) return;
        if (_sphereColor == null || !_sphereColor.Matches(p)) return;

        _hitCount++;
        // 재개 시각 = 지금 + 원점 복귀 + 정지. 전 머신이 이 시각부터 하강을 다시 센다.
        double resumeAt = NetTime() + sphereWall.PauseReturnDuration + PauseDuration;
        EnsureNetSubscribed();
        _netState?.SetBossSphereHit(_activeIndex, _hitCount, resumeAt); // Host 로컬 콜백으로 정지도 즉시 재생

        if (_hitCount >= required)
            bossFightObjective?.NotifyPhaseCleared();
    }

    void HandleBossSphereHitChanged(BossSphereHitState state) => ApplyNetState(state);

    /// <summary>
    /// [전 머신] Host 확정 상태 반영.
    ///  · 번호가 올라감 → 정지 재생 + 색 끔(정지 중 Default) + 새 재개 시각 기억.
    ///  · 같은 번호인데 아직 시작 시각이 없음 → 칸 진입 시작 시각 기억.
    /// 실제 하강 발행은 TryIssueDescent()가 정지가 끝난 뒤 한다.
    /// </summary>
    void ApplyNetState(BossSphereHitState state)
    {
        if (_activeIndex < 0 || state.checkpointIndex != _activeIndex) return;

        if (state.hitSerial > _appliedSerial)
        {
            _appliedSerial = state.hitSerial;
            _descentStart  = state.descentStartServerTime;
            _descentIssued = false;
            _sphereColor?.ResetToDefault();
            if (sphereWall != null) sphereWall.PauseTemporarily(PauseDuration);
            return;
        }

        if (state.hitSerial == _appliedSerial && _descentStart < 0d)
            _descentStart = state.descentStartServerTime;
    }

    /// <summary>시작 시각 수신 + 정지 종료 + 미발행이면 이번 번호의 하강을 발행하고 그 번호의 색을 칠한다.</summary>
    void TryIssueDescent()
    {
        if (_activeIndex < 0 || sphereWall == null) return;
        if (_descentIssued || _descentStart < 0d) return;
        if (sphereWall.IsPausedByColor || sphereWall.IsMoving) return;

        _descentIssued = true;
        ApplyActiveColor(); // 정지 중 Default였던 색을 이번 번호 색으로
        RunActiveCheckpoint();
    }

    float PauseDuration => _sphereColor != null ? _sphereColor.PauseDuration : 2f;

    /// <summary>
    /// 시계 정지 (제자리). PhaseManager.onAllPhasesComplete에 연결 —
    /// 이 이벤트는 StageNetworkState.NotifyAllPhasesComplete 브릿지로 Client에서도 발동한다.
    /// 마지막 칸의 목표는 바닥이라 여기서 스냅하면 승리 순간에 Sphere가 바닥에 닿는 것처럼
    /// 보이므로, 스냅이 아니라 현 위치 정지(Deactivate)를 쓴다.
    /// </summary>
    public void StopClock()
    {
        _activeIndex = -1;
        _sphereColor?.ResetToDefault();
        sphereWall?.Deactivate();
    }

    /// <summary>
    /// 페이즈 시간 초과(=하강 자연 완료) 처리. sphereWall.OnAdvanceCompleted에 연결.
    /// 제때 깨서 CompleteCurrentEntryNow()로 끊긴 경우는 OnAdvanceCompleted 자체가 발동하지 않으므로
    /// "정상 클리어"와 구분할 별도 판정이 필요 없다.
    /// </summary>
    public void HandlePhaseTimeout()
    {
        // 시계가 안 도는 상태(격파 후 / 이미 처리됨)의 잔여 이벤트는 무시 —
        // 클리어와 자연 완료가 같은 프레임에 겹쳤을 때 승리 순간 전멸하는 것을 막는다.
        if (_activeIndex < 0) return;
        if (PhaseManager.Instance != null && PhaseManager.Instance.AllPhasesComplete) return;

        _activeIndex = -1;

        // 전원 즉사 → 실패 통보로 교체(ReviveSystemDesign.md §6). 자동 부활이 들어오면서
        // "전원 즉사"는 전원이 1초 뒤 살아나 실패가 조용히 무시될 수 있는 경로가 됐다.
        // FailStageFromServer에 Host 가드가 내장돼 있어 호출부 가드는 불필요하다.
        StageNetworkState.Instance?.FailStageFromServer("T.Boss 페이즈 시간 초과");
    }

    // ── 내부 ─────────────────────────────────────────────────────

    static bool IsClientOnly()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && !nm.IsServer;
    }

    static double NetTime()
    {
        var nm = NetworkManager.Singleton;
        return nm != null ? nm.ServerTime.Time : Time.timeAsDouble;
    }

    int RequiredHits(int checkpointIndex) =>
        (requiredHitCounts != null && checkpointIndex >= 0 && checkpointIndex < requiredHitCounts.Length)
            ? requiredHitCounts[checkpointIndex]
            : 0;

    bool IsHitCheckpoint(int checkpointIndex) => RequiredHits(checkpointIndex) > 0;

    /// <summary>활성 칸이 히트 칸이면 현재 번호의 색, 아니면 Default(일치 불가)를 칠한다.</summary>
    void ApplyActiveColor()
    {
        if (_sphereColor == null) return;
        if (_activeIndex >= 0 && IsHitCheckpoint(_activeIndex))
            _sphereColor.SetColor(HitColorFor(_activeIndex, _appliedSerial));
        else
            _sphereColor.ResetToDefault();
    }

    /// <summary>
    /// (시드, 칸, 히트 번호) → 색. 전 머신이 같은 입력으로 같은 값을 얻으므로 색 자체는 전송하지 않는다.
    /// 번호 0부터 차례로 뽑으며 직전 색은 후보에서 뺀다(같은 색 연속 방지).
    /// </summary>
    ColorWall.WallColorType HitColorFor(int checkpointIndex, int hitSerial)
    {
        ColorWall.WallColorType[] pool = GameSessionWallColorRemap.FilterPool(hitColorPool);
        if (pool == null || pool.Length == 0) return ColorWall.WallColorType.Black;

        int prev = -1;
        for (int s = 0; s <= hitSerial; s++)
        {
            var rng = new System.Random(NetworkSessionData.Seed ^ HitColorSalt
                                        ^ (checkpointIndex * 0x2545F491)
                                        ^ (s * unchecked((int)0x9E3779B9)));
            if (prev < 0 || pool.Length == 1)
            {
                prev = rng.Next(0, pool.Length);
            }
            else
            {
                int pick = rng.Next(0, pool.Length - 1);
                prev = pick >= prev ? pick + 1 : pick;
            }
        }
        return pool[prev];
    }

    /// <summary>checkpointIndex 칸이 시작하는 절대 거리(= 그 앞 칸들의 checkpointDistances 합).
    /// AdvanceToCheckpoint()의 스냅 앵커, RunActiveCheckpoint()의 "바닥까지 남은 거리" 계산 공용.</summary>
    float CumulativeBefore(int checkpointIndex)
    {
        float sum = 0f;
        if (checkpointDistances == null) return sum;
        for (int i = 0; i < checkpointIndex && i < checkpointDistances.Length; i++)
            sum += checkpointDistances[i];
        return sum;
    }

    /// <summary>
    /// 지금 활성 칸(_activeIndex)의 앵커에서 바닥까지 남은 거리를 하강시킨다(2026-09-11 재설계).
    /// P3 색 히트로 정지가 풀려 Update()가 이걸 재호출할 때도 매번 이 "바닥까지 전체" 거리로 다시
    /// 낙하한다 — 맞출 때마다 위협이 처음부터 다시 다가오는 게 의도(§6 "반복해 버틴다").
    /// 진행도는 Host가 정한 _descentStart 기준 서버 시각으로 계산(RunOnceSynced) — 전 머신 동일.
    /// </summary>
    void RunActiveCheckpoint()
    {
        float anchorDistance = CumulativeBefore(_activeIndex);
        float remaining      = Mathf.Max(TotalDistance - anchorDistance, 0f);
        float timeLimit      = (phaseTimeLimits != null && _activeIndex < phaseTimeLimits.Length)
            ? phaseTimeLimits[_activeIndex]
            : 0f;

        if (timeLimit <= 0f)
            Debug.LogError($"[BossSpherePhaseDriver] '{name}' phaseTimeLimits[{_activeIndex}]가 0 — 하강이 즉시 끝나 전멸한다.", this);

        sphereWall.RunOnceSynced(remaining, timeLimit, _descentStart);
    }

    // ── 에디터 ───────────────────────────────────────────────────
#if UNITY_EDITOR
    [ContextMenu("테스트: 0번 칸부터 시작")]
    void Debug_StartFirst() => AdvanceToCheckpoint(0);

    [ContextMenu("테스트: 시계 정지")]
    void Debug_StopClock() => StopClock();
#endif
}
