using UnityEngine;

/// <summary>
/// Phase 전환 또는 외부 호출 시 지정한 렌더러의 머티리얼 슬롯을 교체하는 범용 컴포넌트.
/// MeshRenderer / SkinnedMeshRenderer 모두 지원.
/// 멀티 머티리얼 슬롯, 멀티 렌더러 동시 교체 가능.
///
/// [사용법]
/// 1. 배경 오브젝트(또는 빈 오브젝트)에 부착
/// 2. Sets 배열에 Phase 수만큼 MaterialSet 등록
/// 3. PhaseManager > onPhaseEnter → MaterialSwapper.Apply(세트인덱스) 연결
///    예) Phase 0 진입 → Apply(0), Phase 1 진입 → Apply(1)
///
/// [범용 사용 예시]
/// - 입 씬:   Phase0=기본 핑크, Phase1=보스 검붉음
/// - 식도 씬: Phase0=기본 색, Phase1=점막 자극 색
/// - 위장 씬: Phase0=기본, Phase1=소화액 강화 색
/// </summary>
public class MaterialSwapper : MonoBehaviour
{
    [System.Serializable]
    public class RendererSlotPair
    {
        [Tooltip("머티리얼을 교체할 렌더러 (MeshRenderer / SkinnedMeshRenderer)")]
        public Renderer targetRenderer;

        [Tooltip("교체할 머티리얼 슬롯 인덱스 (0번부터 시작)")]
        public int slotIndex = 0;

        [Tooltip("이 슬롯에 적용할 머티리얼")]
        public Material material;
    }

    [System.Serializable]
    public class MaterialSet
    {
        [Tooltip("Inspector 표시용 이름 (Phase 이름 등)")]
        public string setName = "Set";

        [Tooltip("이 세트 적용 시 교체할 렌더러/슬롯/머티리얼 목록")]
        public RendererSlotPair[] changes;
    }

    [Header("머티리얼 세트 목록 (인덱스 = Apply 호출 시 사용하는 번호)")]
    [SerializeField] private MaterialSet[] sets;

    [Tooltip("Start 시 자동으로 적용할 세트 인덱스. −1이면 자동 적용 안 함.")]
    [SerializeField] private int applyOnStart = -1;

    void Start()
    {
        if (applyOnStart >= 0)
            Apply(applyOnStart);
    }

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>
    /// 지정한 인덱스의 MaterialSet을 적용.
    /// PhaseManager의 onPhaseEnter UnityEvent에서 직접 연결 가능.
    /// </summary>
    public void Apply(int setIndex)
    {
        if (sets == null || setIndex < 0 || setIndex >= sets.Length)
        {
            Debug.LogWarning($"[MaterialSwapper] {name}: 유효하지 않은 세트 인덱스 {setIndex}. " +
                             $"등록된 세트 수: {(sets != null ? sets.Length : 0)}", this);
            return;
        }

        MaterialSet set = sets[setIndex];
        if (set.changes == null) return;

        foreach (RendererSlotPair pair in set.changes)
        {
            if (pair.targetRenderer == null)
            {
                Debug.LogWarning($"[MaterialSwapper] {name}: 세트 '{set.setName}'에 null 렌더러가 있습니다.", this);
                continue;
            }

            if (pair.material == null)
            {
                Debug.LogWarning($"[MaterialSwapper] {name}: 세트 '{set.setName}'에 null 머티리얼이 있습니다.", this);
                continue;
            }

            Material[] mats = pair.targetRenderer.materials;

            if (pair.slotIndex < 0 || pair.slotIndex >= mats.Length)
            {
                Debug.LogWarning($"[MaterialSwapper] {name}: '{pair.targetRenderer.name}'의 슬롯 {pair.slotIndex}가 " +
                                 $"범위를 벗어났습니다. (슬롯 수: {mats.Length})", this);
                continue;
            }

            mats[pair.slotIndex] = pair.material;
            pair.targetRenderer.materials = mats;
        }
    }

    // ── 에디터 지원 ──────────────────────────────────────────────

    [ContextMenu("테스트: Set 0 적용")]
    void TestApply0() => Apply(0);

    [ContextMenu("테스트: Set 1 적용")]
    void TestApply1() => Apply(1);

    [ContextMenu("테스트: Set 2 적용")]
    void TestApply2() => Apply(2);
}
