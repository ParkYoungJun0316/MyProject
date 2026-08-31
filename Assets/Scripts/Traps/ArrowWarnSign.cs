using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ArrowTrap 발사 전 경고 사인. SpikeLaneWarnMarker와 같이 노랑→빨강으로 보간하고,
/// 타이밍은 SafeZoneWarnSign과 같은 인스펙터 쌍(warnLeadTime / holdAfterFire)으로 조절한다.
///
/// [타이밍]
/// - warnLeadTime: 발사 전에 경고를 미리 보여줄 시간. TrapBase.preFireChargeTime에 반영되어
///   OnPreFireCharge가 이 시간만큼 앞당겨진다. Mouth 연출이 같이 있으면 둘 중 더 긴 값이 쓰인다.
/// - holdAfterFire: 발사 시각 기준 숨김 오프셋. 0 = 발사 즉시 숨김, 양수 = 발사 후에도 유지,
///   음수 = 발사 전에 미리 숨김 (SafeZoneWarnSign과 동일).
/// - 색 보간은 warnLeadTime 동안 0→1. 발사 시점에 완전 빨강(holdAfterFire가 음수면 그 전에 숨김).
///
/// [비주얼]
/// warnSignObject에 메시(URP Lit, _BaseColor)를 두고 이 컴포넌트가 MaterialPropertyBlock으로
/// 색만 덮는다. SpikeLane WarnMarker 메시/머티리얼을 그대로 복제해 써도 된다.
///
/// [동기화 방식 — Mouth 계열과 동일 패턴]
/// Host만 로컬 TrapBase 이벤트를 직접 구독해 재생하고(zero latency, IsServer 가드),
/// Client는 이 로컬 이벤트를 구독하지 않는다 — ArrowTrap이 Host에서만
/// StageNetworkState.SyncArrowChargeClientRpc/SyncArrowFireClientRpc로 릴레이하고,
/// Client는 그 RPC로 도착한 PlayWarnFromNetwork/PlayHideFromNetwork를 통해서만 재생한다.
///
/// [Phase 한정 표시]
/// 기본은 항상 켜짐(warnEnabled=true). 특정 Phase에서만 쓰려면 PhaseData.onPhaseEnter /
/// onPhaseComplete UnityEvent에 SetWarnEnabled(true/false)를 연결한다.
/// 꺼진 상태에서도 preFireChargeTime 자체는 그대로 유지되므로(연출만 스킵) 발사 스케줄
/// 시각은 Phase와 무관하게 절대 변하지 않는다.
/// </summary>
[RequireComponent(typeof(TrapBase))]
public class ArrowWarnSign : MonoBehaviour
{
    [Header("경고 사인")]
    [Tooltip("발사 전 표시할 경고 오브젝트 (메시/머티리얼은 씬에서 미리 구성). 비활성으로 둬도 됨")]
    [SerializeField] private GameObject warnSignObject = null;

    [Tooltip("색을 입힐 Renderer. 비워두면 warnSignObject(또는 그 자식)에서 자동 탐색")]
    [SerializeField] private Renderer targetRenderer = null;

    [Header("색상 보간 (0=경고 시작, 1=발사)")]
    [SerializeField] private string colorProperty = "_BaseColor";
    [SerializeField] private Color warnStartColor = Color.yellow;
    [SerializeField] private Color warnEndColor = Color.red;

    [Header("타이밍")]
    [Tooltip("발사 전 경고를 미리 보여줄 시간(초). TrapBase.preFireChargeTime에 반영됨")]
    [SerializeField] private float warnLeadTime = 1.5f;

    [Tooltip("발사 시각 기준 마커를 숨길 오프셋(초). 0이면 발사 즉시 숨김, 양수면 발사 후에도 유지, " +
             "음수면 발사 전에 미리 숨김(예: warnLeadTime=2, holdAfterFire=-1 → 발사 2초 전에 표시, " +
             "1초 전에 숨김 → 1초간만 노출). warnLeadTime + holdAfterFire가 음수가 되지 않게만 입력할 것 " +
             "(표시 시작 시각보다 먼저 숨기는 건 불가능 — 자동으로 0에서 클램프됨).")]
    [SerializeField] private float holdAfterFire = 0f;

    TrapBase _trap;
    bool     _warnEnabled = true;

    WarnMarkerColorFx _fx;
    Coroutine _warnCoroutine;

