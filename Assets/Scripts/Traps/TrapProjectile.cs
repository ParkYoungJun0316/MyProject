using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 함정에서 발사/생성된 투사체 공통 컴포넌트.
/// - moveDirection : 발사 함정(ArrowTrap/DropTrap)이 런타임에 설정
/// - damage / lifetime : 여기서 단일 관리
/// - 속도(rb.linearVelocity)는 ArrowTrap/DropTrap이 발사 시 직접 설정
/// - 회전은 SpinRoller가 담당
/// - 경로 이동은 WaypointMover가 담당
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class TrapProjectile : MonoBehaviour
{
    [Header("Stats")]
    [Tooltip("플레이어에게 입히는 데미지")]
    [SerializeField] public int damage = 0;

    [Tooltip("자동 파괴 시간(초). 0이면 무제한")]
    [SerializeField] public float lifetime = 0f;

    [Header("Direction (발사 함정이 런타임에 설정)")]
    [SerializeField] public Vector3 moveDirection = Vector3.forward;

    [Header("충돌 파괴 설정")]
    [Tooltip("Player와 충돌 시 파괴. 굴림 오브젝트는 false 권장")]
    [SerializeField] private bool destroyOnPlayer = true;

    [Tooltip("Wall 태그 오브젝트와 충돌 시 파괴")]
    [SerializeField] private bool destroyOnWall = true;

    [Tooltip("Floor 태그 오브젝트와 충돌 시 파괴")]
    [SerializeField] private bool destroyOnFloor = true;

    [Header("충돌 이펙트")]
    [Tooltip("충돌 파괴 시 스폰할 파티클 프리팹")]
    [SerializeField] private GameObject hitEffectPrefab = null;

    bool      isDestroyed;
    Vector3   _lastHitPoint  = Vector3.zero;
    Vector3   _lastHitNormal = Vector3.up;

    void Start()
    {
        if (lifetime <= 0f) return;

        // 네트워크 모드: Host만 수명 파괴 담당 (Destroy → 전원 자동 Despawn)
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

        Destroy(gameObject, lifetime);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isDestroyed) return;
        if (collision.contactCount > 0)
        {
            ContactPoint cp = collision.GetContact(0);
            _lastHitPoint  = cp.point;
            _lastHitNormal = cp.normal;
        }
        HandleContact(collision.gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (isDestroyed) return;
        _lastHitPoint  = transform.position;
        _lastHitNormal = -transform.forward;
        HandleContact(other.gameObject);
    }

    void HandleContact(GameObject other)
    {
        // 네트워크 모드: 서버(Host)만 충돌 처리 — 파괴 및 데미지 판정
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

        if (other.CompareTag("Player"))
        {
            if (damage > 0)
            {
                Player p = other.GetComponent<Player>()
                           ?? other.GetComponentInParent<Player>();
                if (p != null)
                    ApplyDamageToPlayer(p, damage);
            }
            if (destroyOnPlayer) DestroyProjectile();
            return;
        }

        if (destroyOnWall && other.CompareTag("Wall"))
        {
            DestroyProjectile();
            return;
        }

        if (destroyOnFloor && other.CompareTag("Floor"))
        {
            DestroyProjectile();
        }
    }

    static void ApplyDamageToPlayer(Player p, int amount)
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            // 네트워크 모드: Host에서만 데미지 판정 (클라이언트는 무시)
            if (!nm.IsServer) return;

            var netSetup = p.GetComponent<NetworkPlayerSetup>();
            if (netSetup != null)
                netSetup.ApplyDamageFromServer(amount);
            else
                p.TakeDamage(amount, false); // NetworkPlayerSetup 없는 경우 폴백
        }
        else
        {
            // 오프라인
            p.TakeDamage(amount, false);
        }
    }

    void DestroyProjectile()
    {
        if (isDestroyed) return;
        isDestroyed = true;
        SpawnHitEffect();
        Destroy(gameObject);
    }

    void SpawnHitEffect()
    {
        if (hitEffectPrefab == null) return;
        Quaternion rot = _lastHitNormal != Vector3.zero
            ? Quaternion.LookRotation(_lastHitNormal)
            : Quaternion.identity;
        Instantiate(hitEffectPrefab, _lastHitPoint, rot);
    }
}
