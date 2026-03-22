using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상자 컴포넌트.
/// BoxType.Fixed  : 고정 상자 - 이동 불가
/// BoxType.Movable: 이동 가능 상자 - BoxInteraction이 속도를 제출, 이 컴포넌트가 평균 적용
///
/// [협력 이동]
///  requiredGrabbers = 1 : 단독 이동 (기본 동작)
///  requiredGrabbers = 4 : 4명이 동시에 잡아야만 이동
///  필요 인원 미달 시 박스가 움직이지 않음.
///  방향이 반대인 플레이어가 있으면 속도가 평균되어 상쇄됨.
/// </summary>
public class PushableBox : MonoBehaviour
{
    public enum BoxType { Fixed, Movable }

    [Header("박스 타입")]
    [Tooltip("Fixed: 고정(이동 불가) / Movable: 이동 가능")]
    public BoxType boxType = BoxType.Movable;

    public enum BoxOwnerColor { Common, Blue, Red, Green, Yellow }

    [Header("색상 소유권")]
    [Tooltip("Common: 모든 플레이어 이동 가능 / 나머지: 해당 색 플레이어만 이동 가능")]
    public BoxOwnerColor ownerColor = BoxOwnerColor.Common;

    [Header("Movable 전용 설정")]
    [Tooltip("무게. 높을수록 플레이어 Strength가 많이 필요하고 이동 속도가 느려짐")]
    public float weight = 0f;

    [Header("협력 이동")]
    [Tooltip("몇 명이 동시에 잡아야 박스가 움직이는지.\n1 = 단독 이동 (기본) / 2~4 = 협력 필요")]
    public int requiredGrabbers = 1;

    [Header("Runtime (확인용)")]
    [SerializeField] int _grabberCount;

    /// <summary>현재 잡고 있는 인원이 requiredGrabbers 이상이면 true</summary>
    public bool CanMove => _grabbers.Count >= requiredGrabbers;

    Rigidbody rb;
    bool      _wasKinematic;

    // 현재 잡고 있는 BoxInteraction 목록
    readonly List<BoxInteraction>               _grabbers         = new List<BoxInteraction>();
    // 이번 FixedUpdate에서 각 잡는 자가 제출한 목표 속도 (XZ)
    readonly Dictionary<BoxInteraction, Vector3> _desiredVelocities = new Dictionary<BoxInteraction, Vector3>();

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // ── 잡기 등록/해제 (BoxInteraction에서 호출) ─────────────────

    /// <summary>플레이어가 이 박스를 잡기 시작할 때 호출</summary>
    public void RegisterGrab(BoxInteraction bi)
    {
        if (_grabbers.Contains(bi)) return;

        if (_grabbers.Count == 0 && rb != null)
        {
            _wasKinematic  = rb.isKinematic;
            rb.isKinematic = false;
        }

        _grabbers.Add(bi);
        _grabberCount = _grabbers.Count;
    }

    /// <summary>플레이어가 이 박스를 놓을 때 호출</summary>
    public void UnregisterGrab(BoxInteraction bi)
    {
        _grabbers.Remove(bi);
        _desiredVelocities.Remove(bi);
        _grabberCount = _grabbers.Count;

        if (_grabbers.Count == 0 && rb != null)
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic     = _wasKinematic;
        }
    }

    /// <summary>BoxInteraction.FixedUpdate에서 이번 프레임의 목표 속도를 제출</summary>
    public void SubmitVelocity(BoxInteraction bi, Vector3 vel)
    {
        _desiredVelocities[bi] = vel;
    }

    // ── 속도 평균 적용 ────────────────────────────────────────────

    void FixedUpdate()
    {
        if (rb == null || _desiredVelocities.Count == 0)
        {
            _desiredVelocities.Clear();
            return;
        }

        if (!CanMove)
        {
            // 필요 인원 미달: 박스 고정
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            _desiredVelocities.Clear();
            return;
        }

        // 제출된 속도 평균 (XZ) + 중력 Y 유지
        Vector3 sum = Vector3.zero;
        foreach (Vector3 v in _desiredVelocities.Values)
            sum += v;

        Vector3 avg = sum / _desiredVelocities.Count;
        avg.y = rb.linearVelocity.y;

        rb.linearVelocity = avg;
        _desiredVelocities.Clear();
    }

    // ── 외부 API ─────────────────────────────────────────────────

    /// <summary>
    /// 목표 위치 방향으로 speed(m/s)로 이동.
    /// MovePosition 사용: 벽·장애물 충돌 감지 유지.
    /// </summary>
    public void MoveTo(Vector3 targetPos, float speed)
    {
        if (boxType != BoxType.Movable || rb == null) return;
        if (speed <= 0f) return;

        Vector3 newPos = Vector3.MoveTowards(rb.position, targetPos, speed * Time.fixedDeltaTime);
        rb.MovePosition(newPos);
    }
}
