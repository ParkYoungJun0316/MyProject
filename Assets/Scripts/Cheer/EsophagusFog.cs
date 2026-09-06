using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 식도 안개 — 팀 응원 되돌림 대상 (ITeamCheerRevert). 초출 T2, 복습 T4.
/// CoopStageAudit.T.md §5 "지속형/Render Fog 모델" 참고.
///
/// [동작]
/// <c>RenderSettings.fog</c>(거리 기반) 밀도를 ServerTime 경과에 비례해 지속적으로 증가시키고,
/// <see cref="maxDensity"/>에서 캡한다(시야 완전 차단은 아님). M 계열의 암전(화면 전체 알파,
/// 거리 무관)과는 다른 메커니즘 — 가까운 것은 보이고 먼 것만 흐려진다.
/// URP는 이 빌트인 Fog(Lighting > Environment > Fog)를 그대로 쓴다 — Volume 오버라이드가 아니다.
/// URP Lit 계열 셰이더만 반영되므로 커스텀/Unlit 셰이더(Boulder 등)는 이 작업 범위 밖.
///
/// [씬 전역, 구간 분리 없음]
/// RenderSettings.fog는 씬 단위 설정이라 이 씬 안에서 안개 구간/비안개 구간을 나누지 않는다.
/// 팀원 시야도 동일하게 영향받음 — Host/Client 차이 없이 전 클라이언트가 자기 화면에서
/// 각자 이 값을 로컬로 계산해 적용한다(순수 비주얼, 판정에 쓰이지 않음).
/// 씬당 하나만 둘 것 — 두 개가 겹쳐 켜지면 나중 것이 앞의 것이 바꿔놓은 값을 "원래 값"으로
/// 저장해, 꺼질 때 안개가 남는다(CheerService.RegisterRevert 경고로도 드러난다).
///
/// [창 없음 · 동기화 · 재도전]
/// EsophagusSqueeze와 동일 원칙 — 창 개념 없음(<see cref="IsAvailable"/> 항상 true), 새
/// RPC/NetworkVariable 없이 ServerTime 폴링만으로 결정론적 계산, Revert() 안에서
/// NotifyHazardWindow(false)→(true) 펄스로 다음 팀 외침을 다시 허용한다. 자세한 이유는
/// EsophagusSqueeze.cs 클래스 주석 참고(중복 서술 방지).
/// </summary>
public class EsophagusFog : MonoBehaviour, ITeamCheerRevert
{
    [Header("안개 속도")]
    [Tooltip("초당 밀도 증가량. 튜닝은 플레이하며.")]
    [SerializeField] float densityPerSecond = 0.002f;

    [Tooltip("최대 밀도 캡. 시야 완전 차단은 아님.")]
    [SerializeField] float maxDensity = 0.05f;

    [Header("Fog 설정 (활성화 시 적용 — 씬 RenderSettings를 덮어씀)")]
    [SerializeField] FogMode fogMode = FogMode.Exponential;
    [SerializeField] Color fogColor = Color.gray;

    [Header("시작")]
    [Tooltip("켜지는 즉시 짙어지기 시작. 끄면 Begin()을 대화·카운트다운 등에서 호출할 것.")]
    [SerializeField] bool startOnEnable = true;

    double _anchorServerTime = -1d;
    bool _anchoredOnNetworkClock;
    bool _running;
    int _syncGeneration;
    Coroutine _bindRoutine;

    bool _prevFogEnabled;
    FogMode _prevFogMode;
    Color _prevFogColor;
    float _prevFogDensity;

    /// <summary>창 없음 — 컴포넌트가 켜져 있으면 언제든 응원 유효.</summary>
    public bool IsAvailable => isActiveAndEnabled;

    void OnEnable()
    {
        _prevFogEnabled = RenderSettings.fog;
        _prevFogMode    = RenderSettings.fogMode;
        _prevFogColor   = RenderSettings.fogColor;
        _prevFogDensity = RenderSettings.fogDensity;

        RenderSettings.fog      = true;
        RenderSettings.fogMode  = fogMode;
        RenderSettings.fogColor = fogColor;

        // _syncGeneration은 리셋하지 않는다 — 이유는 EsophagusSqueeze.OnEnable 주석 참고.
        _running = startOnEnable;
        ResetToBaseline();
        _bindRoutine = StartCoroutine(BindRevert());
    }

