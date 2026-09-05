using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 혀 함정 — 팀 응원 되돌림 (ITeamCheerRevert). Mouth/Saliva와 같은 머신. 새 RPC 없음.
/// Animation Event <c>SweepBreak(int index)</c> — Animator와 같은 GO에 이 컴포넌트를 붙일 것.
///
/// 4.1 RiseHold: Warning → Rise(가운데) → Hold → 외침 시 Retract 후 복구.
/// 4.2 AttackSweep: Warning → Attack L 또는 R(스윕 10칸) → Hold 클립 없음.
///     안 외치면 칸은 꺼진 채 다음 사이클이 반대쪽. 외침(Warning 또는 Attack 중 큐)이면 꺼진 칸 전부 복구.
/// M.Boss MixedSweep: 창마다 가운데·L·R 중 하나를 시드로 뽑는다. Hold 없음 —
///     가운데도 Rise → 칸 부숨 → Retract로 끝나는 공격이다. 안 외치면 칸이 꺼진 채 남고, 외치면 전부 복구.
/// Rise/Attack이 시작되면 끊지 않음. MouthBG 혀 쓰지 않음.
/// </summary>
public class TongueController : MonoBehaviour, ITeamCheerRevert
{
    // 인스펙터 직렬화 값 유지 — 새 패턴은 뒤에만 추가할 것(0=RiseHold, 1=AttackSweep).
    public enum TonguePattern
    {
        RiseHold,
        AttackSweep,
        MixedSweep,
    }

    enum HazardPhase
    {
        Idle,
        Warning,
        Attacking,
        Holding,
        Recovering,
    }

    enum SweepRegion
    {
        None,
        Center,
        Left,
        Right,
    }

    [Header("패턴")]
    [Tooltip("4.1 SequenceRing = RiseHold. 4.2 ArrowTrap = AttackSweep.\n" +
             "M.Boss = MixedSweep — 창마다 가운데/L/R 중 하나. Hold 없음.")]
    [SerializeField] TonguePattern pattern = TonguePattern.RiseHold;

    [Header("참조")]
    [Tooltip("혀 Animator. 비우면 이 GO 또는 자식에서 탐색. SweepBreak 이벤트는 Animator와 같은 GO 필요.")]
    [SerializeField] Animator tongueAnimator = null;

    [Header("Animator 트리거")]
    [SerializeField] string idleTrigger = "doIdle";
    [SerializeField] string riseTrigger = "doRise";
    [SerializeField] string holdTrigger = "doHold";
    [SerializeField] string retractTrigger = "doRetract";
    [SerializeField] string attackLTrigger = "doAttackL";
    [SerializeField] string attackRTrigger = "doAttackR";

    [Header("클립 길이 (초) — 클립 Length와 맞출 것")]
    [Tooltip("TongueRise. 예) 24fps 160프레임 ≈ 6.67")]
    [SerializeField] float riseClipLength = 0f;

    [Tooltip("TongueAttack_L / _R. 예) 24fps 120프레임 = 5")]
    [SerializeField] float attackClipLength = 0f;

    [Tooltip("TongueRetract. 4.1만. 예) 24fps 40프레임 ≈ 1.67")]
    [SerializeField] float retractClipLength = 0f;

    [Header("1×1 타일 (배열 순서 = 스윕 순서. 가운데 1×5는 넣지 말 것)")]
    [Tooltip("4.1 = 가운데 큰 판 1칸. MixedSweep(보스) = 가운데 3×3 9칸")]
    [SerializeField] GameObject[] centerTiles = new GameObject[0];

    [Tooltip("4.2 왼쪽 2×5 = 10칸")]
    [SerializeField] GameObject[] leftTiles = new GameObject[0];

    [Tooltip("4.2 오른쪽 2×5 = 10칸")]
    [SerializeField] GameObject[] rightTiles = new GameObject[0];

    [Header("랜덤 스케줄")]
    [SerializeField] float randomIntervalMin = 5f;
    [SerializeField] float randomIntervalMax = 15f;
    [SerializeField] float initialDelay = 0f;
    [SerializeField] bool startOnAwake = true;

    [Header("팀 응원 함정")]
    [Tooltip("Attack 전 Warning (초). 수치는 나중에 튜닝.")]
    [SerializeField] float warnDuration = 2f;

    [Header("네트워크 시드")]
    [Tooltip("Mouth 0x4D4F5554 / Saliva 0x53504954 와 겹치지 않게.")]
    [SerializeField] int seedSalt = 0x544F4E47;

