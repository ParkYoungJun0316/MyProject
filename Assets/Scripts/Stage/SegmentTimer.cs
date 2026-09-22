using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 구역 시계 — 통로를 구역으로 나눠 "이 구역을 제한 시간 안에 못 지나가면 여기가 함정밭이 된다"를 만든다.
///
/// [동작]
///  1. 이 오브젝트의 트리거(Is Trigger)에 플레이어가 **처음** 들어오면 시계가 시작한다.
///     (StartTimer()가 public이라 StageStartGate 같은 다른 UnityEvent에 걸어도 된다 — 첫 구역용.)
///  2. duration 초 뒤 activateOnTimeout의 오브젝트를 켠다. 그 아래 TrapBase(SpikeTrap 등)는 같이 Activate().
///  3. 다음 구역은 자기 트리거로 알아서 시작한다 — 구역 사이에 연결 배선이 없다.
///
/// [네트워크]
///  트리거 판정은 Host만 한다. Host가 StageNetworkState.MarkSegmentStart(segmentIndex)로 시작 서버
///  시각을 확정하면, 전 머신이 그 값을 읽어 **각자 로컬로** 센다 — 카운트다운 숫자가 전원 동일하다.
///  함정 발동도 각자 로컬이고, 피는 SpikeTrap이 Host에서만 깎는다(NetworkDamageUtil).
///  이 컴포넌트 자체는 NetworkBehaviour가 아니다 — 씬 배치 NetworkObject의 OnEnable/스폰 레이스를
///  피하려고 상주 릴레이인 StageNetworkState의 슬롯을 쓴다(TrapNetworkBoard.md §7).
///
///  한 번 시작한 구역 시계는 **통과 여부와 무관하게 계속 간다.** 앞선 사람이 다음 구역으로 넘어가도
///  이 구역에 남은 사람에게는 시계가 그대로 흐른다 — 뒤처짐에 대한 압박이 이 설계의 핵심이다.
/// </summary>
public class SegmentTimer : MonoBehaviour
{
    [Header("구역")]
    [Tooltip("StageNetworkState 슬롯 index. 씬의 SegmentTimer마다 겹치지 않게 0,1,2,3…")]
    [SerializeField] int segmentIndex = 0;

    [Tooltip("이 구역의 제한 시간(초). 시계 시작부터 이 시간이 지나면 함정이 켜진다")]
    [SerializeField] float duration = 20f;

    [Header("시간 초과")]
    [Tooltip("시간 초과 시 켤 오브젝트(이 구역에 미리 깔아둔 스파이크 루트 등). " +
             "하위의 TrapBase는 켠 뒤 Activate()까지 호출한다")]
    [SerializeField] GameObject[] activateOnTimeout = new GameObject[0];

    [Header("이벤트")]
    [Tooltip("시계가 시작한 순간(전 머신 동일 시점)")]
    public UnityEvent onTimerStarted;

    [Tooltip("제한 시간이 다 된 순간(전 머신 동일 시점)")]
    public UnityEvent onTimeout;

    // ── 표시용(간판이 읽는다) ─────────────────────────────────────

    /// <summary>시계가 시작했는가.</summary>
    public bool HasStarted => _started;

    /// <summary>제한 시간이 이미 지났는가.</summary>
    public bool HasFired => _fired;

    /// <summary>이 구역의 제한 시간(초).</summary>
    public float Duration => duration;

    /// <summary>남은 시간(초). 시작 전이면 duration, 초과했으면 0.</summary>
    public float RemainingSeconds
    {
        get
        {
            if (!_started) return duration;
            float left = (float)(_startServerTime + duration - Now());
            return Mathf.Max(0f, left);
        }
    }

    /// <summary>남은 시간 비율(1 = 가득, 0 = 소진). 간판 채움용.</summary>
    public float Remaining01 => duration <= 0f ? 0f : Mathf.Clamp01(RemainingSeconds / duration);

    // ── 내부 ──────────────────────────────────────────────────────

    bool   _started;
    bool   _fired;
    double _startServerTime;

    void Update()
    {
        if (!_started)
        {
            double start = ReadStartTime();
            if (start <= 0d) return;

            // 시작 시각을 로컬에 캐시한다 — 이후로는 슬롯을 다시 읽지 않는다.
            _started         = true;
            _startServerTime = start;
            onTimerStarted?.Invoke();
            return;
        }

        if (_fired) return;
        if (Now() < _startServerTime + duration) return;

        _fired = true;
        FireTimeout();
    }

    void OnTriggerEnter(Collider other)
    {
        // 트리거 판정은 Host만. Client는 NV로 시작 시각을 받는다.
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

        if (_started) return;
        if (other.GetComponent<Player>() == null) return;

        StartTimer();
    }

    /// <summary>
    /// 시계 시작. 트리거 대신 다른 이벤트로 시작하고 싶을 때 UnityEvent에 연결한다
    /// (첫 구역을 StageStartGate 완료에 거는 식). 이미 시작했으면 무시.
    /// </summary>
    public void StartTimer()
    {
        var state = StageNetworkState.Instance;
        if (state != null)
        {
            // Host만 실제로 기록한다. 기록된 시각은 NV로 전 머신에 퍼지고, Host 자신도
            // 다음 Update에서 그 값을 읽어 시작한다 — Host/Client가 같은 경로를 탄다.
            state.MarkSegmentStart(segmentIndex);
            return;
        }

        // StageNetworkState가 없는 씬(테스트 등) 폴백 — 로컬 시각으로 혼자 센다.
        if (_started) return;
        _started         = true;
        _startServerTime = Now();
        onTimerStarted?.Invoke();
    }

    void FireTimeout()
    {
        foreach (GameObject go in activateOnTimeout)
        {
            if (go == null) continue;
            go.SetActive(true);

            // startActive=false로 둔 함정도 여기서 돌기 시작한다.
            // (startActive=true면 SetActive 시 스스로 시작하고, Activate()는 isRunning 가드로 무시된다.)
            foreach (TrapBase trap in go.GetComponentsInChildren<TrapBase>(true))
                trap.Activate();
        }

        onTimeout?.Invoke();
    }

    double ReadStartTime()
    {
        StageNetworkState state = StageNetworkState.Instance;
        return state != null ? state.GetSegmentStartTime(segmentIndex) : 0d;
    }

    static double Now()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening ? nm.ServerTime.Time : Time.timeAsDouble;
    }
}
