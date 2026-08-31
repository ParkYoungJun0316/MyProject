using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Animator 기반 입 열기/닫기 연출 컴포넌트 (ArrowTrap 발사 동기화).
///
/// [동기화 방식 — Mouth↔Arrow 타이밍 수정]
/// NetworkBehaviour가 아닌 순수 MonoBehaviour. Client의 NetworkManager.ServerTime은
/// Host 시각의 "추정치"라 Host/Client가 각자 로컬 TrapBase 이벤트(OnPreFireCharge/
/// OnFiring)를 독립적으로 트리거하면 화살 Spawn(Host 발행 신호)과 서로 다른 시계에서
/// 출발해 어긋난다. 그래서 Host만 로컬 TrapBase 이벤트를 직접 구독해 반응하고(zero
/// latency, 아래서 IsServer 가드), Client는 이 로컬 이벤트를 절대 구독하지 않는다 —
/// ArrowTrap이 Host에서만 StageNetworkState.SyncArrowChargeClientRpc/
/// SyncArrowFireClientRpc로 릴레이하고, Client는 그 RPC로 도착한 PlayOpenFromNetwork/
/// PlayHoldFromNetwork를 통해서만 재생한다(DropTrap의 SyncDropWarnClientRpc와 동일 패턴).
/// 실제 발사체 스폰·데미지는 ArrowTrap.OnTrapTrigger / TrapProjectile 쪽에서 별도로
/// IsServer로 보호된다.
///
/// [일반 모드 동작 흐름]
///   OnPreFireCharge → doOpen  (입 벌리기 시작)
///   openClipLength 후 → doHold (자체 타이머로 선(先) 전환 — 아래 참고)
///   실제 발사(OnFiring) → doHold (재확정, holdDuration 타이머 시작)
///   holdDuration 후 → doClose (고정 타이머 — 발사체 도착/탈출 이벤트에 의존하지 않음)
///   closeClipLength 후 → doIdle
///   (Host만 발사체를 Spawn하고 Client는 복제 수신 후 로컬 비행을 시작하므로,
///    Close를 발사체 탈출 이벤트에 묶으면 Client의 Hold가 Spawn/RPC 왕복 시간만큼
///    Host보다 길어진다 — 그래서 Close는 순수 로컬 타이머로 분리한다.)
///
/// [Open→Hold 자체 타이머 — ArrowWarnSign 등과 공존 시 필수]
/// TrapBase.preFireChargeTime은 여러 컴포넌트(Mouth/ArrowWarnSign)가 SetPreFireChargeTime을
/// 부르면 그중 가장 긴 값으로 병합된다(TrapBase 참고). ArrowWarnSign의 warnLeadTime이
/// openClipLength보다 길면 실제 발사(OnFiring)가 Open 클립이 끝난 뒤에도 한참 늦게 온다 —
/// 이 사이에 doHold 트리거가 없으면 Animator가 Open의 마지막 프레임에서 트리거 없이 멈춰
/// 있게 된다(WindTrap의 MouthWindAnimator.TriggerHoldAfterCharge와 동일 문제). 그래서 이
/// 컴포넌트는 openClipLength 후 스스로 doHold를 걸어 Hold(Loop) 상태로 선전환한다 — 실제
/// OnFiring이 그보다 늦게 오면 도착 시 doHold를 다시 걸지만 Hold는 Can Transition to Self
/// = false라 재트리거는 무해하고, holdDuration 타이머만 그 시점(=실제 발사 시점)부터
/// 새로 시작된다.
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

    [Header("일반 모드 Hold 유지 시간")]
    [Tooltip("발사(doHold) 후 doClose까지 대기하는 고정 시간(초).\n" +
             "Host/Client 모두 로컬 타이머로 동일하게 적용 — 발사체 탈출 이벤트에 의존하지 않음.")]
    [SerializeField] private float holdDuration = 0f;

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
    Coroutine _openWaitCoroutine;

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

        // Host만 로컬 TrapBase 이벤트를 직접 구독. Client는 자기 로컬 스케줄(부정확한
        // ServerTime 추정)로 이 이벤트를 받으면 Host가 보낸 RPC(아래 PlayOpenFromNetwork/
        // PlayHoldFromNetwork)와 같은 Animator를 두고 경쟁해 트리거가 뒤섞인다.
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        _trap.OnPreFireCharge += PlayOpenFromNetwork;
        _trap.OnFiring        += PlayHoldFromNetwork;
    }

    void OnDisable()
    {
        StopAllCoroutines();
        _idleReturnCoroutine = null;
        _loopCoroutine       = null;
        _openWaitCoroutine   = null;

        if (!loopOpenClose && _trap != null)
        {
            _trap.OnPreFireCharge -= PlayOpenFromNetwork;
            _trap.OnFiring        -= PlayHoldFromNetwork;
        }

        TriggerSafe(idleTrigger, openTrigger, holdTrigger, closeTrigger);
    }

    // ── 재생 진입점 (Host: 로컬 TrapBase 이벤트 직접 구독 / Client: ArrowTrap.PlayChargeById·
    // PlayFireById가 StageNetworkState RPC 수신 시 호출) ───────────────────

    /// <summary>입 벌리기 시작. Host는 OnPreFireCharge 직접 구독, Client는 SyncArrowChargeClientRpc 수신으로 호출됨.</summary>
    public void PlayOpenFromNetwork()
    {
        if (_idleReturnCoroutine != null)
        {
            StopCoroutine(_idleReturnCoroutine);
            _idleReturnCoroutine = null;
        }
        if (_openWaitCoroutine != null)
        {
            StopCoroutine(_openWaitCoroutine);
            _openWaitCoroutine = null;
        }

        TriggerOpen();

        // openClipLength 후 스스로 doHold로 선전환 (위 클래스 주석 "Open→Hold 자체 타이머" 참고).
        // 실제 발사(OnFiring)가 그보다 늦게 오면 PlayHoldFromNetwork가 이 코루틴을 취소하고
        // holdDuration 타이머를 그 시점부터 새로 시작한다.
        if (openClipLength > 0f)
            _openWaitCoroutine = StartCoroutine(HoldAfterOpenClipRoutine());
    }

    /// <summary>발사(Hold) 확정. Host는 OnFiring 직접 구독, Client는 SyncArrowFireClientRpc 수신으로 호출됨.</summary>
    public void PlayHoldFromNetwork()
    {
        if (_idleReturnCoroutine != null)
        {
            StopCoroutine(_idleReturnCoroutine);
            _idleReturnCoroutine = null;
        }
        if (_openWaitCoroutine != null)
        {
            StopCoroutine(_openWaitCoroutine);
            _openWaitCoroutine = null;
        }

        TriggerHold();
        _idleReturnCoroutine = StartCoroutine(HoldThenCloseRoutine());
    }

    // ── 코루틴 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Open 클립 재생 시간만큼만 기다렸다가 doHold로 선전환 — preFireChargeTime이 openClipLength
    /// 보다 길게 병합돼(ArrowWarnSign 등) 실제 발사가 늦게 와도 Open의 마지막 프레임에 트리거
    /// 없이 멈춰 있지 않도록 한다. holdDuration 타이머는 시작하지 않음(실제 발사 시점부터
    /// PlayHoldFromNetwork가 새로 시작).
    /// </summary>
    IEnumerator HoldAfterOpenClipRoutine()
    {
        yield return new WaitForSeconds(openClipLength);
        TriggerHold();
        _openWaitCoroutine = null;
    }

    /// <summary>
    /// doHold 후 holdDuration → doClose → closeClipLength → doIdle.
    /// 발사체 탈출 이벤트에 의존하지 않는 고정 로컬 타이머 (Host/Client 동일 적용).
    /// </summary>
    IEnumerator HoldThenCloseRoutine()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, holdDuration));
        TriggerClose();

        yield return new WaitForSeconds(Mathf.Max(0f, closeClipLength));
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
            // 각 피어가 로컬로 직접 트리거 (RPC relay 없음, NetworkObject Spawn 시점 자체가 동기화 기준)
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
                _trap.OnPreFireCharge -= PlayOpenFromNetwork;
                _trap.OnFiring        -= PlayHoldFromNetwork;
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
                // Host만 로컬 이벤트 재구독 — OnEnable과 동일한 이유(위 클래스 주석 참조).
                var nm = NetworkManager.Singleton;
                if (nm == null || !nm.IsServer)
                {
                    Debug.LogWarning($"[MouthTrapAnimatorAnim] {name}: Client에서 loopMode=false 전환 — 로컬 이벤트 구독 안 함(RPC로만 재생).", this);
                }
                else
                {
                    _trap.OnPreFireCharge += PlayOpenFromNetwork;
                    _trap.OnFiring        += PlayHoldFromNetwork;
                }
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
        if (mouthAnimator == null)
        {
            Debug.LogWarning($"[MouthTrapAnimatorAnim] {name}: mouthAnimator null — {trigger} 재생 스킵", this);
            return;
        }
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
