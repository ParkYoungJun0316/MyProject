using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Punch 판정 전용 트리거 (Stage5ChaserHitbox와 동일 패턴).
/// 자식 오브젝트에 Trigger Collider + 이 스크립트를 붙이고,
/// 부모(루트)의 PlayerPunch가 쿨다운·스윙 판정을 담당한다.
///
/// [OnTriggerEnter 타이밍 보완]
/// OnTriggerEnter는 콜라이더가 새로 겹치는 순간에만 발생 — 스윙 시작 전에
/// 이미 겹쳐 있던 대상(가만히 붙어 있다가 펀치)은 Enter 이벤트가 새로 안 생겨서
/// 놓친다. PlayerPunch가 스윙을 여는 순간 CheckAlreadyOverlapping()을 호출해
/// 즉시 1회 OverlapSphere로 보완 판정한다. OnTriggerStay는 여전히 사용하지 않는다.
///
/// [Inspector]
/// - 부모에 PlayerPunch, Player 필수
/// </summary>
[DisallowMultipleComponent]
public class PlayerPunchHitbox : MonoBehaviour
{
    [Header("스윙 시작 시 즉시 판정 (OnTriggerEnter 타이밍 보완)")]
    [Tooltip("트리거 콜라이더 크기와 맞춰 튜닝. 스윙 시작 순간 이미 겹쳐 있는 대상을 잡는 용도")]
    [SerializeField] float overlapCheckRadius = 0.6f;

    [Tooltip("겹침 조회 대상 레이어. Player 레이어로 설정")]
    [SerializeField] LayerMask playerLayer;

    PlayerPunch _punch;
    Player _owner;

    void Awake()
    {
        _punch = GetComponentInParent<PlayerPunch>();
        _owner = GetComponentInParent<Player>();
        if (_punch == null || _owner == null)
            Debug.LogWarning($"[PlayerPunchHitbox] 부모에 PlayerPunch/Player가 없습니다: {gameObject.name}");
    }

    void OnTriggerEnter(Collider other) => TryHit(other);

    /// <summary>PlayerPunch가 스윙 시작 시점에 호출. PunchServerRpc 경로상 Host에서만 호출된다.</summary>
    public void CheckAlreadyOverlapping()
    {
        if (_punch == null || _owner == null) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, overlapCheckRadius, playerLayer);
        for (int i = 0; i < hits.Length; i++)
            TryHit(hits[i]);
    }

    void TryHit(Collider other)
    {
        if (_punch == null || _owner == null) return;
        if (!other.CompareTag("Player")) return;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

        Player target = other.GetComponent<Player>();
        if (target == null || target == _owner || target.IsDead) return;

        _punch.TryRegisterHit(target, transform.position);
    }
}
