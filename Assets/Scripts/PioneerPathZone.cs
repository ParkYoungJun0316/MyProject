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

    // 런타임 pioneer 색 (GameSession 기준 교체, 4인이면 pioneerColor와 동일)
    PlayerColorType _effectivePioneerColor;
    Color           _effectivePreviewColor;

    /// <summary>이번 라운드에 실제로 적용되는 pioneer 색 (타일 판정 기준)</summary>
    public PlayerColorType     EffectivePioneerColor => _effectivePioneerColor;
    public PioneerPathManager  Manager               => _manager;

    /// <summary>이 구역의 Path 타일만(Trap 제외) — PioneerPathManager가 네트워크 index 배정에 사용.</summary>
    public PioneerPathTile[]   PathTiles             => _pathTiles;

    // ── 초기화 (PioneerPathManager.Awake에서 호출) ───────────────

    public void Init(PioneerPathManager manager, Color normal, Color unlocked, Color trap)
    {
        _manager      = manager;
        normalColor   = normal;
        unlockedColor = unlocked;
        trapColor     = trap;

        // Inspector 값으로 초기화 (4인 모드 또는 AssignPioneerColors 호출 전 fallback)
        _effectivePioneerColor = pioneerColor;
        _effectivePreviewColor = previewColor;

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

    // ── 외부 호출 (PioneerPathManager에서 호출) ──────────────────

    /// <summary>
    /// GameSession 활성색 기준으로 런타임 pioneer 색과 미리보기 색을 교체.
    /// PioneerPathManager.AssignPioneerColors()에서 호출.
    /// </summary>
    public void SetEffectivePioneer(PlayerColorType color, Color preview)
    {
        _effectivePioneerColor = color;
        _effectivePreviewColor = preview;
    }

    // ── 상태 전환 (PioneerPathManager에서 호출) ──────────────────

    /// <summary>미리보기: 이 구역 Path 타일만 발광 (effective 색 기준)</summary>
    public void ShowPreview()
    {
        if (_pathTiles == null) return;
        for (int i = 0; i < _pathTiles.Length; i++)
            if (_pathTiles[i] != null) _pathTiles[i].ShowPreview(_effectivePreviewColor);
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
