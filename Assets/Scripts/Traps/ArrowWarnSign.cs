using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ArrowTrap 발사 전 표시하는 단순 경고 사인 (빨간 표시 오브젝트 on/off).
/// 오브젝트 자체(모양·색)는 씬에서 미리 배치해두고, 이 컴포넌트는 발사 전 warnLeadTime
/// 동안만 그 오브젝트를 보이게 했다가 발사 순간 즉시 끈다.
///
/// [MouthTrapAnimator가 빠진 ArrowTrap 전용 대체 경고]
/// 원래 MouthTrapAnimator/MouthTrapAnimatorAnim이 TrapBase.preFireChargeTime을 채워
/// "입 벌림 = 경고" 역할을 했다. 이 컴포넌트가 그 역할을 대신하므로, 같은 ArrowTrap에
/// Mouth 연출 컴포넌트를 동시에 붙이면 안 된다 — 둘 다 SetPreFireChargeTime을 호출해
/// 서로 덮어쓴다(Awake 실행 순서에 의존하게 되어 예측 불가).
///
/// [동기화 방식 — Mouth 계열과 동일 패턴]
/// Host만 로컬 TrapBase 이벤트를 직접 구독해 재생하고(zero latency, IsServer 가드),
/// Client는 이 로컬 이벤트를 구독하지 않는다 — ArrowTrap이 Host에서만
/// StageNetworkState.SyncArrowChargeClientRpc/SyncArrowFireClientRpc로 릴레이하고,
/// Client는 그 RPC로 도착한 PlayWarnFromNetwork/PlayHideFromNetwork를 통해서만 재생한다.
///
/// [Phase 한정 표시]
/// 기본은 항상 켜짐(warnEnabled=true). 특정 Phase에서만 쓰려면 PhaseData.onPhaseEnter /
/// onPhaseComplete(또는 다음 Phase의 onPhaseEnter) UnityEvent에 SetWarnEnabled(true/false)를
/// 연결한다(ArrowTrap.SetPhaseSpeedMultiplier와 동일하게 UnityEvent bool 파라미터로 지정).
/// 꺼진 상태에서도 preFireChargeTime 자체는 그대로 유지되므로(연출만 스킵) 발사 스케줄
/// 시각은 Phase와 무관하게 절대 변하지 않는다.
/// </summary>
[RequireComponent(typeof(TrapBase))]
public class ArrowWarnSign : MonoBehaviour
{
    [Header("경고 사인")]
    [Tooltip("발사 전 표시할 경고 오브젝트 (빨간 표시 등 — 모양/색은 씬에서 미리 구성)")]
    [SerializeField] private GameObject warnSignObject = null;

    [Header("타이밍")]
    [Tooltip("발사 전 경고를 표시할 시간(초). TrapBase.preFireChargeTime으로 그대로 반영됨")]
    [SerializeField] private float warnLeadTime = 1.5f;

    TrapBase _trap;
    bool     _warnEnabled = true;

    void Awake()
    {
        _trap = GetComponent<TrapBase>();
        _trap.SetPreFireChargeTime(warnLeadTime);
        SetVisible(false);
    }

    void OnEnable()
    {
        if (_trap == null) return;

        // Host만 로컬 TrapBase 이벤트를 직접 구독 (MouthTrapAnimator와 동일 이유 — 위 클래스 주석 참조)
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        _trap.OnPreFireCharge += PlayWarnFromNetwork;
        _trap.OnFiring        += PlayHideFromNetwork;
    }

    void OnDisable()
    {
        if (_trap != null)
        {
            _trap.OnPreFireCharge -= PlayWarnFromNetwork;
            _trap.OnFiring        -= PlayHideFromNetwork;
        }

        SetVisible(false);
    }

    /// <summary>
    /// 이 경고 사인을 이번 Phase에서 표시할지 여부. PhaseData.onPhaseEnter/onPhaseComplete
    /// UnityEvent에 연결해 특정 Phase에서만 true로 켠다. false여도 preFireChargeTime(발사
    /// 스케줄 앵커)은 그대로 유지 — 연출만 스킵되고 발사 타이밍은 변하지 않는다.
    /// </summary>
    public void SetWarnEnabled(bool enabled)
    {
        _warnEnabled = enabled;
        if (!enabled) SetVisible(false);
    }

    // ── 재생 진입점 (Host: 로컬 TrapBase 이벤트 직접 구독 / Client: ArrowTrap.PlayChargeById·
    // PlayFireById가 StageNetworkState RPC 수신 시 호출) ───────────────────

    /// <summary>Host는 OnPreFireCharge 직접 구독, Client는 SyncArrowChargeClientRpc 수신으로 호출됨.</summary>
    public void PlayWarnFromNetwork()
    {
        if (!_warnEnabled) return;
        SetVisible(true);
    }

    /// <summary>Host는 OnFiring 직접 구독, Client는 SyncArrowFireClientRpc 수신으로 호출됨.</summary>
    public void PlayHideFromNetwork() => SetVisible(false);

    void SetVisible(bool visible)
    {
        if (warnSignObject != null) warnSignObject.SetActive(visible);
    }

    // ── 에디터 테스트 (플레이 중 컴포넌트 우클릭) ─────────────────────────

    [ContextMenu("테스트: 경고 표시")]
    void TestWarn() => PlayWarnFromNetwork();

    [ContextMenu("테스트: 경고 숨김")]
    void TestHide() => PlayHideFromNetwork();
}
