using System.Collections;
using UnityEngine;

/// <summary>
/// Animator 기반 입 열기/닫기 연출 컴포넌트 (ArrowTrap 발사 동기화).
///
/// [일반 모드 동작 흐름]
///   OnPreFireCharge → doOpen  (입 벌리기 시작)
///   openClipLength 후 → 발사 → doHold (입 완전히 열린 채 유지)
///   발사체가 입 탈출 트리거 통과 → NotifyProjectileExited() → doClose
///   closeClipLength 후 → doIdle
///
/// [루프 모드 (loopOpenClose = true)]
///   Open → Hold(loopHoldDuration) → Close → (loopInterval) → Open ... 무한 반복
///   발사체로 날아가는 동안 사용. TrapBase 이벤트 무시.
///   SetLoopMode(bool) 로 런타임 전환 가능.
///
/// [Animator 구성]
///   States   : Idle / Open / Hold / Close
///   Triggers : doOpen / doHold / doClose / doIdle
///   (모든 Transition: Has Exit Time = false, Duration = 0, Can Transition to Self = false)
///   Hold 클립: Loop Time = true
///
/// [입 탈출 트리거 설정]
///   입 앞에 빈 오브젝트 생성 → BoxCollider (Is Trigger = true) 추가
///   MouthExitTrigger 컴포넌트 부착 → mouthAnim 연결
///   콜라이더 두께 얇게 (0.1~0.3), 입 출구 바로 앞에 배치
/// </summary>
public class MouthTrapAnimatorAnim : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("입 메시의 Animator. 비워두면 자식에서 자동 탐색")]
    [SerializeField] private Animator mouthAnimator = null;

    [Header("Animator 파라미터 이름")]
    [Tooltip("입 여는 트리거")]
    [SerializeField] private string openTrigger  = "doOpen";
    [Tooltip("입 열린 채 유지 트리거 (Hold 상태 진입)")]
    [SerializeField] private string holdTrigger  = "doHold";
    [Tooltip("입 닫는 트리거")]
    [SerializeField] private string closeTrigger = "doClose";
    [Tooltip("Idle 복귀 트리거")]
    [SerializeField] private string idleTrigger  = "doIdle";

    [Header("클립 길이 (초) — Unity Inspector 클립 Length와 정확히 맞출 것")]
    [Tooltip("Open 클립 길이(초).\n" +
             "이 값이 preFireChargeTime으로 자동 설정 → Open 끝나는 순간 발사됨.\n" +
             "예) 24fps 5프레임 = 0.208s / 30fps 5프레임 = 0.167s")]
    [SerializeField] private float openClipLength  = 0f;

    [Tooltip("Close 클립 길이(초).\n" +
             "이 시간 후 doIdle 자동 발행.\n" +
             "예) 24fps 7프레임 = 0.292s / 30fps 7프레임 = 0.233s")]
    [SerializeField] private float closeClipLength = 0f;

    [Header("루프 모드 (발사체로 사용 시)")]
    [Tooltip("true: Open→Hold→Close 무한 반복. TrapBase 이벤트 무시.")]
    [SerializeField] private bool  loopOpenClose    = false;

    [Tooltip("루프 모드에서 Hold 유지 시간(초). 0 = 즉시 Close로 전환")]
    [SerializeField] private float loopHoldDuration = 0f;

    [Tooltip("루프 모드에서 Close 끝 후 다음 Open까지 추가 대기 시간(초). 0 = 즉시")]
    [SerializeField] private float loopInterval     = 0f;

    TrapBase  _trap;
    Coroutine _idleReturnCoroutine;
    Coroutine _loopCoroutine;

    void Awake()
    {
        _trap = GetComponent<TrapBase>();

        if (mouthAnimator == null)
            mouthAnimator = GetComponentInChildren<Animator>();

        // Open 클립 끝 = 입 완전히 열린 시점 = 발사 시점
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

        TriggerSafe(idleTrigger, openTrigger, holdTrigger, closeTrigger);
    }

    // ── 이벤트 핸들러 (일반 모드) ──────────────────────────────────────────

    void HandlePreFireCharge()
    {
        // 발사 전: 진행 중인 복귀 코루틴 취소 후 Open 시작
        // 연속 발사 시 Close/Idle 도중에도 바로 Open으로 자연스럽게 전환됨
        if (_idleReturnCoroutine != null)
        {
            StopCoroutine(_idleReturnCoroutine);
            _idleReturnCoroutine = null;
        }

        TriggerOpen();
    }

    void HandleFiring()
    {
        // 발사 순간 → Hold 진입 (입 완전히 열린 채 유지)
        // Close는 발사체가 입 밖으로 나간 뒤 NotifyProjectileExited()에서 발동
        TriggerHold();
    }

    /// <summary>
    /// 입 앞 탈출 트리거에서 발사체가 나갔을 때 MouthExitTrigger가 호출.
    /// Hold → Close → Idle 순서로 진행.
    /// </summary>
    public void NotifyProjectileExited()
    {
        if (_idleReturnCoroutine != null) StopCoroutine(_idleReturnCoroutine);
        TriggerClose();
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
            yield return new WaitForSeconds(Mathf.Max(0f, openClipLength));

            TriggerHold();
            yield return new WaitForSeconds(Mathf.Max(0f, loopHoldDuration));

            TriggerClose();
            yield return new WaitForSeconds(Mathf.Max(0f, closeClipLength + loopInterval));
        }
    }

    // ── 루프 모드 런타임 전환 ──────────────────────────────────────────────

    /// <summary>
    /// 런타임에서 루프 모드 전환.
    /// 발사체가 스폰될 때 외부에서 호출하면 즉시 Open→Hold→Close 루프 시작.
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

    void TriggerOpen()  => TriggerSafe(openTrigger,  holdTrigger,  closeTrigger, idleTrigger);
    void TriggerHold()  => TriggerSafe(holdTrigger,  openTrigger,  closeTrigger, idleTrigger);
    void TriggerClose() => TriggerSafe(closeTrigger, openTrigger,  holdTrigger,  idleTrigger);
    void TriggerIdle()  => TriggerSafe(idleTrigger,  openTrigger,  holdTrigger,  closeTrigger);

    void TriggerSafe(string trigger, string reset1 = null, string reset2 = null, string reset3 = null)
    {
        if (mouthAnimator == null) return;
        if (reset1 != null) mouthAnimator.ResetTrigger(reset1);
        if (reset2 != null) mouthAnimator.ResetTrigger(reset2);
        if (reset3 != null) mouthAnimator.ResetTrigger(reset3);
        mouthAnimator.SetTrigger(trigger);
    }

    // ── 에디터 테스트 (플레이 중 컴포넌트 우클릭) ─────────────────────────

    [ContextMenu("테스트: Open")]
    void TestOpen()  => TriggerOpen();

    [ContextMenu("테스트: Hold")]
    void TestHold()  => TriggerHold();

    [ContextMenu("테스트: Close")]
    void TestClose() => TriggerClose();

    [ContextMenu("테스트: Idle")]
    void TestIdle()  => TriggerIdle();

    [ContextMenu("테스트: 루프 시작")]
    void TestStartLoop() => StartLoop();
}
