using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 식도 조임 — 팀 응원 되돌림 대상 (ITeamCheerRevert). 초출 T1, 복습 T3.
/// CoopStageAudit.T.md §3 "지속형 모델 + 정수 스텝" 참고.
///
/// [동작 — 계단식(정수 스텝)]
/// 식도 원통(BG) <see cref="target"/>은 속이 빈 튜브라 Box/Sphere/Capsule 같은 convex primitive
/// 콜라이더로는 표현이 안 되고 non-convex MeshCollider가 필요하다. Unity/PhysX는 MeshCollider의
/// 스케일이 바뀔 때마다 내부 BVH를 다시 굽는데(re-cook), 이건 매 프레임 부르면 실제로 무거운
/// 작업이다. 그래서 이 컴포넌트는 스케일을 "매 프레임 조금씩"이 아니라 "<see cref="secondsPerStep"/>초마다
/// <see cref="scalePerStep"/>만큼 한 번" 정수 계단으로 낮춘다 — re-cook 호출 자체를 초당 1회
/// 미만으로 억제한다(예: 2초 스텝 = 계단 사이 재굽기 0회, 계단 순간만 1회).
/// x/y를 <see cref="minScale"/>에서 캡한다(즉사 방지 — 조임 자체는 데미지 없음, 핸디캡만).
/// ColorWall은 이 컴포넌트와 별개(원통과 독립된 좌우 압박, 되돌림 대상 아님).
///
/// [창 없음 — M 계열과의 차이]
/// MouthController/SalivaHazard/TongueController는 Idle→Warning→Attack의 "창" 안에서만
/// 응원이 유효하다. 식도 조임은 창 개념이 없다 — 반경이 얼마나 줄어 있든 <see cref="IsAvailable"/>은
/// 항상 true이고, 팀 응원이 성공하면 그 즉시 원래 스케일로 복귀한 뒤 축소가 다시 시작된다.
/// 계속 고함쳐도 "누적만 리셋"될 뿐 영구 정지는 없다 — 다음 스텝 시각부터 다시 줄어든다.
///
/// [동기화]
/// 새 RPC/NetworkVariable 없음. 각 머신이 ServerTime만 폴링해 결정론적으로 스텝 수를 계산한다
/// (AdvancingWall/WallWaveController와 동일 원칙). 팀 응원 성공 시점의 절대 ServerTime을
/// CheerService.BuildRevertOrder/Revert(generation, resumeAtServerTime) 핸드셰이크로 전 머신에
/// 맞춰 배포하므로 별도 첫 창 동기화(PhaseStartServerTime 앵커)가 필요 없다 — T.Stage는 대부분
/// 페이즈 1개뿐이라 씬 로드 시각 차이가 누적 오차로 번지지 않는다.
///
/// [연속 재도전 허용 — NotifyHazardWindow 펄스]
/// CheerService.ValidateTeamCheer는 창이 한 번 소비되면(_teamWindowConsumed) 다음 창이 열릴
/// 때까지 표를 막는다. M 계열은 다음 Warning이 열릴 때 NotifyHazardWindow(true)를 다시 불러
/// 이 락을 푼다. 조임은 창이 늘 열려 있으므로 그 시점이 없다 — Revert() 안에서
/// NotifyHazardWindow(false)→(true)를 즉시 펄스로 호출해 같은 효과를 낸다(새 RPC 추가 없이
/// 기존 API만 재사용). 이게 없으면 스테이지당 팀 응원이 딱 한 번만 먹는다.
/// </summary>
public class EsophagusSqueeze : MonoBehaviour, ITeamCheerRevert
{
    [Header("대상")]
    [Tooltip("반경을 축소할 식도 원통(BG) Transform. MeshCollider(non-convex)가 붙는 오브젝트.\n" +
             "비워두면 이 오브젝트 자신.")]
    [SerializeField] Transform target;

    [Header("조임 속도 — 정수 스텝 (MeshCollider 재굽기 비용 때문에 매 프레임 대신 계단식)")]
    [Tooltip("한 스텝까지 걸리는 시간(초). 예) 2 = 2초마다 한 단계씩 줄어듦.")]
    [SerializeField] float secondsPerStep = 2f;

    [Tooltip("한 스텝마다 줄어드는 스케일 양. 예) 1 = 10,10 → 9,9 → 8,8 … 정수로 튜닝 권장(스텝이\n" +
             "곧 재굽기 호출이므로 애매한 소수를 쓸 이유가 없음).")]
    [SerializeField] float scalePerStep = 1f;

    [Tooltip("최소 반경(스케일 단위). 이 아래로는 줄지 않음 — 즉사 방지 캡, 데미지 없음.")]
    [SerializeField] float minScale = 4f;

