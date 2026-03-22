using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어의 상자 밀기/당기기 컴포넌트.
/// Player GameObject에 추가.
///
/// 좌클릭: 바라보는 방향 기준 가장 가까운 Movable 상자 잡기 / 한 번 더 클릭: 놓기
/// 잡은 상태에서 이동: 플레이어 Strength / 상자 Weight 비율로 상자가 따라옴
///
/// 우선순위(interactionPriority)가 Player의 itemUsePriority보다 낮으면(= 숫자가 작으면)
/// 박스 잡기가 아이템 사용보다 먼저 처리됨.
/// </summary>
[RequireComponent(typeof(Player))]
public class BoxInteraction : MonoBehaviour
{
    [Header("감지 범위")]
    [Tooltip("상자를 잡을 수 있는 최대 거리(m)")]
    public float grabDistance = 0f;

    [Header("방향 감지")]
    [Tooltip(
        "박스를 '바라본다'고 판정할 최소 내적(Dot) 값.\n" +
        " 1 = 정면만 / 0 = 90도까지 / -1 = 전방위(방향 무관)\n" +
        "권장: 0.5(약 60도 이내)")]
    public float facingThreshold = 0f;

    [Header("속도 감소")]
    [Tooltip("상자 조작 중 이동속도 배율. 예: 0.5 = 절반 속도 / 0 = 정지")]
    public float grabSpeedMultiplier = 0f;

    [Header("좌클릭 우선순위")]
    [Tooltip(
        "낮을수록 먼저 처리 (기본: 0).\n" +
        "Player의 itemUsePriority(기본 10)보다 낮으면 박스 잡기가 아이템보다 우선.\n" +
        "새 상호작용 추가 시 이 값과 비교하는 우선순위 필드를 각 컴포넌트에 추가.")]
    public int interactionPriority = 0;

    [Header("Runtime (확인용)")]
    public bool isGrabbing;
    public PushableBox grabbedBox;

    /// <summary>
    /// 이번 프레임에 박스 상호작용이 좌클릭을 소비했으면 true.
    /// Player.UseItem()에서 이 값을 보고 아이템 사용을 건너뜀.
    /// </summary>
    public bool BlockingMouseInput => blockingInput;

    Player     player;
    Rigidbody  playerRb;
    Vector3    grabOffset;
    bool       blockingInput;

    Collider[] _playerCols;   // 충돌 무시용 플레이어 콜라이더 캐시
    Collider[] _grabbedCols;  // 충돌 무시용 잡힌 박스 콜라이더 캐시

    Rigidbody  _grabbedRb;        // 잡힌 박스 Rigidbody 캐시 (null 체크용)

    void Awake()
    {
        player      = GetComponent<Player>();
        playerRb    = GetComponent<Rigidbody>();
        _playerCols = GetComponentsInChildren<Collider>(true);
    }

    void Update()
    {
        // 잡고 있는 동안에는 좌클릭을 소비 (아이템 사용 차단)
        blockingInput = isGrabbing;

        if (player == null || player.IsDead)
        {
            ReleaseBox();
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (isGrabbing)
            {
                // 한 번 더 클릭: 놓기 (blockingInput은 이미 true)
                ReleaseBox();
            }
            else
            {
                // 바라보는 방향에 상자가 있으면 잡기, 성공 시 좌클릭 소비
                if (TryGrab()) blockingInput = true;
            }
        }
    }

    void FixedUpdate()
    {
        if (!isGrabbing || grabbedBox == null || _grabbedRb == null || player == null) return;

        Vector3 targetPos = transform.position + grabOffset;
        Vector3 toTarget  = targetPos - grabbedBox.transform.position;
        toTarget.y = 0f;   // Y는 중력에 맡김

        float strengthFactor = grabbedBox.weight > 0f
            ? Mathf.Clamp01(player.strength / grabbedBox.weight)
            : 1f;

        Vector3 playerVel = playerRb != null ? playerRb.linearVelocity : Vector3.zero;
        float   playerSpd = new Vector3(playerVel.x, 0f, playerVel.z).magnitude;
        float   boxSpeed  = playerSpd * strengthFactor;

        float dist = toTarget.magnitude;
        Vector3 desiredVel = dist > 0.001f
            ? toTarget.normalized * Mathf.Min(dist / Time.fixedDeltaTime, boxSpeed)
            : Vector3.zero;

        // PushableBox에 속도 제출 → FixedUpdate에서 모든 잡는 자의 평균을 적용
        grabbedBox.SubmitVelocity(this, desiredVel);
    }