    Coroutine _cycleCoroutine;
    Coroutine _bindRoutine;

    HazardPhase _phase = HazardPhase.Idle;
    SweepRegion _sweepRegion = SweepRegion.None;
    bool _available;
    bool _prevented;
    bool _recoverQueued;
    bool _skipNextWindow;
    double _resyncDeadline = -1d;
    int _cycleCount;
    int _syncGeneration;
    bool _attackLeft;
    bool _sideInitialized;

    // 소비한 창 수. 스킵·차단·완주 어느 경로로 끝나든 창당 정확히 1회 올라가므로 전 머신이
    // 같은 값을 갖는다 — MixedSweep의 영역 추첨 키.
    int _attackCount;

    // PhaseStartServerTime(Host가 Phase 진입 직전에 찍는 절대 시각)이 전파될 때까지 기다리는 한도.
    // 그 안에 안 오면 앵커가 없는 씬으로 보고 예전처럼 로컬 시각으로 폴백한다.
    const float AnchorWaitTimeout = 3f;

    public bool IsAvailable => _available;

    void Awake()
    {
        if (tongueAnimator == null)
            tongueAnimator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
    }

    void OnEnable()
    {
        ResetHazardFlags();
        RestoreAll();
        TriggerIdle();
        _bindRoutine = StartCoroutine(BindAndStartHazard());
    }

    void OnDisable()
    {
        if (CheerService.Instance != null)
            CheerService.Instance.UnregisterRevert(this);
        StopAllCoroutines();
        _cycleCoroutine = null;
        _bindRoutine = null;
        ResetHazardFlags();
        RestoreAll();
    }

    IEnumerator BindAndStartHazard()
    {
        while (CheerService.Instance == null)
            yield return null;
        _bindRoutine = null;
        if (!isActiveAndEnabled) yield break;
        CheerService.Instance.RegisterRevert(this);
        if (startOnAwake)
            StartCycle();
    }

    public void StartCycle()
    {
        if (_cycleCoroutine != null) StopCoroutine(_cycleCoroutine);
        _cycleCoroutine = StartCoroutine(HazardCycle());
    }

    public void StopCycle()
    {
        if (_cycleCoroutine != null)
        {
            StopCoroutine(_cycleCoroutine);
            _cycleCoroutine = null;
        }
        ResetHazardFlags();
        RestoreAll();
        TriggerIdle();
    }

    public void BuildRevertOrder(out int generation, out double resumeAtServerTime)
    {
        generation = _syncGeneration + 1;
        resumeAtServerTime = GetServerTime() + PickSeededInterval(generation, RevertAxis);
    }

    public void Revert(int generation, double resumeAtServerTime)
    {
        if (generation <= _syncGeneration) return;   // 이미 처리한 세대 / 낡은 명령

        _syncGeneration = generation;
        _resyncDeadline = resumeAtServerTime;

        switch (_phase)
        {
            case HazardPhase.Warning:
                _prevented = true;
                EndWindow();
                break;
            case HazardPhase.Attacking:
            case HazardPhase.Holding:
                _recoverQueued = true;
                EndWindow();
                break;
            case HazardPhase.Idle:
                // 이 머신은 아직 이번 창을 열지 않았다(씬 로드 시각 차이 등). 예전엔 여기서 명령을
                // 통째로 버려서 혼자 뒤늦게 스윕이 돌고 꺼진 1×1이 남았다.
                // 대기 중인 창을 열지 않고 건너뛰어 Host가 준 다음 예약에 위상을 맞춘다.
                // 건너뛰는 분기도 Host와 같이 창을 1회 소비한다(AdvanceAttack) — 안 그러면
                // 4.2 좌/우 순서와 MixedSweep 영역 추첨이 Host와 어긋난다.
                _skipNextWindow = true;
                break;
            // Recovering: 직전 창을 되돌리는 중 — 이번 창은 애초에 열지 않았으므로
            // 위에서 받은 _resyncDeadline만 따라가면 위상이 맞는다.
        }
    }

    /// <summary>클립 Animation Event. 배열 인덱스 = 스윕 순서 (0부터).</summary>
    public void SweepBreak(int index)
    {
        BreakTile(CurrentSweepArray(), index);
    }

