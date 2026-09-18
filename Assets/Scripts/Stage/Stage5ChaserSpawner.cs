using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// Stage5 Chaser 스포너. **씬에 1개** — 맵과 무관하다.
/// `TStage5RunnerRedesign.md` §1.5가 SSOT.
///
/// [동작 — 2026-09-19 단순화]
///  StartSpawning() — 스폰 지점 **전부**(동서남북 4점)에 1마리씩, **한 번만** 스폰.
///                    러너가 시작 존을 벗어날 때 T5RunnerDirector가 호출한다.
///  StopAndClear()  — 활성 Chaser 전부 제거.
///
///  **리스폰은 없다.** 체이서는 러너를 때리면 스스로 소멸하므로(§1.5) 4마리는 곧
///  "러너를 때릴 기회 4번"이고, 다 소진되면 남은 시간은 무위험 구간이 된다 —
///  의도된 곡선이다. 인원별 마릿수 테이블(구 Stage5DifficultyConfig)도 없다: 솔로도 4마리다.
///
/// [스폰 지점이 맵 밖에 있는 이유]
///  맵 7장이 원점에 겹쳐 있고 start/goal이 통일돼 있으므로, **칸 중앙**에 둔 4점은
///  7장 전부에서 통행 가능한 자리가 된다(미로는 스패닝 트리라 모든 칸이 연결되고 벽은 칸 경계에 선다).
///  그래서 스포너가 맵 자식일 이유가 없고, 체이서가 맵 뽑기와 분리된다.
///  같은 4점이라도 매번 다른 미로에서 출발하므로 암기 대상이 되지 않는다.
///
/// [네트워크 — Host 전권 시뮬 (TStageNetworkBoard.md §3.2)]
///  Host만 Instantiate + NetworkObject.Spawn(). Client는 NGO 수신으로 로컬 복제본 자동 생성.
///
/// [Inspector 설정]
///  chaserPrefab      : Chaser 프리팹 1개 (NetworkObject + 서버 권한 NetworkTransform 필요)
///  spawnPoints       : 동서남북 4점. **칸 중앙**에 둘 것
///  spawnSampleRadius : NavMesh 위치 보정 반경(m)
/// </summary>
public class Stage5ChaserSpawner : MonoBehaviour
{
    [Header("Chaser 프리팹")]
    [Tooltip("Chaser 프리팹 1개. 스폰 지점마다 이 프리팹을 1마리씩 스폰한다.")]
    public Stage5ChaserAI chaserPrefab;

    [Header("스폰 위치")]
    [Tooltip("동서남북 4점. 맵 7장 전부에서 통행 가능하도록 **칸 중앙**에 둘 것.\n" +
             "여기 넣은 자리 전부에 1마리씩 나온다 — 마릿수 = 이 배열 길이다.")]
    public Transform[] spawnPoints;

    [Header("NavMesh 샘플링")]
    [Tooltip("스폰 위치를 NavMesh 위 점으로 보정하는 검색 반경(m)")]
    [SerializeField] float spawnSampleRadius = 3f;

    readonly List<Stage5ChaserAI> _activeChasers = new List<Stage5ChaserAI>();

    bool _spawned;

    /// <summary>이미 스폰했는가. 이탈 트리거가 여러 번 발동해도 한 번만 나오게 하는 데 쓴다.</summary>
    public bool HasSpawned => _spawned;

    // ── 외부 API ────────────────────────────────────────────────

    /// <summary>
    /// 러너가 시작 존을 벗어날 때 T5RunnerDirector가 호출. Host 전용 — Client는 즉시 return.
    /// **맵이 활성화된 뒤에 불려야 한다** — NavMesh가 맵별 NavMeshSurface라 맵이 꺼져 있으면
    /// 샘플링이 전부 실패한다(§2).
    /// </summary>
    public void StartSpawning()
    {
        if (_spawned) return;
        if (!gameObject.activeInHierarchy) return;
        if (IsClientOnly()) return; // Host 전권 스폰 (TStageNetworkBoard.md §3.2)

        if (chaserPrefab == null)
        {
            Debug.LogWarning("[Stage5ChaserSpawner] chaserPrefab이 비어 있습니다.", this);
            return;
        }

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[Stage5ChaserSpawner] spawnPoints가 비어 있습니다.", this);
            return;
        }

        _spawned = true;

        foreach (Transform point in spawnPoints)
            SpawnAt(point);

        NetLog.Transition("Stage5ChaserSpawner", "SpawnComplete", $"count={_activeChasers.Count}");
    }

    /// <summary>스테이지 종료·리셋 시 호출.</summary>
    public void StopAndClear()
    {
        _spawned = false;
        CleanupChasers();
    }

    // ── 내부 ────────────────────────────────────────────────────

    void SpawnAt(Transform point)
    {
        if (point == null || chaserPrefab == null) return;

        Vector3        pos    = SampleNavMeshPos(point.position);
        Stage5ChaserAI chaser = Instantiate(chaserPrefab, pos, Quaternion.identity);

        // 타겟은 체이서가 NV(러너 clientId)로 직접 고른다 — 여기서는 후보 풀만 넘긴다.
        chaser.Activate(FindObjectsByType<Player>(FindObjectsSortMode.None));
        _activeChasers.Add(chaser);

        NetworkObject netObj = chaser.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.Spawn(destroyWithScene: true);
        else
            Debug.LogWarning("[Stage5ChaserSpawner] chaserPrefab에 NetworkObject가 없습니다.", this);
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

        // 맵이 아직 꺼져 있으면 여기로 온다 — 호출 순서를 의심할 것(§2).
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

    void OnDisable()
    {
        CleanupChasers();
    }
}
