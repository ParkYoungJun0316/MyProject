using UnityEngine;

/// <summary>
/// 큰 돌이 굴러오는 함정.
/// activateInterval마다 spawnPoint 위치에서 돌 프리팹을 생성.
/// 돌은 TrapProjectile (type=Boulder) 컴포넌트로 이동/충돌 처리.
/// 
/// [Boulder 프리팹 권장 설정]
/// - TrapProjectile: type=Boulder, destroyOnFloor=false, destroyOnWall=true
/// - Rigidbody: Use Gravity=true, Constraints=Freeze Rotation X/Z (선택)
/// - SphereCollider (또는 MeshCollider)
/// </summary>
public class BoulderTrap : TrapBase
{
    [Header("Boulder Trap")]
    [Tooltip("생성할 돌 프리팹 (TrapProjectile 컴포넌트 필수)")]
    [SerializeField] private GameObject boulderPrefab = null;

    [Tooltip("돌이 생성될 위치/방향 기준 Transform. 없으면 이 GameObject 사용")]
    [SerializeField] private Transform spawnPoint = null;

    protected override void OnTrapTrigger()
    {
        if (boulderPrefab == null) return;

        Transform spawn = spawnPoint != null ? spawnPoint : transform;

        // Boss Rock처럼 수평 방향으로 1자 직진 (Y 성분 제거 → firepoint 높이 기준 직선 이동)
        Vector3 flatForward = spawn.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward; // 위/아래 전용일 때 기본 전방
        flatForward.Normalize();

        Quaternion flatRot = Quaternion.LookRotation(flatForward);

        GameObject boulder = Instantiate(boulderPrefab, spawn.position, flatRot);

        // TrapProjectile: 벽 충돌 파괴 담당 (speed/damage/lifetime은 0 권장)
        TrapProjectile proj = boulder.GetComponent<TrapProjectile>();
        if (proj != null)
            proj.moveDirection = flatForward;

        // SpinRoller: 이동/회전/데미지/수명 담당. moveDir을 발사 방향으로 덮어씀
        SpinRoller roller = boulder.GetComponent<SpinRoller>();
        if (roller != null)
            roller.moveDir = flatForward;
    }
}
