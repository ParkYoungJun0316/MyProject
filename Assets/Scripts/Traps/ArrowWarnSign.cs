using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ArrowTrap 발사 전 경고 사인. 타이밍은 SafeZoneWarnSign과 같은 인스펙터 쌍
/// (warnLeadTime / holdAfterFire)으로 조절한다.
///
/// [타이밍]
/// - warnLeadTime: 발사 전에 경고를 미리 보여줄 시간. TrapBase.preFireChargeTime에 반영되어
///   OnPreFireCharge가 이 시간만큼 앞당겨진다. Mouth 연출이 같이 있으면 둘 중 더 긴 값이 쓰인다.
/// - holdAfterFire: 발사 시각 기준 숨김 오프셋. 0 = 발사 즉시 숨김, 양수 = 발사 후에도 유지,
///   음수 = 발사 전에 미리 숨김 (SafeZoneWarnSign과 동일).
///
/// [비주얼 — 2026-09-22]
/// 흰 섬광 방식은 폐기, 셰이더 기반 "시작색→끝색 채움(입→끝) + 발사 직전 펄스"로 복귀.
/// 채움이 끝에 닿는 순간 = 발사. C# 쪽은 채움 위치(startFill+easeOutPower 곡선)를 _Fill에,
/// 시간 진행도를 _Progress에, WarnPalette.Start→End 보간색을 _BaseColor에 넘길 뿐 — 해석은
/// WarnMarker 셰이더가 담당(WarnMarker.shader 상단 주석 참고, ArrowWarnMarker.mat은 _FillMode=0).
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
///
/// [난이도: 시간 경과에 따른 페이드아웃]
/// fadePhases(WarnFadePhase[])를 채우면 Phase 시작 후 경과 시각에 따라 경고 사인 알파가
/// 단계 사이를 선형 보간하며 점점 흐려진다(예: 30초까지 100%→60초에 0%). ArrowTrap의
/// speedPhases(발사 속도 난이도 축)와 별개 축이며, 발사 타이밍/preFireChargeTime에는
/// 영향을 주지 않고 시각 효과(알파)만 바꾼다. 비워두면(기본) 항상 알파 100%로 기존과 동일.
/// </summary>
[RequireComponent(typeof(TrapBase))]
public class ArrowWarnSign : MonoBehaviour
{
    [Header("경고 사인")]
    [Tooltip("발사 전 표시할 경고 오브젝트 (메시/머티리얼은 씬에서 미리 구성). 비활성으로 둬도 됨")]
    [SerializeField] private GameObject warnSignObject = null;

    [Tooltip("색을 입힐 Renderer. 비워두면 warnSignObject(또는 그 자식)에서 자동 탐색")]
    [SerializeField] private Renderer targetRenderer = null;

    [Header("색 (값은 WarnPalette 공용 — 0=경고 시작, 1=발사)")]
    [SerializeField] private string colorProperty = "_BaseColor";

    [Header("채움 곡선 (발사 시각은 불변 — 채움 위치 분배만 바꿈)")]
    [Tooltip("경고가 뜨는 순간 이미 채워져 있는 비율(0~0.9). 0 = 입에서부터 채움. " +
             "플레이어가 거의 안 보는 입 쪽 구간에 시간을 쓰지 않게 한다.")]
    [Range(0f, 0.9f)]
    [SerializeField] private float startFill = 0f;

    [Tooltip("뒤로 갈수록 느려지는 정도. 1 = 일정 속도, 2 = 끝부분에 시간을 몰아줌(권장 시작값). " +
             "3 이상은 끝이 늘어져 발사 순간이 흐려질 수 있음.")]
    [Range(1f, 4f)]
    [SerializeField] private float easeOutPower = 1f;

    [Header("타이밍")]
    [Tooltip("발사 전 경고를 미리 보여줄 시간(초). TrapBase.preFireChargeTime에 반영됨")]
    [SerializeField] private float warnLeadTime = 1.5f;

    [Tooltip("발사 시각 기준 마커를 숨길 오프셋(초). 0이면 발사 즉시 숨김, 양수면 발사 후에도 유지, " +
             "음수면 발사 전에 미리 숨김(예: warnLeadTime=2, holdAfterFire=-1 → 발사 2초 전에 표시, " +
             "1초 전에 숨김 → 1초간만 노출). warnLeadTime + holdAfterFire가 음수가 되지 않게만 입력할 것 " +
             "(표시 시작 시각보다 먼저 숨기는 건 불가능 — 자동으로 0에서 클램프됨).")]
    [SerializeField] private float holdAfterFire = 0f;

