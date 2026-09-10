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
///  ColorWall이 색 일치를 감지하면 AdvancingWall.PauseTemporarily()가 진행 중인 하강 엔트리를
///  죽이고 이번 페이즈 시작 지점(=이전 체크포인트)까지 밀어 올린 뒤 멈춘다. 재개해 줄 주체가
///  없으면(Sphere는 scheduleOnStart=false라 ScheduleRoutine이 안 돈다) 그 페이즈 시계가 영구히
///  멈춘다 — 그래서 Update()가 정지 종료를 감지해 같은 하강을 다시 발행한다.
///  결과: 색을 맞출 때마다 그 페이즈 하강이(2026-09-11부터는 "바닥까지 전체"가) 처음부터 다시
///  = 시간을 버는 행위(§6 "반복해 버틴다"). P3의 실제 종료는 누적 히트 횟수
///  (requiredHitCounts[2], RegisterColorHit() 참고)가 담당하므로 무한정 늘어지지 않는다.
///
/// [Inspector 연결]
///  sphereWall            : Sphere에 붙은 AdvancingWall
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
/// </summary>
public class BossSpherePhaseDriver : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("식도 끝 Sphere에 붙은 AdvancingWall. moveDirection=로컬 하강 방향,\n" +
             "scheduleOnStart=false, activateOnStart=false, maxTotalAdvance=0으로 설정할 것 —\n" +
             "이 컴포넌트가 RunOnce로만 구동한다.")]
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

    [Tooltip("히트 카운트 목표 도달 시 호출할 보스 오브젝티브. Sphere의 ColorWall.OnColorMatch →\n" +
             "이 컴포넌트의 RegisterColorHit()에 연결해두면, 목표 도달 시 여기로 NotifyPhaseCleared()를 보낸다.")]
    [SerializeField] BossFightObjective bossFightObjective;

    /// <summary>지금 시계가 돌고 있는 칸. -1이면 시계 정지 상태.</summary>
    int  _activeIndex = -1;
    bool _wasPausedByColor;

    /// <summary>현재 활성 칸에서 누적된 색 히트 수. AdvanceToCheckpoint()가 칸이 바뀔 때마다 0으로 리셋.</summary>
    int _hitCount;

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
        if (checkpointDistances != null && phaseTimeLimits != null
            && checkpointDistances.Length != phaseTimeLimits.Length)
            Debug.LogError(
                $"[BossSpherePhaseDriver] '{name}' 배열 길이 불일치 — " +
                $"checkpointDistances({checkpointDistances.Length}) != phaseTimeLimits({phaseTimeLimits.Length}). " +
                "두 배열을 페이즈 수만큼 같은 길이로 채울 것.", this);
    }

    void Update()
    {
        // 색 일치 정지가 끝난 순간을 잡아 같은 하강을 재발행한다.
        // "IsMoving == false"만 보고 재발행하면 자연 완료(=시간 초과)나 maxTotalAdvance 오설정에서
        // 무한 재발행이 되므로, 정지 상태의 true→false 전이만 트리거로 쓴다.
        if (_activeIndex < 0 || sphereWall == null) return;

        if (sphereWall.IsPausedByColor)
        {
            _wasPausedByColor = true;
            return;
        }

        if (!_wasPausedByColor) return;

        _wasPausedByColor = false;
        if (!sphereWall.IsMoving)
            RunActiveCheckpoint();
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

        _activeIndex      = checkpointIndex;
        _wasPausedByColor = false;
        _hitCount         = 0; // 새 칸 = 히트 카운트 승리조건도 처음부터
        RunActiveCheckpoint();
    }

    /// <summary>
    /// ColorWall 색 일치 시 호출 (Sphere의 ColorWall.OnColorMatch에 연결).
    /// 현재 활성 칸의 requiredHitCounts를 채우면 BossFightObjective.NotifyPhaseCleared()를 호출한다.
    ///
    /// [Host 전용] ColorWall.HandleContact()는 각 머신이 로컬 충돌 감지로 독립 실행하므로
    /// Client에서도 이 메서드가 호출될 수 있다. NotifyPhaseCleared() 자체도 Host 가드가 있지만,
    /// 이 컴포넌트의 _hitCount도 Client에서 앞서가지 않도록 PhaseSurviveChallenge와 동일하게 여기서 막는다.
    ///
    /// 이미 목표를 채운 뒤(같은 프레임 안에서 다음 onPhaseEnter가 아직 안 왔을 때) 추가로 불려도
    /// _hitCount가 이미 목표 이상이면 무시 — NotifyPhaseCleared() 중복 호출(=페이즈 스킵) 방지.
    /// </summary>
    public void RegisterColorHit()
    {
        if (IsClientOnly()) return;
        if (_activeIndex < 0) return;

        int required = (requiredHitCounts != null && _activeIndex < requiredHitCounts.Length)
            ? requiredHitCounts[_activeIndex]
            : 0;
        if (required <= 0) return; // 이 페이즈는 히트 카운트 승리조건 없음

        if (_hitCount >= required) return; // 이미 클리어 통지함 — 중복 방지

        _hitCount++;
        if (_hitCount >= required)
            bossFightObjective?.NotifyPhaseCleared();
    }

    /// <summary>
    /// 시계 정지 (제자리). PhaseManager.onAllPhasesComplete에 연결 —
    /// 이 이벤트는 StageNetworkState.NotifyAllPhasesComplete 브릿지로 Client에서도 발동한다.
    /// 마지막 칸의 목표는 바닥이라 여기서 스냅하면 승리 순간에 Sphere가 바닥에 닿는 것처럼
    /// 보이므로, 스냅이 아니라 현 위치 정지(Deactivate)를 쓴다.
    /// </summary>
    public void StopClock()
    {
        _activeIndex      = -1;
        _wasPausedByColor = false;
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

        _activeIndex      = -1;
        _wasPausedByColor = false;

        Player[] players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (Player p in players)
        {
            if (p == null || p.IsDead) continue;
            NetworkDamageUtil.ApplyInstantKill(p); // Host 전용 가드 내장 — 호출부 가드 불필요
        }
    }

    // ── 내부 ─────────────────────────────────────────────────────

    static bool IsClientOnly()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && !nm.IsServer;
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
    /// </summary>
    void RunActiveCheckpoint()
    {
        float anchorDistance = CumulativeBefore(_activeIndex);
        float remaining      = Mathf.Max(TotalDistance - anchorDistance, 0f);
        float timeLimit      = (phaseTimeLimits != null && _activeIndex < phaseTimeLimits.Length)
            ? phaseTimeLimits[_activeIndex]
            : 0f;

        sphereWall.RunOnce(remaining, 0f, timeLimit, 0f);
    }

    // ── 에디터 ───────────────────────────────────────────────────
#if UNITY_EDITOR
    [ContextMenu("테스트: 0번 칸부터 시작")]
    void Debug_StartFirst() => AdvanceToCheckpoint(0);

    [ContextMenu("테스트: 시계 정지")]
    void Debug_StopClock() => StopClock();
#endif
}
