using Unity.Netcode;
using UnityEngine;

public class FloorManager : MonoBehaviour
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

    StageNetworkState _netState;

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

    void Start()
    {
        // StageNetworkState.Awake()가 이 컴포넌트의 Start()보다 먼저 실행되는 것을
        // Unity 전역 Awake→Start 순서로 보장받음 (OX/GridBW 등 다른 축과 동일 전제).
        _netState = StageNetworkState.Instance;
        if (_netState != null)
            _netState.OnFloorRollChanged += HandleFloorRollChanged;
    }

    void OnDestroy()
    {
        if (_netState != null)
            _netState.OnFloorRollChanged -= HandleFloorRollChanged;
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

    /// <summary>Client/Host 공통. Host 레인 여부만 다르게 취급.</summary>
    static bool IsClientOnly()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && !nm.IsServer;
    }

    /// <summary>StageManager.StartStage()에서 호출. 타이머를 0부터 시작. Host 레인만 진행.</summary>
    public void StartFloor()
    {
        if (IsClientOnly()) return;

        _isRunning        = true;
        _elapsedTime      = 0f;
        nextTime          = 0f;
        currentPhaseIndex = 0;
    }

    void Update()
    {
        if (!_isRunning) return;

        // Client는 타이머 진행을 전혀 하지 않음 — 타일 결과는 HandleFloorRollChanged로만
        // 반영된다(§11A 이중 계산 금지, SequenceRing에서 이미 겪은 것과 동일 원칙).
        if (IsClientOnly()) return;

        _elapsedTime += Time.deltaTime;
        CheckPhase();

        if (Time.time < nextTime) return;
        nextTime = Time.time + changeInterval;

        RollTiles();
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

    /// <summary>
    /// Host 전용: 새 시드를 뽑아 StageNetworkState에 배포만 한다(로컬 적용 없음).
    /// 실제 타일 색 계산은 전 머신 공통인 HandleFloorRollChanged가 담당 —
    /// Host도 자기 자신의 NV 콜백을 통해 동일하게 적용된다.
    /// </summary>
    void RollTiles()
    {
        if (tiles == null || tiles.Length == 0) return;

        int seed = Random.Range(int.MinValue, int.MaxValue);
        _netState?.FloorRoll(seed, keepBWRatio);
    }

    /// <summary>
    /// Host/Client 공통: 시드로 로컬 재생성. 전역 UnityEngine.Random을 건드리지 않도록
    /// System.Random만 사용(OX RegenerateQuestionOrder와 동일 원칙).
    /// </summary>
    void HandleFloorRollChanged(FloorRollState state)
    {
        if (tiles == null || tiles.Length == 0) return;

        var rng = new System.Random(state.seed);
        for (int i = 0; i < tiles.Length; i++)
        {
            bool useBW = rng.NextDouble() < state.keepBWRatio;
            FloorTile.ColorType t = useBW
                ? (rng.NextDouble() < 0.5 ? FloorTile.ColorType.Black : FloorTile.ColorType.White)
                : FloorTile.ColorType.Reveal;
            tiles[i].SetType(t);
        }
    }
}
