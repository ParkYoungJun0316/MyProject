using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 레인 1개 담당 스포너.
/// 타이밍·루프·정지는 BoulderSpawnManager가 제어.
/// SpawnOne()만 외부에서 호출.
/// </summary>
public class BoulderSpawner : MonoBehaviour
{
    [Header("Boulder")]
    [Tooltip("루트 또는 자식에 SpinRoller 필수")]
    [SerializeField] GameObject boulderPrefab = null;

    [Tooltip("스폰 위치·회전. 비우면 이 오브젝트 Transform")]
    [SerializeField] Transform spawnPoint = null;

    [Header("Waypoints")]
    [Tooltip("길이 0이면 프리팹 SpinRoller의 웨이포인트 그대로")]
    [SerializeField] Transform[] runtimeWaypoints = null;

    [Header("이벤트")]
    public UnityEvent onBoulderSpawned;

    /// <summary>바위 1개 즉시 스폰 후 SpinRoller 시작. BoulderSpawnManager가 호출.</summary>
    public void SpawnOne()
    {
        if (boulderPrefab == null) return;

        Vector3    pos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        GameObject instance = Instantiate(boulderPrefab, pos, rot);

        SpinRoller roller = instance.GetComponent<SpinRoller>()
                         ?? instance.GetComponentInChildren<SpinRoller>(true);
        if (roller == null)
        {
            Destroy(instance);
            return;
        }

        if (runtimeWaypoints != null && runtimeWaypoints.Length > 0)
            roller.SetWaypoints(runtimeWaypoints);

        roller.Deactivate();
        roller.Activate();

        onBoulderSpawned?.Invoke();
    }
}
