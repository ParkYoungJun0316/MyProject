using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Owner 권한 NetworkTransform (Phase 2 → Owner Authority).
/// Owner가 Rigidbody 물리로 위치를 확정하고 전 클라이언트에 브로드캐스트한다.
///
/// [동작 분기]
/// Owner        : IsOwner = true → OnTransformUpdated 즉시 return. FixedUpdate에서 rb.linearVelocity로 직접 이동.
/// Host 비오너  : NT 위치 수신 → OnTransformUpdated → rb.MovePosition()으로 적용 (지터 방지). Trigger 판정 정상.
/// Client 비오너: NT 위치 수신 → OnTransformUpdated → isKinematic = true 이므로 return. NT 기본 동작.
///
/// [배치]
/// Network Player Prefab에 이 컴포넌트를 사용.
/// - Interpolate : ✅ (보간 활성)
///
/// [주의] 클래스명은 레거시(ClientNetworkTransform)이나 Prefab GUID 참조 유지를 위해 이름 유지.
/// </summary>
[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    private Rigidbody rb;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody>();
    }

    protected override bool OnIsServerAuthoritative() => false;

    protected override void OnTransformUpdated()
    {
        if (IsOwner) return;
        if (rb == null) return;
        if (rb.isKinematic) return;

        rb.MovePosition(transform.position);
        rb.linearVelocity = Vector3.zero;
    }
}
