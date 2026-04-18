using System.Collections;
using UnityEngine;

/// <summary>
/// 입 배경 연출 컨트롤러 — BlendShape(Shape Key)로 입을 닫았다 여는 시각 효과.
/// 데미지/충돌 없이 순수 연출 전용.
///
/// [타이밍 모드]
/// FixedInterval  : fixedInterval 초마다 자동 닫힘
/// RandomInterval : randomMin~randomMax 사이 랜덤 간격으로 자동 닫힘
///
/// [설정 방법]
/// 1. 이 스크립트를 루트 GameObject에 부착
/// 2. mouthRenderer : 입 메시의 SkinnedMeshRenderer 연결 (비워두면 자식에서 자동 탐색)
/// 3. closeShapeIndex : Inspector > SkinnedMeshRenderer > BlendShapes 에서 "입 닫기" Shape Key 인덱스 확인 후 입력
///    (Blender 순서 그대로 : Basis 제외하고 0번부터 카운트)
/// </summary>
public class MouthController : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("입 메시의 SkinnedMeshRenderer. 비워두면 자식에서 자동 탐색")]
    [SerializeField] private SkinnedMeshRenderer mouthRenderer = null;

    [Tooltip("'입 닫기' Shape Key 인덱스\n" +
             "Inspector > SkinnedMeshRenderer > BlendShapes 목록에서 확인")]
    [SerializeField] private int closeShapeIndex = 2;

    [Tooltip("WindTrap이 활성화 중일 때 입닫기 사이클을 건너뜀.\n" +
             "Push/Pull 각각 등록 가능. 하나라도 활성화 중이면 건너뜀. 없으면 비워두면 됨.")]
    [SerializeField] private WindTrap[] windTraps = new WindTrap[0];

    [Header("속도 설정")]
    [Tooltip("닫히는 데 걸리는 시간(초)")]
    [SerializeField] private float closeSpeed = 0.3f;

    [Tooltip("완전히 닫힌 상태 유지 시간(초)")]
    [SerializeField] private float holdDuration = 1.0f;

    [Tooltip("다시 열리는 데 걸리는 시간(초)")]
    [SerializeField] private float openSpeed = 0.5f;

    [Tooltip("닫힐 때 커브")]
    [SerializeField] private AnimationCurve closeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("열릴 때 커브")]
    [SerializeField] private AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("타이밍 설정")]
    [Tooltip("FixedInterval: 고정 간격 반복\nRandomInterval: 랜덤 간격 반복")]
    [SerializeField] private TimingMode timingMode = TimingMode.RandomInterval;

    [Tooltip("FixedInterval 모드 전용 — 닫힘 발동 간격(초)")]
    [SerializeField] private float fixedInterval = 10f;

    [Tooltip("RandomInterval 모드 전용 — 최소 간격(초)")]
    [SerializeField] private float randomIntervalMin = 5f;

    [Tooltip("RandomInterval 모드 전용 — 최대 간격(초)")]
    [SerializeField] private float randomIntervalMax = 15f;

    [Tooltip("첫 발동까지 대기 시간(초)")]
    [SerializeField] private float initialDelay = 0f;

    [Tooltip("Start 시 자동으로 사이클 시작 여부")]
    [SerializeField] private bool startOnAwake = true;

    public enum TimingMode { FixedInterval, RandomInterval }

    bool _isClosing;
    bool _isTransitioning;
    Coroutine _cycleCoroutine;

    /// <summary>현재 입이 닫히는 중 또는 열리는 중이면 true. MouthWindAnimator가 대기 여부 판단에 사용.</summary>
    public bool IsBusy => _isClosing;

    /// <summary>전환 연출 중이면 true. 이 동안 AutoCycle이 중단됨.</summary>
    public bool IsTransitioning => _isTransitioning;

    void Awake()
    {
        if (mouthRenderer == null)
            mouthRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
    }

    void Start()
    {
        if (!IsValid())
        {
            Debug.LogWarning($"[MouthController] {name}: mouthRenderer 또는 closeShapeIndex 설정을 확인하세요.", this);
            return;
        }

        if (startOnAwake)
            StartCycle();
    }

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>자동 사이클 시작</summary>
    public void StartCycle()
    {
        if (_cycleCoroutine != null) StopCoroutine(_cycleCoroutine);
        _cycleCoroutine = StartCoroutine(AutoCycle());
    }

    /// <summary>자동 사이클 중지</summary>
    public void StopCycle()
    {
        if (_cycleCoroutine != null)
        {
            StopCoroutine(_cycleCoroutine);
            _cycleCoroutine = null;
        }
    }

    /// <summary>
    /// 스테이지/Phase 전환용 일회성 강제 닫기.
    /// AutoCycle을 일시 중단하고 입을 닫은 뒤 onClosed를 호출하여 오브젝트·머티리얼 교체를 수행하고,
    /// 입이 열리면 onOpened를 호출한 후 AutoCycle을 재개한다.
    /// 이미 전환 중이면 무시.
    /// </summary>
    public void CloseForTransition(System.Action onClosed, System.Action onOpened)
    {
        if (_isTransitioning) return;
        StartCoroutine(TransitionCoroutine(onClosed, onOpened));
    }

    IEnumerator TransitionCoroutine(System.Action onClosed, System.Action onOpened)
    {
        _isTransitioning = true;
        bool wasRunning = _cycleCoroutine != null;
        StopCycle();

        _isClosing = true;
        yield return LerpShape(GetWeight(), 100f, closeSpeed, closeCurve);

        onClosed?.Invoke();
        yield return null; // 1프레임 대기 — 콜백의 오브젝트 활성화 처리 완료 보장

        yield return LerpShape(GetWeight(), 0f, openSpeed, openCurve);

        _isClosing       = false;
        _isTransitioning = false;
        onOpened?.Invoke();

        if (wasRunning) StartCycle();
    }

    // ── 내부 ────────────────────────────────────────────────────

    IEnumerator AutoCycle()
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            if (!_isClosing)
                StartCoroutine(CloseCycle());

            float interval = timingMode == TimingMode.RandomInterval
                ? Random.Range(randomIntervalMin, randomIntervalMax)
                : fixedInterval;

            yield return new WaitForSeconds(interval);
        }
    }

    IEnumerator CloseCycle()
    {
        // 전환 연출 중이거나 등록된 WindTrap 중 하나라도 활성화 중이면 이번 사이클 건너뜀
        if (_isTransitioning) yield break;
        foreach (WindTrap wt in windTraps)
        {
            if (wt != null && wt.IsWindActive)
                yield break;
        }

        _isClosing = true;

        yield return LerpShape(GetWeight(), 100f, closeSpeed, closeCurve);

        if (holdDuration > 0f)
            yield return new WaitForSeconds(holdDuration);

        yield return LerpShape(GetWeight(), 0f, openSpeed, openCurve);

        _isClosing = false;
    }

    IEnumerator LerpShape(float from, float to, float duration, AnimationCurve curve)
    {
        if (!IsValid() || duration <= 0f)
        {
            SetWeight(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = curve.Evaluate(Mathf.Clamp01(elapsed / duration));
            SetWeight(Mathf.Lerp(from, to, t));
            yield return null;
        }
        SetWeight(to);
    }

    // ── 헬퍼 ────────────────────────────────────────────────────

    bool IsValid() =>
        mouthRenderer != null &&
        mouthRenderer.sharedMesh != null &&
        closeShapeIndex >= 0 &&
        closeShapeIndex < mouthRenderer.sharedMesh.blendShapeCount;

    float GetWeight() => IsValid() ? mouthRenderer.GetBlendShapeWeight(closeShapeIndex) : 0f;

    void SetWeight(float w)
    {
        if (IsValid()) mouthRenderer.SetBlendShapeWeight(closeShapeIndex, w);
    }

    // ── 에디터 테스트 (플레이 중 컴포넌트 우클릭) ─────────────────────────

    [ContextMenu("테스트: 즉시 닫기")]
    void TestClose()
    {
        if (_isClosing) return;
        StartCoroutine(CloseCycle());
    }

    [ContextMenu("테스트: 즉시 열기")]
    void TestOpen() => SetWeight(0f);

    [ContextMenu("테스트: BlendShape 목록 출력")]
    void PrintBlendShapes()
    {
        if (mouthRenderer == null) { Debug.LogError("[MouthController] mouthRenderer가 null입니다."); return; }
        int count = mouthRenderer.sharedMesh.blendShapeCount;
        for (int i = 0; i < count; i++)
            Debug.Log($"  [{i}] {mouthRenderer.sharedMesh.GetBlendShapeName(i)}");
    }
}
