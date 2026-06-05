using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pioneer Path 구역 하나 (예: 5×5).
/// 자식 PioneerPathTile을 수집·관리.
/// PioneerPathManager.Awake()가 Init()을 자동 호출.
///
/// [계층 구조]
///  PioneerPathManager
///  └── PioneerPathZone  ← 이 컴포넌트
///      ├── PioneerPathTile (Path)
///      ├── PioneerPathTile (Trap)
///      └── ...
/// </summary>
public class PioneerPathZone : MonoBehaviour
{
    [Header("구역 설정")]
    [Tooltip("이 구역을 먼저 개척해야 하는 플레이어 고유색")]
    public PlayerColorType pioneerColor = PlayerColorType.Green;

    [Header("미리보기 색상")]
    [Tooltip("미리보기 때 이 구역 Path 타일에 적용할 색")]
    public Color previewColor = Color.green;

    [HideInInspector] public Color normalColor   = new Color(0.45f, 0.45f, 0.45f);
    [HideInInspector] public Color unlockedColor = new Color(0.27f, 1f,    0.27f);
    [HideInInspector] public Color trapColor     = new Color(1f,    0.2f,  0.2f);

    PioneerPathManager _manager;
    PioneerPathTile[]  _pathTiles;   // Path 타일만
    PioneerPathTile[]  _allTiles;    // Path + Trap 전체

    public PlayerColorType     PioneerColor => pioneerColor;
    public PioneerPathManager  Manager      => _manager;

    // ── 초기화 (PioneerPathManager.Awake에서 호출) ───────────────

    public void Init(PioneerPathManager manager, Color normal, Color unlocked, Color trap)
    {
        _manager     = manager;
        normalColor  = normal;
        unlockedColor = unlocked;
        trapColor    = trap;

        _allTiles = GetComponentsInChildren<PioneerPathTile>(true);
        var pathList = new List<PioneerPathTile>();

        for (int i = 0; i < _allTiles.Length; i++)
        {
            if (_allTiles[i] == null) continue;
            _allTiles[i].zone         = this;
            _allTiles[i].normalColor  = normalColor;
            _allTiles[i].unlockedColor = unlockedColor;
            _allTiles[i].trapColor    = trapColor;

            if (_allTiles[i].tileType == PioneerPathTile.TileType.Path)
                pathList.Add(_allTiles[i]);
        }
        _pathTiles = pathList.ToArray();
    }

    // ── 상태 전환 (PioneerPathManager에서 호출) ──────────────────

    /// <summary>미리보기: 이 구역 Path 타일만 발광</summary>
    public void ShowPreview()
    {
        if (_pathTiles == null) return;
        for (int i = 0; i < _pathTiles.Length; i++)
            if (_pathTiles[i] != null) _pathTiles[i].ShowPreview(previewColor);
    }

    /// <summary>미리보기 종료: 모든 타일 normalColor 복귀</summary>
    public void HidePreview()
    {
        if (_pathTiles == null) return;
        for (int i = 0; i < _pathTiles.Length; i++)
            if (_pathTiles[i] != null) _pathTiles[i].HidePreview();
    }

    /// <summary>전체 초기화</summary>
    public void Restore()
    {
        if (_allTiles == null) return;
        for (int i = 0; i < _allTiles.Length; i++)
            if (_allTiles[i] != null) _allTiles[i].Restore();
    }
}
