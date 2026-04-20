using UnityEngine;
using System.Collections.Generic;

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
        _isRunning        = true;
        _elapsedTime      = 0f;
        nextTime          = 0f;
        currentPhaseIndex = 0;
    }

    void Update()
    {
        if (!_isRunning) return;

        _elapsedTime += Time.deltaTime;
        CheckPhase();

        if (Time.time < nextTime) return;
        nextTime = Time.time + changeInterval;

        RandomizeTiles();
    }

    void CheckPhase()
    {
        if (phases == null || currentPhaseIndex >= phases.Length) return;

        // Time.timeSinceLevelLoad 대신 트리거 시점 기준 경과 시간 사용
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

        for (int i = 0; i < tiles.Length; i++)
        {
            bool useBW = Random.value < keepBWRatio;

            if (useBW)
            {
                tiles[i].SetType(Random.value < 0.5f ? FloorTile.ColorType.Black : FloorTile.ColorType.White);
            }
            else
            {
                tiles[i].SetType(FloorTile.ColorType.Reveal);
            }
        }
    }
}
