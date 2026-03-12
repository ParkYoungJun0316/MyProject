using System.Collections;
using UnityEngine;

/// <summary>
/// 공중 낙하 공격 함정.
/// activateInterval마다 targetPoints 중 하나를 골라 경고 → 낙하 순서로 공격.
/// Boss 등 외부에서 FireAt(Vector3)으로 직접 낙하 위치를 지정해 호출할 수 있음.
///
/// [낙하체 프리팹 권장 설정]
/// - TrapProjectile: type=Arrow, destroyOnFloor=true, destroyOnWall=false
/// - Rigidbody: Use Gravity=false (TrapProjectile이 속도 제어)
/// - Collider (Trigger)
/// </summary>
public class DropTrap : TrapBase
{
    [Header("Drop Trap")]
    [Tooltip("낙하할 물체 프리팹 (TrapProjectile 컴포넌트 필수)")]
    [SerializeField] private GameObject dropPrefab = null;

    [Tooltip("낙하 목표 지점 목록. 비어 있으면 이 GameObject 위치를 사용")]
    [SerializeField] private Transform[] targetPoints = new Transform[0];

    [Tooltip("타겟 랜덤 선택 여부. false이면 targetPoints 순서대로 순환")]
    [SerializeField] private bool randomTarget = true;

    [Header("경고")]
    [Tooltip("경고 마커 프리팹. 낙하 위치 바닥에 warnDuration만큼 표시. 없으면 생략")]
    [SerializeField] private GameObject warnPrefab = null;

    [Tooltip("경고 표시 시간 (초). 플레이어가 피할 여유 시간")]
    [SerializeField] private float warnDuration = 0f;

    [Header("낙하")]
    [Tooltip("낙하체가 생성될 높이 (타겟 위치 기준 Y 오프셋, m)")]
    [SerializeField] private float spawnHeight = 0f;

    [Tooltip("낙하 속도 (m/s). 0이면 프리팹 기본값 사용")]
    [SerializeField] private float dropSpeed = 0f;

    [Tooltip("낙하체 데미지. 0이면 프리팹 기본값 사용")]
    [SerializeField] private int damage = 0;

    int targetIndex;

    protected override void OnTrapTrigger()
    {
        if (dropPrefab == null) return;
        StartCoroutine(DropCycle(GetNextTargetPos()));
    }

    /// <summary>보스 등 외부에서 직접 낙하 위치를 지정해 호출</summary>
    public void FireAt(Vector3 targetPos)
    {
        if (dropPrefab == null) return;
        StartCoroutine(DropCycle(targetPos));
    }

    Vector3 GetNextTargetPos()
    {
        if (targetPoints == null || targetPoints.Length == 0)
            return transform.position;

        if (randomTarget)
            return targetPoints[Random.Range(0, targetPoints.Length)].position;

        Vector3 pos = targetPoints[targetIndex % targetPoints.Length].position;
        targetIndex++;
        return pos;
    }

    IEnumerator DropCycle(Vector3 targetPos)
    {
        // 경고 마커 표시
        GameObject warn = null;
        if (warnPrefab != null)
            warn = Instantiate(warnPrefab, targetPos, Quaternion.identity);

        if (warnDuration > 0f)
            yield return new WaitForSeconds(warnDuration);

        if (warn != null)
            Destroy(warn);

        // 낙하체 생성 (목표 위치 바로 위 spawnHeight 높이)
        Vector3 spawnPos = targetPos + Vector3.up * spawnHeight;
        GameObject drop = Instantiate(dropPrefab, spawnPos, Quaternion.LookRotation(Vector3.down));

        TrapProjectile proj = drop.GetComponent<TrapProjectile>();
        if (proj == null) yield break;

        proj.moveDirection = Vector3.down;
        if (dropSpeed > 0f) proj.speed = dropSpeed;
        if (damage > 0) proj.damage = damage;
    }
}
