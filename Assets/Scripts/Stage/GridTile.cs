using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 5×5 혼합판(Grid) 보드의 칸 하나. 씬에 25개 미리 배치.
/// GridColorTile/GridBWTile 통합 — 고유색 4종 + 흑/백을 같은 칸 타입으로 다룬다
/// (`CoopStageAudit.M.md` §8, 2026-09-11 확정).
/// State는 PlayerColorType을 그대로 재사용: Common = Default(안전 칸 아님),
/// Blue/Purple/Green/Yellow = 고유색 안전 칸, Black/White = 흑백 안전 칸.
/// GridChallenge가 라운드마다 SetState()로 배정한다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class GridTile : MonoBehaviour
{
    [Header("보드")]
    [Tooltip("0~24. 비어 있으면 형제 순서로 Challenge가 자동 부여")]
    [SerializeField] int gridIndex = 0;

    [Header("머티리얼")]
    [SerializeField] Material materialDefault;
    [SerializeField] Material materialBlue;
    [SerializeField] Material materialPurple;
    [SerializeField] Material materialGreen;
    [SerializeField] Material materialYellow;
    [SerializeField] Material materialBlack;
    [SerializeField] Material materialWhite;

    readonly HashSet<Player> _occupants = new HashSet<Player>();

    Renderer _renderer;
    PlayerColorType _state = PlayerColorType.Common;

    public int GridIndex => gridIndex;

    /// <summary>Common = 안전 칸 아님. Blue/Purple/Green/Yellow = 고유색. Black/White = 흑백.</summary>
    public PlayerColorType State => _state;

    public bool IsSafe => _state != PlayerColorType.Common;

    public void SetGridIndex(int index) => gridIndex = index;

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

    public void SetState(PlayerColorType state)
    {
        _state = state;
        ApplyMaterial();
    }

    void ApplyMaterial()
    {
        if (_renderer == null) return;

        Material m = _state switch
        {
            PlayerColorType.Blue   => materialBlue,
            PlayerColorType.Purple => materialPurple,
            PlayerColorType.Green  => materialGreen,
            PlayerColorType.Yellow => materialYellow,
            PlayerColorType.Black  => materialBlack,
            PlayerColorType.White  => materialWhite,
            _                      => materialDefault,
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
