using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 식도 조임 — 팀 응원 되돌림 대상 (ITeamCheerRevert). 초출 T1, 복습 T3.
/// CoopStageAudit.T.md §3 "주기적 공격 모델(2026-09-06 재확정)" 참고.
///
/// [모델 — Mouth/Saliva와 동일한 창 구조로 회귀]
/// 처음엔 "창 없음, 씬 시작부터 계속 압박" 지속형으로 만들었으나, 실제로는 랜덤 주기로
/// 압박이 오는 "공격" 형태가 필요해서(SalivaHazard와 동일 개념) 이 구조로 바꿨다:
///
///   Idle (응원 무시, 원래 반경)
///   → Warning (UI. 이때부터 응원)
///        ├─ 외침: Squeeze 안 넣음. Idle 유지
///        └─ 없음: Squeeze(공격, attackDuration에 걸쳐 원래 반경 → squeezeTargetRadius)
///            → Hold(그 반경 유지, 외침 전까지 무한 대기)
///            → 외침 시 Recover(recoverDuration에 걸쳐 원래 반경으로) → Idle
///   Idle 시작마다 랜덤 간격(randomIntervalMin~Max) 대기 후 다음 Warning.
///
/// [콜라이더 — MeshCollider 스케일 폐기, Box 조각 링(조리개)으로 교체 2026-09-06]
/// 원통 하나를 스케일하면 non-convex MeshCollider가 매 프레임 재굽기(re-cook)돼야 했다.
/// 대신 이 컴포넌트의 GameObject를 원통 중심(=옛 target 자리)에 두고, 그 자식으로
/// <see cref="segments"/>(평평한 Box 판자, 카메라 조리개 블레이드처럼 방사형 배치)를 건다.
/// 각 판자는 회전 없이 자기 반경 방향으로만 <c>Rigidbody(kinematic).MovePosition</c>으로
/// 평행 이동한다 — 스케일이 아니라 위치 이동이라 재굽기 자체가 없고, 매 프레임 완전히
/// 매끄럽게 움직여도 비용이 없다. 판자 폭을 원래 반경(rest)에서 이웃과 맞물리게 잡으면
/// 반경이 작아질수록(조여들수록) 인접 판자 사이 간격이 오히려 줄어들어 틈이 안 생긴다.
/// 또한 kinematic Rigidbody가 실제로 밀고 들어오므로 Player(dynamic Rigidbody)가
/// MeshCollider 스케일과 달리 정상적으로 밀려난다(AdvancingWall과 동일 원리) — 끼임 위험이 없다.
///
/// [동기화]
/// 새 RPC 없음. SalivaHazard/MouthController와 동일한 "PhaseStartServerTime 앵커로 첫 창
/// 동기화 + ServerTime 폴링 결정론적 랜덤(NetworkSessionData.Seed 기반)"을 그대로 재사용한다.
/// </summary>
public class EsophagusSqueeze : MonoBehaviour, ITeamCheerRevert
{
    enum HazardPhase
    {
        Idle,
        Warning,
        Squeezing,
        Holding,
        Recovering,
    }

    [Header("조임 링 조각 (조리개)")]
    [Tooltip("중심(이 오브젝트)을 향해 방사형으로 이동할 판자들. 각자 Rigidbody(Is Kinematic=true, " +
             "Interpolate)+BoxCollider(Is Trigger=false) 전제 — AdvancingWall과 동일 계약. " +
             "에디터에서 이 오브젝트를 원통 중심에 두고, 자식으로 8개 안팎을 등간격 방사형 배치할 것.\n" +
             "순서 무관 — 각 조각은 자기 로컬 위치(=자기 반경·방향)만 기준으로 움직인다.")]
    [SerializeField] Transform[] segments;

    [Header("조임 강도")]
    [Tooltip("Hold 상태에서 도달하는 반경(압박 강도) — 작을수록 강하게 조임. 각 조각의 원래 반경보다 작아야 함.")]
    [SerializeField] float squeezeTargetRadius = 3f;

