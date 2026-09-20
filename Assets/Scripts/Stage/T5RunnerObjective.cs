using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// T.Stage5 목표 — 러너는 1층 Goal, 안내자는 2층 Goal. 제한시간 안에 **전원이** 자기 Goal에 있어야 한다.
/// SSOT: `Assets/Docs/TStage5RunnerRedesign.md` §1.8
///
/// [왜 전용 클래스인가 — 기존 둘 다 쓸 수 없다]
///  · `SurviveTimeObjective`는 **시간을 채우면 Complete()** 다. T5는 시간 초과가 실패라 승패가 반대다.
///    그대로 쓰면 아무도 Goal에 가지 않고 숨어 있는 것이 정답이 된다.
///  · `ReachZoneObjective`는 **존 하나가 활성 플레이어 전원**을 요구한다. 1층/2층으로 나누면
///    러너는 1층 존에만, 안내자는 2층 존에만 들어가므로 **양쪽 다 영원히 미완**이다.
///  그래서 한 클래스가 두 존을 같이 들고, "각자 자기 층의 존"으로 판정한다.
///
/// [판정 — Host 레인 하나]
///  `StageObjective.Complete()/Fail()`은 Host에서만 불려야 한다(`NetworkDesign.md` §11A.2).
///  트리거 콜백 대신 매 Tick 경계 검사를 쓰는 이유는, 발사·낙하로 존을 스치듯 통과하는 경우에도
///  "지금 안에 있는가"가 그대로 답이 되기 때문이다(Enter/Exit 짝이 어긋날 여지가 없다).
///  인원이 최대 4명이라 비용은 무시할 수 있다.
///
/// [러너가 누구인지]
///  `StageNetworkState.IsT5Runner`(NV)를 본다. 판정이 Host 레인이라 NV 도착을 기다릴 일이 없다.
///  뽑기 자체는 `T5RunnerDirector`가 한다.
///
/// [실패]
///  ① 제한시간 초과 ② **누가 죽든 즉시 실패** — 둘 다 `Fail()` → `StageManager`의 실패 경로.
///
///  T5는 **부활 예외 스테이지**다(`ReviveSystemDesign.md` §7.1). 사망 감지를 여기서 하는 이유는
///  자동 부활 도입으로 "사망 = 리로드"가 더 이상 성립하지 않기 때문이다 — 구 `StageResetOnPlayerDeath`는
///  폐기됐다. 실제로 부활이 일어나지 않게 막는 것은 씬 `StageNetworkState`의 Disable Revive 체크박스이고
///  (그래야 팀 목숨도 소모되지 않는다), 실패 확정은 이 Objective가 낸다.
///
///  **왜 안내자도 즉시 실패인가:** 2층에 SpikeTrap/ContactDamage를 넣으면서 안내자도 죽을 수 있게 됐다.
///  일반 규칙대로면 마지막 안내자가 죽었을 때 생존자가 러너뿐이라 러너 옆(1층)에 부활하는데, §1.3 때문에
///  2층으로 못 올라간다 → 2층 Goal 불가 → 남은 시간을 멍하니 기다린다. "누가 죽든 실패"로 통일하면 이
///  구멍이 사라지고 부활 지점 override 훅도 통째로 불필요해진다. `SequenceRing`·`ColorTileChallenge`의
///  "실수 1회 = 판 다시"와 같은 형태다. 실패 → 리로드 → **러너 재추첨**이라 실패 자체가 러너 교대 기회다.
///
/// [씬 설정]
///  0. 이 씬의 `StageNetworkState`에서 **Disable Revive를 체크**(§7.1).
///  1. `StageManager.objectives`에 이 컴포넌트를 등록(T5는 이것 하나뿐).
///  2. runnerGoal / guideGoal에 각각 1층·2층 Goal 존 콜라이더를 연결. 두 존은 XZ가 같고 높이만 다르다.
///  3. timeLimit = **110** (§1.8).
///
/// [왜 110초인가 — 전환 12회 구조의 시간 산수]
///  경로 27칸 · 피치 12 기준 주행만 32초. 여기에 전환 대기가 붙는다 —
///  최적 12회 × 반응 2초면 56초, 실전값(16회 × 3초)이면 80초다. 90초면 최적 플레이 말고는 거의
///  다 실패한다. 시간이 늘어도 2층은 계속 할 일이 있어 지루해지지 않는다는 판단으로 110을 잡았다.
/// </summary>
public class T5RunnerObjective : StageObjective
{
    [Header("Goal 존")]
    [Tooltip("1층 Goal 존(러너 전용). 콜라이더 경계 안에 러너가 있으면 도달로 친다.")]
    [SerializeField] Collider runnerGoal;

    [Tooltip("2층 Goal 존(안내자 전용). 1층 Goal과 같은 XZ, 2층 높이.")]
    [SerializeField] Collider guideGoal;

