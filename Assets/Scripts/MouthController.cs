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
    double _resyncDeadline = -1d;

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

    public void Revert()
    {
        if (!teamCheerHazard || !_available) return;

        _syncGeneration++;
        _resyncDeadline = GetServerTime() + PickSeededInterval(_syncGeneration);

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
        }
    }

    // ── 코루틴 ────────────────────────────────────────────────────

    IEnumerator AutoCycle()
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            yield return new WaitForSeconds(PickSeededInterval(_cycleCount));
            _cycleCount++;
            yield return StartCoroutine(CloseOpenCycle());
        }
    }

    IEnumerator HazardCycle()
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            if (_resyncDeadline > 0d)
            {
                yield return WaitUntilServerTime(_resyncDeadline);
                _resyncDeadline = -1d;
            }
            else
            {
                yield return new WaitForSeconds(PickSeededInterval(_cycleCount));
                _cycleCount++;
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
                continue;
            }

            _phase = HazardPhase.Holding;
            TriggerSafe(holdTrigger, closeTrigger, openTrigger, idleTrigger);
            while (!_recoverQueued)
                yield return null;

            _recoverQueued = false;
            yield return OpenRoutine();
        }
    }

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
        _phase = HazardPhase.Idle;
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

    IEnumerator WaitUntilServerTime(double deadline)
    {
        while (GetServerTime() < deadline)
            yield return null;
    }

    float PickSeededInterval(int generation)
    {
        int mixedSeed = NetworkSessionData.Seed ^ seedSalt ^ (generation * 0x2545F491);
        UnityEngine.Random.InitState(mixedSeed);
        float min = randomIntervalMin;
        float max = Mathf.Max(min, randomIntervalMax);
        return Random.Range(min, max);
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
