using System.Collections;
using UnityEngine;

/// <summary>
/// DropTrap의 천장 형성 프리팹에 부착. MouthTrapAnimator와 동일한 BlendShape 패턴 사용.
///
/// [Blender Shape Key 설정]
///   Basis           = 완전히 자란 물방울 형태 (기본 메시)
///   Shape Key 1     = 작은 초기 물방울 형태  ← Unity에서 index 0
///   → weight 100 = 작음(Shape Key 적용), weight 0 = 완전히 자람(Basis)
///
/// [타이밍 구조]
///   Initialize(warnDuration) 호출 시 자동 분배:
///     warnDuration × growRatio   → 0→100 성장
///     warnDuration × wobbleRatio → 회전 흔들림 (낙하 직전 긴장감)
///   warnDuration 종료 → DropTrap이 이 오브젝트 파괴 + 실제 물방울 낙하
///
/// [주의]
///   이 프리팹에 Rigidbody 부착 금지 (천장에 고정 유지)
///   Blender Basis(0번)는 Unity 제외 → Blender 1번 Shape Key = Unity index 0
/// </summary>
public class DropletForming : MonoBehaviour
{
    [Header("BlendShape 대상")]
    [Tooltip("물방울 메시의 SkinnedMeshRenderer. 비워두면 자식에서 자동 탐색")]
    [SerializeField] SkinnedMeshRenderer formRenderer = null;

    [Tooltip("Unity BlendShapes 목록의 인덱스 (Blender Basis 제외 → Blender 1번 = Unity 0번)")]
    [SerializeField] int shapeIndex = 0;

    [Header("타이밍 비율 (growRatio + wobbleRatio ≤ 1.0)")]
    [Tooltip("전체 시간 중 성장(0→100)에 쓸 비율")]
    [SerializeField] float growRatio   = 0.75f;

    [Tooltip("전체 시간 중 흔들림에 쓸 비율")]
    [SerializeField] float wobbleRatio = 0.25f;

    [Header("BlendShape 방향")]
    [Tooltip("애니메이션 시작 weight.\n" +
             "Shape Key = 작은 형태인 경우: 100 (Shape Key 최대 적용 = 작음)\n" +
             "Shape Key = 큰 형태인 경우:   0 (Basis = 작음)")]
    [SerializeField] float startWeight = 100f;

    [Tooltip("애니메이션 종료 weight.\n" +
             "Shape Key = 작은 형태인 경우: 0 (Basis = 완전히 자란 상태)\n" +
             "Shape Key = 큰 형태인 경우: 100 (Shape Key 최대 = 완전히 자란 상태)")]
    [SerializeField] float endWeight = 0f;

