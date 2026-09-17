using UnityEngine;

/// <summary>
/// Stage5 Chaser 인원별 스폰 수 테이블.
/// 씬 루트에 1개 배치. Stage5ChaserSpawner가 참조.
///
/// Runner 스폰 수 / 포획 조건은 Stage5TargetObjective의 captureTable에서 페이즈별로 직접 설정.
///
/// [기본값 — TStage5RunnerRedesign.md §1.3]
///  인원   Chaser 스폰
///   1        6   (솔로: 러너가 본인이므로 수를 조금 줄인다)
///   2        8
///   3        8
///   4        8
///  러너 재설계 이후 Chaser는 "러너 1명만" 쫓고 피격 시 소멸→리스폰하므로, 이 값은
///  동시 스폰 수가 아니라 **살아있는 수의 유지 목표치**다(C7에서 스포너가 그렇게 쓴다).
///
/// [Inspector]
///  chaserTable에서 인원별로 직접 수정 가능.
///  playerCount에 해당하는 행이 없으면 마지막 행으로 fallback.
/// </summary>
public class Stage5DifficultyConfig : MonoBehaviour
{
    [System.Serializable]
    public struct ChaserDifficultyRow
    {
        [Tooltip("활성 플레이어 수 (1~4)")]
        public int playerCount;
        [Tooltip("스폰할 Chaser 마릿수")]
        public int spawnCount;
    }

    public static Stage5DifficultyConfig Instance { get; private set; }

    [Header("Chaser 난이도 테이블")]
    [SerializeField] ChaserDifficultyRow[] chaserTable = new ChaserDifficultyRow[]
    {
        new ChaserDifficultyRow { playerCount = 1, spawnCount = 6 },
        new ChaserDifficultyRow { playerCount = 2, spawnCount = 8 },
        new ChaserDifficultyRow { playerCount = 3, spawnCount = 8 },
        new ChaserDifficultyRow { playerCount = 4, spawnCount = 8 },
    };

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── 공개 API ────────────────────────────────────────────────

    public int GetChaserSpawnCount()
    {
        int active = GameSession.Instance != null ? GameSession.Instance.ActivePlayerCount : 4;
        foreach (ChaserDifficultyRow row in chaserTable)
            if (row.playerCount == active) return Mathf.Max(1, row.spawnCount);
        return chaserTable.Length > 0 ? Mathf.Max(1, chaserTable[chaserTable.Length - 1].spawnCount) : 1;
    }
}
