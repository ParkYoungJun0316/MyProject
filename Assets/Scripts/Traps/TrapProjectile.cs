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
/// Host: Spawn "전" PrepareVelocity()로 예약 → OnNetworkSpawn에서 NetworkVariable에 기록
///       → 스폰 메시지에 실려 전파 (Deferred OnSpawn RPC 레이스 없음)
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

    [Header("충돌 이펙트")]
    [Tooltip("충돌 파괴 시 스폰할 파티클 프리팹")]
    [SerializeField] private GameObject hitEffectPrefab = null;

    bool    _isDestroyed;
    Vector3 _lastHitPoint  = Vector3.zero;
    Vector3 _lastHitNormal = Vector3.up;
    Rigidbody _rb;

    // ── B안 Spawn-전 초기화 (Deferred OnSpawn RPC 레이스 방지, 2026-07-27) ──
    // ArrowTrap/DropTrap/BoulderSpawner가 Host에서 NetworkObject.Spawn() 호출 "전"에
    // PrepareVelocity()/PrepareWaypoints()로 예약 → OnNetworkSpawn(Host)에서 아래 NV/List에
    // 기록되면 스폰 메시지 자체에 실려 전파된다 (PlayerSpawnCoordinator._clientColors와 동일
    // 패턴). Spawn 후 별도 ClientRpc로 보내던 이전 방식은 CreateObject 메시지와 RPC 메시지의
    // 전송 경로가 달라, RPC가 먼저 도착하면 최대 SpawnTimeout(10초) 지연되거나 유실됐다.
    readonly NetworkVariable<Vector3> _initialVelocity = new();
    readonly NetworkList<Vector3>     _initialWaypoints = new();
    Vector3?  _pendingVelocity;
    Vector3[] _pendingWaypoints;

    void Awake() => _rb = GetComponent<Rigidbody>();

    /// <summary>ArrowTrap/DropTrap이 Host에서 NetworkObject.Spawn() 호출 "전"에 호출.</summary>
    public void PrepareVelocity(Vector3 velocity) => _pendingVelocity = velocity;

    /// <summary>
    /// BoulderSpawner가 Host에서 NetworkObject.Spawn() 호출 "전"에 호출.
    /// positions가 비어 있으면 Client는 프리팹 기본 웨이포인트 사용.
    /// </summary>
    public void PrepareWaypoints(Vector3[] positions) => _pendingWaypoints = positions ?? System.Array.Empty<Vector3>();

    // ── 온라인 초기화 ────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (_pendingVelocity.HasValue)
            {
                _initialVelocity.Value = _pendingVelocity.Value;
                _pendingVelocity = null;
            }
            if (_pendingWaypoints != null)
            {
                _initialWaypoints.Clear();
                foreach (Vector3 p in _pendingWaypoints)
                    _initialWaypoints.Add(p);
                _pendingWaypoints = null;
            }
        }

        if (_initialVelocity.Value != Vector3.zero)
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            _rb.linearVelocity = _initialVelocity.Value;
        }

        // B안: 경로 이동 투사체(Boulder 등) — Client만 위치 웨이포인트로 로컬 시뮬 시작.
        // Host는 BoulderSpawner가 이미 Transform[] 웨이포인트로 WaypointMover를 구동 중이라 제외.
        if (!IsServer)
        {
            WaypointMover mover = GetComponent<WaypointMover>()
                               ?? GetComponentInChildren<WaypointMover>(true);
            if (mover != null)
            {
                mover.Deactivate();
                if (_initialWaypoints.Count > 0)
                {
                    var positions = new Vector3[_initialWaypoints.Count];
                    for (int i = 0; i < positions.Length; i++)
                        positions[i] = _initialWaypoints[i];
                    mover.SetWaypointPositions(positions);
                }
                mover.Activate();
            }
        }

        // Host만 수명 만료 후 Despawn
        if (IsServer && lifetime > 0f)
            StartCoroutine(LifetimeRoutine());
    }

    System.Collections.IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(lifetime);
        DestroyProjectileOnServer();
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
        }

        // ── Wall / Floor ─────────────────────────────────────────────────
        // Wall/Floor 태그 파괴 판정 제거(2026-07-27, 티켓 D) — Despawn 후에도
        // 로컬 콜라이더가 살아있어 재충돌마다 RequestDestroyServerRpc가 중복 전송되고,
        // 이미 Despawn된 id로 도착한 RPC가 NGO DeferredMessageManager에서
        // "Deferred OnSpawn" 대기 → SpawnTimeout(10초) 후 purge 경고로 이어졌다.
        // 정리는 lifetime(수명) 만료 또는 StageManager.DestroyAllProjectiles() 일괄
        // Despawn(둘 다 Host 직접 호출, RPC 아님)에 위임한다.
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
