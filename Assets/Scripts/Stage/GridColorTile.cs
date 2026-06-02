using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 5×5 고유색 보드의 칸 하나. 씬에 25개 미리 배치.
/// GridColorChallenge가 라운드마다 Default / SafeBlue / SafePurple / SafeGreen / SafeYellow 를 설정합니다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class GridColorTile : MonoBehaviour
{
    public enum TileState
    {
        Default    = 0,
        SafeBlue   = 1,
        SafePurple = 2,
        SafeGreen  = 3,
        SafeYellow = 4,
    }

    [Header("보드")]
    [Tooltip("0~24. 비어 있으면 형제 순서로 Challenge가 자동 부여")]
    [SerializeField] int gridIndex = 0;

    [Header("머티리얼")]
    [SerializeField] Material materialDefault;
    [SerializeField] Material materialBlue;
    [SerializeField] Material materialPurple;
    [SerializeField] Material materialGreen;
    [SerializeField] Material materialYellow;

    readonly HashSet<Player> _occupants = new HashSet<Player>();

    Renderer  _renderer;
    TileState _state = TileState.Default;

    public int GridIndex => gridIndex;
    public TileState State => _state;
    public bool IsSafe => _state != TileState.Default;

    public void SetGridIndex(int index) => gridIndex = index;

    /// <summary>이 안전 칸에 대응하는 PlayerColorType. Default 칸이면 Common 반환.</summary>
    public PlayerColorType RequiredColorType => _state switch
    {
        TileState.SafeBlue   => PlayerColorType.Blue,
        TileState.SafePurple => PlayerColorType.Purple,
        TileState.SafeGreen  => PlayerColorType.Green,
        TileState.SafeYellow => PlayerColorType.Yellow,
        _                    => PlayerColorType.Common,
    };

    public bool ContainsPlayer(Player p) => p != null && _occupants.Contains(p);

    public IReadOnlyCollection<Player> Occupants => _occupants;

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        _renderer = GetComponent<Renderer>();
        if (_renderer == null)
            _renderer = GetComponentInChildren<Renderer>();
    }

    public void SetState(TileState state)
    {
        _state = state;
        ApplyMaterial();
    }

    void ApplyMaterial()
    {
        if (_renderer == null) return;

        Material m = _state switch
        {
            TileState.SafeBlue   => materialBlue,
            TileState.SafePurple => materialPurple,
            TileState.SafeGreen  => materialGreen,
            TileState.SafeYellow => materialYellow,
            _                    => materialDefault,
        };

        if (m != null)
            _renderer.sharedMaterial = m;
    }

    void OnTriggerEnter(Collider other)
    {
        Player p = other.GetComponentInParent<Player>();
        if (p == null || p.IsDead) return;
        _occupants.Add(p);
    }

    void OnTriggerStay(Collider other)
    {
        Player p = other.GetComponentInParent<Player>();
        if (p == null || p.IsDead) return;
        _occupants.Add(p);
    }

    void OnTriggerExit(Collider other)
    {
        Player p = other.GetComponentInParent<Player>();
        if (p == null) return;
        _occupants.Remove(p);
    }

    /// <summary>사망·리셋 시 유령 점유 제거.</summary>
    public void RefreshOccupants()
    {
        if (_occupants.Count == 0) return;
        _occupants.RemoveWhere(p => p == null || p.IsDead);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = IsSafe ? Color.cyan : Color.gray;
        Gizmos.DrawWireCube(transform.position, transform.lossyScale * 1.02f);
    }
}
