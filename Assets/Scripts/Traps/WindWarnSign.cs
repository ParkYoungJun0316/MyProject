using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// WindTrap 발동 전 바닥 경고 (노랑→빨강 존 + 방향 화살표).
/// 존/화살표 메시는 씬에서 미리 배치하고, 이 컴포넌트는 표시 타이밍·색 보간·Push/Pull
/// 방향만 담당한다. 화살표는 하나 — 에디터에서 Push 방향으로 배치해 두면, Pull일 때
/// 월드 Y 180도만 돌려 -forward로 맞춘다(트랩 루트·콜라이더·입는 돌리지 않음).
///
/// [타이밍]
/// 표시 시작 = 바람 시작 − warnLeadTime, 숨김 = 바람 시작 + hideOffset.
/// hideOffset=0 이면 바람 시작 순간 숨김, -1 이면 1초 전에 숨김 (SafeZoneWarnSign.holdAfterFire와 동일).
/// warnLeadTime이 MouthWindAnimator.chargeTime보다 길면 WindTrap 충전을 그만큼 늘린다
/// (SetWindChargeTime이 더 긴 쪽을 유지).
///
/// [동기화 방식 — MouthWindAnimator와 동일]
/// Host만 로컬 OnWindCharge/OnWindEnd를 직접 구독해 재생하고(zero latency, IsServer 가드),
/// Client는 이 로컬 이벤트를 구독하지 않는다 — WindTrap이 Host에서만
/// StageNetworkState.SyncWindChargeClientRpc/SyncWindEndClientRpc로 릴레이하고,
/// Client는 그 RPC로 도착한 PlayWarnFromNetwork/PlayHideFromNetwork를 통해서만 재생한다.
///
/// [Phase 한정 표시]
/// 기본은 항상 켜짐(warnEnabled=true). 특정 Phase에서만 쓰려면 PhaseData UnityEvent에
/// SetWarnEnabled(true/false)를 연결. false여도 충전 시간(바람 스케줄)은 그대로 유지 — 연출만 스킵.
/// </summary>
[RequireComponent(typeof(WindTrap))]
public class WindWarnSign : MonoBehaviour
{
    [Header("경고 사인")]
    [Tooltip("바람 범위를 덮는 바닥 면. 노랑→빨강 보간 대상. 없으면 면 연출 생략")]
    [SerializeField] private GameObject zoneVisual = null;

    [Tooltip("색을 입힐 Renderer. 비워두면 zoneVisual에서 자동 탐색")]
    [SerializeField] private Renderer zoneRenderer = null;

    [Tooltip("방향 화살표 오브젝트. Push 기준으로 씬에서 배치. Pull이면 월드 Y 180도 회전. " +
             "WindTrap 루트 자신은 넣지 말 것(콜라이더·입이 같이 돌아감)")]
    [SerializeField] private GameObject arrowObject = null;

    [Header("색상 보간 (0=표시 시작, 1=숨김 순간)")]
    [Tooltip("Renderer 머티리얼의 색 셰이더 프로퍼티 이름")]
    [SerializeField] private string colorProperty = "_BaseColor";

    [SerializeField] private Color warnStartColor = Color.yellow;
    [SerializeField] private Color warnEndColor = Color.red;

    [Header("타이밍")]
    [Tooltip("바람 시작 몇 초 전에 경고를 켤지. Mouth chargeTime보다 길면 바람 발동을 이 값까지 미룸")]
    [SerializeField] private float warnLeadTime = 1.5f;

    [Tooltip("바람 시작 시각 기준 숨김 오프셋(초). 0이면 시작 순간 숨김, 양수면 시작 후에도 유지, " +
             "음수면 시작 전에 미리 숨김(예: warnLeadTime=2, hideOffset=-1 → 시작 2초 전에 표시, " +
             "1초 전에 숨김 → 1초간만 노출). warnLeadTime + hideOffset가 음수가 되면 표시하지 않음 " +
             "(SafeZoneWarnSign.holdAfterFire와 동일 클램프).")]
    [SerializeField] private float hideOffset = 0f;

    WindTrap _wind;
    WarnMarkerColorFx _fx;
    Quaternion _arrowRestLocalRotation;
    bool _arrowRestCached;
    bool _warnEnabled = true;
    Coroutine _routine;

    void Awake()
    {
        _wind = GetComponent<WindTrap>();
        _wind.SetWindChargeTime(warnLeadTime);

        if (zoneRenderer == null && zoneVisual != null)
            zoneRenderer = zoneVisual.GetComponent<Renderer>()
                        ?? zoneVisual.GetComponentInChildren<Renderer>(true);

        _fx = new WarnMarkerColorFx(zoneRenderer, colorProperty, warnStartColor, warnEndColor);

        CacheArrowRest();
        SetVisible(false);
        SetProgress(0f);
    }

    void OnEnable()
    {
        if (_wind == null) return;

        // Host만 로컬 WindTrap 이벤트를 직접 구독 (MouthWindAnimator와 동일 이유 — 위 클래스 주석 참조)
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        _wind.OnWindCharge += PlayWarnFromNetwork;
        _wind.OnWindEnd    += PlayHideFromNetwork;
    }

    void OnDisable()
    {
        if (_wind != null)
        {
            _wind.OnWindCharge -= PlayWarnFromNetwork;
            _wind.OnWindEnd    -= PlayHideFromNetwork;
        }

        StopWarnRoutine();
        SetVisible(false);
        RestoreArrowRest();
    }