    [Header("클립 길이 (초) — 수치는 스테이지 때")]
    [Tooltip("Warning 종료 후 원래 반경 → squeezeTargetRadius까지 걸리는 시간. 도중에 외치면 그 지점에서 즉시 Recover로 전환된다.")]
    [SerializeField] float attackDuration = 1.5f;

    [Tooltip("외침 성공 후 squeezeTargetRadius → 원래 반경까지 걸리는 시간.")]
    [SerializeField] float recoverDuration = 1.0f;

    [Header("랜덤 스케줄")]
    [SerializeField] float randomIntervalMin = 8f;
    [SerializeField] float randomIntervalMax = 18f;
    [SerializeField] float initialDelay = 0f;
    [SerializeField] bool startOnAwake = true;

    [Header("팀 응원 함정")]
    [Tooltip("Squeeze 전 Warning 유지 시간(초). 수치는 나중에 튜닝.")]
    [SerializeField] float warnDuration = 2f;

    [Header("네트워크 시드 (Host/Client 동기화)")]
    [Tooltip("다른 트랩 seedSalt와 겹치지 않게 유지 " +
             "(기존 salt: 0x050AD5E7, 0x43484153, 0x5716D000, 0x4D4F5554, 0x5B1DE000, 0x52554E52, " +
             "0x434F4C57, 0x574C525A, 0x4D43_0001, 0x544F4E47, 0x53504954).")]
    [SerializeField] int seedSalt = 0x45534F51;

    // 조각별 캐시 — Awake 1회. 로컬 좌표 기준(부모 = 이 오브젝트)이라 이 오브젝트가 곧 링 중심.
    Vector3[] _restLocalDir;   // 조각의 원래 로컬 위치 방향(정규화) — "바깥"
    float[]   _restRadius;     // 조각의 원래 로컬 반경(=원래 로컬 위치 길이)
    Rigidbody[] _rbs;          // 없으면 null → transform.position 직접 대입으로 폴백

    Coroutine _cycleCoroutine;
    Coroutine _bindRoutine;

    HazardPhase _phase = HazardPhase.Idle;
    bool _available;
    bool _prevented;
    bool _recoverQueued;
    bool _skipNextWindow;
    double _resyncDeadline = -1d;

    // PhaseStartServerTime(Host가 Phase 진입 직전에 찍는 절대 시각)이 전파될 때까지 기다리는 한도.
    // 그 안에 안 오면 앵커가 없는 씬으로 보고 예전처럼 로컬 시각으로 폴백한다.
    const float AnchorWaitTimeout = 3f;
    int _cycleCount;
    int _syncGeneration;

    // 현재 조임 진행도(0 = 원래 반경, 1 = squeezeTargetRadius). Recover가 "1에서 시작"을 가정하면
    // 부분 진행 상태(재시작·런타임 값 변경 등)에서 반경이 튄다 — SalivaHazard._coverVisualAlpha와 같은 이유.
    float _visualNormalized;

    public bool IsAvailable => _available;

    void Awake()
    {
        CacheSegments();
    }

