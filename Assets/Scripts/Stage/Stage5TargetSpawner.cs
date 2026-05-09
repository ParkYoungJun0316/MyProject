using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// Stage5 타겟 스포너.
/// 코너 Transform에 NavMesh 보정 후 Stage5TargetRunner를 배치.
/// 매 Begin() 호출(라운드 시작/리셋)마다 null이 아닌 프리팹 인덱스를 셔플해 어떤 색이 어느 코너에 갈지 랜덤화.
///
/// [Inspector 설정]
/// - targetPrefabs: null이 아닌 항목만 스폰 (개수 고정 없음, 1개·5개 등 가능)
/// - cornerPoints: 사각 Ground 4 꼭지점 Transform (배열 길이 4 이상). 순서 무관
/// - spawnSampleRadius: 코너 좌표에서 유효 NavMesh 점을 탐색하는 반경
/// </summary>
public class Stage5TargetSpawner : MonoBehaviour
{
    [Header("타겟 프리팹")]
    [Tooltip("null이 아닌 항목만 스폰. 개수 제한 없음")]
    public Stage5TargetRunner[] targetPrefabs;

    [Header("스폰 위치")]
    [Tooltip("사각 Ground 꼭지점 Transform. 배열 길이 4 이상")]
    public Transform[] cornerPoints;

    [Header("NavMesh 샘플링")]
    [Tooltip("코너 좌표에서 유효한 NavMesh 위치를 탐색하는 최대 반경(m)")]
    [SerializeField] float spawnSampleRadius = 3f;

    /// <summary>
    /// null이 아닌 프리팹 개수만큼 스폰해 반환. Stage5TargetObjective.Begin()에서 호출.
    /// </summary>
    public List<Stage5TargetRunner> SpawnTargets()
    {
        List<Stage5TargetRunner> result = new List<Stage5TargetRunner>();

        if (targetPrefabs == null || targetPrefabs.Length == 0)
        {
            Debug.LogWarning("[Stage5TargetSpawner] targetPrefabs가 비어 있습니다.");
            return result;
        }

        int[] prefabIndices = CollectNonNullPrefabIndices(targetPrefabs);
        if (prefabIndices == null || prefabIndices.Length == 0)
        {
            Debug.LogWarning("[Stage5TargetSpawner] targetPrefabs에 null이 아닌 프리팹이 없습니다.");
            return result;
        }

        if (cornerPoints == null || cornerPoints.Length < 4)
        {
            Debug.LogWarning("[Stage5TargetSpawner] cornerPoints가 4개 미만입니다. 등록을 확인하세요.");
            return result;
        }

        Shuffle(prefabIndices);

        int n = prefabIndices.Length;
        for (int i = 0; i < n; i++)
        {
            int pi = prefabIndices[i];
            Stage5TargetRunner prefab = targetPrefabs[pi];
            if (prefab == null) continue;

            Transform corner = cornerPoints[i % cornerPoints.Length];
            if (corner == null)
            {
                Debug.LogWarning($"[Stage5TargetSpawner] cornerPoints[{i % cornerPoints.Length}]가 null입니다.");
                continue;
            }

            Vector3 spawnPos = SampleNavMeshPos(corner.position);
            Stage5TargetRunner runner = Instantiate(prefab, spawnPos, Quaternion.identity);
            result.Add(runner);
        }

        return result;
    }

    // ── NavMesh 샘플링 ───────────────────────────────────────────

    Vector3 SampleNavMeshPos(Vector3 origin)
    {
        if (NavMesh.SamplePosition(origin, out NavMeshHit hit, spawnSampleRadius, NavMesh.AllAreas))
            return hit.position;

        // 반경을 2배로 늘려 재시도
        if (NavMesh.SamplePosition(origin, out NavMeshHit fallback, spawnSampleRadius * 2f, NavMesh.AllAreas))
        {
            Debug.LogWarning($"[Stage5TargetSpawner] NavMesh 샘플링 1차 실패, 2배 반경으로 보정: {origin}");
            return fallback.position;
        }

        Debug.LogWarning($"[Stage5TargetSpawner] NavMesh 샘플링 완전 실패. 원본 좌표 사용: {origin}");
        return origin;
    }

    // ── 유틸 ─────────────────────────────────────────────────────

    static int[] CollectNonNullPrefabIndices(Stage5TargetRunner[] prefabs)
    {
        int count = 0;
        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null) count++;
        }

        if (count == 0) return null;

        int[] indices = new int[count];
        int w = 0;
        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null)
                indices[w++] = i;
        }

        return indices;
    }

    static void Shuffle(int[] arr)
    {
        for (int i = arr.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: 스폰 미리보기 (코너 Gizmos)")]
    void Debug_PreviewCorners()
    {
        if (cornerPoints == null) return;
        for (int i = 0; i < cornerPoints.Length; i++)
        {
            if (cornerPoints[i] != null)
                Debug.Log($"[Stage5TargetSpawner] Corner[{i}]: {cornerPoints[i].position}");
        }
    }
#endif
}
