using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 입 배경 연출 컨트롤러 — Animator 기반.
///
/// [기본] 랜덤 스케줄 Close → Hold → Open 반복 (기존).
/// [팀 응원 함정] teamCheerHazard=true 인 씬(M1·M3·M.Boss)만:
///   Idle (응원 무시)
///   → Warning (UI. 이때부터 응원)
///        ├─ 외침: Close 안 넣음. Idle 유지
///        └─ 없음: Close 끝까지 → Hold (암전 유지) → 외침 시 Open → Idle
///   자동 Open 없음. 데미지 없음. ScreenFader만.
/// </summary>
public class MouthController : MonoBehaviour, ITeamCheerRevert
{
    enum HazardPhase
    {
        Idle,
        Warning,
        Closing,
        Holding,
        Opening,
    }

    [Header("참조")]
    [Tooltip("입 오브젝트의 Animator. 비워두면 자식에서 자동 탐색")]
    [SerializeField] private Animator mouthAnimator = null;

    [Header("Animator 파라미터 이름")]
    [SerializeField] private string openTrigger  = "doOpen";
    [SerializeField] private string holdTrigger  = "doHold";
    [SerializeField] private string closeTrigger = "doClose";
    [SerializeField] private string idleTrigger  = "doIdle";

    [Header("클립 길이 (초) — Animator 클립 Length와 정확히 맞출 것")]
    [Tooltip("Close 클립 길이(초). 입이 완전히 닫히는 데 걸리는 시간.\n예) 24fps 8프레임 = 0.333s")]
    [SerializeField] private float closeClipLength = 0f;

    [Tooltip("Hold 유지 시간(초). teamCheerHazard이면 무시하고 외침까지 Hold.")]
    [SerializeField] private float holdDuration    = 0f;

    [Tooltip("Open 클립 길이(초). 입이 완전히 열리는 데 걸리는 시간.\n예) 24fps 8프레임 = 0.333s")]
    [SerializeField] private float openClipLength  = 0f;

    [Header("랜덤 스케줄")]
    [Tooltip("입 닫기 사이클 사이 최소 대기 시간(초)")]
    [SerializeField] private float randomIntervalMin = 5f;

    [Tooltip("입 닫기 사이클 사이 최대 대기 시간(초)")]
    [SerializeField] private float randomIntervalMax = 15f;

    [Tooltip("게임 시작 후 첫 발동까지의 딜레이(초)")]
    [SerializeField] private float initialDelay = 0f;

    [Tooltip("Start 시 자동으로 사이클 시작 여부")]
    [SerializeField] private bool startOnAwake = true;

    [Header("팀 응원 함정")]
    [Tooltip("켜면 Close/Hold가 팀 응원 되돌림 대상이 된다. M1·M3·M.Boss만 켠다. M2는 SalivaHazard가 revert. M4·M5는 끈다.")]
    [SerializeField] private bool teamCheerHazard = false;

    [Tooltip("Close 전 Warning 유지 시간(초). 수치는 나중에 튜닝.")]
    [SerializeField] private float warnDuration = 2f;

    [Header("암전 연동 (선택)")]
    [Tooltip("입 닫힐 때 FadeOut, 열릴 때 FadeIn 을 자동 호출.\n비워두면 암전 없음.")]
    [SerializeField] private ScreenFader screenFader = null;

    [Header("네트워크 시드 (Host/Client 동기화)")]
    [Tooltip("씬당 배경 입은 1개뿐이라 상수로 고정. 다른 Random 사용처(WindTrap 등)와 값이 겹치지 않게 임의의 값 유지.")]
    [SerializeField] private int seedSalt = 0x4D4F5554;

    Coroutine _cycleCoroutine;
    Coroutine _bindRoutine;
    bool _isBusy;
    int _cycleCount;
    int _syncGeneration;

    HazardPhase _phase = HazardPhase.Idle;
    bool _available;
    bool _prevented;
    bool _recoverQueued;
    bool _skipNextWindow;
    double _resyncDeadline = -1d;

