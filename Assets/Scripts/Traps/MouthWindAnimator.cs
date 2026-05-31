using System.Collections;
using UnityEngine;

/// <summary>
/// WindTrap의 바람 흡입/방출에 맞춰 입 오브젝트의 Animator를 제어하는 컴포넌트.
///
/// [동작 흐름 — Pull(흡입)]
///   OnWindCharge  → doPullOpen  (입 벌리기)
///   chargeTime 후 → doPullHold  (입 벌린 채 바람 유지)
///   OnWindEnd     → doPullClose (입 닫기 복귀)
///
/// [동작 흐름 — Push(내뱉기)]
///   OnWindCharge  → doPushClose  (입 오므리기)
///   chargeTime 후 → doPushHold   (오므린 채 바람 유지)
///   OnWindEnd     → doPushOpen   (입 되돌리기)
///
/// [Animator 구성]
///   States   : Idle / PullOpen / PullHold / PushClose / PushHold
///   Triggers : doPullOpen / doPullHold / doPullClose
///              doPushClose / doPushHold / doPushOpen / doIdle
///   (모든 Transition: Has Exit Time = false, Duration = 0)
///   Hold 클립: Loop Time = true
///
/// [설정 방법]
///   1. WindTrap과 같은 GameObject에 부착
///   2. mouthAnimator : 입 메시의 Animator 연결 (비워두면 자식에서 자동 탐색)
///   3. chargeTime : Open/Close 클립 길이와 맞출 것 — WindTrap 발동이 이만큼 지연됨
/// </summary>
[RequireComponent(typeof(WindTrap))]
public class MouthWindAnimator : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("입 오브젝트의 Animator. 비워두면 자식에서 자동 탐색")]
    [SerializeField] Animator mouthAnimator = null;

    [Tooltip("MouthController 참조 (선택). 설정 시 MouthController 전환 연출이 끝난 후 바람 발동.\n" +
             "비워두면 동기화 없이 즉시 발동.")]
    [SerializeField] MouthController mouthController = null;

    [Header("Animator 파라미터 이름 — Pull(흡입)")]
    [SerializeField] string pullOpenTrigger  = "doPullOpen";
    [SerializeField] string pullHoldTrigger  = "doPullHold";
    [SerializeField] string pullCloseTrigger = "doPullClose";

    [Header("Animator 파라미터 이름 — Push(내뱉기)")]
    [SerializeField] string pushCloseTrigger = "doPushClose";
    [SerializeField] string pushHoldTrigger  = "doPushHold";
    [SerializeField] string pushOpenTrigger  = "doPushOpen";

    [Header("Animator 파라미터 이름 — 공용")]
    [SerializeField] string idleTrigger = "doIdle";

    [Header("타이밍 (초)")]
    [Tooltip("Open/Close 클립 길이(초). 이 값만큼 WindTrap 발동이 지연되어 입 애니메이션과 동기화됨.\n" +
             "예) 24fps 10프레임 = 0.417s")]
    [SerializeField] float chargeTime = 0f;

    WindTrap  _wind;
    Coroutine _holdCoroutine;

    void Awake()
    {
        _wind = GetComponent<WindTrap>();

        if (mouthAnimator == null)
            mouthAnimator = GetComponentInChildren<Animator>();

        // chargeTime만큼 WindTrap 발동을 지연 → 입 오픈/클로즈 애니메이션과 자동 동기화
        _wind.SetWindChargeTime(chargeTime);

        // MouthController 전환 연출 완료 대기 훅 등록
        if (mouthController != null)
            _wind.PreChargeHook = WaitForMouthIdle;
    }

    void OnEnable()
    {
        if (_wind == null) return;
        _wind.OnWindCharge += HandleWindCharge;
        _wind.OnWindEnd    += HandleWindEnd;
    }

    void OnDisable()
    {
        if (_wind != null)
        {
            _wind.OnWindCharge -= HandleWindCharge;
            _wind.OnWindEnd    -= HandleWindEnd;
        }

        if (_holdCoroutine != null) { StopCoroutine(_holdCoroutine); _holdCoroutine = null; }
        TriggerIdle();
    }

    // ── 이벤트 핸들러 ──────────────────────────────────────────────────────

    void HandleWindCharge()
    {
        if (_holdCoroutine != null) { StopCoroutine(_holdCoroutine); _holdCoroutine = null; }

        if (_wind.CurrentWindMode == WindTrap.WindMode.Pull)
        {
            TriggerSafe(pullOpenTrigger, pullHoldTrigger, pullCloseTrigger, pushCloseTrigger, pushHoldTrigger, pushOpenTrigger);
        }
        else
        {
            TriggerSafe(pushCloseTrigger, pullOpenTrigger, pullHoldTrigger, pullCloseTrigger, pushHoldTrigger, pushOpenTrigger);
        }

        // chargeTime 후 Hold 진입 — Wind가 실제로 부는 동안 입 상태를 유지
        _holdCoroutine = StartCoroutine(TriggerHoldAfterCharge());
    }

    void HandleWindEnd()
    {
        if (_holdCoroutine != null) { StopCoroutine(_holdCoroutine); _holdCoroutine = null; }

        if (_wind.CurrentWindMode == WindTrap.WindMode.Pull)
        {
            TriggerSafe(pullCloseTrigger, pullOpenTrigger, pullHoldTrigger, pushCloseTrigger, pushHoldTrigger, pushOpenTrigger);
        }
        else
        {
            TriggerSafe(pushOpenTrigger, pullOpenTrigger, pullHoldTrigger, pullCloseTrigger, pushCloseTrigger, pushHoldTrigger);
        }
    }

    // ── 코루틴 ─────────────────────────────────────────────────────────────

    IEnumerator TriggerHoldAfterCharge()
    {
        if (chargeTime > 0f)
            yield return new WaitForSeconds(chargeTime);

        // OnWindEnd가 chargeTime보다 빨리 왔으면 이미 _holdCoroutine이 null
        if (_wind == null || !_wind.IsWindActive) yield break;

        if (_wind.CurrentWindMode == WindTrap.WindMode.Pull)
            TriggerSafe(pullHoldTrigger, pullOpenTrigger, pullCloseTrigger, pushCloseTrigger, pushHoldTrigger, pushOpenTrigger);
        else
            TriggerSafe(pushHoldTrigger, pullOpenTrigger, pullHoldTrigger, pullCloseTrigger, pushCloseTrigger, pushOpenTrigger);

        _holdCoroutine = null;
    }

    /// <summary>
    /// MouthController 전환 연출이 진행 중이면 완료까지 대기. 안전 타임아웃 5초.
    /// </summary>
    IEnumerator WaitForMouthIdle()
    {
        if (mouthController == null) yield break;

        float elapsed = 0f;
        while (mouthController.IsBusy && elapsed < 5f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // ── 외부 호출 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 코루틴 중단 + Idle 복귀. SetActive 사이클 없이 외부에서 직접 리셋할 때 사용.
    /// </summary>
    public void ResetToNeutral()
    {
        if (_holdCoroutine != null) { StopCoroutine(_holdCoroutine); _holdCoroutine = null; }
        TriggerIdle();
    }

    // ── 트리거 헬퍼 ────────────────────────────────────────────────────────

    void TriggerIdle() => TriggerSafe(idleTrigger,
        pullOpenTrigger, pullHoldTrigger, pullCloseTrigger,
        pushCloseTrigger, pushHoldTrigger, pushOpenTrigger);

    void TriggerSafe(string trigger,
        string r1 = null, string r2 = null, string r3 = null,
        string r4 = null, string r5 = null, string r6 = null)
    {
        if (mouthAnimator == null) return;
        if (r1 != null) mouthAnimator.ResetTrigger(r1);
        if (r2 != null) mouthAnimator.ResetTrigger(r2);
        if (r3 != null) mouthAnimator.ResetTrigger(r3);
        if (r4 != null) mouthAnimator.ResetTrigger(r4);
        if (r5 != null) mouthAnimator.ResetTrigger(r5);
        if (r6 != null) mouthAnimator.ResetTrigger(r6);
        mouthAnimator.SetTrigger(trigger);
    }

    // ── 에디터 테스트 (플레이 중 컴포넌트 우클릭) ─────────────────────────

    [ContextMenu("테스트: Pull Open")]
    void TestPullOpen()
    {
        TriggerSafe(pullOpenTrigger, pullHoldTrigger, pullCloseTrigger,
                    pushCloseTrigger, pushHoldTrigger, pushOpenTrigger);
    }

    [ContextMenu("테스트: Pull Hold")]
    void TestPullHold()
    {
        TriggerSafe(pullHoldTrigger, pullOpenTrigger, pullCloseTrigger,
                    pushCloseTrigger, pushHoldTrigger, pushOpenTrigger);
    }

    [ContextMenu("테스트: Pull Close")]
    void TestPullClose()
    {
        TriggerSafe(pullCloseTrigger, pullOpenTrigger, pullHoldTrigger,
                    pushCloseTrigger, pushHoldTrigger, pushOpenTrigger);
    }

    [ContextMenu("테스트: Push Close")]
    void TestPushClose()
    {
        TriggerSafe(pushCloseTrigger, pullOpenTrigger, pullHoldTrigger,
                    pullCloseTrigger, pushHoldTrigger, pushOpenTrigger);
    }

    [ContextMenu("테스트: Push Hold")]
    void TestPushHold()
    {
        TriggerSafe(pushHoldTrigger, pullOpenTrigger, pullHoldTrigger,
                    pullCloseTrigger, pushCloseTrigger, pushOpenTrigger);
    }

    [ContextMenu("테스트: Push Open")]
    void TestPushOpen()
    {
        TriggerSafe(pushOpenTrigger, pullOpenTrigger, pullHoldTrigger,
                    pullCloseTrigger, pushCloseTrigger, pushHoldTrigger);
    }

    [ContextMenu("테스트: Idle 복귀")]
    void TestIdle() => TriggerIdle();
}
