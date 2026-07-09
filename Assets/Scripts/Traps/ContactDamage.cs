using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 접촉 시 플레이어에게 지속 데미지를 주는 범용 컴포넌트.
/// 위산 벽, 용암 바닥, 독가스 구역 등 "닿으면 계속 데미지" 상황에 사용.
///
/// [설정 방법]
///  1. 데미지를 줄 오브젝트에 이 스크립트 추가
///  2. Collider 추가
///     - Is Trigger = false : 물리적으로 막으면서 데미지 (벽 등)
///     - Is Trigger = true  : 통과하면서 데미지만 (구역 등)
///  3. damage, damageInterval Inspector에서 설정
/// </summary>
[RequireComponent(typeof(Collider))]
public class ContactDamage : MonoBehaviour
{
    [Header("데미지")]
    [Tooltip("플레이어에게 입히는 데미지")]
    [SerializeField] int damage = 0;

    [Tooltip("연속 데미지 간격(초). 닿아있는 동안 이 간격마다 데미지 적용\n0이면 매 프레임마다 적용 (권장: 0.5 이상)")]
    [SerializeField] float damageInterval = 0f;

    [Header("설정")]
    [Tooltip("true: 이 컴포넌트가 활성화된 동안만 데미지 적용\nDeactivate()로 비활성화 가능")]
    [SerializeField] bool isActive = true;

    float _nextDamageTime;

    // ── 외부 호출 ────────────────────────────────────────────────

    public void Activate()   => isActive = true;
    public void Deactivate() => isActive = false;

    // ── 충돌 감지 ────────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        TryDamage(other);
    }

    void OnTriggerStay(Collider other)
    {
        TryDamage(other);
    }

    // Trigger가 아닌 일반 Collider로 사용할 때도 감지
    void OnCollisionEnter(Collision collision)
    {
        TryDamage(collision.collider);
    }

    void OnCollisionStay(Collision collision)
    {
        TryDamage(collision.collider);
    }

    // ── 내부 ────────────────────────────────────────────────────

    void TryDamage(Collider other)
    {
        if (!isActive) return;
        if (damage <= 0) return;
        if (!other.CompareTag("Player")) return;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

        if (Time.time < _nextDamageTime) return;

        Player p = other.GetComponent<Player>()
                   ?? other.GetComponentInParent<Player>();
        if (p == null) return;

        NetworkDamageUtil.ApplyDamage(p, damage, false);
        _nextDamageTime = Time.time + Mathf.Max(damageInterval, 0.05f);
    }
}