    // PhaseStartServerTime(Host가 Phase 진입 직전에 찍는 절대 시각)이 전파될 때까지 기다리는 한도.
    // 그 안에 안 오면 앵커가 없는 씬으로 보고 예전처럼 로컬 시각으로 폴백한다.
    const float AnchorWaitTimeout = 3f;

    /// <summary>현재 Close/Hold/Open 사이클 진행 중이면 true.</summary>
    public bool IsBusy => _isBusy;

    public bool IsAvailable => teamCheerHazard && _available;

    void Awake()
    {
        if (mouthAnimator == null)
            mouthAnimator = GetComponentInChildren<Animator>();
    }

    void OnEnable()
    {
        ResetHazardFlags();
        _isBusy = false;
        _cycleCount = 0;
        TriggerIdle();
        if (teamCheerHazard)
            _bindRoutine = StartCoroutine(BindAndStartHazard());
        else if (startOnAwake)
            StartCycle();
    }

    void OnDisable()
    {
        if (teamCheerHazard && CheerService.Instance != null)
            CheerService.Instance.UnregisterRevert(this);
        StopAllCoroutines();
        _cycleCoroutine = null;
        _bindRoutine = null;
        _isBusy = false;
        ResetHazardFlags();
        // 페이드는 ScreenFader 자기 코루틴으로 돌기 때문에 위 StopAllCoroutines로 안 멈춘다.
        // Closing/Holding 중에 입이 꺼지면(스테이지 종료·Phase 전환) 화면이 암전인 채로 굳으므로
        // 여기서 걷어준다. StopCycle()에도 같은 복구가 있지만 그쪽은 ContextMenu 전용이다.
        screenFader?.FadeIn(0f);
    }

    IEnumerator BindAndStartHazard()
    {
        while (CheerService.Instance == null)
            yield return null;
        _bindRoutine = null;
        if (!isActiveAndEnabled || !teamCheerHazard) yield break;
        CheerService.Instance.RegisterRevert(this);
        if (startOnAwake)
            StartCycle();
    }

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>랜덤 사이클 시작.</summary>
    public void StartCycle()
    {
        if (_cycleCoroutine != null) StopCoroutine(_cycleCoroutine);
        _cycleCoroutine = StartCoroutine(teamCheerHazard ? HazardCycle() : AutoCycle());
    }

    /// <summary>랜덤 사이클 중지. 현재 진행 중인 Open/Hold/Close는 즉시 중단됨.</summary>
    public void StopCycle()
    {
        if (_cycleCoroutine != null)
        {
            StopCoroutine(_cycleCoroutine);
            _cycleCoroutine = null;
        }
        _isBusy = false;
        ResetHazardFlags();
        TriggerIdle();
        screenFader?.FadeIn(0f);
    }

    public void BuildRevertOrder(out int generation, out double resumeAtServerTime)
    {
        generation = _syncGeneration + 1;
        resumeAtServerTime = GetServerTime() + PickSeededInterval(generation, RevertAxis);
    }

    public void Revert(int generation, double resumeAtServerTime)
    {
        if (!teamCheerHazard) return;
        if (generation <= _syncGeneration) return;   // 이미 처리한 세대 / 낡은 명령

        _syncGeneration = generation;
        _resyncDeadline = resumeAtServerTime;

        switch (_phase)
        {
            case HazardPhase.Warning:
                _prevented = true;
                EndWindow();
                break;
            case HazardPhase.Closing:
            case HazardPhase.Holding:
                _recoverQueued = true;
                EndWindow();
                break;
            case HazardPhase.Idle:
                // 이 머신은 아직 이번 창을 열지 않았다(씬 로드 시각 차이 등). 예전엔 여기서 명령을
                // 통째로 버려서 혼자 뒤늦게 입이 닫히고 다음 성공까지 암전이 유지됐다.
                // 대기 중인 창을 열지 않고 건너뛰어 Host가 준 다음 예약에 위상을 맞춘다.
                _skipNextWindow = true;
                break;
            // Opening: 직전 창을 되돌리는 중 — 이번 창은 애초에 열지 않았으므로
            // 위에서 받은 _resyncDeadline만 따라가면 위상이 맞는다.
        }
    }

