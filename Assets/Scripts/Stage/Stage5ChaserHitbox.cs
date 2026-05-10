using UnityEngine;

/// <summary>
/// Chaser 접촉 데미지 전용 트리거 (EnemyHitbox와 동일 패턴).
/// 자식 오브젝트에 BoxCollider 등 isTrigger + 이 스크립트를 붙이고,
/// 루트의 Stage5ChaserAI가 이동·차지 판정을 담당한다.
///
/// [Inspector]
/// - damage: 1 등으로 설정 (0이면 피격 없음)
/// - 부모에 Stage5ChaserAI 필수
/// </summary>
[DisallowMultipleComponent]
public class Stage5ChaserHitbox : MonoBehaviour
{
    [Header("Damage")]
    [Tooltip("0이면 피격 처리 안 함")]
    [SerializeField] int damage = 0;

    Stage5ChaserAI _chaser;

    void Awake()
    {
        _chaser = GetComponentInParent<Stage5ChaserAI>();
        if (_chaser == null)
            Debug.LogWarning($"[Stage5ChaserHitbox] 부모에 Stage5ChaserAI가 없습니다: {gameObject.name}");
    }

    void OnTriggerEnter(Collider other) => TryHit(other);
    void OnTriggerStay(Collider other)  => TryHit(other);

    void TryHit(Collider other)
    {
        if (damage <= 0 || _chaser == null) return;
        if (!_chaser.CanApplyDamage()) return;
        if (!other.CompareTag("Player")) return;

        Player p = other.GetComponent<Player>();
        if (p == null || p.IsDead) return;

        // 넉백 없음 (EnemyHitbox와 구분). 피격이 실제로 들어갔을 때만 Chaser 정지 연동.
        if (!p.TryTakeDamage(damage, false)) return;
        _chaser.NotifyHitFromHitbox();
    }
}
