using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// Stage5 Chaser 스포너 (StageObjective 아님).
/// ColoredStartZone / SurviveTimeObjective 등에서 StartSpawning()을 직접 호출.
///
/// [동작]
/// - StartSpawning(): chaserPrefabs 중 null이 아닌 항목을 한 프레임에 모두 스폰.
/// - StopAndClear(): 활성 Chaser 제거 (리셋 시 등).
///
/// [씬 설정]
/// - chaserPrefabs: Stage5ChaserAI 프리팹 배열 (null 슬롯은 건너뜀)
/// - spawnPoints: 위치 Transform (프리팹 인덱스보다 적으면 순환)
/// </summary>
public class Stage5ChaserSpawner : MonoBehaviour
{
    [Header("Chaser 프리팹")]
    [Tooltip("null이 아닌 항목만 스폰")]
    public Stage5ChaserAI[] chaserPrefabs;

    [Header("스폰 위치")]
    [Tooltip("Chaser가 나타날 위치. 프리팹보다 적으면 순환")]
    public Transform[] spawnPoints;

    [Header("NavMesh 샘플링")]
    [Tooltip("스폰 위치를 NavMesh 위 점으로 보정하는 검색 반경(m)")]
    [SerializeField] float spawnSampleRadius = 0f;

    readonly List<Stage5ChaserAI> _activeChasers = new List<Stage5ChaserAI>();

    /// <summary>스테이지 시작 등에서 외부가 호출.</summary>
    public void StartSpawning()
    {
        CleanupChasers();

        if (!gameObject.activeInHierarchy) return;

        if (chaserPrefabs == null || chaserPrefabs.Length == 0)
        {
            Debug.LogWarning("[Stage5ChaserSpawner] chaserPrefabs가 비어 있습니다.");
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[Stage5ChaserSpawner] spawnPoints가 비어 있습니다.");
            return;
        }

        Player[] players    = FindObjectsByType<Player>(FindObjectsSortMode.None);
        int      spawnCount = spawnPoints.Length;

        for (int i = 0; i < chaserPrefabs.Length; i++)
        {
            Stage5ChaserAI prefab = chaserPrefabs[i];
            if (prefab == null) continue;

            Transform point = spawnPoints[i % spawnCount];
            if (point == null)
            {
                Debug.LogWarning($"[Stage5ChaserSpawner] spawnPoints[{i % spawnCount}]이 null입니다.");
                continue;
            }

            Vector3 pos = SampleNavMeshPos(point.position);
            Stage5ChaserAI chaser = Instantiate(prefab, pos, Quaternion.identity);
            chaser.Activate(players);
            _activeChasers.Add(chaser);
        }
    }

    /// <summary>리셋 시 등에서 호출.</summary>
    public void StopAndClear()
    {
        CleanupChasers();
    }

    Vector3 SampleNavMeshPos(Vector3 origin)
    {
        if (NavMesh.SamplePosition(origin, out NavMeshHit hit, spawnSampleRadius, NavMesh.AllAreas))
            return hit.position;

        if (NavMesh.SamplePosition(origin, out NavMeshHit fallback, spawnSampleRadius * 2f, NavMesh.AllAreas))
        {
            Debug.LogWarning($"[Stage5ChaserSpawner] NavMesh 1차 실패, 2배 반경으로 보정: {origin}");
            return fallback.position;
        }

        Debug.LogWarning($"[Stage5ChaserSpawner] NavMesh 샘플링 실패. 원본 좌표 사용: {origin}");
        return origin;
    }

    void CleanupChasers()
    {
        foreach (Stage5ChaserAI c in _activeChasers)
        {
            if (c == null) continue;
            c.Deactivate();
            Destroy(c.gameObject);
        }
        _activeChasers.Clear();
    }

    void OnDisable()
    {
        CleanupChasers();
    }
}
