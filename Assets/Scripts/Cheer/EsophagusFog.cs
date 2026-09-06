using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 식도 안개 — 팀 응원 되돌림 대상 (ITeamCheerRevert). 초출 T2, 복습 T4.
/// CoopStageAudit.T.md §5 "주기적 공격 모델(2026-09-06 재확정)" 참고.
///
/// [모델 — Mouth/Saliva와 동일한 창 구조로 회귀]
/// 처음엔 "창 없음, 씬 시작부터 계속 짙어짐" 지속형으로 만들었으나, 실제로는 랜덤 주기로
/// 압박이 오는 "공격" 형태가 필요해서(SalivaHazard와 동일 개념) 이 구조로 바꿨다:
///
///   Idle (응원 무시, 안개 없음)
///   → Warning (UI. 이때부터 응원)
///        ├─ 외침: Thicken 안 넣음. Idle 유지
///        └─ 없음: Thicken(공격, attackDuration에 걸쳐 밀도 0 → maxDensity)
///            → Hold(그 밀도 유지, 외침 전까지 무한 대기)
///            → 외침 시 Recover(recoverDuration에 걸쳐 밀도 0으로) → Idle
///   Idle 시작마다 랜덤 간격(randomIntervalMin~Max) 대기 후 다음 Warning.
///
/// <c>RenderSettings.fog</c>(거리 기반)를 쓴다 — M 계열의 암전(화면 전체 알파, 거리 무관)과는
/// 다른 메커니즘. URP는 빌트인 Fog(Lighting > Environment)를 그대로 쓰고, URP Lit 계열만
/// 반영되며 커스텀/Unlit 셰이더(Boulder 등)는 범위 밖. 씬 전역 적용 — 구간 분리 없음, 팀원
/// 시야도 동일하게 영향받음(Host/Client 차이 없이 각자 로컬로 계산).
///
/// [동기화]
/// 새 RPC 없음. SalivaHazard/MouthController와 동일한 "PhaseStartServerTime 앵커로 첫 창
/// 동기화 + ServerTime 폴링 결정론적 랜덤(NetworkSessionData.Seed 기반)"을 그대로 재사용한다.
/// </summary>
public class EsophagusFog : MonoBehaviour, ITeamCheerRevert
{
    enum HazardPhase
    {
        Idle,
        Warning,
        Thickening,
        Holding,
        Recovering,
    }

    [Header("안개 강도")]
    [Tooltip("Hold 상태에서 도달하는 밀도(압박 강도). 시야 완전 차단은 아님.")]
    [SerializeField] float maxDensity = 0.05f;

    [Header("Fog 설정 (활성화 시 적용 — 씬 RenderSettings를 덮어씀)")]
    [SerializeField] FogMode fogMode = FogMode.Exponential;
    [SerializeField] Color fogColor = Color.gray;

    [Header("클립 길이 (초) — 수치는 스테이지 때")]
    [Tooltip("Warning 종료 후 밀도 0 → maxDensity까지 걸리는 시간. 도중에 외치면 그 지점에서 즉시 Recover로 전환된다.")]
    [SerializeField] float attackDuration = 1.5f;

    [Tooltip("외침 성공 후 maxDensity → 0까지 걸리는 시간.")]
    [SerializeField] float recoverDuration = 1.0f;

    [Header("랜덤 스케줄")]
    [SerializeField] float randomIntervalMin = 8f;
    [SerializeField] float randomIntervalMax = 18f;
    [SerializeField] float initialDelay = 0f;
    [SerializeField] bool startOnAwake = true;

    [Header("팀 응원 함정")]
    [Tooltip("Thicken 전 Warning 유지 시간(초). 수치는 나중에 튜닝.")]
    [SerializeField] float warnDuration = 2f;

    [Header("네트워크 시드 (Host/Client 동기화)")]
    [Tooltip("다른 트랩 seedSalt와 겹치지 않게 유지 " +
             "(기존 salt: 0x050AD5E7, 0x43484153, 0x5716D000, 0x4D4F5554, 0x5B1DE000, 0x52554E52, " +
             "0x434F4C57, 0x574C525A, 0x4D43_0001, 0x544F4E47, 0x53504954, EsophagusSqueeze 0x45534F51).")]
    [SerializeField] int seedSalt = 0x45534F46;

    Coroutine _cycleCoroutine;
    Coroutine _bindRoutine;

    HazardPhase _phase = HazardPhase.Idle;
    bool _available;
    bool _prevented;
    bool _recoverQueued;
    bool _skipNextWindow;
    double _resyncDeadline = -1d;