    IEnumerator HazardCycle()
    {
        yield return ResolveFirstWindow();

        while (true)
        {
            if (_resyncDeadline > 0d)
            {
                yield return WaitForResyncDeadline();
            }
            else
            {
                yield return new WaitForSeconds(PickSeededInterval(_cycleCount, ScheduleAxis));
                _cycleCount++;
            }

            if (_skipNextWindow)
            {
                // 팀이 이미 되돌린 창 — 열지 않고 다음 예약으로 넘어간다.
                // Host의 Warning 차단 경로와 같은 뒷정리(복구·방향 소비)를 해야 꺼진 칸과
                // 다음 공격 방향(4.2)이 Host와 어긋나지 않는다.
                _skipNextWindow = false;
                EnsureAttackSide();
                RestoreAll();
                TriggerIdle();
                AdvanceAttack();
                _phase = HazardPhase.Idle;
                continue;
            }

            EnsureAttackSide();
            _prevented = false;
            _recoverQueued = false;
            _phase = HazardPhase.Warning;
            _available = true;
            CheerService.Instance?.NotifyHazardWindow(true);

            float warnElapsed = 0f;
            float warn = Mathf.Max(0f, warnDuration);
            while (warnElapsed < warn && !_prevented)
            {
                warnElapsed += Time.deltaTime;
                yield return null;
            }

            if (_prevented)
            {
                _prevented = false;
                RestoreAll();
                TriggerIdle();
                AdvanceAttack();
                _phase = HazardPhase.Idle;
                continue;
            }

            yield return AttackRoutine();
            BreakRemaining();

            if (pattern == TonguePattern.RiseHold)
            {
                if (_recoverQueued)
                {
                    _recoverQueued = false;
                    yield return RecoverRoutine();
                    _phase = HazardPhase.Idle;
                    continue;
                }

                _phase = HazardPhase.Holding;
                FireTrigger(holdTrigger);
                while (!_recoverQueued)
                    yield return null;

                _recoverQueued = false;
                yield return RecoverRoutine();
                _phase = HazardPhase.Idle;
                continue;
            }

            if (_recoverQueued)
            {
                _recoverQueued = false;
                yield return RecoverRoutine();
                _phase = HazardPhase.Idle;
            }
            else
            {
                EndWindow();

                // 가운데는 혀를 내리되 칸은 복구하지 않는다 — 안 외친 대가는 L/R과 같다.
                SweepRegion region = _sweepRegion;
                _sweepRegion = SweepRegion.None;
                yield return RetractIfCenter(region);

                // 여기까지 _phase는 Attacking이라 늦게 도착한 명령도 _recoverQueued로 잡힌다.
                // Host가 창 마지막 순간에 표를 받아들이면 명령이 RTT만큼 늦게 오는데, 그걸 흘리면
                // Host는 칸을 복구했는데 이 머신만 꺼진 채 남아 혼자 낙사한다. 한 프레임 더 본다.
                yield return null;
                if (_recoverQueued)
                {
                    _recoverQueued = false;
                    RestoreAll();
                }

                TriggerIdle();
                _phase = HazardPhase.Idle;
            }

            AdvanceAttack();
        }
    }

    IEnumerator AttackRoutine()
    {
        _phase = HazardPhase.Attacking;
        if (pattern == TonguePattern.RiseHold)
        {
            _sweepRegion = SweepRegion.Center;
            FireTrigger(riseTrigger);
            if (riseClipLength > 0f)
                yield return new WaitForSeconds(riseClipLength);
            yield break;
        }

        _sweepRegion = pattern == TonguePattern.MixedSweep
            ? PickSeededRegion(_attackCount)
            : (_attackLeft ? SweepRegion.Left : SweepRegion.Right);

        if (_sweepRegion == SweepRegion.Center)
        {
            // MixedSweep의 가운데 — Rise를 공격으로 쓴다. Hold 없이 부수고 Retract로 내려간다.
            FireTrigger(riseTrigger);
            if (riseClipLength > 0f)
                yield return new WaitForSeconds(riseClipLength);
            yield break;
        }

        FireTrigger(_sweepRegion == SweepRegion.Left ? attackLTrigger : attackRTrigger);
        if (attackClipLength > 0f)
            yield return new WaitForSeconds(attackClipLength);
    }

