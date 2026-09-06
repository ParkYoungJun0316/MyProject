using UnityEngine;

/// <summary>
/// 문 퍼즐 스케일링. DoorController와 같은 GameObject에 부착한다.
///
/// [토폴로지]
///  Hold — 발판에서 떠나면 닫히는 문. 1인 시 latch=true 승격. requiredCount는 "전원-1" 스케일.
///  Solo — 단독 패드. (latch 변경 없이 requiredCount만 "전원-1" 스케일)
///  All  — 전원 필요. requiredCount = 현재 활성 인원 그대로(스케일 없음, designPlayerCount 무시).
///         latch는 관여하지 않음 — 계속 열려 있게 하려면 DoorController.latchOnOpen을 직접 켤 것.
///
/// [requiredCount 스케일 — Hold/Solo만]
///  designPlayerCount > 1 이면 (Hold/Solo 한정) 항상 적용됨.
///  공식: min(designPlayerCount, activeCount - 1) → "전원 - 1" 기준 스케일
///  예) designPlayerCount=3: 4인→3, 3인→2, 2인→1
///  All은 이 스케일을 타지 않고 늘 activeCount 그대로 요구한다 — designPlayerCount 필드는 무시됨.
///
/// [Inspector 설정]
///  topology          : 이 문의 퍼즐 방식 (Hold / Solo / All)
///  designPlayerCount : 4인 기준 원래 requiredCount. 1이면 스케일 생략. All에는 영향 없음.
///
/// [주의 — 과거 깨진 직렬화 값]
///  옛 버전엔 topology 3번째 자리(ordinal 2)에 다른 뜻(Trigger/Simultaneous류)이 있었고,
///  일부 씬·프리팹(Door.B/Y/G/P, T.Stage1 Door.C/Door.C.1 등)에 topology: 2로 그 흔적이
///  남아 있다 — 지금 enum엔 대응 값이 없어 인스펙터에 빈 값으로 보인다(Hold도 Solo도 아님).
///  All을 그 자리(2)에 넣으면 이 깨진 데이터가 전부 조용히 "전원"으로 되살아나 버리므로,
///  일부러 3번으로 배정해 피해간다. 저 프리팹/씬은 여전히 사용자가 Solo(또는 원하면 All)로
///  직접 다시 선택해야 한다 — 자동으로 고쳐지지 않음.
///
/// [호출]
///  StagePressurePadSetup.Start() → ApplyScaling(activeCount)
/// </summary>
[RequireComponent(typeof(DoorController))]
public class DoorPuzzleGroup : MonoBehaviour
{
    public enum PuzzleTopology
    {
        Hold = 0, // 발판 유지형 — 1인 시 latch=true 승격
        Solo = 1, // 단독 패드   — requiredCount 인원 스케일
        // 2는 과거 깨진 직렬화 값(Trigger/Simultaneous류)과 겹치는 자리라 비워둔다.
        All  = 3, // 전원 필요 — requiredCount = 현재 활성 인원 그대로(스케일 없음)
    }

    [Header("퍼즐 토폴로지")]
    [Tooltip("Hold: 발판 유지형 / Solo: requiredCount 스케일 / All: 전원 필요(스케일 없음)")]
    [SerializeField] PuzzleTopology topology = PuzzleTopology.Hold;

    [Tooltip("4인 기준 원래 requiredCount. 1이면 스케일 생략.\n" +
             "Hold/Solo에서만 적용됨 (All은 항상 activeCount 그대로 요구 — 이 값 무시).\n" +
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
        // All — 전원 필요. 스케일 없이 항상 activeCount 그대로 요구하고 끝낸다.
        // Hold/Solo의 "전원-1" 스케일이나 designPlayerCount와는 완전히 별개 경로.
        if (topology == PuzzleTopology.All)
        {
            ApplyAll(activeCount);
            return;
        }

        // 토폴로지별 처리 (latch 승격 등) — Hold만
        if (topology == PuzzleTopology.Hold)
            ApplyHold(activeCount);

        // requiredCount 스케일 — Hold/Solo 한정, designPlayerCount > 1이면 항상 적용
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

    /// <summary>
    /// requiredPads 전체의 requiredCount = activeCount(현재 활성 인원) 그대로.
    /// designPlayerCount는 쓰지 않는다 — "전원"은 스케일할 대상이 아니라 늘 전원이기 때문.
    /// latch는 건드리지 않는다 — 계속 열려 있길 원하면 DoorController.latchOnOpen을
    /// Inspector에서 직접 켤 것 (전원이 함께 서 있을 때만 열리게 하려면 꺼진 채로 둔다).
    /// </summary>
    void ApplyAll(int activeCount)
    {
        if (_door.requiredPads == null) return;

        int required = Mathf.Max(1, activeCount);
        foreach (PressurePad pad in _door.requiredPads)
            if (pad != null) pad.requiredCount = required;
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