    const float AnchorWaitTimeout = 3f;
    int _cycleCount;
    int _syncGeneration;

    bool _prevFogEnabled;
    FogMode _prevFogMode;
    Color _prevFogColor;
    float _prevFogDensity;

    public bool IsAvailable => _available;

    void OnEnable()
    {
        _prevFogEnabled = RenderSettings.fog;
        _prevFogMode    = RenderSettings.fogMode;
        _prevFogColor   = RenderSettings.fogColor;
        _prevFogDensity = RenderSettings.fogDensity;

        ApplyHazardFogSettings();

        ResetHazardFlags();
        _bindRoutine = StartCoroutine(BindAndStartHazard());
    }

    void OnDisable()
    {
        if (CheerService.Instance != null)
            CheerService.Instance.UnregisterRevert(this);
        StopAllCoroutines();
        _cycleCoroutine = null;
        _bindRoutine = null;
        ResetHazardFlags();
        RestoreSceneFog();
    }

    void ApplyHazardFogSettings()
    {
        RenderSettings.fog        = true;
        RenderSettings.fogMode    = fogMode;
        RenderSettings.fogColor   = fogColor;
        RenderSettings.fogDensity = 0f;
    }

    void RestoreSceneFog()
    {
        RenderSettings.fog        = _prevFogEnabled;
        RenderSettings.fogMode    = _prevFogMode;
        RenderSettings.fogColor   = _prevFogColor;
        RenderSettings.fogDensity = _prevFogDensity;
    }

    IEnumerator BindAndStartHazard()
    {
        while (CheerService.Instance == null)
            yield return null;
        _bindRoutine = null;
        if (!isActiveAndEnabled) yield break;
        CheerService.Instance.RegisterRevert(this);
        if (startOnAwake)
            StartCycle();
    }

    // ── 외부 호출 ────────────────────────────────────────────────

    public void StartCycle()
    {
        // StopCycle이 씬 원본 Fog로 되돌려 놓았을 수 있으므로 다시 이 함정 설정으로 잡는다.
        ApplyHazardFogSettings();
        if (_cycleCoroutine != null) StopCoroutine(_cycleCoroutine);
        _cycleCoroutine = StartCoroutine(HazardCycle());
    }

    public void StopCycle()
    {
        if (_cycleCoroutine != null)
        {
            StopCoroutine(_cycleCoroutine);
            _cycleCoroutine = null;
        }
        ResetHazardFlags();
        RestoreSceneFog(); // 안개 상태 SSOT = OnEnable에 찍은 _prevFog* 스냅샷. 여기서만 0으로 밀면 OnDisable과 갈린다.
    }

    public void BuildRevertOrder(out int generation, out double resumeAtServerTime)
    {
        generation = _syncGeneration + 1;
        resumeAtServerTime = GetServerTime() + PickSeededInterval(generation, RevertAxis);
    }

    public void Revert(int generation, double resumeAtServerTime)
    {
        if (generation <= _syncGeneration) return; // 이미 처리한 세대 / 낡은 명령

        _syncGeneration = generation;
        _resyncDeadline = resumeAtServerTime;

        switch (_phase)
        {
            case HazardPhase.Warning:
                _prevented = true;
                EndWindow();
                break;
            case HazardPhase.Thickening:
            case HazardPhase.Holding:
                _recoverQueued = true;
                EndWindow();
                break;
            case HazardPhase.Idle:
                _skipNextWindow = true;
                break;
            // Recovering: 직전 창을 되돌리는 중 — 위에서 받은 _resyncDeadline만 따라가면 위상이 맞는다.
        }
    }

    // ── 코루틴 ────────────────────────────────────────────────────

    IEnumerator HazardCycle()
    {
        yield return ResolveFirstWindow();

        while (true)
        {
            if (_resyncDeadline > 0d)
            {
                yield return WaitForResyncDeadline();
            }
            else
            {
                yield return new WaitForSeconds(PickSeededInterval(_cycleCount, ScheduleAxis));
                _cycleCount++;
            }

            if (_skipNextWindow)
            {
                _skipNextWindow = false;
                _phase = HazardPhase.Idle;
                continue;
            }

            _prevented = false;
            _recoverQueued = false;
            _phase = HazardPhase.Warning;
            _available = true;
            CheerService.Instance?.NotifyHazardWindow(true);

            float warnElapsed = 0f;
            float warn = Mathf.Max(0f, warnDuration);
            while (warnElapsed < warn && !_prevented)
            {
                warnElapsed += Time.deltaTime;
                yield return null;
            }

            if (_prevented)
            {
                _prevented = false;
                _phase = HazardPhase.Idle;
                continue;
            }

            yield return ThickenRoutine();

            if (_recoverQueued)
            {
                _recoverQueued = false;
                yield return RecoverRoutine();
                _phase = HazardPhase.Idle;
                continue;
            }

            _phase = HazardPhase.Holding;
            while (!_recoverQueued)
                yield return null;

            _recoverQueued = false;
            yield return RecoverRoutine();
            _phase = HazardPhase.Idle;
        }
    }

