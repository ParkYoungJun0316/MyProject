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

    Player    player;
    Rigidbody playerRb;
    Vector3   grabOffset;
    bool      blockingInput;

    void Awake()
    {
        player   = GetComponent<Player>();
        playerRb = GetComponent<Rigidbody>();
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
        if (!isGrabbing || grabbedBox == null || player == null) return;

        // 플레이어 기준 상자 목표 위치 유지 (밀기/당기기 자연스럽게 처리)
        Vector3 targetPos = transform.position + grabOffset;

        // 힘(Strength) / 무게(Weight) 비율로 박스 이동 속도 결정
        float strengthFactor = grabbedBox.weight > 0f
            ? Mathf.Clamp01(player.strength / grabbedBox.weight)
            : 1f;

        Vector3 playerVel = playerRb != null ? playerRb.linearVelocity : Vector3.zero;
        float   playerSpd = new Vector3(playerVel.x, 0f, playerVel.z).magnitude;
        float   boxSpeed  = playerSpd * strengthFactor;

        grabbedBox.MoveTo(targetPos, boxSpeed);
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
        return true;
    }

    void ReleaseBox()
    {
        grabbedBox = null;
        isGrabbing = false;
        if (player != null) player.moveSpeedMultiplier = 1f;
    }
}
