using System.Collections;
using UnityEngine;

/// <summary>
/// 모든 함정의 추상 기반 클래스.
/// 일정 간격(activateInterval)마다 OnTrapTrigger를 호출.
/// 새 함정 추가 시 이 클래스를 상속받아 OnTrapTrigger만 구현.
/// </summary>
public abstract class TrapBase : MonoBehaviour
{
    [Header("Trap - Base")]
    [Tooltip("함정 발동 주기(초). 0이면 단발")]
    [SerializeField] protected float activateInterval = 0f;

    [Tooltip("게임 시작 후 첫 발동까지의 딜레이(초)")]
    [SerializeField] protected float initialDelay = 0f;

    [Tooltip("시작 시 자동 활성화 여부")]
    [SerializeField] protected bool startActive = true;

    protected bool isRunning;
    Coroutine trapCoroutine;

    /// <summary>발사 chargeTime 전에 호출됨. 구체 애니메이션 컴포넌트(MouthTrapAnimator 등)가 구독.</summary>
    public event System.Action OnPreFireCharge;

    /// <summary>발사 직전(프로젝타일 생성 직전)에 호출됨.</summary>
    public event System.Action OnFiring;

    /// <summary>MouthTrapAnimator 등이 Awake에서 설정. 이 시간만큼 앞당겨 OnPreFireCharge를 발행하고 발사를 지연.</summary>
    protected float preFireChargeTime = 0f;

    public void SetPreFireChargeTime(float t) => preFireChargeTime = Mathf.Max(0f, t);

    protected virtual void Start()
    {
        // OnEnable이 먼저 호출되므로 Start에서는 중복 활성화 방지
        if (startActive && !isRunning) Activate();
    }

    // Stage SetActive(false → true) 사이클 시 자동 리셋
    protected virtual void OnEnable()
    {
        if (startActive) Activate();
    }

    protected virtual void OnDisable()
    {
        isRunning = false;
        if (trapCoroutine != null)
        {
            StopCoroutine(trapCoroutine);
            trapCoroutine = null;
        }
    }

    /// <summary>함정 활성화. 이미 실행 중이면 무시.</summary>
    public void Activate()
    {
        if (isRunning) return;
        isRunning = true;
        trapCoroutine = StartCoroutine(TrapLoop());
    }

    /// <summary>함정 비활성화. 진행 중인 루프를 중단.</summary>
    public void Deactivate()
    {
        isRunning = false;
        if (trapCoroutine != null)
        {
            StopCoroutine(trapCoroutine);
            trapCoroutine = null;
        }
        OnDeactivated();
    }

    protected virtual IEnumerator TrapLoop()
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        while (isRunning)
        {
            yield return StartCoroutine(FireWithCharge());

            if (activateInterval > 0f)
                yield return new WaitForSeconds(activateInterval);
            else
            {
                isRunning = false;
                yield break;
            }
        }
    }

    /// <summary>
    /// preFireChargeTime 이 0보다 크면 OnPreFireCharge → 대기 → OnFiring → OnTrapTrigger 순서로 실행.
    /// 0이면 즉시 OnFiring → OnTrapTrigger.
    /// </summary>
    protected IEnumerator FireWithCharge()
    {
        if (preFireChargeTime > 0f)
        {
            OnPreFireCharge?.Invoke();
            yield return new WaitForSeconds(preFireChargeTime);
        }
        OnFiring?.Invoke();
        OnTrapTrigger();
    }

    /// <summary>함정이 발동될 때 호출. 하위 클래스에서 구현.</summary>
    protected abstract void OnTrapTrigger();

    /// <summary>Deactivate 시 후처리가 필요한 경우 오버라이드.</summary>
    protected virtual void OnDeactivated() { }
}
