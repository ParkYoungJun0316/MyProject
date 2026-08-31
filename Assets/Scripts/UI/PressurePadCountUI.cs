using TMPro;
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
///  3. offset : 발판 중심 기준 텍스트 위치 오프셋 (예: 0, 1.5, 0)
///  4. fontSize : World Space 텍스트 크기 (예: 3)
/// </summary>
[RequireComponent(typeof(PressurePad))]
public class PressurePadCountUI : MonoBehaviour
{
    [Tooltip("이 발판을 requiredPads에 포함하는 DoorController. latch 상태 확인에 사용.")]
    [SerializeField] DoorController door;

    [Tooltip("발판 중심 기준 텍스트 표시 위치 오프셋 (예: 0, 1.5, 0)")]
    [SerializeField] Vector3 offset = Vector3.zero;

    [Tooltip("World Space 텍스트 폰트 크기 (예: 3)")]
    [SerializeField] float fontSize = 0f;

    PressurePad _pad;
    TextMeshPro _text;
    Transform   _camTransform;

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

    void LateUpdate()
    {
        if (_text == null) return;
        // Y 축만 카메라를 따라 수평 회전 — X·Z 고정으로 텍스트 항상 수직 유지
        if (_camTransform == null) _camTransform = Camera.main?.transform;
        if (_camTransform != null)
        {
            float yaw = _camTransform.eulerAngles.y;
            _text.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }

    // ── 외부 API ────────────────────────────────────────────────

    /// <summary>
    /// StagePressurePadSetup.ApplySeedAndColors() 완료 후 호출.
    /// 스케일링된 최종 requiredCount를 기준으로 표시를 초기화한다.
    /// </summary>
    public void Refresh()
    {
        if (_text == null)
            BuildText();

        _text.gameObject.SetActive(true);
        RefreshText();
    }

    // ── 내부 ────────────────────────────────────────────────────

    void OnCountChanged(int current, int required)
    {
        if (_text == null)
            BuildText();

        _text.gameObject.SetActive(true);

        if (door != null && door.latchOnOpen && door.IsOpen)
            SetText(required, required);
        else
            SetText(current, required);
    }

    void RefreshText()
    {
        if (door != null && door.latchOnOpen && door.IsOpen)
            SetText(_pad.requiredCount, _pad.requiredCount);
        else
            SetText(_pad.CurrentCount, _pad.requiredCount);
    }

    void SetText(int current, int required)
    {
        if (_text == null) return;
        _text.text = $"{current}/{required}";
    }

    void BuildText()
    {
        var go = new GameObject("PadCountText");
        go.transform.SetParent(transform);
        go.transform.localPosition = offset;
        go.transform.localRotation = Quaternion.identity;

        _text           = go.AddComponent<TextMeshPro>();
        _text.alignment = TextAlignmentOptions.Center;
        _text.color     = Color.white;
        _text.fontStyle = FontStyles.Bold;

        if (fontSize > 0f)
            _text.fontSize = fontSize;

        _text.outlineWidth = 0.2f;
        _text.outlineColor = new Color32(0, 0, 0, 255);
    }
}