    void CacheSegments()
    {
        int count = segments != null ? segments.Length : 0;
        _restLocalDir = new Vector3[count];
        _restRadius   = new float[count];
        _rbs          = new Rigidbody[count];

        for (int i = 0; i < count; i++)
        {
            Transform seg = segments[i];
            if (seg == null) continue;

            Vector3 localPos = seg.localPosition;
            _restRadius[i] = localPos.magnitude;
            _restLocalDir[i] = _restRadius[i] > 0.0001f ? localPos / _restRadius[i] : Vector3.zero;

            _rbs[i] = seg.GetComponent<Rigidbody>();
            if (_rbs[i] != null) _rbs[i].isKinematic = true;

            if (_restRadius[i] <= squeezeTargetRadius || _restRadius[i] <= 0.0001f)
                Debug.LogWarning(
                    $"[EsophagusSqueeze] segments[{i}]('{seg.name}')의 원래 반경({_restRadius[i]:F2})이 " +
                    $"squeezeTargetRadius({squeezeTargetRadius})보다 작거나 같습니다 — 조임이 전혀 안 걸리거나 반전됩니다.", this);

            if (_rbs[i] == null)
                Debug.LogWarning(
                    $"[EsophagusSqueeze] segments[{i}]('{seg.name}')에 Rigidbody가 없습니다 — " +
                    "transform.position 직접 대입으로 움직이므로 Player를 물리적으로 밀어내지 못합니다. " +
                    "Rigidbody(Is Kinematic=true, Interpolate) 추가 권장.", seg);
        }

        if (count == 0)
            Debug.LogWarning("[EsophagusSqueeze] segments가 비어 있습니다 — 조임이 아무 것도 움직이지 않습니다.", this);
    }

