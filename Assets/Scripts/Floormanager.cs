using UnityEngine;
using System.Collections.Generic;

public class FloorManager : MonoBehaviour
{
    public float changeInterval = 2f;

    [Range(0f, 1f)] public float keepBWRatio = 0.8f; // 흑/백 유지 비율 (0.8이면 80%는 흑/백)
    FloorTile[] tiles;

    float nextTime;

    void Awake()
    {
        tiles = GetComponentsInChildren<FloorTile>(true);
    }

    void Update()
    {
        if (Time.time < nextTime) return;
        nextTime = Time.time + changeInterval;

        RandomizeTiles();
    }

    void RandomizeTiles()
    {
        if (tiles == null || tiles.Length == 0) return;

        for (int i = 0; i < tiles.Length; i++)
        {
            bool useBW = Random.value < keepBWRatio;

            if (useBW)
            {
                // 흑/백 랜덤
                tiles[i].SetType(Random.value < 0.5f ? FloorTile.ColorType.Black : FloorTile.ColorType.White);
            }
            else
            {
                // 초록
                tiles[i].SetType(FloorTile.ColorType.Reveal);
            }
        }
    }
}