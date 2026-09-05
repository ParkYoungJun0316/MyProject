using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// M.Stage1 incoming ArrowTrap 4개(Mouth1~4)를 총괄 발사하는 감독.
/// CoopStageAudit.M.md §2.2 확정 — Barrier 색 배정·라운드(ChallengeStart)와 완전히 분리되어
/// 있다. 이 감독은 어느 색 문이 열렸는지 전혀 모르고, 레인은 그냥 "방향(자리)"일 뿐이다.
///
/// [규칙]
/// - 동시 발사 없음. 한 번에 1레인만.
/// - 직전에 쐈던 레인은 다음 추첨에서 제외(나머지 중 랜덤). 가방 셔플 아님 — 매번 재추첨.
/// - 텀(발사 간격) = 유일한 난이도 축. stepAtSeconds[i] 경과 시점부터 termSteps[i] 적용
///   (계단식, 오름차순 입력). speedPhases는 안 씀.
/// - Barrier/입 닫힘 창 등 다른 상태로 이 루프를 멈추지 않음 — 연동 안 함.
///
/// [권한] Host 전용 루프(nm.IsServer 가드). lanes[i].FireOnce()만 부른다 — 시드/NV 불필요.
/// Client는 ArrowTrap 자체 스케줄이 돌던 방식과 동일한 통로(OnPreFireCharge/OnFiring →
/// SyncArrowChargeClientRpc/SyncArrowFireClientRpc)로만 결과를 본다. 새 RPC 없음.
///
/// [전제] lanes로 연결하는 ArrowTrap 4개는 에디터에서 startActive=false로 자체 루프를 꺼야
/// 한다(ArrowTrap.FireOnce() 주석 참고) — 안 그러면 자체 스케줄 발사와 이 감독의 발사가
/// 동시에 겹쳐 "한 번에 1레인만" 규칙이 깨진다.
///
/// [버그 수정 2026-09-05] OnEnable이 GO 활성화 즉시 루프를 시작해서, StageStartGate 카운트다운/
/// 다이얼로그를 기다리지 않고 씬 진입 직후부터 화살이 나왔다. TrapPlayerTracker와 동일 원칙 —
/// 부모 계층의 StageManager를 찾아 IsStarted == true(StageManager.StartStage() 호출 후)가 될
/// 때까지 대기한 뒤에야 스케줄 앵커(PhaseStartServerTime)를 읽고 루프를 시작한다. StartStage()가
/// 이 앵커를 재확정하므로(StageManager.StartStage 주석 참고) 대기 없이 즉시 읽으면 다이얼로그로
/// 지연된 시간만큼 "이미 지난 텀"으로 오인해 계단이 앞당겨지는 문제도 같이 막는다.
/// StageManager가 없는(부모에 없는) 씬/테스트에서는 대기 없이 즉시 시작(기존 동작 유지).
/// </summary>
public class ArrowIncomingDirector : MonoBehaviour
{
    [Header("레인 (Mouth1~4, 4개)")]
    [Tooltip("startActive=false로 자체 스케줄을 꺼둔 incoming ArrowTrap만 연결.")]
    [SerializeField] private ArrowTrap[] lanes = new ArrowTrap[0];

    [Header("텀 계단 (난이도 축 — 유일)")]
    [Tooltip("경과 시각(초, PhaseStartServerTime 기준) 오름차순. 예: [0, 25, 45]")]
    [SerializeField] private float[] stepAtSeconds = new float[0];
    [Tooltip("위 stepAtSeconds와 같은 순서로 대응하는 텀(발사 간격, 초). 예: [7, 5, 3]")]
    [SerializeField] private float[] termSteps = new float[0];
    [Tooltip("stepAtSeconds/termSteps가 비었을 때 쓰는 고정 텀(초)")]
    [SerializeField] private float fallbackTerm = 5f;

    int _lastLaneIndex = -1;
    Coroutine _loop;
    StageManager _stageManager;

    void Awake()
    {
        _stageManager = GetComponentInParent<StageManager>();
    }

    void OnEnable()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return; // Host 전용 — Client는 루프를 돌리지 않음
        if (lanes == null || lanes.Length < 2) return; // 2개 미만이면 "직전 제외 추첨"이 성립 안 함

        _loop = StartCoroutine(WaitForStageStartThenDirect());
    }

    void OnDisable()
    {
        if (_loop != null) StopCoroutine(_loop);
        _loop = null;
    }

    /// <summary>
    /// TrapPlayerTracker.TrackingBootstrapRoutine과 동일 원칙 — StageManager 자식이면
    /// StartStage() 호출(IsStarted == true) 전까지 대기. 독립 씬(_stageManager == null)은
    /// 즉시 진행(기존 동작 유지).
    /// </summary>
    IEnumerator WaitForStageStartThenDirect()
    {
        if (_stageManager != null)
            while (!_stageManager.IsStarted)
                yield return null;

        yield return DirectorLoop();
    }

    IEnumerator DirectorLoop()
    {
        var nm = NetworkManager.Singleton;

        // ArrowTrap.TrapLoop()과 동일한 앵커 원칙(NetworkDesign.md 패턴) — Host가 이 Phase에
        // 진입한 절대 서버 시각을 기준으로 잡아야 텀 계단 경과 시간이 씬 재진입/리로드와
        // 무관하게 일정하게 시작한다. StageNetworkState 없는 테스트 씬은 로컬 시각으로 폴백.
        // (위 WaitForStageStartThenDirect가 StartStage() 완료 후까지 대기했으므로, 여기서 읽는
        // PhaseStartServerTime은 StartStage()가 재확정한 최신 값이다.)
        float scheduleStartTime = (StageNetworkState.Instance != null && StageNetworkState.Instance.PhaseStartServerTime > 0)
            ? (float)StageNetworkState.Instance.PhaseStartServerTime
            : (nm != null ? (float)nm.ServerTime.Time : Time.time);

        while (true)
        {
            float now     = nm != null ? (float)nm.ServerTime.Time : Time.time;
            float elapsed = now - scheduleStartTime;
            float term    = GetCurrentTerm(elapsed);

            yield return new WaitForSeconds(term);

            FireRandomLane();
        }
    }

    float GetCurrentTerm(float elapsed)
    {
        if (termSteps == null || termSteps.Length == 0) return fallbackTerm;

        float term = termSteps[0];
        int   n    = Mathf.Min(stepAtSeconds.Length, termSteps.Length);
        for (int i = 0; i < n; i++)
        {
            if (elapsed >= stepAtSeconds[i]) term = termSteps[i];
        }
        return term;
    }

    void FireRandomLane()
    {
        int pick;
        do
        {
            pick = Random.Range(0, lanes.Length);
        } while (pick == _lastLaneIndex);

        _lastLaneIndex = pick;
        lanes[pick]?.FireOnce();
    }
}
