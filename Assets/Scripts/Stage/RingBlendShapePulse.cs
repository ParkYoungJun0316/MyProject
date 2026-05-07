using UnityEngine;

/// <summary>
/// 링 BlendShape를 반복 수축/이완시키는 컴포넌트.
///
/// [핵심]
/// - Collider는 고정, 비주얼(BlendShape)만 반복 애니메이션
/// - 여러 링에 phaseOffset을 다르게 주면 위상차 연출 가능
///
/// [설정]
/// 1. ringRenderer: 링의 SkinnedMeshRenderer (비우면 자식에서 자동 탐색)
/// 2. blendShapeIndex: 반복시킬 BlendShape 인덱스
/// 3. minWeight/maxWeight: 수축~이완 범위(0~100)
/// 4. cyclePerSecond: 1초당 반복 횟수
/// </summary>
public class RingBlendShapePulse : MonoBehaviour
{
    [Header("대상")]
    [Tooltip("링 메시의 SkinnedMeshRenderer. 비우면 자식에서 자동 탐색")]
    [SerializeField] SkinnedMeshRenderer ringRenderer = null;

    [Tooltip("SkinnedMeshRenderer의 BlendShape 인덱스")]
    [SerializeField] int blendShapeIndex = 0;

    [Header("가중치 범위")]
    [Tooltip("최소 Weight (0~100)")]
    [SerializeField] float minWeight = 0f;

    [Tooltip("최대 Weight (0~100)")]
    [SerializeField] float maxWeight = 0f;

    [Header("반복 설정")]
    [Tooltip("1초당 반복 횟수 (Hz)")]
    [SerializeField] float cyclePerSecond = 0f;

    void Awake()
    {
        if (ringRenderer == null)
            ringRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
    }

    void OnEnable()
    {
        ApplyWeight(EvaluateWeight(Time.time));
    }

    void Update()
    {
        if (!IsValid()) return;
        ApplyWeight(EvaluateWeight(Time.time));
    }

    /// <summary>외부에서 즉시 기준값(minWeight)으로 리셋할 때 사용.</summary>
    public void ResetToMin()
    {
        ApplyWeight(minWeight);
    }

    /// <summary>외부에서 즉시 최대값(maxWeight)으로 설정할 때 사용.</summary>
    public void SetToMax()
    {
        ApplyWeight(maxWeight);
    }

    float EvaluateWeight(float timeSec)
    {
        float lo = Mathf.Clamp(minWeight, 0f, 100f);
        float hi = Mathf.Clamp(maxWeight, 0f, 100f);
        if (hi < lo)
        {
            float tmp = lo;
            lo = hi;
            hi = tmp;
        }

        if (Mathf.Approximately(hi, lo) || cyclePerSecond <= 0f)
            return lo;

        float phase = (timeSec * cyclePerSecond) % 1f;
        if (phase < 0f) phase += 1f;

        float t = 0.5f + 0.5f * Mathf.Sin(phase * Mathf.PI * 2f);

        return Mathf.Lerp(lo, hi, t);
    }

    bool IsValid()
    {
        return ringRenderer != null &&
               ringRenderer.sharedMesh != null &&
               blendShapeIndex >= 0 &&
               blendShapeIndex < ringRenderer.sharedMesh.blendShapeCount;
    }

    void ApplyWeight(float value)
    {
        if (!IsValid()) return;
        ringRenderer.SetBlendShapeWeight(blendShapeIndex, Mathf.Clamp(value, 0f, 100f));
    }

    [ContextMenu("테스트: BlendShape 목록 출력")]
    void Debug_PrintBlendShapes()
    {
        if (ringRenderer == null || ringRenderer.sharedMesh == null)
        {
            Debug.LogWarning($"[RingBlendShapePulse] {name}: ringRenderer/sharedMesh가 비어 있습니다.", this);
            return;
        }

        int count = ringRenderer.sharedMesh.blendShapeCount;
        for (int i = 0; i < count; i++)
            Debug.Log($"[RingBlendShapePulse] {name} BlendShape[{i}] = {ringRenderer.sharedMesh.GetBlendShapeName(i)}", this);
    }
}