    // ── 코루틴 ────────────────────────────────────────────────────

    IEnumerator AutoCycle()
    {
        // 연출 전용 입(TransitionPhase*)도 첫 사이클은 함정과 같은 앵커에 건다. 간격은 시드가 같으니
        // 첫 시작만 맞추면 이후가 따라온다. 앵커가 없는 씬은 ResolveFirstWindow가 로컬 폴백.
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

            yield return StartCoroutine(CloseOpenCycle());
        }
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
                // Host의 Warning 차단 경로와 같은 상태로 맞춘다.
                _skipNextWindow = false;
                _phase = HazardPhase.Idle;
                TriggerIdle();
                continue;
            }

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
                _phase = HazardPhase.Idle;
                TriggerIdle();
                continue;
            }

            _phase = HazardPhase.Closing;
            _isBusy = true;
            TriggerSafe(closeTrigger, openTrigger, holdTrigger, idleTrigger);
            screenFader?.FadeOut(closeClipLength > 0f ? closeClipLength : 0f);
            if (closeClipLength > 0f)
                yield return new WaitForSeconds(closeClipLength);

            if (_recoverQueued)
            {
                _recoverQueued = false;
                yield return OpenRoutine();
                _phase = HazardPhase.Idle;
                continue;
            }

            _phase = HazardPhase.Holding;
            TriggerSafe(holdTrigger, closeTrigger, openTrigger, idleTrigger);
            while (!_recoverQueued)
                yield return null;

            _recoverQueued = false;
            yield return OpenRoutine();
            _phase = HazardPhase.Idle;
        }
    }

    /// <summary>
    /// 첫 사이클을 Host/Client 공통 절대 시각에 건다(함정 창·연출 사이클 공통). 예전엔 로컬
    /// OnEnable + WaitForSeconds라 씬 로드 시각 차이만큼 첫 Warning 창이 어긋났고, 창 밖에서
    /// 외친 표는 Host에서 조용히 버려졌다.
    /// 앵커는 WindTrap/ArrowTrap과 같은 PhaseStartServerTime — 앵커가 없는 씬에서는 로컬 폴백.
    /// </summary>
    IEnumerator ResolveFirstWindow()
    {
        // PhaseManager.EnterPhase()는 objectsToEnable.SetActive(true) 다음에야 MarkAndSyncPhase()를
        // 찍는다. Phase가 이 함정을 켜주는 경우 OnEnable에서 곧바로 읽으면 Host가 직전 Phase의 낡은
        // 앵커를 잡아 Client와 첫 창이 어긋난다(SafeZoneWarnSign과 같은 이유). 한 프레임 양보하면
        // 같은 EnterPhase의 MarkAndSyncPhase가 끝난 뒤 새 앵커를 읽는다.
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

    // _phase = Idle 은 호출부(HazardCycle)가 찍는다 — Idle이 "다음 창을 기다리는 중"만 뜻해야
    // Revert가 "창 밖이라 건너뛸 머신"과 "직전 창을 되돌리는 중인 머신"을 구분할 수 있다.
    IEnumerator OpenRoutine()
    {
        _phase = HazardPhase.Opening;
        EndWindow();
        TriggerSafe(openTrigger, closeTrigger, holdTrigger, idleTrigger);
        screenFader?.FadeIn(openClipLength > 0f ? openClipLength : 0f);
        if (openClipLength > 0f)
            yield return new WaitForSeconds(openClipLength);

        TriggerIdle();
        _isBusy = false;
    }

    IEnumerator CloseOpenCycle()
    {
        _isBusy = true;

        TriggerSafe(closeTrigger, openTrigger, holdTrigger, idleTrigger);
        screenFader?.FadeOut(closeClipLength > 0f ? closeClipLength : 0f);
        if (closeClipLength > 0f)
            yield return new WaitForSeconds(closeClipLength);

        TriggerSafe(holdTrigger, closeTrigger, openTrigger, idleTrigger);
        if (holdDuration > 0f)
            yield return new WaitForSeconds(holdDuration);

        TriggerSafe(openTrigger, closeTrigger, holdTrigger, idleTrigger);
        screenFader?.FadeIn(openClipLength > 0f ? openClipLength : 0f);
        if (openClipLength > 0f)
            yield return new WaitForSeconds(openClipLength);

        TriggerIdle();
        _isBusy = false;
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

    // 간격을 뽑는 축이 둘이다 — 로컬 스케줄(_cycleCount)과 되돌림 세대(_syncGeneration).
    // 둘 다 1,2,3…으로 올라가므로 축을 안 섞으면 같은 정수 → 같은 간격이 그대로 반복된다.
    const int ScheduleAxis = 0;
    const int RevertAxis   = 1;

    float PickSeededInterval(int generation, int axis)
    {
        int mixedSeed = NetworkSessionData.Seed ^ seedSalt ^ (generation * 0x2545F491) ^ (axis * 0x27220A95);
        // InitState는 전역 RNG를 갈아엎는다 — 뽑고 나서 되돌려야 같은 씬의 다른 시스템이
        // 이 시드 스트림을 물려받지 않는다. 결정성은 그대로.
        var prevState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(mixedSeed);
        float min = randomIntervalMin;
        float max = Mathf.Max(min, randomIntervalMax);
        float interval = Random.Range(min, max);
        UnityEngine.Random.state = prevState;
        return interval;
    }

    static double GetServerTime()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening ? nm.ServerTime.Time : Time.timeAsDouble;
    }

    void EndWindow()
    {
        _available = false;
        CheerService.Instance?.NotifyHazardWindow(false);
    }

    void ResetHazardFlags()
    {
        _phase = HazardPhase.Idle;
        _available = false;
        _prevented = false;
        _recoverQueued = false;
        _skipNextWindow = false;
        _resyncDeadline = -1d;
        if (teamCheerHazard)
            CheerService.Instance?.NotifyHazardWindow(false);
    }

    // ── 트리거 헬퍼 ────────────────────────────────────────────────

    void TriggerIdle() => TriggerSafe(idleTrigger, openTrigger, holdTrigger, closeTrigger);

    void TriggerSafe(string trigger, string r1 = null, string r2 = null, string r3 = null)
    {
        if (mouthAnimator == null) return;
        if (r1 != null) mouthAnimator.ResetTrigger(r1);
        if (r2 != null) mouthAnimator.ResetTrigger(r2);
        if (r3 != null) mouthAnimator.ResetTrigger(r3);
        mouthAnimator.SetTrigger(trigger);
    }

    // ── 에디터 테스트 (플레이 중 컴포넌트 우클릭) ─────────────────────────

    [ContextMenu("테스트: Open")]
    void TestOpen() => TriggerSafe(openTrigger, holdTrigger, closeTrigger, idleTrigger);

    [ContextMenu("테스트: Hold")]
    void TestHold() => TriggerSafe(holdTrigger, openTrigger, closeTrigger, idleTrigger);

    [ContextMenu("테스트: Close")]
    void TestClose() => TriggerSafe(closeTrigger, openTrigger, holdTrigger, idleTrigger);

    [ContextMenu("테스트: Idle 복귀")]
    void TestIdle() => TriggerIdle();

    [ContextMenu("테스트: 사이클 1회 실행")]
    void TestOneCycle()
    {
        if (_isBusy) return;
        StartCoroutine(CloseOpenCycle());
    }

    [ContextMenu("테스트: 사이클 시작")]
    void TestStartCycle() => StartCycle();

    [ContextMenu("테스트: 사이클 중지")]
    void TestStopCycle() => StopCycle();
}
