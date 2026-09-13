using Unity.Netcode;
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

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

        Player p = other.GetComponent<Player>();
        // IsDowned 제외: 다운 중엔 HP가 0으로 고정돼 ApplyDamage 자체는 무해하지만, 이 체크가
        // 없으면 Chaser가 다운된 플레이어를 실제로 "공격"한 것처럼 반응(NotifyHitFromHitbox →
        // 정지·doHit 애니·SFX)해 버린다 — 적 AI는 다운된 플레이어를 공격하지 않는다(§5).
        if (p == null || p.IsDead || p.IsDowned) return;

        // 넉백 없음. 데미지 적용 후 Chaser 정지 연동 (서버에서만).
        NetworkDamageUtil.ApplyDamage(p, damage);
        _chaser.NotifyHitFromHitbox();
    }
}
