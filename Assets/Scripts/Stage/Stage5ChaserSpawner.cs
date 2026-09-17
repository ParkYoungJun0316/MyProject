using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// Stage5 Chaser 스포너. 맵(Map_XX)마다 1개.
/// `TStage5RunnerRedesign.md` §1.3이 SSOT.
///
/// [동작 — 러너 재설계 2026-09-18]
///  StartSpawning()  — Stage5DifficultyConfig에서 인원별 **유지 목표치**를 읽어 그만큼 스폰.
///                     라운드 시작 3초 유예 후 T5RunnerRoundDirector가 호출한다.
///  StopAndClear()   — 활성 Chaser 전부 제거 (라운드 종료·리셋 시).
///
///  스폰 후에는 **살아있는 수를 유지**한다. 체이서는 러너를 때리면 스스로 소멸하므로(§1.3),
///  Host가 매 프레임 살아있는 수를 세어 부족하면 respawnDelay(6초) 뒤에 1마리씩 다시 채운다.
///  리스폰 지점은 **러너에서 minRunnerDistance(30m) 이상** 떨어진 후보 중에서 고른다 —
///  바로 옆에서 튀어나오면 피할 수가 없다.
///
/// [네트워크 — Host 전권 시뮬 (TStageNetworkBoard.md §3.2 확정)]
///  Host만 Instantiate + NetworkObject.Spawn(). Client는 NGO 수신으로 로컬 복제본 자동 생성.
///  유지·리스폰 판정도 전부 Host 레인 — Client는 이 클래스의 Update를 타지 않는다.
///  최초 셔플은 NetworkSessionData.Seed 기반(재현성·로그 목적, Host만 쓰는 값).
///
/// [Inspector 설정]
///  chaserPrefab   : Chaser 프리팹 1개 (NetworkObject + 서버 권한 NetworkTransform 필요)
///  spawnPoints    : 스폰 후보 Transform (맵당 16개). 1개 이상 필요.
///  spawnSampleRadius : NavMesh 위치 보정 반경(m)
/// </summary>
public class Stage5ChaserSpawner : MonoBehaviour
{
    [Header("Chaser 프리팹")]
    [Tooltip("Chaser 프리팹 1개. 인원에 따라 이 프리팹을 여러 번 스폰한다.")]
    public Stage5ChaserAI chaserPrefab;

    [Header("스폰 위치")]
    [Tooltip("스폰 후보 Transform. 최초 스폰은 셔플 후 앞에서 count개(중복 없음),\n" +
             "리스폰은 러너에서 충분히 떨어진 후보 중 랜덤.")]
    public Transform[] spawnPoints;

    [Header("NavMesh 샘플링")]
    [Tooltip("스폰 위치를 NavMesh 위 점으로 보정하는 검색 반경(m)")]
    [SerializeField] float spawnSampleRadius = 3f;

    [Header("리스폰 (§1.3)")]
    [Tooltip("체이서가 소멸한 뒤 다시 채우기까지의 시간(초)")]
    [SerializeField] float respawnDelay = 6f;

    [Tooltip("리스폰 지점이 러너에게서 떨어져 있어야 하는 최소 거리(m)")]
    [SerializeField] float minRunnerDistance = 30f;

    readonly List<Stage5ChaserAI> _activeChasers = new List<Stage5ChaserAI>();

    // Host 레인 전용 유지 상태
    bool _maintaining;
    int  _targetCount;

    // 소멸한 체이서 1마리당 "다시 채울 시각" 하나. 큐로 두는 이유는 같은 프레임에 2마리가
    // 사라졌을 때 타이머 하나를 돌려쓰면 두 번째가 respawnDelay의 2배만큼 늦게 나오기 때문.
    readonly List<float> _respawnDueAt = new List<float>();

    // ── 외부 API ────────────────────────────────────────────────

    /// <summary>
    /// 라운드 시작 유예 후 T5RunnerRoundDirector가 호출.
    /// Stage5DifficultyConfig에서 인원별 유지 목표치를 읽어 스폰한다. Config가 없으면 8.
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

        int actualCount = Mathf.Min(count, spawnPoints.Length);