    /// <summary>1층 Goal 존(읽기 전용). 지도 UI가 goal 점 위치로 쓴다 — 좌표를 따로 적어두지 않기 위해서다.</summary>
    public Collider RunnerGoalZone => runnerGoal;

    [Header("제한 시간")]
    [Tooltip("이 시간(초)을 넘기면 실패. §1.8 = 110초 (실측 밴드 100~120).")]
    public float timeLimit = 110f;

    [Header("이벤트 (UI 연결용)")]
    [Tooltip("남은 시간이 갱신될 때 호출(올림 초가 바뀔 때만). ObjectiveUI가 자동 구독.")]
    public UnityEvent<float> OnTimeChanged;

    /// <summary>남은 시간(초).</summary>
    public float Remaining => Mathf.Max(0f, timeLimit - _elapsed);

    float _elapsed;
    int   _shownSeconds = -1;

    public override void Begin()
    {
        _elapsed      = 0f;
        _shownSeconds = -1;

        // Begin은 StageManager.StartStage()마다 불릴 수 있다 — 먼저 떼고 붙여 중복 구독을 막는다.
        var net = StageNetworkState.Instance;
        if (net != null)
        {
            net.OnT5RemainingSync -= NotifyRemainingTime;
            net.OnT5RemainingSync += NotifyRemainingTime;
        }

        RaiseTimeIfDisplayChanged();
    }

    void OnDestroy()
    {
        var net = StageNetworkState.Instance;
        if (net != null) net.OnT5RemainingSync -= NotifyRemainingTime;
    }

    public override void Tick()
    {
        if (IsCompleted || IsFailed) return;

        // Client는 시간을 스스로 굴리지 않는다 — Host의 SyncT5RemainingClientRpc만 받는다
        // (§11A "Progress는 Host 레인 하나").
        if (!IsHostLane()) return;

        _elapsed += Time.deltaTime;

        if (RaiseTimeIfDisplayChanged())
        {
            var net = StageNetworkState.Instance;
            var nm  = NetworkManager.Singleton;
            if (net != null && nm != null && nm.IsListening && nm.IsServer)
                net.SyncT5RemainingClientRpc(Remaining);
        }

        // 누가 죽든 즉시 실패(§7.1). 이 씬은 부활이 없으므로 그대로 두면 아무도 못 살아나고
        // 남은 시간만 흘러간다. 트리거 구독이 아니라 매 Tick 경계 검사인 것은 이 클래스의
        // Goal 판정과 같은 이유 — Enter/Exit 짝이 어긋날 여지가 없다.
        if (AnyPlayerDead())
        {
            Fail();
            return;
        }

        if (AllReachedGoal())
        {
            Complete();
            return;
        }

        if (_elapsed >= timeLimit) Fail();
    }

    /// <summary>Client 전용 진입점 — Host가 보낸 남은 시간으로 UI만 갱신한다.</summary>
    public void NotifyRemainingTime(float remaining)
    {
        _elapsed = Mathf.Max(0f, timeLimit - remaining);
        OnTimeChanged?.Invoke(remaining);
    }

    // ── 판정 ───────────────────────────────────────────────────

    /// <summary>한 명이라도 죽어 있는가. Host 레인에서만 호출된다(§7.1 — 러너·안내자 구분 없음).</summary>
    static bool AnyPlayerDead()
    {
        foreach (Player p in FindObjectsByType<Player>(FindObjectsSortMode.None))
            if (p != null && p.IsDead) return true;
        return false;
    }

    /// <summary>
    /// 플레이어 전원이 자기 층의 Goal 존 안에 있는가.
    /// 사망자는 위 AnyPlayerDead()가 이미 실패로 처리하므로 여기서는 나오지 않는다.
    /// </summary>
    bool AllReachedGoal()
    {
        if (runnerGoal == null || guideGoal == null) return false;

        var net = StageNetworkState.Instance;
        if (net == null) return false;

        int counted = 0;
        foreach (Player p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if (p == null || p.IsDead) return false;

            NetworkObject no = p.GetComponent<NetworkObject>();
            if (no == null) continue;

            Collider zone = net.IsT5Runner(no.OwnerClientId) ? runnerGoal : guideGoal;
            if (!zone.bounds.Contains(p.transform.position)) return false;

            counted++;
        }

        return counted > 0;
    }

    /// <summary>표시값(올림 초)이 실제로 바뀔 때만 이벤트를 쏜다 — 매 프레임이면 같은 글자를 다시 쓴다.</summary>
    bool RaiseTimeIfDisplayChanged()
    {
        int seconds = Mathf.CeilToInt(Remaining);
        if (seconds == _shownSeconds) return false;

        _shownSeconds = seconds;
        OnTimeChanged?.Invoke(Remaining);
        return true;
    }

    static bool IsHostLane()
    {
        var nm = NetworkManager.Singleton;
        return nm == null || !nm.IsListening || nm.IsServer;
    }
}