    [Header("성장 곡선")]
    [Tooltip("처음엔 천천히, 끝에 빠르게 커지는 EaseIn 권장")]
    [SerializeField] AnimationCurve growCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("흔들림")]
    [Tooltip("흔들리는 최대 각도 (도)")]
    [SerializeField] float wobbleAngle     = 14f;

    [Tooltip("흔들림 진동 속도 (Hz)")]
    [SerializeField] float wobbleFrequency = 9f;

    [Header("폴백 (Initialize 미호출 시 사용)")]
    [Tooltip("DropTrap.Initialize()가 호출되지 않을 때 사용할 전체 시간")]
    [SerializeField] float fallbackTotalTime = 1.5f;

    Quaternion _baseRot;
    bool       _initialized;
    float      _totalTime;
    Transform  _rotateTarget; // 실제 회전을 적용할 Transform (formRenderer의 Transform 사용)

    // ── 외부 API ────────────────────────────────────────────────────────────

    /// <summary>DropTrap이 Instantiate 직후 호출. warnDuration을 그대로 넘기면 됨.</summary>
    public void Initialize(float totalTime)
    {
        _totalTime   = Mathf.Max(0.1f, totalTime);
        _initialized = true;
    }

    // ── Unity 라이프사이클 ───────────────────────────────────────────────────

    void Awake()
    {
        if (formRenderer == null)
            formRenderer = GetComponentInChildren<SkinnedMeshRenderer>();

        // 형성 프리팹에 Rigidbody가 있으면 kinematic 강제 설정 (Physics가 회전을 덮어쓰지 못하게)
        Rigidbody rb = GetComponentInChildren<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        // 회전 대상: 루트(자신)를 기준으로 회전 — 빈 부모 구조 권장
        // 구조: [Drop_Forming(빈, DropletForming 부착)] → [FBX 메시 자식]
        _rotateTarget = transform;
    }

    void Start()
    {
        if (!_initialized)
            _totalTime = fallbackTotalTime;

        _baseRot = _rotateTarget.localRotation;

        // BlendShape 유효성 검사 — 실패해도 wobble은 계속 실행
        if (!IsValid())
        {
        }
        else
        {
            string shapeName = formRenderer.sharedMesh.GetBlendShapeName(shapeIndex);
            SetWeight(startWeight);
        }

        StartCoroutine(FormRoutine()); // IsValid() 실패해도 FormRoutine은 항상 실행
    }

    // ── 애니메이션 루틴 ─────────────────────────────────────────────────────

    IEnumerator FormRoutine()
    {
        float growTime   = _totalTime * growRatio;
        float wobbleTime = _totalTime * wobbleRatio;

        // 1단계: 성장 (startWeight → endWeight)
        yield return LerpShape(startWeight, endWeight, growTime);

        // 2단계: 흔들림 (회전 흔들기, weight는 100 유지)
        if (wobbleTime > 0f)
            yield return WobbleRoutine(wobbleTime);

        // 이후 DropTrap이 Destroy 호출
    }

    IEnumerator LerpShape(float from, float to, float duration)
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
            float t = growCurve.Evaluate(Mathf.Clamp01(elapsed / duration));
            SetWeight(Mathf.Lerp(from, to, t));
            yield return null;
        }
        SetWeight(to);
    }

    IEnumerator WobbleRoutine(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed    += Time.deltaTime;
            float fade  = 1f - (elapsed / duration);
            float angle = Mathf.Sin(elapsed * wobbleFrequency * Mathf.PI * 2f)
                          * wobbleAngle * fade;
            _rotateTarget.localRotation = _baseRot * Quaternion.Euler(angle, 0f, angle * 0.3f);
            yield return null;
        }
        _rotateTarget.localRotation = _baseRot;
    }

    // ── 헬퍼 ────────────────────────────────────────────────────────────────

    bool IsValid() =>
        formRenderer != null &&
        formRenderer.sharedMesh != null &&
        shapeIndex >= 0 &&
        shapeIndex < formRenderer.sharedMesh.blendShapeCount;

    float GetWeight() => IsValid() ? formRenderer.GetBlendShapeWeight(shapeIndex) : 0f;

    void SetWeight(float w)
    {
        if (IsValid()) formRenderer.SetBlendShapeWeight(shapeIndex, w);
    }

    // ── 에디터 테스트 (플레이 중 컴포넌트 우클릭) ─────────────────────────

    [ContextMenu("테스트: 성장 재생")]
    void TestGrow()
    {
        StopAllCoroutines();
        SetWeight(startWeight);
        float dur = _totalTime * growRatio > 0f ? _totalTime * growRatio : 1.0f;
        StartCoroutine(LerpShape(startWeight, endWeight, dur));
    }

    [ContextMenu("테스트: 시작 weight (작은 상태)")]
    void TestReset() => SetWeight(startWeight);

    [ContextMenu("테스트: 끝 weight (완전 성장)")]
    void TestFull() => SetWeight(endWeight);

    [ContextMenu("테스트: BlendShape 목록 출력")]
    void PrintBlendShapes()
    {
    }
}
