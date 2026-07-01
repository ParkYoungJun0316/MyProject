using UnityEngine;

/// <summary>
/// 문 퍼즐 스케일링. DoorController와 같은 GameObject에 부착한다.
///
/// [토폴로지]
///  Hold — 발판에서 떠나면 닫히는 문. 1인 시 latch=true 승격.
///  Solo — 단독 패드. (latch 변경 없이 requiredCount만 스케일)
///
/// [requiredCount 스케일]
///  designPlayerCount > 1 이면 topology에 무관하게 항상 적용됨.
///  공식: min(designPlayerCount, activeCount - 1) → "전원 - 1" 기준 스케일
///  예) designPlayerCount=3: 4인→3, 3인→2, 2인→1
///
/// [Inspector 설정]
///  topology          : 이 문의 퍼즐 방식
///  designPlayerCount : 4인 기준 원래 requiredCount. 1이면 스케일 생략.
///
/// [호출]
///  StagePressurePadSetup.Start() → ApplyScaling(activeCount)
/// </summary>
[RequireComponent(typeof(DoorController))]
public class DoorPuzzleGroup : MonoBehaviour
{
    public enum PuzzleTopology
    {
        Hold, // 발판 유지형 — 1인 시 latch=true 승격
        Solo, // 단독 패드   — requiredCount 인원 스케일
    }

    [Header("퍼즐 토폴로지")]
    [Tooltip("Hold: 발판 유지형 / Solo: requiredCount 스케일")]
    [SerializeField] PuzzleTopology topology = PuzzleTopology.Hold;

    [Tooltip("4인 기준 원래 requiredCount. 1이면 스케일 생략.\n" +
             "topology에 무관하게 항상 적용됨.\n" +
             "예) Hold + 3 → latch 승격 + requiredCount=min(3, activeCount-1)\n" +
             "예) Solo + 3 → requiredCount=min(3, activeCount-1)\n" +
             "결과: 4인→3, 3인→2, 2인→1 (전원-1 스케일)")]
    [SerializeField] int designPlayerCount = 1;

    DoorController _door;

    void Awake()
    {
        _door = GetComponent<DoorController>();
    }

    // ── 외부 API ────────────────────────────────────────────────

    /// <summary>활성 인원에 맞춰 스케일링을 적용한다.</summary>
    public void ApplyScaling(int activeCount)
    {
        // 토폴로지별 처리 (latch 승격, 패드 축소 등)
        if (topology == PuzzleTopology.Hold)
            ApplyHold(activeCount);

        // requiredCount 스케일 — topology 무관하게 designPlayerCount > 1이면 항상 적용
        if (designPlayerCount > 1)
            ScaleRequiredCount(activeCount);
    }

    // ── 토폴로지 처리 ────────────────────────────────────────────

    /// <summary>1인 시 latch=true로 승격. 2인 이상은 원래 Hold 동작 유지.</summary>
    void ApplyHold(int activeCount)
    {
        if (activeCount < 2)
            _door.SetLatchOnOpen(true);
    }

    /// <summary>
    /// requiredPads 전체의 requiredCount = min(designPlayerCount, activeCount - 1).
    /// "전원 - 1" 기준으로 스케일해 2인에서도 1명이 열 수 있게 보장.
    /// </summary>
    void ScaleRequiredCount(int activeCount)
    {
        if (_door.requiredPads == null) return;

        // activeCount - 1 을 상한으로 사용: 4인→3, 3인→2, 2인→1
        int scaled = Mathf.Max(1, Mathf.Min(designPlayerCount, activeCount - 1));
        foreach (PressurePad pad in _door.requiredPads)
            if (pad != null) pad.requiredCount = scaled;
    }

    // ── 에디터 ──────────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 1인 스케일링")]
    void Debug_1P() => ApplyScaling(1);

    [ContextMenu("테스트: 2인 스케일링")]
    void Debug_2P() => ApplyScaling(2);

    [ContextMenu("테스트: 3인 스케일링")]
    void Debug_3P() => ApplyScaling(3);

    [ContextMenu("테스트: 4인 스케일링")]
    void Debug_4P() => ApplyScaling(4);
#endif
}
