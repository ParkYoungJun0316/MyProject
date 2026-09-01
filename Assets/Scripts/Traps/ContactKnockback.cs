using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 접촉 시 HP는 건드리지 않고 순수 넉백 + PunchHit 연출을 주는 범용 컴포넌트.
/// 튕기는 벽·범퍼 등 "닿으면 밖으로 밀려남" 상황에 사용.
/// 데미지 함정은 ContactDamage를 쓴다. 같은 오브젝트에 둘 다 붙이지 말 것.
///
/// [흐름]
///  Host가 충돌을 감지 → NetworkDamageUtil.ApplyKnockback (Owner AddForce)
///  → NetworkPlayerSetup.NotifyPunchHitFromServer (doPunchHit + PunchHit 3D SFX)
///
/// [설정 방법]
///  1. 튕길 오브젝트에 이 스크립트 추가
///  2. Collider 추가
///     - Is Trigger = false : 물리적으로 막으면서 튕김 (벽 등)
///     - Is Trigger = true  : 통과하면서 튕김만 (구역 등)
///  3. 힘 min/max, knockbackInterval Inspector에서 설정
/// </summary>
[RequireComponent(typeof(Collider))]
public class ContactKnockback : MonoBehaviour
{
    [Header("넉백 (세기만 랜덤, 방향은 이 오브젝트 중심 → 플레이어)")]
    [Tooltip("넉백 힘 최소값")]
    [SerializeField] float knockbackForceMin = 5f;

    [Tooltip("넉백 힘 최대값")]
    [SerializeField] float knockbackForceMax = 10f;

    [Tooltip("닿아있는 동안 이 간격마다 다시 튕김(초).\n너무 작으면 PunchHit SFX가 연타로 나감 (권장: 0.2 이상)")]
    [SerializeField] float knockbackInterval = 0.25f;

    [Header("설정")]
    [Tooltip("true: 이 컴포넌트가 활성화된 동안만 넉백 적용\nDeactivate()로 비활성화 가능")]
    [SerializeField] bool isActive = true;

    // 플레이어별로 다음 튕김 시각. 한 명이 타이머를 잡아먹으면 다른 명이 안 튕기는 것을 막는다.
    readonly Dictionary<int, float> _nextKnockbackTime = new Dictionary<int, float>();

    // ── 외부 호출 ────────────────────────────────────────────────

    public void Activate()   => isActive = true;
    public void Deactivate() => isActive = false;

    // ── 충돌 감지 ────────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        TryKnockback(other);
    }

    void OnTriggerStay(Collider other)
    {
        TryKnockback(other);
    }

    // Trigger가 아닌 일반 Collider로 사용할 때도 감지
    void OnCollisionEnter(Collision collision)
    {
        TryKnockback(collision.collider);
    }

    void OnCollisionStay(Collision collision)
    {
        TryKnockback(collision.collider);
    }

    // ── 내부 ────────────────────────────────────────────────────

    void TryKnockback(Collider other)
    {
        if (!isActive) return;
        if (!other.CompareTag("Player")) return;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

        Player p = other.GetComponent<Player>()
                   ?? other.GetComponentInParent<Player>();
        if (p == null || p.IsDead) return;

        int id = p.GetInstanceID();
        if (_nextKnockbackTime.TryGetValue(id, out float next) && Time.time < next)
            return;

        Vector3 dir = p.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        float force = Random.Range(knockbackForceMin, knockbackForceMax);
        NetworkDamageUtil.ApplyKnockback(p, dir, force);
        p.GetComponent<NetworkPlayerSetup>()?.NotifyPunchHitFromServer();

        _nextKnockbackTime[id] = Time.time + Mathf.Max(knockbackInterval, 0.05f);
    }
}
