using UnityEngine;

/// <summary>
/// 자식의 (트리거가 아닌) 콜라이더 전부에 **마찰 0** 재질을 씌운다. 벽·문처럼 "옆면에 비비는" 면 전용.
///
/// [왜 필요한가 — TStage5RunnerRedesign.md §1.4]
///  Player.Move()는 매 FixedUpdate마다 velocity.x/z를 입력값으로 덮어써서, 벽에 대고 방향키를 누르면
///  캡슐이 벽을 계속 누르는 상태가 된다. 기본 마찰(0.6)이 걸리면 그 수직항력만큼 **y축 운동에 마찰**이 생긴다.
///   - 올라가는 문(SlideUp, 0.35초에 9m)에 비비면 문과 같이 딸려 올라가 문 윗면에 올라타거나 튕겨 나간다.
///   - 발사대(VerticalUp)로 올라가며 발사통 벽에 비비면 상승 속도가 깎여 2층에 못 닿는다.
///  마찰이 0이면 두 경우 모두 y축은 중력·발사 속도만으로 움직인다.
///
/// [왜 Minimum인가]
///  플레이어 캡슐은 재질이 없어 기본값(Average)이다. 두 콜라이더의 결합 모드가 다르면 우선순위가 높은 쪽
///  (Maximum > Multiply > Minimum > Average)을 쓰므로 Minimum이 이겨 min(0.6, 0) = 0이 된다 —
///  플레이어 프리팹을 건드리지 않고 이 면에서만 마찰이 사라진다.
///
/// [주의]
///  바닥에는 붙이지 말 것 — 넉백(Move()가 x/z를 덮어쓰지 않는 0.25초) 동안 미끄러지는 거리가 늘어난다.
///  PhysicsMaterial로 "얼음 바닥"을 만들 수 없는 것(CoopStageAudit.M.md)과는 별개다 — 그건 x/z 문제고
///  Move()가 매 프레임 덮어써서 안 먹는 것이다. 여기서 없애는 건 Move()가 건드리지 않는 y축 마찰이다.
///  (2026-09-29) 공중에서 비비는 경우는 전 스테이지에서 Player.UpdateAirFriction이 몸통 쪽으로 막는다 —
///  이 컴포넌트는 서 있는 채로 올라가는 문에 비비는 T5 경우까지 막는 용도로 남는다.
/// </summary>
[DisallowMultipleComponent]
public class FrictionlessColliders : MonoBehaviour
{
    static PhysicsMaterial _shared;

    void Awake()
    {
        if (_shared == null)
        {
            _shared = new PhysicsMaterial("Frictionless (runtime)")
            {
                dynamicFriction = 0f,
                staticFriction  = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounciness      = 0f,
                bounceCombine   = PhysicsMaterialCombine.Minimum,
            };
        }

        foreach (Collider c in GetComponentsInChildren<Collider>(true))
        {
            if (c.isTrigger) continue;
            c.sharedMaterial = _shared;
        }
    }
}