    IEnumerator ResolveFirstWindow()
    {
        yield return null;

        double anchor = -1d;
        float waited = 0f;
        while (waited < AnchorWaitTimeout)
        {
            var sns = StageNetworkState.Instance;
            if (sns != null && sns.PhaseStartServerTime > 0d)
            {
                anchor = sns.PhaseStartServerTime;
                break;
            }
            waited += Time.deltaTime;
            yield return null;
        }

        if (anchor > 0d)
        {
            _resyncDeadline = anchor + initialDelay + PickSeededInterval(_cycleCount, ScheduleAxis);
            _cycleCount++;
            yield break;
        }

        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);
    }

    // _recoverQueued를 루프 조건에 넣어 외침이 들어온 즉시 멈춘다(2026-09-06 변경 — 이전엔 끝까지
    // 짙어진 뒤에야 Recover를 시작했다). HazardCycle이 곧이어 RecoverRoutine을 시작하며, 그쪽은
    // 하드코딩된 maxDensity가 아니라 RenderSettings.fogDensity(현재 값, SSOT)에서 되돌리므로 튀지 않는다.
    IEnumerator ThickenRoutine()
    {
        _phase = HazardPhase.Thickening;

        float dur = Mathf.Max(0f, attackDuration);
        if (dur <= 0f)
        {
            if (!_recoverQueued) RenderSettings.fogDensity = maxDensity;
            yield break;
        }

        float from = RenderSettings.fogDensity;
        float t = 0f;
        while (t < dur && !_recoverQueued)
        {
            t += Time.deltaTime;
            RenderSettings.fogDensity = Mathf.Lerp(from, maxDensity, Mathf.Clamp01(t / dur));
            yield return null;
        }
        if (!_recoverQueued)
            RenderSettings.fogDensity = maxDensity;
    }

    IEnumerator RecoverRoutine()
    {
        _phase = HazardPhase.Recovering;
        EndWindow();

        float dur = Mathf.Max(0f, recoverDuration);
        // 안개 밀도의 현재 상태는 RenderSettings 자체가 SSOT — maxDensity를 시작점으로 하드코딩하면
        // 런타임에 그 값을 바꿨거나 부분 진행 중일 때 밀도가 튄다.
        float from = RenderSettings.fogDensity;
        if (dur <= 0f)
        {
            RenderSettings.fogDensity = 0f;
            yield break;
        }

        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            RenderSettings.fogDensity = Mathf.Lerp(from, 0f, Mathf.Clamp01(t / dur));
            yield return null;
        }
        RenderSettings.fogDensity = 0f;
    }

    IEnumerator WaitForResyncDeadline()
    {
        while (_resyncDeadline > 0d && GetServerTime() < _resyncDeadline)
            yield return null;
        _resyncDeadline = -1d;
    }

    const int ScheduleAxis = 0;
    const int RevertAxis   = 1;

    float PickSeededInterval(int generation, int axis)
    {
        int mixedSeed = NetworkSessionData.Seed ^ seedSalt ^ (generation * 0x2545F491) ^ (axis * 0x27220A95);
        var prevState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(mixedSeed);
        float min = randomIntervalMin;
        float max = Mathf.Max(min, randomIntervalMax);
        float interval = Random.Range(min, max);
        UnityEngine.Random.state = prevState;
        return interval;
    }

    static double GetServerTime()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening ? nm.ServerTime.Time : Time.timeAsDouble;
    }

    void EndWindow()
    {
        _available = false;
        CheerService.Instance?.NotifyHazardWindow(false);
    }

    void ResetHazardFlags()
    {
        _phase = HazardPhase.Idle;
        _available = false;
        _prevented = false;
        _recoverQueued = false;
        _skipNextWindow = false;
        _resyncDeadline = -1d;
        CheerService.Instance?.NotifyHazardWindow(false);
    }

    [ContextMenu("테스트: 사이클 시작")]
    void TestStartCycle() => StartCycle();

    [ContextMenu("테스트: 사이클 중지")]
    void TestStopCycle() => StopCycle();
}