    void Awake()
    {
        _trap = GetComponent<TrapBase>();
        _trap.SetPreFireChargeTime(warnLeadTime);

        // warnSignObject 범위 밖(예: Mouth 메시)까지 뒤지지 않는다 — 잘못된 Renderer를 집어
        // 색을 덮어쓰는 사고를 막기 위해 warnSignObject 하위로만 탐색을 제한한다.
        if (targetRenderer == null && warnSignObject != null)
            targetRenderer = warnSignObject.GetComponentInChildren<Renderer>(true);
        if (targetRenderer == null)
            Debug.LogWarning($"[ArrowWarnSign] {name}: targetRenderer를 찾지 못했습니다 — " +
                              "warnSignObject 또는 targetRenderer를 인스펙터에서 지정하세요.", this);

        _fx = new WarnMarkerColorFx(targetRenderer, colorProperty, warnStartColor, warnEndColor);
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

        StopWarnRoutine();
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
        if (!enabled)
        {
            StopWarnRoutine();
            SetVisible(false);
        }
    }

    // ── 재생 진입점 (Host: 로컬 TrapBase 이벤트 직접 구독 / Client: ArrowTrap.PlayChargeById·
    // PlayFireById가 StageNetworkState RPC 수신 시 호출) ───────────────────

    /// <summary>Host는 OnPreFireCharge 직접 구독, Client는 SyncArrowChargeClientRpc 수신으로 호출됨.</summary>
    public void PlayWarnFromNetwork()
    {
        StopWarnRoutine();
        if (!_warnEnabled) return;
        if (warnLeadTime <= 0f && holdAfterFire <= 0f) return;

        _warnCoroutine = StartCoroutine(WarnRoutine());
    }

    /// <summary>Host는 OnFiring 직접 구독, Client는 SyncArrowFireClientRpc 수신으로 호출됨.
    /// 경고 루틴이 아직 안 끝났어도(비활성화 등으로 일찍 발사되는 경우 대비) 항상 정리하는
    /// 안전망 — WindWarnSign.PlayHideFromNetwork와 동일 패턴.</summary>
    public void PlayHideFromNetwork()
    {
        StopWarnRoutine();

        if (holdAfterFire <= 0f)
        {
            SetVisible(false);
            return;
        }

        _warnCoroutine = StartCoroutine(HoldAfterFireRoutine());
    }

    /// <summary>
    /// warnLeadTime 동안만 노랑→빨강으로 보이게 하고, 발사(OnFiring)까지 정확히 그 순간에
    /// 끝나도록 표시 시작을 지연시킨다(WindWarnSign.WarnRoutine과 동일한 showDelay 방식).
    /// Mouth 연출이 같은 오브젝트에 있어 실제 preFireChargeTime(=_trap.PreFireChargeTime)이
    /// warnLeadTime보다 길게 병합된 경우, 그 차이(showDelay)만큼 기다렸다가 표시를 시작해야
    /// "표시 시작~빨강 도달"이 정확히 warnLeadTime초가 되고 딱 발사 순간에 맞아떨어진다.
    /// </summary>
    IEnumerator WarnRoutine()
    {
        float charge = _trap != null ? _trap.PreFireChargeTime : warnLeadTime;

        // holdAfterFire < 0: 발사 전에 미리 숨김. 그 외에는 발사 순간까지 표시.
        float visibleDuration = holdAfterFire < 0f
            ? Mathf.Max(0f, warnLeadTime + holdAfterFire)
            : warnLeadTime;
        if (visibleDuration <= 0f) yield break;

        float showDelay = Mathf.Max(0f, charge - warnLeadTime);
        if (showDelay > 0f)
            yield return new WaitForSeconds(showDelay);

        SetVisible(true);
        SetProgress(0f);

        float elapsed = 0f;
        while (elapsed < visibleDuration)
        {
            elapsed += Time.deltaTime;
            SetProgress(elapsed / visibleDuration);
            yield return null;
        }

        SetProgress(1f);

        // holdAfterFire >= 0: 발사 순간까지 빨간 채로 유지 — PlayHideFromNetwork가 끈다.
        if (holdAfterFire < 0f)
            SetVisible(false);

        _warnCoroutine = null;
    }

    IEnumerator HoldAfterFireRoutine()
    {
        SetProgress(1f);
        yield return new WaitForSeconds(holdAfterFire);
        SetVisible(false);
        _warnCoroutine = null;
    }

    void StopWarnRoutine()
    {
        if (_warnCoroutine == null) return;
        StopCoroutine(_warnCoroutine);
        _warnCoroutine = null;
    }

    void SetProgress(float t) => _fx.SetProgress(t);

    void SetVisible(bool visible)
    {
        if (warnSignObject != null)
            warnSignObject.SetActive(visible);

        _fx.SetRendererVisible(visible);
    }

    // ── 에디터 테스트 (플레이 중 컴포넌트 우클릭) ─────────────────────────

    [ContextMenu("테스트: 경고 표시")]
    void TestWarn() => PlayWarnFromNetwork();

    [ContextMenu("테스트: 경고 숨김")]
    void TestHide()
    {
        StopWarnRoutine();
        SetVisible(false);
    }
}
