using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 라운드가 진행될수록 안전 칸 수를 줄여 난이도를 상승시키는 단계 설정.
/// afterRound: 이 라운드 인덱스(0부터)부터 safeCount 적용.
/// 배열은 afterRound 오름차순으로 입력. 비어 있으면 항상 1칸.
/// 예시: [{afterRound:0, safeCount:3}, {afterRound:3, safeCount:2}, {afterRound:6, safeCount:1}]
/// </summary>
[System.Serializable]
public class SafeCountPhase
{
    [Tooltip("이 라운드 인덱스(0부터)부터 safeCount를 적용")]
    public int afterRound;

    [Tooltip("안전 칸 수. 1 이상")]
    public int safeCount = 1;
}

/// <summary>
/// 5×5 흑/백 보드 챌린지 (모드 A).
///
/// [라운드]
///  - 25칸 중 safeCount개가 Safe(Black/White 랜덤 혼합), 나머지 Default
///  - roundDuration 후 정산: Default 위 1명이라도 → 팀 데미지
///  - 정산 시 생존 플레이어가 각자 자기 isBlack에 맞는 안전 칸에 없으면 → 팀 데미지
///  - 성공 시 데미지 없이 쿨타임 후 다음 라운드 (totalRounds회)
///
/// [안전 칸 수 단계]
///  - safeTilePhases로 라운드 경과에 따라 안전 칸 수를 단계적으로 조정
///  - 초반 칸 많음 → 후반 1칸 : SpeedPhase와 동일한 패턴
///
/// [데미지]
///  - Default 정산 / 라운드 실패: 팀 전체 (Inspector)
///  - DropTrap: 개별 (별도 씬 설정, TrapPlayerTracker Layer Mask = Nothing 권장)
///
/// [씬]
///  - 자식에 GridBWTile 25개 + Collider(Is Trigger)
///  - 안전 칸은 여러 개 배치를 위해 트리거 크기 조정 가능
///
/// [시작]
///  - autoStart = true: 씬 로드 후 자동 Activate()
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

    [Header("안전 칸 수 단계 (SafeCountPhase)")]
    [Tooltip("라운드 경과에 따라 안전 칸 수를 줄여 난이도를 올린다.\n" +
             "afterRound 오름차순 입력. 비어 있으면 항상 1칸.\n" +
             "예: [{0, 3}, {3, 2}, {6, 1}] → 0라운드 3칸, 3라운드부터 2칸, 6라운드부터 1칸")]
    [SerializeField] SafeCountPhase[] safeTilePhases = new SafeCountPhase[0];

    [Header("플레이어")]
    [Tooltip("0이면 생존한 모든 Player를 검사. 4인 플레이 시 4 권장")]
    [SerializeField] int requiredAliveCount = 0;

    [Header("정산 데미지 (개인)")]
    [Tooltip("정산 시 자기 BW 색 Safe 칸에 없는 플레이어 개인에게 적용.\n" +
             "Default 칸이거나, Safe 칸이어도 isBlack 불일치면 데미지.")]
    [SerializeField] int individualDamageOnFail = 0;

    [Header("이벤트")]
    public UnityEvent OnChallengeStarted;
    public UnityEvent OnChallengeComplete;
    public UnityEvent OnChallengeCancelled;

    [Tooltip("라운드 인덱스(0부터)")]
    public UnityEvent<int> OnRoundStarted;

    [Tooltip("라운드 인덱스, 성공 여부")]
    public UnityEvent<int, bool> OnRoundSettled;

    // 현재 라운드 안전 칸 목록. (tileIndex, isBlack) 쌍
    struct SafeTileEntry { public int tileIndex; public bool isBlack; }

    bool _isRunning;
    readonly List<SafeTileEntry> _currentSafeTiles = new List<SafeTileEntry>();
    Coroutine _routine;

    public bool IsRunning => _isRunning;
    public int TotalRounds => totalRounds;
    public int CurrentRoundIndex { get; private set; }

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
            PickRandomSafeTiles(GetCurrentSafeCount());
            ApplyTileStates();
            OnRoundStarted?.Invoke(round);

            yield return new WaitForSeconds(roundDuration);

            List<Player> aliveAtSettlement = GatherAlivePlayers();
            EvaluateRound(aliveAtSettlement, out bool roundSuccess);
            ApplyIndividualDamage(aliveAtSettlement);
            OnRoundSettled?.Invoke(round, roundSuccess);

            SetAllTilesDefault();

            if (round < totalRounds - 1 && cooldownBetweenRounds > 0f)
                yield return new WaitForSeconds(cooldownBetweenRounds);
        }

        _isRunning = false;
        _currentSafeTiles.Clear();
        OnChallengeComplete?.Invoke();
        _routine = null;
    }

    /// <summary>현재 라운드에 적용할 안전 칸 수를 safeTilePhases에서 읽어 반환.</summary>
    int GetCurrentSafeCount()
    {
        int count = 1;
        foreach (SafeCountPhase phase in safeTilePhases)
            if (CurrentRoundIndex >= phase.afterRound)
                count = phase.safeCount;
        return Mathf.Clamp(count, 1, tiles.Length);
    }

    /// <summary>
    /// tiles 중 count개를 서로 겹치지 않게 랜덤 선택.
    /// 각 칸의 Black/White 색은 독립적으로 랜덤 결정.
    /// </summary>
    void PickRandomSafeTiles(int count)
    {
        _currentSafeTiles.Clear();

        var pool = new List<int>(tiles.Length);
        for (int i = 0; i < tiles.Length; i++) pool.Add(i);

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int pick = Random.Range(0, pool.Count);
            _currentSafeTiles.Add(new SafeTileEntry
            {
                tileIndex = pool[pick],
                isBlack   = Random.value >= 0.5f,
            });
            pool.RemoveAt(pick);
        }
    }

    void ApplyTileStates()
    {
        // 안전 칸 목록을 빠른 조회용 딕셔너리로 변환
        var safeMap = new Dictionary<int, bool>(_currentSafeTiles.Count);
        foreach (SafeTileEntry e in _currentSafeTiles)
            safeMap[e.tileIndex] = e.isBlack;

        for (int i = 0; i < tiles.Length; i++)
        {
            if (tiles[i] == null) continue;

            if (safeMap.TryGetValue(i, out bool isBlack))
                tiles[i].SetState(isBlack ? GridBWTile.TileState.SafeBlack : GridBWTile.TileState.SafeWhite);
            else
                tiles[i].SetState(GridBWTile.TileState.Default);
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

        roundSuccess = CheckAllPlayersOnSafe(alive);
        return anyOnDefault;
    }

    /// <summary>
    /// 안전 칸이 여러 개인 경우 각 플레이어가 어느 안전 칸이든
    /// 자기 isBlack과 색이 일치하는 칸에 있으면 성공.
    /// 같은 칸에 몰릴 필요 없음.
    /// </summary>
    bool CheckAllPlayersOnSafe(List<Player> alive)
    {
        foreach (Player p in alive)
        {
            GridBWTile tile = GetDominantTileForPlayer(p);
            if (tile == null || !tile.IsSafe)
                return false;
            if (p.isBlack != tile.RequiresBlack)
                return false;
        }
        return alive.Count > 0;
    }

    /// <summary>안전 칸 트리거가 겹칠 때 Safe 우선.</summary>
    GridBWTile GetDominantTileForPlayer(Player p)
    {
        GridBWTile safeMatch = null;
        GridBWTile anyMatch  = null;

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

    /// <summary>
    /// 정산 시 자기 BW Safe 칸에 없는 플레이어에게 개인 데미지 적용.
    /// 조건: IsSafe가 true이고, 타일의 RequiresBlack과 플레이어의 isBlack이 일치해야 통과.
    /// </summary>
    void ApplyIndividualDamage(List<Player> alive)
    {
        if (individualDamageOnFail <= 0 || alive.Count == 0) return;

        foreach (Player p in alive)
        {
            if (p == null || p.IsDead) continue;

            GridBWTile tile = GetDominantTileForPlayer(p);
            bool passed = tile != null
                       && tile.IsSafe
                       && p.isBlack == tile.RequiresBlack;

            if (!passed)
                p.ReceiveDamage(individualDamageOnFail, null);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: Activate")]
    void Debug_Activate() => Activate();

    [ContextMenu("테스트: Cancel")]
    void Debug_Cancel() => Cancel();
#endif
}
