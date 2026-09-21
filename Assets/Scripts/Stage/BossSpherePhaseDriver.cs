using Unity.Netcode;
using UnityEngine;

/// <summary>
/// T.Boss 전용 — 식도 끝 Sphere(천장)의 페이즈별 하강 시계를 구동한다.
/// CoopStageAudit.T.md §H.4 "T.Boss Sphere 컨트롤러" / §6 참고.
///
/// [2026-09-21 — P3 "Sphere에 색 맞춰 부딪히기" 전면 폐기]
///  Sphere는 이제 **순수한 시계**다. 어떤 페이즈도 Sphere와 상호작용하지 않는다.
///  같이 사라진 것: ColorWall 부착·색 히트 보고(StageNetworkState.ReportBossSphereHit)·
///  히트 카운트 승리조건(requiredHitCounts)·히트 색 추첨(hitColorPool)·히트 후 정지/재개.
///  폐기 이유는 멀미(튕김 발판 초속 55m/s 수직 발사)와 진행 곡선 부재(히트마다 앵커로 완전
///  복귀라 15회가 전부 동일 반복). ContactKnockback.VerticalUp 자체는 T3·T5가 쓰므로 남겨둔다 —
///  P3의 튕김 발판은 씬에서만 빼면 된다.
///  ⚠ 그 결과 **P3에는 지금 클리어 조건이 없다** — 새 P3 판이 정해질 때까지 P3는 시간 초과로만
///  끝난다. 이건 의도된 임시 상태다(사용자 확정 2026-09-21).
///
/// UnityEvent는 인자 4개짜리 메서드를 Inspector에서 직접 못 물기 때문에
/// AdvancingWall.RunOnceSynced를 감싸는 래퍼가 필요하다 —
/// 다만 인자 1개(int)는 Inspector에서 값 지정이 되므로, 페이즈 인덱스는 씬에서 명시한다.
///
/// [왜 내부 카운터를 쓰지 않는가 — 2026-09-10 리뷰]
///  "호출될 때마다 ++" 방식은 Host/Client에서 호출 횟수가 갈라지면 그대로 desync가 된다:
///   ① PhaseManager.EnterPhaseOnClient()는 건너뛴 중간 페이즈의 onPhaseEnter를 재생하지 않는다.
///   ② RunOnce()는 이동 중이면 조용히 무시되는데 카운터는 그대로 올라간다.
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
///     → HandlePhaseTimeout() → 스테이지 실패 통보.
///  4. 보스 격파: PhaseManager.onAllPhasesComplete → StopClock() (제자리 정지)
///
/// [체크포인트 모델 — 2026-09-11 재설계, "거의 안 움직인다" 체감 문제 대응]
///  이전엔 각 페이즈가 checkpointDistances[i]만큼(예: 전체÷4)만 내려가고, 클리어하면 그 작은
///  구간의 끝점으로 스냅했다 — 거대한 Sphere가 90초에 전체의 25%만 움직이니 체감상 거의 정지.
///  지금은 각 페이즈의 목적지가 항상 "바닥"(=전체 하강거리, 닿으면 실패)이고, 클리어하면 그 순간
///  위치가 아니라 "그 페이즈가 시작한 체크포인트"(=이전 체크포인트들의 누적 거리)로 위로 스냅한다.
///  그래서 체크포인트 절대 위치는 예전과 동일(전체÷4 지점들)하지만, 페이즈 진행 중 실제로 움직이는
///  거리는 "그 체크포인트부터 바닥까지 남은 전체" — P1은 전체 거리를, P4는 마지막 1/4만(그전과 동일,
///  마지막 칸은 원래도 남은 전체=1/4였으므로). 그래서 엔트리 끝점 스냅이 아니라
///  AdvancingWall.SnapToDistance(절대 거리)를 쓴다.
///
/// [하강 시계 동기화 — 2026-09-18, MovingCorridor와 같은 버그 클래스]
///  예전 하강은 AdvancingWall.RunOnce(로컬 경과 시간 누적)였고 시작도 각 머신이 이벤트를 받은 시각이라,
///  Client는 늦게 출발하고 프레임 히치로 버려진 시간이 100~200초 하강 내내 남았다. 지금은 Host가
///  하강 시작 서버 시각을 BossSphereDescentState.descentStartServerTime에 실어 보내고,
///  전 머신이 AdvancingWall.RunOnceSynced(진행도 = 서버 시각 경과 / 시간제한)로 하강한다.
///  시간 초과(바닥 도달) 판정 시각도 Host/Client가 같다.
///
/// [Inspector 연결]
///  sphereWall            : Sphere에 붙은 AdvancingWall
///                          (moveDirection=로컬 하강 방향, scheduleOnStart=false,
///                           activateOnStart=false, maxTotalAdvance=0 — 0이 아니면 RunEntry가
///                           OnMaxReached로 빠져 하강 자체가 무효가 된다)
///                          Sphere에 ColorWall / WallLineRandomizer를 두지 말 것 — 둘 다 로컬
///                          타이머·로컬 물리라 Host/Client 하강을 갈라놓는다.
///  checkpointDistances[] : 4칸, 전체 하강거리 ÷ 4를 동일하게 (4등분 확정).
///                          누적합이 체크포인트 절대 위치가 된다 — 위 [체크포인트 모델] 참고.
///  phaseTimeLimits[]     : 4칸, 페이즈별 시간제한(개별 튜닝).
///
/// [씬 배선]
///  StageStartGate.OnCountdownComplete → AdvanceToCheckpoint(0)
///  P2/P3/P4 각 PhaseData.onPhaseEnter → AdvanceToCheckpoint(1 / 2 / 3)
///  PhaseManager.onAllPhasesComplete   → StopClock()
///  sphereWall.OnAdvanceCompleted      → HandlePhaseTimeout()
///  ※ onPhaseComplete에는 아무것도 걸지 않는다 (Host 전용 레인 — 위 리뷰 참고)
/// </summary>
public class BossSpherePhaseDriver : MonoBehaviour
{
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
             "OnAdvanceCompleted → HandlePhaseTimeout()으로 스테이지 실패(2026-09-11 — 예전엔\n" +
             "다음 체크포인트까지만 내려왔지만, 지금은 목적지가 항상 바닥).")]
    [SerializeField] float[] phaseTimeLimits = new float[4];

    /// <summary>지금 시계가 돌고 있는 칸. -1이면 시계 정지 상태.</summary>
    int _activeIndex = -1;

    /// <summary>[전 머신] 현재 칸의 하강 시작 서버 시각(Host 확정값). -1 = 아직 수신 전.</summary>
    double _descentStart = -1d;
    /// <summary>현재 칸의 하강을 이미 발행했는지 — 자연 완료(시간 초과) 뒤 재발행 방지.</summary>
    bool   _descentIssued;

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
        if (sphereWall != null)
        {
            if (sphereWall.GetComponent<ColorWall>() != null)
                Debug.LogError(
                    $"[BossSpherePhaseDriver] '{sphereWall.name}'에 ColorWall이 붙어 있음 — " +
                    "Sphere 색 히트는 2026-09-21에 폐기됐다. 머신별 로컬 물리로 정지가 걸려 " +
                    "Host/Client 하강이 갈라지므로 컴포넌트를 제거할 것.", this);

            var randomizer = sphereWall.GetComponent<WallLineRandomizer>();
            if (randomizer != null && randomizer.enabled)
                Debug.LogError(
                    $"[BossSpherePhaseDriver] '{sphereWall.name}'에 WallLineRandomizer가 켜져 있음 — " +
                    "Sphere 하강을 로컬 타이머로 덮어써 Host/Client가 갈라진다. 컴포넌트를 제거할 것.", this);
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
        if (_netState != null) _netState.OnBossSphereDescentChanged -= HandleDescentChanged;
    }

    void Update()
    {
        EnsureNetSubscribed();

        // 시작 시각을 받았으면 이번 칸의 하강을 한 번만 발행한다.
        // 칸당 1회(_descentIssued)라 자연 완료(=시간 초과) 뒤 무한 재발행되지 않는다.
        TryIssueDescent();
    }

    /// <summary>StageNetworkState는 씬 NetworkObject라 Awake 순서가 보장되지 않아 늦게 구독한다.
    /// 사망 리로드로 인스턴스가 바뀌면 새 인스턴스로 다시 붙는다.</summary>
    void EnsureNetSubscribed()
    {
        var sns = StageNetworkState.Instance;
        if (sns == _netState) return;
        if (_netState != null) _netState.OnBossSphereDescentChanged -= HandleDescentChanged;
        _netState = sns;
        if (_netState != null) _netState.OnBossSphereDescentChanged += HandleDescentChanged;
    }

    // ── 씬 배선용 (Inspector) ────────────────────────────────────

    /// <summary>
    /// checkpointIndex 칸의 하강을 시작. 이 칸이 시작하는 고정 앵커(= 이전 체크포인트들의 누적 거리)로
    /// 즉시 스냅한 뒤, 그 앵커에서 바닥까지 남은 거리를 새로 하강시킨다(2026-09-11 재설계 — 목적지가
    /// 항상 바닥이라 이전 엔트리의 끝점이 아니라 임의 절대 거리 스냅(SnapToDistance)을 쓴다.
    /// Host·Client 모두 이 경로를 타므로 onPhaseComplete 배선이 필요 없다).
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
        // 즉시 스냅"(§H.4) 규칙 그대로.
        float anchorDistance = CumulativeBefore(checkpointIndex);
        sphereWall.SnapToDistance(anchorDistance);

        _activeIndex   = checkpointIndex;
        _descentStart  = -1d;
        _descentIssued = false;

        EnsureNetSubscribed();
        if (!IsClientOnly())
        {
            // Host: 이 칸의 하강 시작 시각 = 지금. 로컬 콜백(HandleDescentChanged)으로 Host도 같은 경로.
            _netState?.SetBossSphereDescent(checkpointIndex, NetTime());
        }
        else if (_netState != null)
        {
            // Client: 이 칸 상태가 onPhaseEnter보다 먼저 도착해 있었을 수 있다(NV 간 도착 순서 무보장).
            ApplyNetState(_netState.BossSphereDescent);
        }

        TryIssueDescent();
    }

    void HandleDescentChanged(BossSphereDescentState state) => ApplyNetState(state);

    /// <summary>[전 머신] Host 확정 상태 반영. 실제 하강 발행은 TryIssueDescent()가 한다.</summary>
    void ApplyNetState(BossSphereDescentState state)
    {
        if (_activeIndex < 0 || state.checkpointIndex != _activeIndex) return;
        if (_descentStart >= 0d) return;

        _descentStart = state.descentStartServerTime;
    }

    /// <summary>시작 시각을 받았고 아직 안 발행했으면 이번 칸의 하강을 발행한다.</summary>
    void TryIssueDescent()
    {
        if (_activeIndex < 0 || sphereWall == null) return;
        if (_descentIssued || _descentStart < 0d) return;
        if (sphereWall.IsMoving) return;

        _descentIssued = true;
        RunActiveCheckpoint();
    }

    /// <summary>
    /// 시계 정지 (제자리). PhaseManager.onAllPhasesComplete에 연결 —
    /// 이 이벤트는 StageNetworkState.NotifyAllPhasesComplete 브릿지로 Client에서도 발동한다.
    /// 마지막 칸의 목표는 바닥이라 여기서 스냅하면 승리 순간에 Sphere가 바닥에 닿는 것처럼
    /// 보이므로, 스냅이 아니라 현 위치 정지(Deactivate)를 쓴다.
    /// </summary>
    public void StopClock()
    {
        _activeIndex = -1;
        sphereWall?.Deactivate();
    }

    /// <summary>
    /// 페이즈 시간 초과(=하강 자연 완료) 처리. sphereWall.OnAdvanceCompleted에 연결.
    /// 제때 깨서 다음 칸으로 스냅된 경우는 OnAdvanceCompleted 자체가 발동하지 않으므로
    /// "정상 클리어"와 구분할 별도 판정이 필요 없다.
    /// </summary>
    public void HandlePhaseTimeout()
    {
        // 시계가 안 도는 상태(격파 후 / 이미 처리됨)의 잔여 이벤트는 무시 —
        // 클리어와 자연 완료가 같은 프레임에 겹쳤을 때 승리 순간 실패하는 것을 막는다.
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
            Debug.LogError($"[BossSpherePhaseDriver] '{name}' phaseTimeLimits[{_activeIndex}]가 0 — 하강이 즉시 끝나 실패 처리된다.", this);

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