    void OnDisable()
    {
        if (_bindRoutine != null) StopCoroutine(_bindRoutine);
        _bindRoutine = null;
        if (CheerService.Instance != null)
            CheerService.Instance.UnregisterRevert(this);

        _running = false;
        _anchorServerTime = -1d;

        RenderSettings.fog        = _prevFogEnabled;
        RenderSettings.fogMode    = _prevFogMode;
        RenderSettings.fogColor   = _prevFogColor;
        RenderSettings.fogDensity = _prevFogDensity;
    }

    IEnumerator BindRevert()
    {
        while (CheerService.Instance == null)
            yield return null;
        _bindRoutine = null;
        if (!isActiveAndEnabled) yield break;
        CheerService.Instance.RegisterRevert(this);
        CheerService.Instance.NotifyHazardWindow(true); // 창 없음 — 등록 즉시 상시 유효
    }

    /// <summary>startOnEnable=false로 두고 대화·카운트다운 뒤에 시작할 때(UnityEvent 연결용).</summary>
    public void Begin()
    {
        _running = true;
        ResetToBaseline();
    }

    void Update()
    {
        if (!_running) return;

        // 클럭 소스가 바뀌면 다시 앵커한다 — 이유는 EsophagusSqueeze.Update 주석 참고
        // (로컬 시계로 잡은 앵커를 ServerTime으로 재면 안개가 한 번에 최대 밀도로 튄다).
        bool networkClock = HasNetworkClock;
        if (_anchorServerTime < 0d || networkClock != _anchoredOnNetworkClock)
        {
            _anchoredOnNetworkClock = networkClock;
            _anchorServerTime = GetServerTime();
            ApplyDensity(0f);
            return;
        }

        double elapsed = System.Math.Max(0d, GetServerTime() - _anchorServerTime);
        ApplyDensity(Mathf.Min(maxDensity, (float)elapsed * Mathf.Max(0f, densityPerSecond)));
    }

    // ── ITeamCheerRevert ──────────────────────────────────────────

    public void BuildRevertOrder(out int generation, out double resumeAtServerTime)
    {
        generation = _syncGeneration + 1;
        resumeAtServerTime = GetServerTime(); // 창 없음 — 지연 없이 즉시 걷힘
    }

    public void Revert(int generation, double resumeAtServerTime)
    {
        if (generation <= _syncGeneration) return; // 이미 처리한 세대 / 낡은 명령

        _syncGeneration   = generation;
        _anchorServerTime = resumeAtServerTime; // Host가 정한 절대 ServerTime — 전 머신 동일
        _anchoredOnNetworkClock = HasNetworkClock;
        ApplyDensity(0f);

        PulseHazardWindow();
    }

    // ── 내부 ──────────────────────────────────────────────────────

    void ResetToBaseline()
    {
        _anchorServerTime = -1d; // 다음 Update에서 그때의 클럭으로 앵커
        ApplyDensity(0f);
    }

    static void ApplyDensity(float density)
    {
        if (Mathf.Approximately(RenderSettings.fogDensity, density)) return;
        RenderSettings.fogDensity = density;
    }

    /// <summary>창 소비 락 해제 — 이유는 EsophagusSqueeze 클래스 주석 참고.</summary>
    static void PulseHazardWindow()
    {
        var svc = CheerService.Instance;
        if (svc == null) return;
        svc.NotifyHazardWindow(false);
        svc.NotifyHazardWindow(true);
    }

    static bool HasNetworkClock
    {
        get
        {
            var nm = NetworkManager.Singleton;
            return nm != null && nm.IsListening;
        }
    }

    static double GetServerTime()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening ? nm.ServerTime.Time : Time.timeAsDouble;
    }

#if UNITY_EDITOR
    /// <summary>이 머신에서만 걷는다(Host 핸드셰이크를 안 탐) — 다인 테스트에는 CheerService의 "팀 버프 강제 발동"을 쓸 것.</summary>
    [ContextMenu("테스트: 로컬만 안개 걷기")]
    void Debug_RevertLocal()
    {
        BuildRevertOrder(out int gen, out double t);
        Revert(gen, t);
    }
#endif
}