    [Header("난이도: 시간 경과에 따라 경고 사인이 점점 흐려짐")]
    [Tooltip("Phase 시작(PhaseStartServerTime) 후 경과 시각별 알파 배율. afterSeconds 오름차순 입력, " +
             "단계 사이는 선형 보간. 예: [ {0,1}, {30,1}, {60,0} ] → 30초까지 완전히 보이고 " +
             "30~60초 사이 서서히 흐려지다가 60초부터 안 보임. 비워두면(0개) 항상 알파 1(변화 없음).")]
    [SerializeField] private WarnFadePhase[] fadePhases = new WarnFadePhase[0];

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

        _fx = new WarnMarkerColorFx(targetRenderer, colorProperty, WarnPalette.Start, WarnPalette.End, "_Fill", "_Progress");
        SetVisible(false);
    }

    void OnEnable()
    {
        if (_trap == null) return;

        // Host만 로컬 TrapBase 이벤트를 직접 구독 (MouthTrapAnimatorAnim과 동일 이유)
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        _trap.OnPreFireCharge += PlayWarnFromNetwork;
        _trap.OnFiring        += PlayHideFromNetwork;
        _trap.OnChargeCancelled += PlayCancelFromNetwork;
    }

    void OnDisable()
    {
        if (_trap != null)
        {
            _trap.OnPreFireCharge -= PlayWarnFromNetwork;
            _trap.OnFiring        -= PlayHideFromNetwork;
            _trap.OnChargeCancelled -= PlayCancelFromNetwork;
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
        // Client는 Phase 활성화가 Host보다 늦어 비활성 상태에서 RPC가 도착할 수 있다(StartCoroutine 에러 방지).
        if (!_warnEnabled || !isActiveAndEnabled) return;
        if (warnLeadTime <= 0f && holdAfterFire <= 0f) return;

        _warnCoroutine = StartCoroutine(WarnRoutine());
    }

    /// <summary>Host는 OnFiring 직접 구독, Client는 SyncArrowFireClientRpc 수신으로 호출됨.
    /// 경고 루틴이 아직 안 끝났어도(비활성화 등으로 일찍 발사되는 경우 대비) 항상 정리하는
    /// 안전망.</summary>
    public void PlayHideFromNetwork()
    {
        StopWarnRoutine();

        if (holdAfterFire <= 0f || !isActiveAndEnabled)
        {
            SetVisible(false);
            return;
        }

        _warnCoroutine = StartCoroutine(HoldAfterFireRoutine());
    }

    /// <summary>충전이 발사 없이 끝남(Deactivate/Freeze). Host는 OnChargeCancelled 직접 구독,
    /// Client는 SyncArrowCancelClientRpc 수신으로 호출됨. holdAfterFire와 무관하게 즉시 숨긴다.</summary>
    public void PlayCancelFromNetwork()
    {
        StopWarnRoutine();
        SetVisible(false);
    }

    /// <summary>
    /// warnLeadTime 동안만 시작색→끝색으로 채워 보이게 하고, 발사(OnFiring)까지 정확히 그 순간에
    /// 끝나도록 표시 시작을 지연시킨다(showDelay 방식).
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

        // 완전히 페이드된 구간: 투명 메시를 그리지 않고, warnSignObject의 다른 자식 비주얼도 켜지 않는다.
        if (GetFadeAlphaMultiplier() <= 0f)
        {
            _warnCoroutine = null;
            yield break;
        }

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

    // 색·펄스는 시간 t 기준, 채움 위치만 startFill + ease-out으로 재분배 — t=1에서 항상 fill=1이라
    // "채움이 끝에 닿는 순간 = 발사"는 유지된다.
    void SetProgress(float t) => _fx.SetProgress(t, GetFadeAlphaMultiplier(), EvaluateFill(t));

    float EvaluateFill(float t)
    {
        t = Mathf.Clamp01(t);
        float eased = 1f - Mathf.Pow(1f - t, Mathf.Max(1f, easeOutPower));
        return Mathf.Lerp(Mathf.Clamp01(startFill), 1f, eased);
    }

    float GetFadeAlphaMultiplier() => WarnFadePhase.Evaluate(fadePhases);

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
