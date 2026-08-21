using System.Collections;
using UnityEngine;

/// <summary>
/// 입 배경 연출 컨트롤러 — Animator 기반, 함정처럼 랜덤 스케줄로 동작.
///
/// 배경 입은 평소 열린 상태(Idle)이므로 사이클은 닫기 → Hold → 열기 순서.
///
/// [동작 흐름]
///   initialDelay 대기
///   → 랜덤 간격(randomIntervalMin ~ randomIntervalMax) 대기
///   → doClose (입 닫기 시작) → closeClipLength 대기
///   → doHold (입 닫힌 채 유지) → holdDuration 대기
///   → doOpen (입 열기) → openClipLength 대기
///   → doIdle 복귀
///   → 다시 랜덤 대기 반복
///
/// [Animator 구성]
///   States   : Idle / Close / Hold / Open
///   Triggers : doClose / doHold / doOpen / doIdle
///   (모든 Transition: Has Exit Time = false, Duration = 0)
///   Idle 클립 : Loop Time = true
///   Hold 클립 : Loop Time = true  ← holdDuration 동안 입 닫힌 채 유지
///
/// [설정 방법]
///   1. 이 스크립트를 빈 GameObject에 부착 (배경 입 메시와 분리)
///   2. mouthAnimator : 배경 입 메시의 Animator를 Inspector에서 직접 연결
///   3. 클립 길이 필드를 실제 Animator 클립 Length와 정확히 맞출 것
/// </summary>
public class MouthController : MonoBehaviour
{
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

    [Tooltip("Hold 유지 시간(초). 이 시간 동안 입이 닫힌 채 루프함.")]
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

    [Header("암전 연동 (선택)")]
    [Tooltip("입 닫힐 때 FadeOut, 열릴 때 FadeIn 을 자동 호출.\n비워두면 암전 없음.")]
    [SerializeField] private ScreenFader screenFader = null;

    [Header("네트워크 시드 (Host/Client 동기화)")]
    [Tooltip("씬당 배경 입은 1개뿐이라 상수로 고정. 다른 Random 사용처(WindTrap 등)와 값이 겹치지 않게 임의의 값 유지.")]
    [SerializeField] private int seedSalt = 0x4D4F5554;

    Coroutine _cycleCoroutine;
    bool _isBusy;
    int _cycleCount;

    /// <summary>현재 Close/Hold/Open 사이클 진행 중이면 true.</summary>
    public bool IsBusy => _isBusy;

    void Awake()
    {
        if (mouthAnimator == null)
            mouthAnimator = GetComponentInChildren<Animator>();
    }

    void OnEnable()
    {
        _isBusy = false;
        _cycleCount = 0;
        TriggerIdle();
        if (startOnAwake)
            StartCycle();
    }

    void OnDisable()
    {
        StopAllCoroutines();
        _cycleCoroutine = null;
        _isBusy = false;
    }

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>랜덤 사이클 시작.</summary>
    public void StartCycle()
    {
        if (_cycleCoroutine != null) StopCoroutine(_cycleCoroutine);
        _cycleCoroutine = StartCoroutine(AutoCycle());
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
        TriggerIdle();
    }

    // ── 코루틴 ────────────────────────────────────────────────────

    IEnumerator AutoCycle()
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            // Host/Client가 같은 간격을 뽑도록 세션 시드 기반으로 InitState 후 뽑는다
            // (WindTrap.OnWindCharge Random 모드와 동일 관례 — RPC 없이 시드만 동일하면 됨).
            int mixedSeed = NetworkSessionData.Seed ^ seedSalt ^ (_cycleCount * 0x2545F491);
            UnityEngine.Random.InitState(mixedSeed);
            float interval = Random.Range(randomIntervalMin, randomIntervalMax);
            _cycleCount++;

            yield return new WaitForSeconds(interval);

            yield return StartCoroutine(CloseOpenCycle());
        }
    }

    IEnumerator CloseOpenCycle()
    {
        _isBusy = true;

        // 닫기 (Idle 열린 상태 → 닫힘)
        TriggerSafe(closeTrigger, openTrigger, holdTrigger, idleTrigger);
        screenFader?.FadeOut(closeClipLength > 0f ? closeClipLength : 0f);
        if (closeClipLength > 0f)
            yield return new WaitForSeconds(closeClipLength);

        // Hold (닫힌 채 유지)
        TriggerSafe(holdTrigger, closeTrigger, openTrigger, idleTrigger);
        if (holdDuration > 0f)
            yield return new WaitForSeconds(holdDuration);

        // 열기 (닫힘 → 다시 열림)
        TriggerSafe(openTrigger, closeTrigger, holdTrigger, idleTrigger);
        screenFader?.FadeIn(openClipLength > 0f ? openClipLength : 0f);
        if (openClipLength > 0f)
            yield return new WaitForSeconds(openClipLength);

        // Idle 복귀
        TriggerIdle();
        _isBusy = false;
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