    void OnEnable()
    {
        ResetHazardFlags();
        SnapSqueeze(0f); // SalivaHazard.OnEnable의 HideCoverImmediate와 같은 자리 — 켜질 때 항상 원래 반경부터
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
        SnapSqueeze(0f); // 즉시 원래 반경으로 — 애니메이션 없이 스냅
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
        SnapSqueeze(0f);
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
            case HazardPhase.Squeezing:
            case HazardPhase.Holding:
                _recoverQueued = true;
                EndWindow();
                break;
            case HazardPhase.Idle:
                // 이 머신은 아직 이번 창을 열지 않았다(씬 로드 시각 차이 등) — 열지 않고
                // Host가 준 다음 예약으로 건너뛴다(MouthController.Revert와 동일 원칙).
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

            yield return SqueezeRoutine();

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

    /// <summary>첫 창을 Host/Client 공통 절대 시각에 건다 — MouthController.ResolveFirstWindow와 동일 원칙.</summary>
    IEnumerator ResolveFirstWindow()
    {
        yield return null; // PhaseManager.EnterPhase의 MarkAndSyncPhase가 끝난 뒤 앵커를 읽기 위한 1프레임 양보

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

    // Squeeze/Recover는 Rigidbody.MovePosition으로 실제 물리 이동을 일으키므로 AdvancingWall.LerpTo와
    // 같이 FixedUpdate 간격으로 진행한다 — Update 간격으로 MovePosition을 호출하면 보간이 덜 매끄럽다.
    // _recoverQueued를 루프 조건에 넣어 외침이 들어온 즉시 멈춘다(2026-09-06 변경 — 이전엔 끝까지
    // 조인 뒤에야 Recover를 시작했다). HazardCycle이 곧이어 RecoverRoutine을 시작하며, 그쪽은
    // 하드코딩된 1f가 아니라 _visualNormalized(현재 진행도)에서 되돌리므로 튀지 않는다.
    IEnumerator SqueezeRoutine()
    {
        _phase = HazardPhase.Squeezing;

        float dur = Mathf.Max(0f, attackDuration);
        if (dur <= 0f)
        {
            if (!_recoverQueued) ApplySqueeze(1f);
            yield break;
        }

        float t = 0f;
        while (t < dur && !_recoverQueued)
        {
            t += Time.fixedDeltaTime;
            ApplySqueeze(Mathf.Clamp01(t / dur));
            yield return new WaitForFixedUpdate();
        }
        if (!_recoverQueued)
            ApplySqueeze(1f);
    }

    // _phase = Idle 은 호출부(HazardCycle)가 찍는다 — Idle이 "다음 창을 기다리는 중"만 뜻해야
    // Revert가 "창 밖이라 건너뛸 머신"과 "직전 창을 되돌리는 중인 머신"을 구분할 수 있다.
    IEnumerator RecoverRoutine()
    {
        _phase = HazardPhase.Recovering;
        EndWindow();

        float dur = Mathf.Max(0f, recoverDuration);
        float from = _visualNormalized; // 하드코딩 1f 대신 현재 진행도에서 되돌린다
        if (dur <= 0f)
        {
            ApplySqueeze(0f);
            yield break;
        }

        float t = 0f;
        while (t < dur)
        {
            t += Time.fixedDeltaTime;
            ApplySqueeze(Mathf.Lerp(from, 0f, Mathf.Clamp01(t / dur)));
            yield return new WaitForFixedUpdate();
        }
        ApplySqueeze(0f);
    }

    /// <summary>예약된 재개 시각까지 대기 — 대기 중 Revert가 예약을 갱신하면 그 값을 그대로 따라간다.</summary>
    IEnumerator WaitForResyncDeadline()
    {
        while (_resyncDeadline > 0d && GetServerTime() < _resyncDeadline)
            yield return null;
        _resyncDeadline = -1d;
    }

    // 간격을 뽑는 축이 둘이다 — 로컬 스케줄(_cycleCount)과 되돌림 세대(_syncGeneration).
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

    /// <summary>
    /// normalized 0 = 원래 반경, 1 = squeezeTargetRadius. 조각마다 자기 반경만 보간해 같은 방향으로
    /// MovePosition — WallWaveController.FixedUpdate와 동일한 "world 목표 계산 후 rb 있으면 MovePosition,
    /// 없으면 transform 직접 대입" 패턴.
    /// </summary>
    void ApplySqueeze(float normalized)
    {
        _visualNormalized = normalized;
        if (segments == null) return;

        for (int i = 0; i < segments.Length; i++)
        {
            Transform seg = segments[i];
            if (seg == null) continue;

            float radius = Mathf.Lerp(_restRadius[i], squeezeTargetRadius, normalized);
            Vector3 localPos = _restLocalDir[i] * radius;
            Vector3 worldPos = transform.TransformPoint(localPos);

            if (_rbs[i] != null)
                _rbs[i].MovePosition(worldPos);
            else
                seg.position = worldPos;
        }
    }

    /// <summary>즉시 스냅(보간 없이) — OnEnable/OnDisable/StopCycle 전용. kinematic Rigidbody는
    /// MovePosition이 아니라 rb.position에 직접 대입해야 그 프레임에 바로 이동한다.</summary>
    void SnapSqueeze(float normalized)
    {
        _visualNormalized = normalized;
        if (segments == null) return;

        for (int i = 0; i < segments.Length; i++)
        {
            Transform seg = segments[i];
            if (seg == null) continue;

            float radius = Mathf.Lerp(_restRadius[i], squeezeTargetRadius, normalized);
            Vector3 localPos = _restLocalDir[i] * radius;
            Vector3 worldPos = transform.TransformPoint(localPos);

            if (_rbs[i] != null)
                _rbs[i].position = worldPos;
            else
                seg.position = worldPos;
        }
    }

    [ContextMenu("테스트: 사이클 시작")]
    void TestStartCycle() => StartCycle();

    [ContextMenu("테스트: 사이클 중지")]
    void TestStopCycle() => StopCycle();

    void OnDrawGizmosSelected()
    {
        if (segments == null) return;

        for (int i = 0; i < segments.Length; i++)
        {
            Transform seg = segments[i];
            if (seg == null) continue;

            Vector3 localPos = seg.localPosition;
            float radius = localPos.magnitude;
            if (radius <= 0.0001f) continue;
            Vector3 dir = localPos / radius;

            Vector3 restWorld   = transform.TransformPoint(dir * radius);
            Vector3 targetWorld = transform.TransformPoint(dir * squeezeTargetRadius);

            // 원래 위치(주황) → 목표 위치(파랑)로 이동 경로 표시. AdvancingWall.OnDrawGizmos와 동일 색상 관례.
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.9f);
            Gizmos.DrawWireSphere(restWorld, 0.15f);
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.9f);
            Gizmos.DrawLine(restWorld, targetWorld);
            Gizmos.DrawWireSphere(targetWorld, 0.1f);
        }

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, 0.08f);
    }
}
