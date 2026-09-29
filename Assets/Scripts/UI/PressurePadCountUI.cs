using UnityEngine;

/// <summary>
/// 압력 발판 위 "현재/필요" 인원을 World Space 텍스트로 표시.
/// 컴포넌트가 붙은 발판은 인원·스케일된 requiredCount와 무관하게 항상 표시.
///
/// [latch 동작]
///  - latchOnOpen = false : 실시간 CurrentCount / requiredCount 표시
///  - latchOnOpen = true + 문 열림 : requiredCount / requiredCount 고정 (성공 상태 유지)
///  - latchOnFullyOpen = true (버텨야 하는 문, T.Stage3 Bread_20·Bread_21) :
///      아랫줄에 주황 남은 초를 모이기 전부터 표시 (대기 중엔 duration, 전원 올라가면 카운트다운,
///      도중에 끊기면 다시 duration — 재도전 시 duration 전체가 다시 걸림).
///      끝까지 열려 고정되면 초는 지우고 requiredCount / requiredCount를 초록으로 유지
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

    // 버텨야 하는 발판의 남은 초 — 흰 인원 숫자와 한눈에 구분되는 주황
    static readonly Color HoldTimerColor = new Color32(255, 176, 32, 255);
    // 끝까지 열려 고정된 발판의 인원 숫자
    static readonly Color LatchedColor   = new Color32(90, 220, 110, 255);

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
        Show(current, required);
    }

    void Update()
    {
        // 남은 초는 문 애니메이션 진행에 따라 바뀌므로 인원 이벤트와 별개로 매 프레임 갱신
        if (_label != null && HasHoldTimer)
            RefreshText();
    }

    void RefreshText() => Show(_pad.CurrentCount, _pad.requiredCount);

    // latchOnOpen이 켜져 있으면 그쪽이 먼저 걸려 버틸 시간이 없다 (DoorController 툴팁과 동일)
    bool HasHoldTimer => door != null && door.latchOnFullyOpen && !door.latchOnOpen;

    void Show(int current, int required)
    {
        if (door != null && door.latchOnOpen && door.IsOpen)
        {
            _label.Set(required, required);
            return;
        }

        if (!HasHoldTimer)
        {
            _label.Set(current, required);
            return;
        }

        // 끝까지 내려가 고정 — 다들 내려와도 초록 required/required 유지, 남은 초는 지운다
        if (door.IsLatched)
        {
            _label.Set(required, required, LatchedColor);
            return;
        }

        // 모이기 전부터 버텨야 할 초를 보여준다. 끝나는 프레임에 0이 잠깐 보이지 않게 최소 1
        int seconds = Mathf.Max(1, Mathf.CeilToInt(door.OpenTimeRemaining));
        _label.Set(current, required, Color.white, seconds.ToString(), HoldTimerColor);
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
