using Unity.Netcode;
using UnityEngine;

public class FloorManager : NetworkBehaviour
{
    [System.Serializable]
    public struct FloorPhase
    {
        [Tooltip("씬 시작 후 이 페이즈로 전환될 경과 시간(초)")]
        public float triggerTime;
        [Tooltip("전환 후 타일 변경 간격(초)")]
        public float changeInterval;
        [Range(0f, 1f), Tooltip("전환 후 흑/백 유지 비율")]
        public float keepBWRatio;
    }

    [Header("초기 설정")]
    public float changeInterval = 0f;
    [Range(0f, 1f)] public float keepBWRatio = 0f;

    [Header("페이즈 설정")]
    [Tooltip("triggerTime 오름차순으로 설정하세요")]
    public FloorPhase[] phases;

    FloorTile[] tiles;
    float nextTime;
    int   currentPhaseIndex;

    bool  _isRunning;
    float _elapsedTime;
    bool  _hasStageManager;

    void Awake()
    {
        tiles = GetComponentsInChildren<FloorTile>(true);

        StageManager sm = GetComponentInParent<StageManager>();
        if (sm != null)
        {
            _hasStageManager = true;
            sm.RegisterFloor(this);
        }
    }

    void OnEnable()
    {
        // StageManager 자식이 아닐 때만 활성화 즉시 자동 시작
        if (!_hasStageManager)
            StartFloor();
    }

    void OnDisable()
    {
        _isRunning = false;
    }

    /// <summary>StageManager.StartStage()에서 호출. 타이머를 0부터 시작.</summary>
    public void StartFloor()
    {
        // Client는 타일을 SyncTilesClientRpc로만 수신 — 로컬 타이머 불필요
        if (IsMultiplayer() && !IsServer) return;

        _isRunning        = true;
        _elapsedTime      = 0f;
        nextTime          = 0f;
        currentPhaseIndex = 0;
    }

    void Update()
    {
        if (!_isRunning) return;

        // Client는 SyncTilesClientRpc로만 타일 상태를 수신 — 모든 로컬 연산 건너뜀
        if (IsMultiplayer() && !IsServer) return;

        _elapsedTime += Time.deltaTime;
        CheckPhase();

        if (Time.time < nextTime) return;
        nextTime = Time.time + changeInterval;

        RandomizeTiles();
    }

    void CheckPhase()
    {
        if (phases == null || currentPhaseIndex >= phases.Length) return;

        while (currentPhaseIndex < phases.Length &&
               _elapsedTime >= phases[currentPhaseIndex].triggerTime)
        {
            changeInterval = phases[currentPhaseIndex].changeInterval;
            keepBWRatio    = phases[currentPhaseIndex].keepBWRatio;
            currentPhaseIndex++;
        }
    }

    void RandomizeTiles()
    {
        if (tiles == null || tiles.Length == 0) return;

        byte[] states = new byte[tiles.Length];
        for (int i = 0; i < tiles.Length; i++)
        {
            bool useBW = Random.value < keepBWRatio;
            FloorTile.ColorType t = useBW
                ? (Random.value < 0.5f ? FloorTile.ColorType.Black : FloorTile.ColorType.White)
                : FloorTile.ColorType.Reveal;
            tiles[i].SetType(t);
            states[i] = (byte)t;
        }

        // 멀티 중일 때만 Client에 전파
        if (IsMultiplayer())
            SyncTilesClientRpc(states);
    }

    /// <summary>
    /// Host가 결정한 타일 상태 배열을 Client에 전파.
    /// Host(IsServer)는 RandomizeTiles()에서 이미 적용했으므로 건너뜀.
    /// </summary>
    [ClientRpc]
    void SyncTilesClientRpc(byte[] states)
    {
        if (IsServer) return;

        for (int i = 0; i < tiles.Length && i < states.Length; i++)
            tiles[i].SetType((FloorTile.ColorType)states[i]);
    }

    /// <summary>NGO가 활성화된 멀티플레이 세션인지 확인. 솔로(오프라인) 모드 구분용.</summary>
    bool IsMultiplayer() =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
}
