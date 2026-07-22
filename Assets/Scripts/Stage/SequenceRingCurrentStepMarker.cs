using UnityEngine;

/// <summary>
/// SequenceRing 미니게임에서 "지금 눌러야 할 칸"을 가리키는 월드 마커.
///
/// [비주얼 패턴 — PressurePadCountUI와 동일]
/// 텍스트 대신 쿼드/스프라이트를 쓴다는 점만 다르고, 위치는 월드에 고정,
/// 회전은 Y축만 각자 로컬 카메라 쪽으로 맞추는 빌보드 방식은 동일하다.
/// 즉 플레이어를 따라다니는 UI가 아니라 "현재 스텝 타일" 위에 고정된 표시이며,
/// 각 클라이언트 화면에서 자기 카메라를 향해 정면으로 보이도록 회전만 로컬로 계산한다.
///
/// [동작]
/// - SequenceRingMinigame.OnCurrentTileRingChanged 구독 → 해당 링 칸 위치로 이동
/// - OnMinigameSuccess/OnMinigameFailed → 숨김
/// - State != Playing (Idle 등) 이면 기본적으로 숨김
///
/// [씬 설정]
/// 1. 링 루트(SequenceRingMinigame과 같은 계층 또는 그 자식)에 빈 GameObject 생성 → 이 스크립트 부착
/// 2. 화살표 등 비주얼(쿼드/스프라이트)을 이 오브젝트의 자식으로 배치
/// 3. minigame : 같은 Phase의 SequenceRingMinigame 연결 (비우면 부모 계층에서 자동 탐색)
/// 4. visualRoot : 켬/끔 대상. 비우면 이 오브젝트 자체를 SetActive
/// 5. offset : 타일 중심 기준 마커 위치 오프셋 (예: 0, 0.5, 0)
/// </summary>
public class SequenceRingCurrentStepMarker : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("비우면 부모 계층에서 자동 탐색 (GetComponentInParent)")]
    [SerializeField] SequenceRingMinigame minigame;

    [Tooltip("켬/끔 대상 비주얼(쿼드/스프라이트 루트). 비우면 이 오브젝트 자체를 SetActive")]
    [SerializeField] GameObject visualRoot;

    [Header("표시")]
    [Tooltip("타일 중심 기준 마커 위치 오프셋")]
    [SerializeField] Vector3 offset = new Vector3(0f, 0.5f, 0f);

    Transform _camTransform;

    void Awake()
    {
        if (minigame == null)
            minigame = GetComponentInParent<SequenceRingMinigame>();
        if (visualRoot == null)
            visualRoot = gameObject;
    }

    void OnEnable()
    {
        if (minigame == null)
        {
            SetVisible(false);
            return;
        }

        minigame.OnCurrentTileRingChanged += HandleRingChanged;
        minigame.OnMinigameSuccess.AddListener(HandleEnded);
        minigame.OnMinigameFailed.AddListener(HandleEnded);

        if (minigame.State == SequenceRingMinigame.MinigameState.Playing)
            HandleRingChanged(minigame.CurrentStepIndex % SequenceRingMinigame.RingTileCount);
        else
            SetVisible(false);
    }

    void OnDisable()
    {
        if (minigame == null) return;

        minigame.OnCurrentTileRingChanged -= HandleRingChanged;
        minigame.OnMinigameSuccess.RemoveListener(HandleEnded);
        minigame.OnMinigameFailed.RemoveListener(HandleEnded);
    }

    void LateUpdate()
    {
        // PressurePadCountUI.LateUpdate와 동일: Y축만 카메라 쪽으로 회전 — X·Z 고정으로
        // 마커가 항상 수직 유지되며, 각자 로컬 카메라 기준이라 플레이어마다 보는 각도가 달라도
        // 자기 화면에서는 항상 정면으로 보인다.
        if (_camTransform == null) _camTransform = Camera.main?.transform;
        if (_camTransform == null) return;

        float yaw = _camTransform.eulerAngles.y;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    void HandleRingChanged(int ringIndex)
    {
        if (minigame == null) { SetVisible(false); return; }

        bool playing = minigame.State == SequenceRingMinigame.MinigameState.Playing;
        SetVisible(playing);
        if (!playing) return;

        Transform tile = minigame.GetTileTransform(ringIndex);
        if (tile == null) return;

        transform.position = tile.position + offset;
    }

    void HandleEnded() => SetVisible(false);

    void SetVisible(bool visible)
    {
        if (visualRoot != null)
            visualRoot.SetActive(visible);
    }
}
