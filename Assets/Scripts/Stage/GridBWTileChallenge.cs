using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 5×5 흑/백 보드 챌린지 (모드 A).
///
/// [라운드]
///  - 25칸 중 1칸만 Safe(흑 또는 백 랜덤), 나머지 Default
///  - roundDuration(기본 3초) 후 정산: Default 위 1명이라도 → 팀 데미지
///  - 정산 시 생존 플레이어 전원이 안전 칸 + BW 일치 아니면 → 팀 데미지
///  - 성공 시 데미지 없이 쿨타임 후 다음 랜덤 칸 (totalRounds회)
///
/// [데미지]
///  - Default 정산 / 라운드 실패: 팀 전체 (Inspector)
///  - DropTrap: 개별 (별도 씬 설정, TrapPlayerTracker Layer Mask = Nothing 권장)
///
/// [씬]
///  - 자식에 GridBWTile 25개 + Collider(Is Trigger)
///  - 안전 칸은 4인 수용을 위해 트리거 확대 가능
///
/// [시작]
///  - autoStart = true: 씬 로드 후 자동 Activate() (PlayerTriggerZone 불필요)
///  - autoStart = false: Activate()를 트리거·StageStartGate 등에 연결
/// </summary>
public class GridBWTileChallenge : MonoBehaviour
{
    public const int ExpectedTileCount = 25;

    [Header("타일 (비우면 자식 GridBWTile 자동 수집)")]
    [SerializeField] GridBWTile[] tiles = new GridBWTile[0];

    [Header("시작 설정")]
    [Tooltip("true: 씬 로드 후 자동 Activate()\n" +
             "false: Activate()를 PlayerTriggerZone·StageStartGate 등에 연결")]
    [SerializeField] bool autoStart = false;

    [Tooltip("autoStart=true일 때 Activate()까지 대기(초). 0이면 Start() 직후")]
    [SerializeField] float autoStartDelay = 0f;

    [Header("라운드")]
    [Tooltip("한 라운드 제한 시간(초). 끝에 한 번 판정")]
    [SerializeField] float roundDuration = 0f;

    [Tooltip("Activate() 1회 시 진행할 라운드 수")]
    [SerializeField] int totalRounds = 0;

    [Tooltip("라운드 사이 대기(초). 0이면 즉시 다음 라운드")]
    [SerializeField] float cooldownBetweenRounds = 0f;

    [Header("플레이어")]
    [Tooltip("0이면 생존한 모든 Player를 검사. 4인 플레이 시 4 권장")]
    [SerializeField] int requiredAliveCount = 0;

    [Header("정산 데미지 (팀)")]
    [Tooltip("정산 시 1명이라도 Default 칸에 있으면 생존자 전원에게 적용")]
    [SerializeField] int teamDamageIfAnyOnDefault = 0;

    [Tooltip("정산 시 안전 칸+BW 조건 미달 시 생존자 전원에게 적용")]
    [SerializeField] int teamDamageIfRoundFail = 0;

    [Header("이벤트")]
    public UnityEvent OnChallengeStarted;
    public UnityEvent OnChallengeComplete;
    public UnityEvent OnChallengeCancelled;

    [Tooltip("라운드 인덱스(0부터)")]
    public UnityEvent<int> OnRoundStarted;

    [Tooltip("라운드 인덱스, 성공 여부")]
    public UnityEvent<int, bool> OnRoundSettled;

    bool _isRunning;
    int _currentSafeIndex = -1;
    bool _currentSafeIsBlack;
    Coroutine _routine;

    public bool IsRunning => _isRunning;
    public int CurrentRoundIndex { get; private set; }
    public int CurrentSafeGridIndex => _currentSafeIndex;

    void Awake()
    {
        if (tiles == null || tiles.Length == 0)
            CollectTilesFromChildren();
    }

    void Start()
    {
        if (!autoStart) return;

        if (autoStartDelay > 0f)
            StartCoroutine(AutoStartRoutine());
        else
            Activate();
    }

    IEnumerator AutoStartRoutine()
    {
        yield return new WaitForSeconds(autoStartDelay);
        Activate();
    }

    void CollectTilesFromChildren()
    {
        GridBWTile[] found = GetComponentsInChildren<GridBWTile>(true);
        System.Array.Sort(found, (a, b) => a.GridIndex.CompareTo(b.GridIndex));

        for (int i = 0; i < found.Length; i++)
            found[i].SetGridIndex(i);

        tiles = found;
    }

