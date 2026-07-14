using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 함정에서 발사/생성된 투사체 공통 컴포넌트.
/// - moveDirection : 발사 함정(ArrowTrap/DropTrap)이 런타임에 설정
/// - damage / lifetime : 여기서 단일 관리
/// - 속도(rb.linearVelocity)는 ArrowTrap/DropTrap이 발사 시 직접 설정
/// - 회전은 SpinRoller가 담당
/// - 경로 이동은 WaypointMover가 담당
///
/// [B안 네트워크 흐름]
/// Host: Spawn + 초기 velocity 설정 → InitializeVelocityClientRpc로 전파
/// 각 Client/Host: 받은 velocity로 로컬 비행 (NetworkTransform 위치 동기화 없음)
/// 피격: 누구든 OnTrigger → ServerRpc → Host 검증·데미지·Despawn
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class TrapProjectile : NetworkBehaviour
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

    bool    _isDestroyed;
    Vector3 _lastHitPoint  = Vector3.zero;
    Vector3 _lastHitNormal = Vector3.up;
    Rigidbody _rb;

    void Awake() => _rb = GetComponent<Rigidbody>();

    // ── 온라인 초기화 ────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        // Host만 수명 만료 후 Despawn
        if (IsServer && lifetime > 0f)
            StartCoroutine(LifetimeRoutine());
    }

    System.Collections.IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(lifetime);
        DestroyProjectileOnServer();
    }

    // ── B안: Host → 전 Client 초기 velocity 주입 ─────────────────────────
    /// <summary>
    /// ArrowTrap/DropTrap이 Host에서 NetworkObject.Spawn() 직후 호출.
    /// SendTo.NotServer → Host는 수신하지 않음 (이미 velocity 설정됨).
    /// Client는 이 velocity로 로컬 비행 시작.
    /// </summary>
    [Rpc(SendTo.NotServer)]
    public void InitializeVelocityClientRpc(Vector3 velocity)
    {
        if (_rb == null) _rb = GetComponent<Rigidbody>();
        _rb.linearVelocity = velocity;
    }

    // ── B안: Host → 전 Client 웨이포인트 경로 주입 (Boulder 등 경로 이동 투사체) ────
    /// <summary>
    /// BoulderSpawner가 Host에서 NetworkObject.Spawn() 직후 호출.
    /// positions가 비어 있으면 프리팹 기본 웨이포인트 사용.
    /// Client는 NetworkTransform 없이 이 경로로 WaypointMover를 로컬 시뮬.
    /// </summary>
    [Rpc(SendTo.NotServer)]
    public void InitializeWaypointsClientRpc(Vector3[] positions)
    {
        WaypointMover mover = GetComponent<WaypointMover>()
                           ?? GetComponentInChildren<WaypointMover>(true);
        if (mover == null) return;

        mover.Deactivate();
        if (positions != null && positions.Length > 0)
            mover.SetWaypointPositions(positions);
        mover.Activate();
    }

    // ── 충돌 ─────────────────────────────────────────────────────────────
    void OnCollisionEnter(Collision collision)
    {
        if (_isDestroyed) return;
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
        if (_isDestroyed) return;
        _lastHitPoint  = transform.position;
        _lastHitNormal = -transform.forward;
        HandleContact(other.gameObject);
    }

    void HandleContact(GameObject other)
    {
        // ── Player ──────────────────────────────────────────────────────
        if (other.CompareTag("Player"))
        {
            // 온라인(B안): ServerRpc 경로만. Host·Client 모두 동일.
            Player p = other.GetComponent<Player>() ?? other.GetComponentInParent<Player>();
            if (p == null) return;

            // 로컬에서 즉시 숨김: Despawn RTT 동안 화살이 플레이어를 통과하며
            // 중간에서 사라지는 것을 방지. rb는 건드리지 않아 비행 방향 유지.
            if (destroyOnPlayer) HideLocal();

            var pNetObj = p.GetComponent<NetworkObject>();
            if (pNetObj != null)
                ReportHitServerRpc(pNetObj.NetworkObjectId);
            else if (destroyOnPlayer)
                RequestDestroyServerRpc();
            return;
        }

        // ── Wall / Floor ─────────────────────────────────────────────────
        bool hitWall  = destroyOnWall  && other.CompareTag("Wall");
        bool hitFloor = destroyOnFloor && other.CompareTag("Floor");
        if (!hitWall && !hitFloor) return;

        RequestDestroyServerRpc();
    }

    // ── ServerRpc: 피격 보고 → Host 데미지 + Despawn ─────────────────────
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void ReportHitServerRpc(ulong playerNetId)
    {
        if (_isDestroyed) return;

        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(playerNetId, out var netObj))
        {
            Player p = netObj.GetComponent<Player>();
            if (p != null && damage > 0)
                NetworkDamageUtil.ApplyDamage(p, damage, false);
        }

        if (destroyOnPlayer) DestroyProjectileOnServer();
    }

    // ── ServerRpc: 벽·바닥·수명 파괴 요청 → Host Despawn ─────────────────
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void RequestDestroyServerRpc()
    {
        if (!_isDestroyed) DestroyProjectileOnServer();
    }

    // ── 내부 파괴 ────────────────────────────────────────────────────────

    /// <summary>온라인: 반드시 Host에서만 호출. 전원 Despawn.</summary>
    void DestroyProjectileOnServer()
    {
        if (_isDestroyed) return;
        _isDestroyed = true;
        SpawnHitEffect();
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
            netObj.Despawn(true);
        else
            Destroy(gameObject);
    }

    /// <summary>
    /// 온라인 전용. 플레이어 충돌 즉시 로컬에서 숨김.
    /// Rigidbody는 건드리지 않음(비행 유지). 콜라이더·렌더러만 끔.
    /// 실제 Despawn은 Host ReportHitServerRpc가 처리.
    /// </summary>
    void HideLocal()
    {
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;
        foreach (var rend in GetComponentsInChildren<Renderer>())
            rend.enabled = false;
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
