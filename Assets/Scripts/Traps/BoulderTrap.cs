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

        GameObject boulder = Instantiate(boulderPrefab, spawn.position, spawn.rotation);

        // TrapProjectile: 벽 충돌 파괴 담당 (speed/damage/lifetime은 0 권장)
        TrapProjectile proj = boulder.GetComponent<TrapProjectile>();
        if (proj != null)
            proj.moveDirection = spawn.forward;

        // SpinRoller: 이동/회전/데미지/수명 담당. moveDir을 발사 방향으로 덮어씀
        SpinRoller roller = boulder.GetComponent<SpinRoller>();
        if (roller != null)
            roller.moveDir = spawn.forward;
    }
}