    /// <summary>챌린지 시작 (totalRounds회). 이미 실행 중이면 무시.</summary>
    public void Activate()
    {
        if (_isRunning) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ChallengeRoutine());
    }

    /// <summary>진행 중단 + 모든 칸 Default.</summary>
    public void Cancel()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        _isRunning = false;
        SetAllTilesDefault();
        OnChallengeCancelled?.Invoke();
    }

    IEnumerator ChallengeRoutine()
    {
        if (tiles == null || tiles.Length == 0)
            CollectTilesFromChildren();

        if (tiles.Length == 0)
        {
            Debug.LogWarning("[GridBWTileChallenge] GridBWTile이 없습니다.", this);
            yield break;
        }

        if (tiles.Length != ExpectedTileCount)
            Debug.LogWarning($"[GridBWTileChallenge] 타일 수 {tiles.Length} (권장 {ExpectedTileCount}).", this);

        if (totalRounds <= 0)
        {
            Debug.LogWarning("[GridBWTileChallenge] totalRounds는 1 이상이어야 합니다.", this);
            yield break;
        }

        if (roundDuration <= 0f)
        {
            Debug.LogWarning("[GridBWTileChallenge] roundDuration은 0보다 커야 합니다.", this);
            yield break;
        }

        _isRunning = true;
        OnChallengeStarted?.Invoke();

        for (int round = 0; round < totalRounds; round++)
        {
            CurrentRoundIndex = round;
            PickRandomSafeTile();
            ApplyTileStates();
            OnRoundStarted?.Invoke(round);

            yield return new WaitForSeconds(roundDuration);

            // 정산 시점에 한 번만 수집해서 EvaluateRound·ApplySettlement에 동일한 리스트 전달
            List<Player> aliveAtSettlement = GatherAlivePlayers();
            bool anyOnDefault = EvaluateRound(aliveAtSettlement, out bool roundSuccess);
            ApplySettlement(anyOnDefault, roundSuccess, aliveAtSettlement);
            OnRoundSettled?.Invoke(round, roundSuccess);

            SetAllTilesDefault();

            if (round < totalRounds - 1 && cooldownBetweenRounds > 0f)
                yield return new WaitForSeconds(cooldownBetweenRounds);
        }

        _isRunning = false;
        _currentSafeIndex = -1;
        OnChallengeComplete?.Invoke();
        _routine = null;
    }

    void PickRandomSafeTile()
    {
        _currentSafeIndex = Random.Range(0, tiles.Length);
        _currentSafeIsBlack = Random.value >= 0.5f;
    }

    void ApplyTileStates()
    {
        for (int i = 0; i < tiles.Length; i++)
        {
            if (tiles[i] == null) continue;

            if (i == _currentSafeIndex)
            {
                tiles[i].SetState(_currentSafeIsBlack
                    ? GridBWTile.TileState.SafeBlack
                    : GridBWTile.TileState.SafeWhite);
            }
            else
            {
                tiles[i].SetState(GridBWTile.TileState.Default);
            }
        }
    }

    void SetAllTilesDefault()
    {
        foreach (GridBWTile t in tiles)
        {
            if (t != null)
                t.SetState(GridBWTile.TileState.Default);
        }
    }

    /// <summary>정산 시점: Default 칸 점유 여부 + 라운드 성공 여부.</summary>
    bool EvaluateRound(List<Player> alive, out bool roundSuccess)
    {
        roundSuccess = false;

        int required = requiredAliveCount > 0 ? requiredAliveCount : alive.Count;

        if (required <= 0)
            return false;

        foreach (GridBWTile t in tiles)
            t?.RefreshOccupants();

        bool anyOnDefault = false;

        foreach (Player p in alive)
        {
            GridBWTile tile = GetDominantTileForPlayer(p);
            if (tile == null || tile.State == GridBWTile.TileState.Default)
                anyOnDefault = true;
        }

        if (alive.Count < required)
        {
            roundSuccess = false;
            return anyOnDefault;
        }

        roundSuccess = CheckAllPlayersOnSafeWithMatchingBw(alive);
        return anyOnDefault;
    }

    bool CheckAllPlayersOnSafeWithMatchingBw(List<Player> alive)
    {
        GridBWTile safeTile = null;

        foreach (Player p in alive)
        {
            GridBWTile tile = GetDominantTileForPlayer(p);
            if (tile == null || !tile.IsSafe)
                return false;

            if (safeTile == null)
                safeTile = tile;
            else if (safeTile != tile)
                return false;

            if (p.isBlack != tile.RequiresBlack)
                return false;
        }

        return safeTile != null;
    }

    /// <summary>안전 칸 트리거가 겹칠 때 Safe 우선.</summary>
    GridBWTile GetDominantTileForPlayer(Player p)
    {
        GridBWTile safeMatch = null;
        GridBWTile anyMatch = null;

        foreach (GridBWTile t in tiles)
        {
            if (t == null || !t.ContainsPlayer(p)) continue;
            anyMatch = t;
            if (t.IsSafe)
                safeMatch = t;
        }

        return safeMatch != null ? safeMatch : anyMatch;
    }

    List<Player> GatherAlivePlayers()
    {
        var list = new List<Player>();
        Player[] all = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (Player p in all)
        {
            if (p != null && !p.IsDead)
                list.Add(p);
        }

        return list;
    }

    void ApplySettlement(bool anyOnDefault, bool roundSuccess, List<Player> alive)
    {
        if (alive.Count == 0) return;

        // anyOnDefault 데미지 우선. 둘 다 해당해도 중복 적용 방지
        if (anyOnDefault && teamDamageIfAnyOnDefault > 0)
            ApplyTeamDamage(alive, teamDamageIfAnyOnDefault);
        else if (!roundSuccess && teamDamageIfRoundFail > 0)
            ApplyTeamDamage(alive, teamDamageIfRoundFail);
    }

    static void ApplyTeamDamage(List<Player> targets, int amount)
    {
        foreach (Player p in targets)
        {
            if (p == null || p.IsDead) continue;
            p.ReceiveDamage(amount, null);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: Activate")]
    void Debug_Activate() => Activate();

    [ContextMenu("테스트: Cancel")]
    void Debug_Cancel() => Cancel();
#endif
}
