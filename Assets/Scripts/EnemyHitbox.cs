using UnityEngine;

/// <summary>
/// 적 근거리 공격용 히트박스. 이 컴포넌트가 붙은 Collider(Trigger)가 Player와 겹치면 데미지 적용.
/// Enemy의 meleeArea(BoxCollider)에 붙이고, damage는 Inspector 또는 Enemy가 Awake에서 할당.
/// </summary>
public class EnemyHitbox : MonoBehaviour
{
    [Header("Damage")]
    [Tooltip("Inspector에서 설정하거나, Enemy 스크립트가 Awake에서 meleeDamage로 덮어씀")]
    public int damage = 0;

    void OnTriggerEnter(Collider other)
    {
        if (damage <= 0) return;
        if (!other.CompareTag("Player")) return;

        Player p = other.GetComponent<Player>();
        if (p != null)
            p.TakeDamage(damage, true);
    }
}
