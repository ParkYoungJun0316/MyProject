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

    // 스테이지 클리어로 "이 씬은 끝났다"가 확정된 상태. isRunning과 별개로 둬야 하는 이유:
    // 감독 컴포넌트(ArrowIncomingDirector / TrapPlayerTracker)가 붙는 레인은 startActive=false로
    // 자체 루프를 꺼두기 때문에 isRunning이 애초에 항상 false다 — 그래서 ArrowTrap.FireOnce()의
    // `if (isRunning) return;` 류 가드로는 Deactivate 이후의 단발 발사를 막을 수 없었다
    // (2026-09-08 리뷰: 클리어 후에도 화살·낙하물이 계속 나오던 원인).
    // Activate()가 해제하므로 Phase 재활성화 흐름에는 영향 없다.
    protected bool isFrozen;

    Coroutine trapCoroutine;
    Coroutine fireCoroutine;

    // 부모 계층에 StageManager가 있으면 startActive를 무시하고 StageManager.StartStage()로만 시작
    bool _hasStageManager;

    /// <summary>발사 chargeTime 전에 호출됨. 구체 애니메이션 컴포넌트(MouthTrapAnimatorAnim 등)가 구독.</summary>
    public event System.Action OnPreFireCharge;

    /// <summary>발사 직전(프로젝타일 생성 직전)에 호출됨.</summary>
    public event System.Action OnFiring;

    /// <summary>MouthTrapAnimatorAnim / ArrowWarnSign 등이 Awake에서 설정. 이 시간만큼 앞당겨 OnPreFireCharge를 발행하고 발사를 지연.
    /// 여러 컴포넌트가 호출하면 가장 긴 값을 유지한다(짧은 쪽이 긴 경고/입 벌림을 덮어쓰지 않게).</summary>
    protected float preFireChargeTime = 0f;

    public float PreFireChargeTime => preFireChargeTime;

    public void SetPreFireChargeTime(float t) =>
        preFireChargeTime = Mathf.Max(preFireChargeTime, Mathf.Max(0f, t));

    protected virtual void Awake()
    {
        StageManager sm = GetComponentInParent<StageManager>();
        if (sm != null)
        {
            _hasStageManager = true;
            sm.RegisterTrap(this);
        }
    }

    protected virtual void Start()
    {
        // OnEnable이 먼저 호출되므로 Start에서는 중복 활성화 방지
        if (startActive && !_hasStageManager && !isRunning) Activate();
    }

    // Stage SetActive(false → true) 사이클 시 자동 리셋
    // StageManager 자식이면 startActive 무시 — StartStage()로만 시작
    protected virtual void OnEnable()
    {
        if (startActive && !_hasStageManager) Activate();
    }

    protected virtual void OnDisable()
    {
        isRunning = false;
        StopAllCoroutines();
        trapCoroutine = null;
        fireCoroutine = null;
    }

    /// <summary>함정 활성화. 이미 실행 중이면 무시.</summary>
    public void Activate()
    {
        isFrozen = false;
        if (isRunning) return;
        isRunning = true;
        trapCoroutine = StartCoroutine(TrapLoop());
    }

    /// <summary>
    /// 스테이지 클리어 정지. Deactivate()에 더해 이후 도착하는 단발 발사 요청
    /// (ArrowTrap.FireOnce / DropTrap.FireAt)까지 차단한다 — 감독 컴포넌트의 루프를 세우는 것과
    /// 별개로, 이미 큐에 걸린 마지막 호출이 새 투사체를 스폰하는 것을 막는 안전장치.
    /// SceneFlowManager.FreezeAllHazardsNow()에서만 호출한다 (Phase 전환용 정지는 Deactivate()).
    /// </summary>
    public void Freeze()
    {
        isFrozen = true;
        Deactivate();
    }

    /// <summary>함정 비활성화. 이 인스턴스의 모든 코루틴(TrapLoop, FireWithCharge, DropCycle 등)을 즉시 중단.</summary>
    public void Deactivate()
    {
        isRunning = false;
        StopAllCoroutines();
        trapCoroutine = null;
        fireCoroutine = null;
        OnDeactivated();
    }

    protected virtual IEnumerator TrapLoop()
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        while (isRunning)
        {
            fireCoroutine = StartCoroutine(FireWithCharge());
            yield return fireCoroutine;
            fireCoroutine = null;

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
        if (!isRunning) yield break;
        OnFiring?.Invoke();
        OnTrapTrigger();
    }

    /// <summary>함정이 발동될 때 호출. 하위 클래스에서 구현.</summary>
    protected abstract void OnTrapTrigger();

    /// <summary>Deactivate 시 후처리가 필요한 경우 오버라이드.</summary>
    protected virtual void OnDeactivated() { }
}