    [Header("시작")]
    [Tooltip("켜지는 즉시 조이기 시작. 끄면 Begin()을 대화·카운트다운 등에서 호출할 것.")]
    [SerializeField] bool startOnEnable = true;

    Vector3 _initialScale;
    double _anchorServerTime = -1d;
    bool _anchoredOnNetworkClock;
    bool _running;
    int _appliedStep = -1;     // 마지막으로 실제 대입(=재굽기)한 스텝 번호. 같으면 대입 스킵.
    int _syncGeneration;
    Coroutine _bindRoutine;

    /// <summary>창 없음 — 컴포넌트가 켜져 있으면 언제든 응원 유효.</summary>
    public bool IsAvailable => isActiveAndEnabled;

    void Awake()
    {
        if (target == null) target = transform;
        _initialScale = target.localScale;

        if (minScale >= Mathf.Min(_initialScale.x, _initialScale.y))
            Debug.LogWarning(
                $"[EsophagusSqueeze] minScale({minScale})이 시작 스케일({_initialScale.x}, {_initialScale.y}) 이상입니다 " +
                "— 조임이 전혀 걸리지 않거나 시작하자마자 원통이 커집니다.", this);
    }

    void OnEnable()
    {
        // _syncGeneration은 리셋하지 않는다 — Mouth/Saliva/Tongue와 같은 규칙.
        // 한 머신에서만 이 오브젝트가 껐다 켜져 세대가 0으로 돌아가면, 그 머신은 Host가 보낸
        // 낮은 세대 명령을 "낡은 명령"으로 버리고 혼자 영영 안 돌아온다.
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
        ResetToBaseline();
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

        // 네트워크 클럭이 붙기 전(DevStageHostBootstrap은 1프레임 뒤에 Host를 띄운다)에 로컬
        // 시계로 앵커를 잡아두면, ServerTime으로 바뀌는 순간 두 시계의 원점 차이가 그대로
        // 경과 시간이 돼 스텝이 한 번에 여러 칸 튄다. 클럭 소스가 바뀌면 다시 앵커한다.
        bool networkClock = HasNetworkClock;
        if (_anchorServerTime < 0d || networkClock != _anchoredOnNetworkClock)
        {
            _anchoredOnNetworkClock = networkClock;
            _anchorServerTime = GetServerTime();
            ApplyStep(0);
            return;
        }

        double elapsed = System.Math.Max(0d, GetServerTime() - _anchorServerTime);
        int step = secondsPerStep > 0f ? (int)(elapsed / secondsPerStep) : 0;
        ApplyStep(step);
    }

    // ── ITeamCheerRevert ──────────────────────────────────────────

    public void BuildRevertOrder(out int generation, out double resumeAtServerTime)
    {
        generation = _syncGeneration + 1;
        resumeAtServerTime = GetServerTime(); // 창 없음 — 지연 없이 즉시 원상 복귀
    }

    public void Revert(int generation, double resumeAtServerTime)
    {
        if (generation <= _syncGeneration) return; // 이미 처리한 세대 / 낡은 명령

        _syncGeneration = generation;
        _anchorServerTime = resumeAtServerTime; // Host가 정한 절대 ServerTime — 전 머신 동일
        _anchoredOnNetworkClock = HasNetworkClock;
        ApplyStep(0);

        PulseHazardWindow();
    }

    // ── 내부 ──────────────────────────────────────────────────────

    void ResetToBaseline()
    {
        _anchorServerTime = -1d; // 다음 Update에서 그때의 클럭으로 앵커
        ApplyStep(0);
    }

    /// <summary>
    /// step 번호가 이전과 같으면 아무것도 안 한다 — MeshCollider 재굽기는 실제로 스케일을
    /// 대입하는 이 경로에서만 발생하므로, 스텝 사이 프레임에는 절대 손대지 않는 것이 핵심이다.
    /// </summary>
    void ApplyStep(int step)
    {
        if (step == _appliedStep) return;
        _appliedStep = step;

        float shrink = step * Mathf.Max(0f, scalePerStep);
        float x = Mathf.Max(minScale, _initialScale.x - shrink);
        float y = Mathf.Max(minScale, _initialScale.y - shrink);
        target.localScale = new Vector3(x, y, _initialScale.z);
    }

    /// <summary>창 소비 락 해제 — 이유는 클래스 주석 참고. 같은 프레임 안의 false→true라 경고 UI는 깜빡이지 않는다.</summary>
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
    /// <summary>이 머신에서만 되돌린다(Host 핸드셰이크를 안 탐) — 다인 테스트에는 CheerService의 "팀 버프 강제 발동"을 쓸 것.</summary>
    [ContextMenu("테스트: 로컬만 원상 복구")]
    void Debug_RevertLocal()
    {
        BuildRevertOrder(out int gen, out double t);
        Revert(gen, t);
    }
#endif
}
