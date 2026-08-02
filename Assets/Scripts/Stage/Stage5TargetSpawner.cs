using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// Stage5 Runner 스포너.
///
/// [동작]
///  SpawnTargets(count) — spawnPoints[]를 셔플 후 앞에서 count개 선택, 각 위치에 Runner 1마리 스폰.
///  count > spawnPoints.Length 면 spawnPoints 전부 사용 (중복 스폰 없음).
///
/// [네트워크 — Host 전권 시뮬 (TStageNetworkBoard.md §3.2 확정)]
///  Host만 Instantiate + NetworkObject.Spawn(). Client는 IsClientOnly()면 즉시 빈 리스트 반환 —
///  NGO 수신으로 로컬 복제본은 자동 생성되지만 Stage5TargetObjective가 직접 들고 있는 목록엔
///  들어가지 않는다(포획 판정은 Host-only이므로 Client가 로컬로 들고 있을 이유가 없음).
///  셔플은 NetworkSessionData.Seed 기반 — Host만 쓰는 값이라 재현성·로그 목적.
///
/// [Inspector 설정]
///  targetPrefab  : Runner 프리팹 1개 (NetworkObject + 서버 권한 NetworkTransform 필요)
///  spawnPoints   : 스폰 후보 위치 (씬에 여러 개 배치, 10개 권장). 1개 이상 필요.
///  spawnSampleRadius : NavMesh 위치 보정 반경(m)
/// </summary>
public class Stage5TargetSpawner : MonoBehaviour
{
    [Header("타겟 프리팹")]
    [Tooltip("Runner 프리팹 1개. 인원에 따라 이 프리팹을 여러 번 스폰한다.")]
    public Stage5TargetRunner targetPrefab;

    [Header("스폰 위치")]
    [Tooltip("스폰 후보 Transform. 매 스폰마다 셔플 후 앞에서 count개 선택 (중복 없음).\n" +
             "최소 1개. 10개 이상 배치 권장.")]
    public Transform[] spawnPoints;

    [Header("NavMesh 샘플링")]
    [Tooltip("스폰 위치에서 유효 NavMesh 점을 탐색하는 최대 반경(m)")]
    [SerializeField] float spawnSampleRadius = 3f;

    /// <summary>
    /// count마리 스폰 후 반환. Stage5TargetObjective.Begin()에서 호출.
    /// spawnPoints 중 count개를 랜덤 선택해 각 위치에 1마리씩 배치.
    /// Host 전용 — Client는 즉시 빈 리스트 반환 (TStageNetworkBoard.md §3.2).
    /// </summary>
    public List<Stage5TargetRunner> SpawnTargets(int count)
    {
        var result = new List<Stage5TargetRunner>();

        if (IsClientOnly()) return result;

        if (targetPrefab == null)
        {
            Debug.LogWarning("[Stage5TargetSpawner] targetPrefab이 비어 있습니다.");
            return result;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[Stage5TargetSpawner] spawnPoints가 비어 있습니다.");
            return result;
        }

        int actualCount = Mathf.Min(count, spawnPoints.Length);

        const int salt = 0x52554E52; // 셔플 결정성용 salt("RUNR") — 값 자체는 Host만 사용
        UnityEngine.Random.InitState(NetworkSessionData.Seed ^ salt);
        int[] indices = ShuffledIndices(spawnPoints.Length);

        for (int i = 0; i < actualCount; i++)
        {
            Transform point = spawnPoints[indices[i]];
            if (point == null) continue;

            Vector3 pos = SampleNavMeshPos(point.position);
            Stage5TargetRunner runner = Instantiate(targetPrefab, pos, Quaternion.identity);

            NetworkObject netObj = runner.GetComponent<NetworkObject>();
            if (netObj != null)
                netObj.Spawn(destroyWithScene: true);
            else
                Debug.LogWarning("[Stage5TargetSpawner] targetPrefab에 NetworkObject가 없습니다.");

            result.Add(runner);
        }

        NetLog.Transition("Stage5TargetSpawner", "SpawnComplete", $"count={result.Count} seed={NetworkSessionData.Seed}");
        return result;
    }

    // ── NavMesh 샘플링 ───────────────────────────────────────────

    Vector3 SampleNavMeshPos(Vector3 origin)
    {
        if (NavMesh.SamplePosition(origin, out NavMeshHit hit, spawnSampleRadius, NavMesh.AllAreas))
            return hit.position;

        if (NavMesh.SamplePosition(origin, out NavMeshHit fallback, spawnSampleRadius * 2f, NavMesh.AllAreas))
        {
            Debug.LogWarning($"[Stage5TargetSpawner] NavMesh 1차 실패, 2배 반경으로 보정: {origin}");
            return fallback.position;
        }

        Debug.LogWarning($"[Stage5TargetSpawner] NavMesh 샘플링 실패. 원본 좌표 사용: {origin}");
        return origin;
    }

    // ── 유틸 ─────────────────────────────────────────────────────

    static bool IsClientOnly()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && !nm.IsServer;
    }

    static int[] ShuffledIndices(int length)
    {
        int[] arr = new int[length];
        for (int i = 0; i < length; i++) arr[i] = i;
        for (int i = length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
        return arr;
    }
}
