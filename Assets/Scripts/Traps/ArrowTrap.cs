using UnityEngine;

/// <summary>
/// 벽에 부착하는 화살 함정.
/// activateInterval마다 firePoint 방향으로 화살 프리팹을 직선 발사.
/// firePoint를 지정하지 않으면 이 GameObject의 Transform을 기준으로 발사.
/// </summary>
public class ArrowTrap : TrapBase
{
    [Header("Arrow Trap")]
    [Tooltip("발사할 화살 프리팹 (TrapProjectile 컴포넌트 필수)")]
    [SerializeField] private GameObject arrowPrefab = null;

    [Tooltip("화살이 생성될 위치/방향 기준 Transform. 없으면 이 GameObject 사용")]
    [SerializeField] private Transform firePoint = null;

    protected override void OnTrapTrigger()
    {
        if (arrowPrefab == null) return;

        Transform spawn = firePoint != null ? firePoint : transform;

        GameObject arrow = Instantiate(arrowPrefab, spawn.position, spawn.rotation);

        TrapProjectile proj = arrow.GetComponent<TrapProjectile>();
        if (proj != null)
            proj.moveDirection = spawn.forward;
    }
}