    /// <summary>
    /// 이 경고 사인을 이번 Phase에서 표시할지 여부. PhaseData.onPhaseEnter/onPhaseComplete
    /// UnityEvent에 연결해 특정 Phase에서만 true로 켠다. false여도 충전 시간(바람 스케줄
    /// 앵커)은 그대로 유지 — 연출만 스킵되고 발동 타이밍은 변하지 않는다.
    /// </summary>
    public void SetWarnEnabled(bool enabled)
    {
        _warnEnabled = enabled;
        if (!enabled) PlayHideFromNetwork();
    }

    // ── 재생 진입점 (Host: 로컬 OnWindCharge 직접 구독 / Client: WindTrap.PlayChargeById가
    // StageNetworkState RPC 수신 시 호출) ──────────────────────────────────

    /// <summary>Host는 OnWindCharge 직접 구독, Client는 SyncWindChargeClientRpc 수신으로 호출됨.</summary>
    public void PlayWarnFromNetwork()
    {
        StopWarnRoutine();
        if (!_warnEnabled) return;
        _routine = StartCoroutine(WarnRoutine());
    }

    /// <summary>Host는 OnWindEnd 직접 구독(중단 안전망), Client는 SyncWindEndClientRpc 수신으로 호출됨.
    /// 주 숨김은 WarnRoutine의 hideOffset 시각이고, 이 진입점은 바람 종료·비활성 시 잔여 표시를 끈다.</summary>
    public void PlayHideFromNetwork()
    {
        StopWarnRoutine();
        SetVisible(false);
        RestoreArrowRest();
    }

    IEnumerator WarnRoutine()
    {
        float charge  = _wind != null ? _wind.WindChargeTime : warnLeadTime;
        float visible = Mathf.Max(0f, warnLeadTime + hideOffset);
        if (visible <= 0f) yield break;

        float showDelay = Mathf.Max(0f, charge - warnLeadTime);
        if (showDelay > 0f)
            yield return new WaitForSeconds(showDelay);

        OrientArrow();
        SetProgress(0f);
        SetVisible(true);

        float elapsed = 0f;
        while (elapsed < visible)
        {
            elapsed += Time.deltaTime;
            SetProgress(elapsed / visible);
            yield return null;
        }

        SetProgress(1f);
        SetVisible(false);
        RestoreArrowRest();
        _routine = null;
    }

    void StopWarnRoutine()
    {
        if (_routine == null) return;
        StopCoroutine(_routine);
        _routine = null;
    }

    void SetVisible(bool visible)
    {
        if (zoneVisual != null) zoneVisual.SetActive(visible);
        if (arrowObject != null) arrowObject.SetActive(visible);
    }

    void SetProgress(float t) => _fx.SetProgress(t);

    void CacheArrowRest()
    {
        if (arrowObject == null) return;
        _arrowRestLocalRotation = arrowObject.transform.localRotation;
        _arrowRestCached = true;
    }

    void RestoreArrowRest()
    {
        if (arrowObject == null || !_arrowRestCached) return;
        if (arrowObject.transform == transform) return;
        arrowObject.transform.localRotation = _arrowRestLocalRotation;
    }

    /// <summary>
    /// Push = 에디터에서 잡아 둔 rest 회전 그대로.
    /// Pull = rest로 되돌린 뒤 월드 업 축 180도 — 바닥 데칼이 X로 누워 있어도 면이 뒤집히지 않고
    /// 수평 방향만 반전된다. 트랩 루트는 돌리지 않는다.
    /// </summary>
    void OrientArrow()
    {
        if (arrowObject == null || !_arrowRestCached) return;
        if (arrowObject.transform == transform) return;

        Transform arrow = arrowObject.transform;
        arrow.localRotation = _arrowRestLocalRotation;

        if (_wind != null && _wind.CurrentWindMode == WindTrap.WindMode.Pull)
            arrow.Rotate(0f, 180f, 0f, Space.World);
    }

    // ── 에디터 테스트 (플레이 중 컴포넌트 우클릭) ─────────────────────────

    [ContextMenu("테스트: 경고 표시")]
    void TestWarn()
    {
        StopWarnRoutine();
        OrientArrow();
        SetProgress(0f);
        SetVisible(true);
        _routine = StartCoroutine(TestLerp());
    }

    IEnumerator TestLerp()
    {
        float visible = Mathf.Max(0.01f, warnLeadTime + hideOffset);
        float elapsed = 0f;
        while (elapsed < visible)
        {
            elapsed += Time.deltaTime;
            SetProgress(elapsed / visible);
            yield return null;
        }
        SetProgress(1f);
        _routine = null;
    }

    [ContextMenu("테스트: 경고 숨김")]
    void TestHide() => PlayHideFromNetwork();

    [ContextMenu("테스트: 화살표 Push 방향")]
    void TestArrowPush()
    {
        RestoreArrowRest();
        if (arrowObject != null) arrowObject.SetActive(true);
    }

    [ContextMenu("테스트: 화살표 Pull 방향 (Y 180)")]
    void TestArrowPull()
    {
        RestoreArrowRest();
        if (arrowObject == null || arrowObject.transform == transform) return;
        arrowObject.SetActive(true);
        arrowObject.transform.Rotate(0f, 180f, 0f, Space.World);
    }
}
