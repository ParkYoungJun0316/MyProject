using UnityEngine;

/// <summary>
/// 진행축(예: MovingCorridor.moveDirection)만 추적하는 탑다운 프리뷰용 pivot.
///
/// [동작]
///  Awake 시점의 자기 위치(배치된 씬 좌표)를 anchor로 고정한다.
///  매 프레임, anchor 기준 플레이어 위치를 진행축에 투영해 그 성분만 따라간다 —
///  진행축과 수직인 성분(좌우 흔들림)은 버려지므로 카메라가 좌우로 흐르지 않는다.
///
///  예: axis = (0,0,1)인 복도라면 사실상 "pivot.z = player.z, x/y는 anchor 고정"과 동일.
///
/// [씬 설정]
///  1. 빈 GameObject를 복도 중심선 위 원하는 위치에 배치 (좌우 기준점)
///  2. 이 컴포넌트 추가, corridor(MovingCorridor)에 축 소스 연결
///  3. CorridorTopDownIntroController.pivot에 이 Transform 연결
///  4. 따라갈 대상(로컬 플레이어)은 컨트롤러가 BeginTopDown() 시점에 코드로 채운다
///  ⚠ 벽(Ring.B 등) 자식으로 두지 말 것 — 별도 오브젝트로 배치
/// </summary>
public class CorridorTopDownPivot : MonoBehaviour
{
    [Tooltip("진행축(moveDirection)을 읽어올 MovingCorridor")]
    [SerializeField] MovingCorridor corridor;

    Transform _followTarget;
    Vector3 _anchor;

    /// <summary>진행축(정규화). corridor 미연결/zero면 Vector3.zero.</summary>
    public Vector3 Axis => corridor != null ? corridor.moveDirection.normalized : Vector3.zero;

    public void SetFollowTarget(Transform followTarget) => _followTarget = followTarget;

    void Awake()
    {
        _anchor = transform.position;
    }

    void Update()
    {
        if (_followTarget == null) return;

        Vector3 axis = Axis;
        if (axis == Vector3.zero) return;

        float along = Vector3.Dot(_followTarget.position - _anchor, axis);
        transform.position = _anchor + axis * along;
    }

    void OnDrawGizmos()
    {
        Vector3 anchor = Application.isPlaying ? _anchor : transform.position;
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
        Gizmos.DrawSphere(anchor, 0.5f);

        Vector3 axis = Axis;
        if (axis != Vector3.zero)
            Gizmos.DrawLine(anchor - axis * 10f, anchor + axis * 10f);
    }
}
