using System.Collections;
using UnityEngine;

/// <summary>
/// Animator 기반 입 열기/닫기 연출 컴포넌트 (ArrowTrap 발사 동기화).
///
/// [일반 모드 동작 흐름]
///   OnPreFireCharge → doOpen  (입 벌리기 시작)
///   openClipLength 후 → 발사 (입 완전히 열린 상태)
///   OnFiring        → doClose (입 닫기 시작)
///   closeClipLength 후 → doIdle (idle 복귀)
///   다음 발사가 오면 → doOpen 재시작 (idle/close 중단)
///
/// [루프 모드 (loopOpenClose = true)]
///   Open → Close → Open → Close ... 무한 반복
///   발사체로 날아가는 동안 사용. TrapBase 이벤트 무시.
///   SetLoopMode(bool) 로 런타임 전환 가능.
///
/// [Animator 구성]
///   States   : Idle / Open / Close
///   Triggers : doOpen / doClose / doIdle
///   (모든 Transition: Has Exit Time = false, Duration = 0, Can Transition to Self = false)
/// </summary>
public class MouthTrapAnimatorAnim : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("입 메시의 Animator. 비워두면 자식에서 자동 탐색")]
    [SerializeField] private Animator mouthAnimator = null;

    [Header("Animator 파라미터 이름")]
    [Tooltip("입 여는 트리거. Animator의 Trigger 파라미터 이름과 일치시킬 것")]
    [SerializeField] private string openTrigger  = "doOpen";
    [Tooltip("입 닫는 트리거. Animator의 Trigger 파라미터 이름과 일치시킬 것")]
    [SerializeField] private string closeTrigger = "doClose";
    [Tooltip("Idle 복귀 트리거. Animator의 Trigger 파라미터 이름과 일치시킬 것")]
    [SerializeField] private string idleTrigger  = "doIdle";

    [Header("클립 길이 (초) — Blender 클립 길이와 정확히 맞출 것")]
    [Tooltip("Open 애니메이션 클립 길이(초).\n" +
             "이 값이 preFireChargeTime으로 자동 설정되므로 실제 클립 길이와 반드시 일치해야 함.\n" +
             "예) 30fps 기준 9프레임 = 0.30s")]
    [SerializeField] private float openClipLength  = 0f;

    [Tooltip("Close 애니메이션 클립 길이(초).\n" +
             "발사 직후 닫히는 시간. 이 시간 후 Idle로 복귀.\n" +
             "예) 30fps 기준 8프레임 = 0.267s")]
    [SerializeField] private float closeClipLength = 0f;

    [Header("루프 모드 (발사체로 사용 시)")]
    [Tooltip("true: Open↔Close 무한 반복. TrapBase 이벤트 무시.\n" +
             "false: 발사 스케줄에 동기화 (일반 함정 사용).")]
    [SerializeField] private bool  loopOpenClose = false;

    [Tooltip("루프 모드에서 Open→Close, Close→Open 전환 사이 추가 대기 시간(초). 0 = 즉시 전환")]
    [SerializeField] private float loopInterval  = 0f;

    TrapBase  _trap;
    Coroutine _idleReturnCoroutine;
    Coroutine _loopCoroutine;

    void Awake()
    {
        _trap = GetComponent<TrapBase>();

        if (mouthAnimator == null)
            mouthAnimator = GetComponentInChildren<Animator>();

        // 루프 모드가 아닐 때만 발사 타이밍 자동 설정
        // openClipLength = Open 애니메이션이 끝나는 시점 = 입이 완전히 열린 시점 = 발사 시점
        if (!loopOpenClose)
        {
            if (_trap != null)
                _trap.SetPreFireChargeTime(openClipLength);
            else
                Debug.LogWarning($"[MouthTrapAnimatorAnim] {name}: loopOpenClose=false인데 TrapBase가 없습니다. 발사 동기화가 동작하지 않습니다.", this);
        }
    }

    void OnEnable()
    {
        if (loopOpenClose)
        {
            StartLoop();
            return;
        }

        if (_trap == null) return;
        _trap.OnPreFireCharge += HandlePreFireCharge;
        _trap.OnFiring        += HandleFiring;
    }

    void OnDisable()
    {
        StopAllCoroutines();
        _idleReturnCoroutine = null;
        _loopCoroutine       = null;

        if (!loopOpenClose && _trap != null)
        {
            _trap.OnPreFireCharge -= HandlePreFireCharge;
            _trap.OnFiring        -= HandleFiring;
        }

        TriggerSafe(idleTrigger, openTrigger, closeTrigger);
    }

    // ── 이벤트 핸들러 (일반 모드) ──────────────────────────────────────────

    void HandlePreFireCharge()
    {
        // 발사 전 : idle/close 복귀 코루틴 취소 후 Open 트리거
        // 연속 발사 시 닫히다가 다시 열림도 자연스럽게 처리됨
        if (_idleReturnCoroutine != null)
        {
            StopCoroutine(_idleReturnCoroutine);
            _idleReturnCoroutine = null;
        }

        TriggerOpen();
    }

    void HandleFiring()
    {
        // 발사 순간 (입이 완전히 열린 상태) → 닫기 시작
        TriggerClose();

        // closeClipLength 후 다음 PreFireCharge가 없으면 Idle 복귀
        if (_idleReturnCoroutine != null) StopCoroutine(_idleReturnCoroutine);
        _idleReturnCoroutine = StartCoroutine(ReturnToIdleAfterClose());
    }

    // ── 코루틴 ─────────────────────────────────────────────────────────────

    IEnumerator ReturnToIdleAfterClose()
    {
        yield return new WaitForSeconds(closeClipLength);
        TriggerIdle();
        _idleReturnCoroutine = null;
    }

    void StartLoop()
    {
        if (_loopCoroutine != null) StopCoroutine(_loopCoroutine);
        _loopCoroutine = StartCoroutine(LoopRoutine());
    }

    IEnumerator LoopRoutine()
    {
        while (true)
        {
            TriggerOpen();
            yield return new WaitForSeconds(Mathf.Max(0f, openClipLength + loopInterval));
            TriggerClose();
            yield return new WaitForSeconds(Mathf.Max(0f, closeClipLength + loopInterval));
        }
    }

    // ── 루프 모드 런타임 전환 ──────────────────────────────────────────────

    /// <summary>
    /// 런타임에서 루프 모드 전환.
    /// 발사체(Projectile)가 스폰될 때 외부에서 호출하면 즉시 Open↔Close 루프 시작.
    /// </summary>
    public void SetLoopMode(bool enable)
    {
        if (loopOpenClose == enable) return;
        loopOpenClose = enable;

        if (enable)
        {
            if (_trap != null)
            {
                _trap.OnPreFireCharge -= HandlePreFireCharge;
                _trap.OnFiring        -= HandleFiring;
            }
            if (_idleReturnCoroutine != null)
            {
                StopCoroutine(_idleReturnCoroutine);
                _idleReturnCoroutine = null;
            }
            StartLoop();
        }
        else
        {
            if (_loopCoroutine != null)
            {
                StopCoroutine(_loopCoroutine);
                _loopCoroutine = null;
            }
            if (_trap != null)
            {
                _trap.OnPreFireCharge += HandlePreFireCharge;
                _trap.OnFiring        += HandleFiring;
                _trap.SetPreFireChargeTime(openClipLength);
            }
            else
            {
                Debug.LogWarning($"[MouthTrapAnimatorAnim] {name}: loopMode=false 전환 시 TrapBase가 없습니다.", this);
            }
            TriggerIdle();
        }
    }

    // ── 트리거 헬퍼 ────────────────────────────────────────────────────────

    void TriggerOpen()  => TriggerSafe(openTrigger,  closeTrigger, idleTrigger);
    void TriggerClose() => TriggerSafe(closeTrigger, openTrigger,  idleTrigger);
    void TriggerIdle()  => TriggerSafe(idleTrigger,  openTrigger,  closeTrigger);

    void TriggerSafe(string trigger, string reset1 = null, string reset2 = null)
    {
        if (mouthAnimator == null) return;
        if (reset1 != null) mouthAnimator.ResetTrigger(reset1);
        if (reset2 != null) mouthAnimator.ResetTrigger(reset2);
        mouthAnimator.SetTrigger(trigger);
    }

    // ── 에디터 테스트 (플레이 중 컴포넌트 우클릭) ─────────────────────────

    [ContextMenu("테스트: Open")]
    void TestOpen()  => TriggerOpen();

    [ContextMenu("테스트: Close")]
    void TestClose() => TriggerClose();

    [ContextMenu("테스트: Idle")]
    void TestIdle()  => TriggerIdle();

    [ContextMenu("테스트: 루프 시작")]
    void TestStartLoop() => StartLoop();
}
