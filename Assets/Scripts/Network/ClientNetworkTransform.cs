using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Owner가 Transform 권한을 갖는 NetworkTransform.
/// 기본 NetworkTransform(Server 권한)과 달리, 오너 클라이언트의 Rigidbody 물리 이동을
/// 그대로 다른 클라이언트에 브로드캐스트한다.
///
/// [배치]
/// Network Player Prefab에 NetworkTransform 대신 이 컴포넌트를 추가.
/// - Interpolate : ✅ (보간 활성)
/// - 그 외 기본값 사용
/// </summary>
[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative() => false;
}
