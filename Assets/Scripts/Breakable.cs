using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 충돌 시 파괴되는 오브젝트 컴포넌트.
/// breakTriggerLayers에 해당하는 레이어 오브젝트가 닿으면 즉시 파괴 + 파편 이펙트.
///
/// [권장 사용]
/// - 돌굴림 맵의 Floor/Wall 피스에 부착
/// - 돌 프리팹 → "Boulder" 레이어 설정 → breakTriggerLayers에 Boulder 지정
/// - TrapProjectile.destroyOnWall=false, destroyOnFloor=false 로 설정해야 돌이 계속 굴러감
///
/// [외부 호출]
/// Break() 를 직접 호출하면 트리거 없이 즉시 파괴 가능 (연출용 등)
/// </summary>
[RequireComponent(typeof(Collider))]
public class Breakable : MonoBehaviour
{
    [Header("파괴 조건")]
    [Tooltip("이 레이어마스크에 해당하는 오브젝트가 닿을 때만 파괴.\n0(Nothing)이면 모든 충돌에 반응.")]
    [SerializeField] private LayerMask breakTriggerLayers;

    [Tooltip("파괴 최소 충돌 속도(m/s). 0이면 속도 무관 즉시 파괴.\n" +
             "천천히 밀리는 상황에서 실수로 깨지는 것을 방지할 때 사용.")]
    [SerializeField] private float minBreakSpeed = 0f;

    [Header("파편 / 이펙트")]
    [Tooltip("파괴 시 생성할 파편 또는 Particle 프리팹. 없으면 생략.")]
    [SerializeField] private GameObject debrisPrefab = null;

    [Tooltip("파편 자동 소멸 시간(초). 0이면 자동 소멸 안 함.")]
    [SerializeField] private float debrisLifetime = 0f;

    [Header("사운드")]
    [Tooltip("파괴 시 재생할 AudioClip. 없으면 생략.")]
    [SerializeField] private AudioClip breakSound = null;

    [Tooltip("파괴 사운드 볼륨 (0~1)")]
    [SerializeField] [Range(0f, 1f)] private float breakSoundVolume = 1f;

    [Header("범위 즉사 (선택)")]
    [Tooltip("파괴 시 반경 내 플레이어를 즉사시킬지 여부.\n" +
             "천장이 무너지거나 기둥이 쓰러져 플레이어를 으깨는 상황에 사용.")]
    [SerializeField] private bool killPlayerOnBreak = false;

    [Tooltip("즉사 반경(m). killPlayerOnBreak=true일 때만 사용.")]
    [SerializeField] private float killRadius = 0f;

    [Tooltip("플레이어 감지 레이어. killPlayerOnBreak=true일 때 사용.")]
    [SerializeField] private LayerMask playerLayer;

    [Header("이벤트")]
    [Tooltip("파괴 직전 호출. 연출·스테이지 연동 등에 사용.")]
    public UnityEvent OnBreak;

    bool _broken;

    // ── 물리 충돌 (non-trigger Collider) ──────────────────────────────

    void OnCollisionEnter(Collision col)
    {
        if (_broken) return;
        if (ShouldBreak(col.gameObject, col.relativeVelocity.magnitude))
            Break();
    }

    // ── 트리거 충돌 ──────────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (_broken) return;
        Rigidbody rb  = other.attachedRigidbody;
        float     spd = rb != null ? rb.linearVelocity.magnitude : 0f;
        if (ShouldBreak(other.gameObject, spd))
            Break();
    }

    // ── 파괴 조건 판단 ───────────────────────────────────────────────

    bool ShouldBreak(GameObject other, float speed)
    {
        // 레이어 필터 (0 = Nothing → 모든 충돌 허용)
        if (breakTriggerLayers.value != 0 &&
            (breakTriggerLayers.value & (1 << other.layer)) == 0)
            return false;

        // 최소 속도 체크
        if (minBreakSpeed > 0f && speed < minBreakSpeed)
            return false;

        return true;
    }

    // ── 파괴 처리 (외부에서 직접 호출 가능) ─────────────────────────

    /// <summary>
    /// 즉시 파괴. 파편 이펙트, 사운드, 범위 즉사, 이벤트 모두 발동.
    /// 외부(연출·트리거 등)에서도 직접 호출 가능.
    /// </summary>
    public void Break()
    {
        if (_broken) return;
        _broken = true;

        // 이벤트 먼저 발동 (구독자가 gameObject 참조 가능한 시점)
        OnBreak?.Invoke();

        // 파편 이펙트 생성
        if (debrisPrefab != null)
        {
            GameObject debris = Instantiate(debrisPrefab, transform.position, transform.rotation);
            if (debrisLifetime > 0f)
                Destroy(debris, debrisLifetime);
        }

        // 파괴 사운드 (오브젝트 삭제 후에도 재생되도록 PlayClipAtPoint 사용)
        if (breakSound != null)
            AudioSource.PlayClipAtPoint(breakSound, transform.position, breakSoundVolume);

        // 범위 즉사 처리
        if (killPlayerOnBreak && killRadius > 0f)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, killRadius, playerLayer);
            for (int i = 0; i < hits.Length; i++)
            {
                Player p = hits[i].GetComponent<Player>()
                           ?? hits[i].GetComponentInParent<Player>();
                p?.KillInstantly();
            }
        }

        Destroy(gameObject);
    }

    // ── 에디터 기즈모 ─────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!killPlayerOnBreak || killRadius <= 0f) return;

        Gizmos.color = new Color(1f, 0.2f, 0f, 0.15f);
        Gizmos.DrawSphere(transform.position, killRadius);

        Gizmos.color = new Color(1f, 0.2f, 0f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, killRadius);
    }
#endif
}
