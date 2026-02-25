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

    protected virtual void Start()
    {
        if (startActive) Activate();
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

    IEnumerator TrapLoop()
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        while (isRunning)
        {
            OnTrapTrigger();

            if (activateInterval > 0f)
                yield return new WaitForSeconds(activateInterval);
            else
            {
                isRunning = false;
                yield break;
            }
        }
    }

    /// <summary>함정이 발동될 때 호출. 하위 클래스에서 구현.</summary>
    protected abstract void OnTrapTrigger();

    /// <summary>Deactivate 시 후처리가 필요한 경우 오버라이드.</summary>
    protected virtual void OnDeactivated() { }
}
