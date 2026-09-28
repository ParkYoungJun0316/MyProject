using UnityEngine;

/// <summary>
/// 압력 발판 위 "현재/필요" 인원을 World Space 텍스트로 표시.
/// 컴포넌트가 붙은 발판은 인원·스케일된 requiredCount와 무관하게 항상 표시.
///
/// [latch 동작]
///  - latchOnOpen = false : 실시간 CurrentCount / requiredCount 표시
///  - latchOnOpen = true + 문 열림 : requiredCount / requiredCount 고정 (성공 상태 유지)
///
/// [동작 흐름]
///  PressurePad.OnCountChanged → OnCountChanged() → SetText()
///  StagePressurePadSetup.ApplySeedAndColors() 완료 → Refresh() → 초기 상태 표시
///
/// [씬 설정]
///  1. 발판 GameObject에 PressurePadCountUI 추가
///  2. door : 이 발판을 requiredPads에 포함하는 DoorController 연결
///  3. offset : 발판 메시 윗면 기준 텍스트 위치 오프셋, 월드 m (예: 0, 1.8, 0)
///  4. fontSize : World Space 텍스트 크기 (예: 3). 발판 Scale은 상쇄되므로 월드 1배 기준
/// </summary>
[RequireComponent(typeof(PressurePad))]
public class PressurePadCountUI : MonoBehaviour
{
    [Tooltip("이 발판을 requiredPads에 포함하는 DoorController. latch 상태 확인에 사용.")]
    [SerializeField] DoorController door;

    [Tooltip("발판 메시 윗면 기준 텍스트 위치 오프셋, 월드 m 단위 — 발판 Scale과 무관 (예: 0, 1.8, 0)")]
    [SerializeField] Vector3 offset = Vector3.zero;

    [Tooltip("World Space 텍스트 폰트 크기 (예: 3)")]
    [SerializeField] float fontSize = 0f;

    PressurePad     _pad;
    WorldCountLabel _label;

    void Awake()
    {
        _pad = GetComponent<PressurePad>();
    }

    void Start()
    {
        _pad.OnCountChanged.AddListener(OnCountChanged);
        // 초기 표시는 StagePressurePadSetup.ApplySeedAndColors() 완료 후 Refresh()에서 처리.
    }

    void OnDestroy()
    {
        if (_pad != null)
            _pad.OnCountChanged.RemoveListener(OnCountChanged);
    }

    // ── 외부 API ────────────────────────────────────────────────

    /// <summary>
    /// StagePressurePadSetup.ApplySeedAndColors() 완료 후 호출.
    /// 스케일링된 최종 requiredCount를 기준으로 표시를 초기화한다.
    /// </summary>
    public void Refresh()
    {
        if (_label == null)
            BuildLabel();

        _label.gameObject.SetActive(true);
        RefreshText();
    }

    // ── 내부 ────────────────────────────────────────────────────

    void OnCountChanged(int current, int required)
    {
        if (_label == null)
            BuildLabel();

        _label.gameObject.SetActive(true);

        if (door != null && door.latchOnOpen && door.IsOpen)
            _label.Set(required, required);
        else
            _label.Set(current, required);
    }

    void RefreshText()
    {
        if (door != null && door.latchOnOpen && door.IsOpen)
            _label.Set(_pad.requiredCount, _pad.requiredCount);
        else
            _label.Set(_pad.CurrentCount, _pad.requiredCount);
    }

    void BuildLabel()
    {
        // 높이 기준은 pivot이 아니라 발판 메시 윗면 — 쿠키는 pivot이 바닥이고 두께가 Scale에
        // 비례해서, pivot 기준이면 배율마다 글자 높이가 달라진다. 부모 Scale 상쇄는 WorldCountLabel 몫.
        Vector3 basePos = transform.position;
        Renderer body = GetComponentInChildren<Renderer>();
        if (body != null) basePos.y = body.bounds.max.y;

        _label = WorldCountLabel.Create(transform, basePos + offset, fontSize);
    }
}
