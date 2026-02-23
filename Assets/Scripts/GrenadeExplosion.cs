using UnityEngine;

/// <summary>
/// 수류탄에 부착. 생성 후 fuseTime(초) 뒤 범위 폭발.
/// 런타임에 Player.ThrowGrenadeWithForce()에서 값을 주입합니다.
/// </summary>
public class GrenadeExplosion : MonoBehaviour
{
    [Tooltip("폭발까지 걸리는 시간(초)")]
    public float fuseTime = 0f;

    [Tooltip("폭발 반경")]
    public float explosionRadius = 0f;

    [Tooltip("폭발 데미지")]
    public int explosionDamage = 0;

    [Tooltip("폭발 대상 레이어")]
    public LayerMask damageMask;

    [Tooltip("폭발 이펙트 프리팹 (선택)")]
    public GameObject explosionEffect;

    float timer;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= fuseTime)
        {
            Explode();
            enabled = false;
        }
    }

    void Explode()
    {
        Vector3 pos = transform.position;
        Collider[] hits = Physics.OverlapSphere(pos, explosionRadius, damageMask);
        for (int i = 0; i < hits.Length; i++)
        {
            Enemy e = hits[i].GetComponent<Enemy>();
            if (e != null)
            {
                e.HitByGrenade(pos);
                continue;
            }
        }

        if (explosionEffect != null)
            Instantiate(explosionEffect, pos, Quaternion.identity);

        Destroy(gameObject);
    }
}
