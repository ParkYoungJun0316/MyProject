using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 레인 1개 담당 스포너.
/// 타이밍·루프·정지는 BoulderSpawnManager가 제어.
/// SpawnOne()만 외부에서 호출.
///
/// [네트워크]
/// Host만 Instantiate + NetworkObject.Spawn(). Client는 NGO 수신으로 자동 생성 (nm == null이면 스폰 스킵).
/// boulderPrefab에 NetworkObject 컴포넌트가 없으면 경고 로그 출력.
/// </summary>
public class BoulderSpawner : MonoBehaviour
{
    [Header("Boulder")]
    [Tooltip("루트 또는 자식에 WaypointMover 필수")]
    [SerializeField] GameObject boulderPrefab = null;

    [Tooltip("스폰 위치·회전. 비우면 이 오브젝트 Transform")]
    [SerializeField] Transform spawnPoint = null;

    [Header("Waypoints")]
    [Tooltip("길이 0이면 프리팹 WaypointMover의 웨이포인트 그대로")]
    [SerializeField] Transform[] runtimeWaypoints = null;

    [Header("이벤트")]
    public UnityEvent onBoulderSpawned;

    /// <summary>바위 1개 즉시 스폰 후 WaypointMover 시작. BoulderSpawnManager가 호출.</summary>
    public void SpawnOne()
    {
        if (boulderPrefab == null) return;

        var nm = NetworkManager.Singleton;

        // 멀티: Host만 스폰. NGO가 Client에 자동 전파하므로 Client는 아무것도 하지 않음.
        if (nm == null || !nm.IsServer) return;

        Vector3    pos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        GameObject instance = Instantiate(boulderPrefab, pos, rot);

        WaypointMover mover = instance.GetComponent<WaypointMover>()
                           ?? instance.GetComponentInChildren<WaypointMover>(true);
        if (mover == null)
        {
            Destroy(instance);
            return;
        }

        if (runtimeWaypoints != null && runtimeWaypoints.Length > 0)
            mover.SetWaypoints(runtimeWaypoints);

        mover.Deactivate();
        mover.Activate();

        // NetworkObject.Spawn()으로 Client에 전파.
        // Host의 Destroy(lifetime 만료 등) → 전원 Despawn 자동 처리.
        var netObj = instance.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            // B안: NetworkTransform 없이 Client 로컬 WaypointMover 시뮬.
            // runtimeWaypoints의 현재 씬 위치를 Vector3[]로 Spawn "전"에 예약해 두면 스폰
            // 메시지 자체에 실려 전파된다 (Deferred OnSpawn RPC 레이스 방지 — 2026-07-27 수정).
            // positions가 비어 있으면 Client는 프리팹 기본 웨이포인트 사용.
            var proj = instance.GetComponent<TrapProjectile>();
            if (proj != null)
            {
                Vector3[] positions;
                if (runtimeWaypoints != null && runtimeWaypoints.Length > 0)
                {
                    positions = new Vector3[runtimeWaypoints.Length];
                    for (int i = 0; i < runtimeWaypoints.Length; i++)
                        positions[i] = runtimeWaypoints[i] != null
                            ? runtimeWaypoints[i].position
                            : Vector3.zero;
                }
                else
                {
                    positions = new Vector3[0];
                }
                proj.PrepareWaypoints(positions);
            }

            netObj.Spawn(destroyWithScene: true);
        }
        else
            Debug.LogWarning("[BoulderSpawner] boulderPrefab에 NetworkObject가 없습니다. " +
                             "BreakableBoulder.prefab에 NetworkObject 컴포넌트를 추가하세요.");

        onBoulderSpawned?.Invoke();
    }
}
