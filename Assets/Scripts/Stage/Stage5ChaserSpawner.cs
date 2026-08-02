using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// Stage5 Chaser 스포너.
///
/// [동작]
///  StartSpawning() — Stage5DifficultyConfig에서 인원별 수를 읽어 스폰.
///                    StageStartGate.OnCountdownComplete Unity 이벤트에 연결.
///  StartSpawning(count) — 직접 마릿수를 지정해 스폰 (외부 호출용).
///  StopAndClear() — 활성 Chaser 전부 제거 (리셋 시).
///
///  spawnPoints[]를 셔플 후 앞에서 count개 선택 → 각 위치에 Chaser 1마리 스폰.
///
/// [네트워크 — Host 전권 시뮬 (TStageNetworkBoard.md §3.2 확정)]
///  OnCountdownComplete는 전 머신에서 로컬로 발동하므로 StartSpawning() 자체가 Host 가드.
///  Host만 Instantiate + NetworkObject.Spawn(). Client는 NGO 수신으로 로컬 복제본 자동 생성.
///  셔플은 NetworkSessionData.Seed 기반 — Host만 쓰는 값이라 재현성·로그 목적.
///
/// [Inspector 설정]
///  chaserPrefab   : Chaser 프리팹 1개 (NetworkObject + 서버 권한 NetworkTransform 필요)
///  spawnPoints    : 스폰 후보 Transform (10개 권장). 1개 이상 필요.
///  spawnSampleRadius : NavMesh 위치 보정 반경(m)
/// </summary>
public class Stage5ChaserSpawner : MonoBehaviour
{
    [Header("Chaser 프리팹")]
    [Tooltip("Chaser 프리팹 1개. 인원에 따라 이 프리팹을 여러 번 스폰한다.")]
    public Stage5ChaserAI chaserPrefab;

    [Header("스폰 위치")]
    [Tooltip("스폰 후보 Transform. 매 스폰마다 셔플 후 앞에서 count개 선택 (중복 없음).\n" +
             "최소 1개. 10개 이상 배치 권장.")]
    public Transform[] spawnPoints;

    [Header("NavMesh 샘플링")]
    [Tooltip("스폰 위치를 NavMesh 위 점으로 보정하는 검색 반경(m)")]
    [SerializeField] float spawnSampleRadius = 3f;

    readonly List<Stage5ChaserAI> _activeChasers = new List<Stage5ChaserAI>();

    // ── 외부 API ────────────────────────────────────────────────

    /// <summary>
    /// StageStartGate.OnCountdownComplete Unity 이벤트에 연결.
    /// Stage5DifficultyConfig에서 인원별 Chaser 수를 읽어 스폰.
    /// Config가 없으면 4인 기본값 사용.
    /// </summary>
    public void StartSpawning()
    {
        int count = Stage5DifficultyConfig.Instance != null
            ? Stage5DifficultyConfig.Instance.GetChaserSpawnCount()
            : 8;

        StartSpawning(count);
    }

    /// <summary>직접 마릿수를 지정해 스폰. Host 전용 — Client는 이 시점 즉시 return.</summary>
    public void StartSpawning(int count)
    {
        CleanupChasers();

        if (!gameObject.activeInHierarchy) return;
        if (IsClientOnly()) return; // Host 전권 스폰 (TStageNetworkBoard.md §3.2)

        if (chaserPrefab == null)
        {
            Debug.LogWarning("[Stage5ChaserSpawner] chaserPrefab이 비어 있습니다.");
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[Stage5ChaserSpawner] spawnPoints가 비어 있습니다.");
            return;
        }

        Player[] players    = FindObjectsByType<Player>(FindObjectsSortMode.None);
        int      actualCount = Mathf.Min(count, spawnPoints.Length);

        const int salt = 0x43484153; // 셔플 결정성용 salt("CHAS") — 값 자체는 Host만 사용
        UnityEngine.Random.InitState(NetworkSessionData.Seed ^ salt);
        int[] indices = ShuffledIndices(spawnPoints.Length);

        for (int i = 0; i < actualCount; i++)
        {
            Transform point = spawnPoints[indices[i]];
            if (point == null) continue;

            Vector3        pos    = SampleNavMeshPos(point.position);
            Stage5ChaserAI chaser = Instantiate(chaserPrefab, pos, Quaternion.identity);
            chaser.Activate(players);
            _activeChasers.Add(chaser);

            NetworkObject netObj = chaser.GetComponent<NetworkObject>();
            if (netObj != null)
                netObj.Spawn(destroyWithScene: true);
            else
                Debug.LogWarning("[Stage5ChaserSpawner] chaserPrefab에 NetworkObject가 없습니다.");
        }

        NetLog.Transition("Stage5ChaserSpawner", "SpawnComplete", $"count={actualCount} seed={NetworkSessionData.Seed}");
    }

    /// <summary>리셋 시 호출.</summary>
    public void StopAndClear()
    {
        CleanupChasers();
    }

    // ── 내부 ────────────────────────────────────────────────────

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

            NetworkObject netObj = c.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
                netObj.Despawn(true);
            else
                Destroy(c.gameObject);
        }
        _activeChasers.Clear();
    }

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

    void OnDisable()
    {
        CleanupChasers();
    }
}
