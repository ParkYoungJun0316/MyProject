using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Host-Spawn 발사체에 부착.
///
/// [역할]
/// Host가 NetworkObject.Spawn()으로 생성하면
/// 클라이언트 측 Rigidbody를 kinematic으로 설정해
/// NetworkTransform이 위치를 단독 제어하도록 한다.
///
/// [프리팹 설정]
/// Arrow 프리팹에 NetworkObject + NetworkTransform + 이 컴포넌트를 추가.
/// NetworkRigidbody는 추가하지 않는다 (OnNetworkSpawn 전 kinematic 강제 문제).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class NetworkProjectile : NetworkBehaviour
{
    Rigidbody _rb;

    void Awake() => _rb = GetComponent<Rigidbody>();

    public override void OnNetworkSpawn()
    {
        // 클라이언트: Rigidbody 물리 비활성, NetworkTransform이 위치 제어
        if (!IsServer && _rb != null)
        {
            _rb.isKinematic = true;
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }
}