        const int salt = 0x43484153; // 셔플 결정성용 salt("CHAS") — 값 자체는 Host만 사용
        UnityEngine.Random.InitState(NetworkSessionData.Seed ^ salt);
        int[] indices = ShuffledIndices(spawnPoints.Length);

        for (int i = 0; i < actualCount; i++)
            SpawnAt(spawnPoints[indices[i]]);

        // 이후부터는 살아있는 수를 유지한다.
        _targetCount = actualCount;
        _maintaining = true;
        _respawnDueAt.Clear();

        NetLog.Transition("Stage5ChaserSpawner", "SpawnComplete", $"count={actualCount} seed={NetworkSessionData.Seed}");
    }

    /// <summary>라운드 종료·리셋 시 호출. 유지도 함께 멈춘다.</summary>
    public void StopAndClear()
    {
        _maintaining = false;
        _respawnDueAt.Clear();
        CleanupChasers();
    }

    // ── 살아있는 수 유지 (Host 레인) ────────────────────────────

    void Update()
    {
        if (!_maintaining || IsClientOnly()) return;

        // 소멸한(Despawn된) 체이서를 걷어내고, 사라진 수만큼 리스폰 예약을 건다.
        int before = _activeChasers.Count;
        for (int i = _activeChasers.Count - 1; i >= 0; i--)
            if (_activeChasers[i] == null) _activeChasers.RemoveAt(i);

        for (int i = _activeChasers.Count; i < before; i++)
            _respawnDueAt.Add(Time.time + respawnDelay);

        if (_respawnDueAt.Count == 0) return;

        for (int i = _respawnDueAt.Count - 1; i >= 0; i--)
        {
            if (Time.time < _respawnDueAt[i]) continue;

            // 목표치를 이미 채웠다면(수동 스폰 등) 예약만 버린다.
            if (_activeChasers.Count >= _targetCount) { _respawnDueAt.RemoveAt(i); continue; }

            Transform point = PickRespawnPoint();
            if (point == null) continue; // 조건에 맞는 자리가 없으면 예약을 남겨 다음 프레임에 재시도

            SpawnAt(point);
            _respawnDueAt.RemoveAt(i);
        }
    }

    /// <summary>
    /// 러너에서 minRunnerDistance 이상 떨어진 후보 중 랜덤. 러너를 못 찾으면 거리 조건을 버리고
    /// 아무 후보나 쓴다(라운드 밖·명단 미확보 같은 예외 상황에서 리스폰이 영영 멈추지 않도록).
    /// </summary>
    Transform PickRespawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return null;

        Vector3? runnerPos = RunnerPosition();
        var candidates = new List<Transform>();

        foreach (Transform t in spawnPoints)
        {
            if (t == null) continue;
            if (runnerPos != null &&
                (t.position - runnerPos.Value).sqrMagnitude < minRunnerDistance * minRunnerDistance)
                continue;
            candidates.Add(t);
        }

        if (candidates.Count == 0)
        {
            if (runnerPos == null) return null;
            // 러너가 맵 한가운데라 30m 밖 후보가 하나도 없는 경우 — 가장 먼 자리로 타협한다.
            Transform farthest  = null;
            float     bestDistSq = -1f;
            foreach (Transform t in spawnPoints)
            {
                if (t == null) continue;
                float d = (t.position - runnerPos.Value).sqrMagnitude;
                if (d > bestDistSq) { bestDistSq = d; farthest = t; }
            }
            return farthest;
        }

        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    Vector3? RunnerPosition()
    {
        var net = StageNetworkState.Instance;
        var nm  = NetworkManager.Singleton;
        if (net == null || nm == null || net.T5CurrentRound < 0) return null;

        ulong runnerId = net.T5CurrentRunnerClientId;
        if (!nm.ConnectedClients.TryGetValue(runnerId, out NetworkClient client)) return null;
        if (client.PlayerObject == null) return null;

        return client.PlayerObject.transform.position;
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
            Debug.LogWarning("[Stage5ChaserSpawner] chaserPrefab에 NetworkObject가 없습니다.");
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
        _maintaining = false;
        _respawnDueAt.Clear();
        CleanupChasers();
    }
}
