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
    int currentPhaseIndex;

    void Awake()
    {
        tiles = GetComponentsInChildren<FloorTile>(true);
        currentPhaseIndex = 0;
    }

    void Update()
    {
        CheckPhase();

        if (Time.time < nextTime) return;
        nextTime = Time.time + changeInterval;

        RandomizeTiles();
    }

    void CheckPhase()
    {
        if (phases == null || currentPhaseIndex >= phases.Length) return;

        while (currentPhaseIndex < phases.Length &&
               Time.timeSinceLevelLoad >= phases[currentPhaseIndex].triggerTime)
        {
            changeInterval = phases[currentPhaseIndex].changeInterval;
            keepBWRatio = phases[currentPhaseIndex].keepBWRatio;
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