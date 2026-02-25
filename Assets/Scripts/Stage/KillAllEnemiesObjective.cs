using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 목표 2: 지정된 적을 모두 처치하기.
/// enemies[] 를 비우면 씬 전체 Enemy를 자동 수집.
/// 특정 구역 적만 지정하려면 Inspector에서 직접 등록.
/// </summary>
public class KillAllEnemiesObjective : StageObjective
{
    [Header("처치 대상")]
    [Tooltip("처치해야 할 적 목록. 비우면 씬 전체 Enemy 자동 수집")]
    public Enemy[] enemies;

    [Header("Runtime (확인용)")]
    [SerializeField] int _totalCount;
    [SerializeField] int _killedCount;

    public int TotalCount  => _totalCount;
    public int KilledCount => _killedCount;
    public int Remaining   => _totalCount - _killedCount;

    public UnityEvent<int, int> OnKillCountChanged; // (현재 처치 수, 전체 수)

    public override void Begin()
    {
        if (enemies == null || enemies.Length == 0)
            enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);

        _totalCount  = enemies.Length;
        _killedCount = 0;
    }

    public override void Tick()
    {
        if (IsCompleted || IsFailed) return;
        if (enemies == null || enemies.Length == 0) return;

        int killed = 0;
        for (int i = 0; i < enemies.Length; i++)
            if (enemies[i] == null || enemies[i].isDead)
                killed++;

        if (killed != _killedCount)
        {
            _killedCount = killed;
            OnKillCountChanged?.Invoke(_killedCount, _totalCount);
        }

        if (_killedCount >= _totalCount)
            Complete();
    }
}