    /// <summary>
    /// 가운데 스윕(4.1 Rise / MixedSweep 가운데)은 클립이 혀가 올라간 채 끝나므로 Retract로 내린다.
    /// L/R 클립은 스스로 내려가므로 Retract 없음(4.2 잠금).
    /// </summary>
    IEnumerator RetractIfCenter(SweepRegion region)
    {
        if (region != SweepRegion.Center) yield break;
        FireTrigger(retractTrigger);
        if (retractClipLength > 0f)
            yield return new WaitForSeconds(retractClipLength);
    }

    // _phase = Idle 은 호출부(HazardCycle)가 찍는다 — Idle이 "다음 창을 기다리는 중"만 뜻해야
    // Revert가 "창 밖이라 건너뛸 머신"과 "직전 창을 되돌리는 중인 머신"을 구분할 수 있다.
    IEnumerator RecoverRoutine()
    {
        _phase = HazardPhase.Recovering;
        EndWindow();
        SweepRegion region = _sweepRegion;
        _sweepRegion = SweepRegion.None;

        yield return RetractIfCenter(region);

        RestoreAll();
        TriggerIdle();
    }

    /// <summary>
    /// 첫 창을 Host/Client 공통 절대 시각에 건다. 예전엔 로컬 OnEnable + WaitForSeconds라 씬 로드
    /// 시각 차이만큼 첫 Warning 창이 어긋났고, 창 밖에서 외친 표는 Host에서 조용히 버려졌다.
    /// 앵커는 WindTrap/ArrowTrap과 같은 PhaseStartServerTime — 앵커가 없는 씬에서는 로컬 폴백.
    /// </summary>
    IEnumerator ResolveFirstWindow()
    {
        // PhaseManager.EnterPhase()는 objectsToEnable.SetActive(true) 다음에야 MarkAndSyncPhase()를
        // 찍는다. Phase가 이 함정을 켜주는 경우(4.1↔4.2 전환) OnEnable에서 곧바로 읽으면 Host가
        // 직전 Phase의 낡은 앵커를 잡아 Client와 첫 창이 어긋난다(SafeZoneWarnSign과 같은 이유).
        // 한 프레임 양보하면 같은 EnterPhase의 MarkAndSyncPhase가 끝난 뒤 새 앵커를 읽는다.
        yield return null;

        double anchor = -1d;
        float waited = 0f;
        while (waited < AnchorWaitTimeout)
        {
            var sns = StageNetworkState.Instance;
            if (sns != null && sns.PhaseStartServerTime > 0d)
            {
                anchor = sns.PhaseStartServerTime;
                break;
            }
            waited += Time.deltaTime;
            yield return null;
        }

        if (anchor > 0d)
        {
            _resyncDeadline = anchor + initialDelay + PickSeededInterval(_cycleCount, ScheduleAxis);
            _cycleCount++;
            yield break;
        }

        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);
    }

    /// <summary>
    /// 예약된 재개 시각까지 대기. 대기 중에 Revert가 예약을 갱신할 수 있으므로 매 프레임 필드를
    /// 다시 읽는다 — 인자로 붙잡아 두면 갱신된 예약이 대기 종료 직후 지워져 로컬 랜덤으로 샌다.
    /// </summary>
    IEnumerator WaitForResyncDeadline()
    {
        while (_resyncDeadline > 0d && GetServerTime() < _resyncDeadline)
            yield return null;
        _resyncDeadline = -1d;
    }

    // 시드를 뽑는 축이 셋이다 — 로컬 스케줄(_cycleCount) / 되돌림 세대(_syncGeneration) /
    // 영역 추첨(_attackCount). 전부 1,2,3…으로 올라가므로 축을 안 섞으면 같은 정수가 같은 값을 준다.
    const int ScheduleAxis = 0;
    const int RevertAxis   = 1;
    const int RegionAxis   = 2;

    int MixSeed(int index, int axis)
        => NetworkSessionData.Seed ^ seedSalt ^ (index * 0x2545F491) ^ (axis * 0x27220A95);

    float PickSeededInterval(int generation, int axis)
    {
        // InitState는 전역 RNG를 갈아엎는다 — 뽑고 나서 되돌려야 같은 씬의 다른 시스템이
        // 이 시드 스트림을 물려받지 않는다. 결정성은 그대로.
        var prevState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(MixSeed(generation, axis));
        float min = randomIntervalMin;
        float max = Mathf.Max(min, randomIntervalMax);
        float interval = Random.Range(min, max);
        UnityEngine.Random.state = prevState;
        return interval;
    }

    static readonly SweepRegion[] MixedRegions =
    {
        SweepRegion.Center,
        SweepRegion.Left,
        SweepRegion.Right,
    };

    /// <summary>
    /// MixedSweep의 이번 창 영역. 전 머신이 같은 값을 뽑아야 꺼진 칸이 어긋나지 않으므로
    /// 로컬 Random을 쓰지 않고 세션 시드 + 창 번호로 결정한다(§7 "클라이언트마다 Random 없음").
    /// 이미 부서진 영역이 또 나오는 것은 허용 — 그 창은 헛방이 된다.
    /// </summary>
    SweepRegion PickSeededRegion(int index)
    {
        var prevState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(MixSeed(index, RegionAxis));
        int pick = Random.Range(0, MixedRegions.Length);
        UnityEngine.Random.state = prevState;
        return MixedRegions[pick];
    }

    static double GetServerTime()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening ? nm.ServerTime.Time : Time.timeAsDouble;
    }

    void EnsureAttackSide()
    {
        if (pattern != TonguePattern.AttackSweep || _sideInitialized) return;
        _attackLeft = ((NetworkSessionData.Seed ^ seedSalt) & 1) == 0;
        _sideInitialized = true;
    }

    /// <summary>
    /// 창 하나를 소비했다는 표시. 스킵·차단·완주 세 경로에서 **정확히 1회씩** 불려야
    /// Host/Client의 공격 방향(4.2)과 영역 추첨(MixedSweep)이 어긋나지 않는다.
    /// </summary>
    void AdvanceAttack()
    {
        _attackCount++;
        if (pattern == TonguePattern.AttackSweep)
            _attackLeft = !_attackLeft;
    }

    void EndWindow()
    {
        _available = false;
        CheerService.Instance?.NotifyHazardWindow(false);
    }

    void ResetHazardFlags()
    {
        _phase = HazardPhase.Idle;
        _sweepRegion = SweepRegion.None;
        _available = false;
        _prevented = false;
        _recoverQueued = false;
        _skipNextWindow = false;
        _resyncDeadline = -1d;
        CheerService.Instance?.NotifyHazardWindow(false);
    }

    GameObject[] CurrentSweepArray()
    {
        switch (_sweepRegion)
        {
            case SweepRegion.Center: return centerTiles;
            case SweepRegion.Left: return leftTiles;
            case SweepRegion.Right: return rightTiles;
            default: return null;
        }
    }

    void BreakTile(GameObject[] tiles, int index)
    {
        if (tiles == null || index < 0 || index >= tiles.Length) return;
        GameObject tile = tiles[index];
        if (tile != null && tile.activeSelf)
            tile.SetActive(false);
    }

    void BreakRemaining()
    {
        GameObject[] tiles = CurrentSweepArray();
        if (tiles == null) return;
        for (int i = 0; i < tiles.Length; i++)
            BreakTile(tiles, i);
    }

    void RestoreAll()
    {
        RestoreArray(centerTiles);
        RestoreArray(leftTiles);
        RestoreArray(rightTiles);
    }

    static void RestoreArray(GameObject[] tiles)
    {
        if (tiles == null) return;
        for (int i = 0; i < tiles.Length; i++)
        {
            GameObject tile = tiles[i];
            if (tile != null && !tile.activeSelf)
                tile.SetActive(true);
        }
    }

    void TriggerIdle() => FireTrigger(idleTrigger);

    void FireTrigger(string trigger)
    {
        if (tongueAnimator == null || string.IsNullOrEmpty(trigger)) return;
        if (!string.IsNullOrEmpty(idleTrigger)) tongueAnimator.ResetTrigger(idleTrigger);
        if (!string.IsNullOrEmpty(riseTrigger)) tongueAnimator.ResetTrigger(riseTrigger);
        if (!string.IsNullOrEmpty(holdTrigger)) tongueAnimator.ResetTrigger(holdTrigger);
        if (!string.IsNullOrEmpty(retractTrigger)) tongueAnimator.ResetTrigger(retractTrigger);
        if (!string.IsNullOrEmpty(attackLTrigger)) tongueAnimator.ResetTrigger(attackLTrigger);
        if (!string.IsNullOrEmpty(attackRTrigger)) tongueAnimator.ResetTrigger(attackRTrigger);
        tongueAnimator.SetTrigger(trigger);
    }

    [ContextMenu("테스트: 사이클 시작")]
    void TestStartCycle() => StartCycle();

    [ContextMenu("테스트: 사이클 중지")]
    void TestStopCycle() => StopCycle();
}