    // ── 내부 헬퍼 ────────────────────────────────────────────

    /// <summary>
    /// 바라보는 방향 기준 가장 가까운 Movable 상자를 잡음.
    /// 성공하면 true 반환.
    /// </summary>
    bool TryGrab()
    {
        Collider[]  hits        = Physics.OverlapSphere(transform.position, grabDistance);
        PushableBox nearest     = null;
        float       nearestDist = float.MaxValue;

        // 플레이어가 바라보는 수평 방향
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        else forward.Normalize();

        for (int i = 0; i < hits.Length; i++)
        {
            var box = hits[i].GetComponent<PushableBox>();
            if (box == null || box.boxType != PushableBox.BoxType.Movable) continue;

            // 색상 소유권 체크: Common이 아닌 경우 플레이어가 고유색 모드(isUniqueColor=true)이고
            // 고유색이 박스 ownerColor와 일치해야 잡을 수 있음 (흑/백 모드에서는 불가)
            if (box.ownerColor != PushableBox.BoxOwnerColor.Common &&
                (!player.isUniqueColor || (int)box.ownerColor != (int)player.playerColorType)) continue;

            // 상자 방향 내적 체크: facingThreshold 미만이면 등 뒤 → 스킵
            Vector3 dirToBox = hits[i].transform.position - transform.position;
            dirToBox.y = 0f;
            if (dirToBox.sqrMagnitude < 0.0001f) continue;
            dirToBox.Normalize();

            if (Vector3.Dot(forward, dirToBox) < facingThreshold) continue;

            float d = Vector3.Distance(transform.position, hits[i].transform.position);
            if (d < nearestDist)
            {
                nearestDist = d;
                nearest     = box;
            }
        }

        if (nearest == null) return false;

        grabbedBox                 = nearest;
        isGrabbing                 = true;
        grabOffset                 = nearest.transform.position - transform.position;
        player.moveSpeedMultiplier = grabSpeedMultiplier;

        // Rigidbody 캐시 (null 체크용 — kinematic 관리는 PushableBox.RegisterGrab에서 처리)
        _grabbedRb = nearest.GetComponent<Rigidbody>();

        // 잡기 등록: PushableBox가 isKinematic 해제 및 협력 인원 추적을 담당
        nearest.RegisterGrab(this);

        // 플레이어↔박스 사이만 충돌 무시 (다른 오브젝트와는 충돌 유지)
        _grabbedCols = nearest.GetComponentsInChildren<Collider>(true);
        SetIgnoreCollision(_grabbedCols, true);
        return true;
    }

    void ReleaseBox()
    {
        // 충돌 무시 해제
        if (_grabbedCols != null)
        {
            SetIgnoreCollision(_grabbedCols, false);
            _grabbedCols = null;
        }

        // 잡기 해제: PushableBox가 velocity 정리 및 kinematic 복원을 담당
        if (grabbedBox != null)
            grabbedBox.UnregisterGrab(this);

        _grabbedRb = null;
        grabbedBox = null;
        isGrabbing = false;
        if (player != null) player.moveSpeedMultiplier = 1f;
    }

    void SetIgnoreCollision(Collider[] boxCols, bool ignore)
    {
        if (_playerCols == null || boxCols == null) return;
        for (int p = 0; p < _playerCols.Length; p++)
            for (int b = 0; b < boxCols.Length; b++)
                if (_playerCols[p] != null && boxCols[b] != null)
                    Physics.IgnoreCollision(_playerCols[p], boxCols[b], ignore);
    }
}
