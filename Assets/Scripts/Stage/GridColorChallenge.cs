using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 5×5 고유색 보드 챌린지 (모드 B).
///
/// [라운드]
///  - 25칸 중 4칸이 Safe (Blue / Purple / Green / Yellow 각 1개, 위치 랜덤), 나머지 Default
///  - roundDuration 후 정산: Default 위 1명이라도 → 팀 데미지
///  - 정산 시 각 플레이어가 자기 playerColorType에 맞는 칸에 없으면 → 팀 데미지
///  - 성공 시 데미지 없이 쿨타임 후 다음 라운드 (totalRounds회)
///
/// [성공 조건]
///  - 모든 생존 플레이어가 isUniqueColor = true
///  - 각자 자기 playerColorType에 해당하는 안전 칸에 서 있어야 함
///  - 같은 칸에 모일 필요 없음 (BW 모드와 반대)
///
/// [씬]
///  - 자식에 GridColorTile 25개 + Collider(Is Trigger)
///  - 4색 머티리얼을 각 GridColorTile Inspector에 연결할 것
///
/// [시작]
///  - autoStart = true: 씬 로드 후 자동 Activate()
///  - autoStart = false: Activate()를 PlayerTriggerZone·StageStartGate 등에 연결
/// </summary>
public class GridColorChallenge : MonoBehaviour
{
    public const int ExpectedTileCount = 25;

    // 라운드마다 이 순서로 1칸씩 랜덤 배치
    static readonly PlayerColorType[] ColorOrder =
    {
        PlayerColorType.Blue,
        PlayerColorType.Purple,
        PlayerColorType.Green,
        PlayerColorType.Yellow,
    };

    [Header("타일 (비우면 자식 GridColorTile 자동 수집)")]
    [SerializeField] GridColorTile[] tiles = new GridColorTile[0];

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

    [Header("정산 데미지 (개인)")]
    [Tooltip("정산 시 자기 색 Safe 칸에 없는 플레이어 개인에게 적용.\n" +
             "Default 칸이거나, Safe 칸이어도 자기 색이 아니면 데미지.")]
    [SerializeField] int individualDamageOnFail = 0;

    [Header("이벤트")]
    public UnityEvent OnChallengeStarted;
    public UnityEvent OnChallengeComplete;
    public UnityEvent OnChallengeCancelled;

    [Tooltip("라운드 인덱스(0부터)")]
    public UnityEvent<int> OnRoundStarted;

    [Tooltip("라운드 인덱스, 성공 여부")]
    public UnityEvent<int, bool> OnRoundSettled;

    bool _isRunning;
    Coroutine _routine;

    // 현재 라운드: 색 → 타일 인덱스
    readonly Dictionary<PlayerColorType, int> _currentSafeIndices =
        new Dictionary<PlayerColorType, int>();

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
        GridColorTile[] found = GetComponentsInChildren<GridColorTile>(true);
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
            Debug.LogWarning("[GridColorChallenge] GridColorTile이 없습니다.", this);
            yield break;
        }

        if (tiles.Length != ExpectedTileCount)
            Debug.LogWarning($"[GridColorChallenge] 타일 수 {tiles.Length} (권장 {ExpectedTileCount}).", this);

        if (totalRounds <= 0)
        {
            Debug.LogWarning("[GridColorChallenge] totalRounds는 1 이상이어야 합니다.", this);
            yield break;
        }

        if (roundDuration <= 0f)
        {
            Debug.LogWarning("[GridColorChallenge] roundDuration은 0보다 커야 합니다.", this);
            yield break;
        }

        _isRunning = true;
        OnChallengeStarted?.Invoke();

        for (int round = 0; round < totalRounds; round++)
        {
            CurrentRoundIndex = round;
            PickRandomColorTiles();
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
        _currentSafeIndices.Clear();
        OnChallengeComplete?.Invoke();
        _routine = null;
    }

    /// <summary>
    /// Blue / Purple / Green / Yellow 각 1개씩,
    /// 25칸 중 서로 겹치지 않게 랜덤 위치 배정.
    /// </summary>
    void PickRandomColorTiles()
    {
        _currentSafeIndices.Clear();

        var pool = new List<int>(tiles.Length);
        for (int i = 0; i < tiles.Length; i++) pool.Add(i);

        foreach (PlayerColorType color in ColorOrder)
        {
            int pick = Random.Range(0, pool.Count);
            _currentSafeIndices[color] = pool[pick];
            pool.RemoveAt(pick);
        }
    }

    void ApplyTileStates()
    {
        // 역매핑: tileIndex → TileState
        var stateMap = new Dictionary<int, GridColorTile.TileState>(ColorOrder.Length);
        foreach (var kv in _currentSafeIndices)
        {
            GridColorTile.TileState s = kv.Key switch
            {
                PlayerColorType.Blue   => GridColorTile.TileState.SafeBlue,
                PlayerColorType.Purple => GridColorTile.TileState.SafePurple,
                PlayerColorType.Green  => GridColorTile.TileState.SafeGreen,
                PlayerColorType.Yellow => GridColorTile.TileState.SafeYellow,
                _                      => GridColorTile.TileState.Default,
            };
            stateMap[kv.Value] = s;
        }

        for (int i = 0; i < tiles.Length; i++)
        {
            if (tiles[i] == null) continue;
            tiles[i].SetState(
                stateMap.TryGetValue(i, out GridColorTile.TileState state)
                    ? state
                    : GridColorTile.TileState.Default);
        }
    }

    void SetAllTilesDefault()
    {
        foreach (GridColorTile t in tiles)
            t?.SetState(GridColorTile.TileState.Default);
    }

    /// <summary>정산 시점: Default 칸 점유 여부 + 라운드 성공 여부.</summary>
    bool EvaluateRound(List<Player> alive, out bool roundSuccess)
    {
        roundSuccess = false;

        int required = requiredAliveCount > 0 ? requiredAliveCount : alive.Count;
        if (required <= 0) return false;

        foreach (GridColorTile t in tiles)
            t?.RefreshOccupants();

        bool anyOnDefault = false;
        foreach (Player p in alive)
        {
            GridColorTile tile = GetDominantTileForPlayer(p);
            if (tile == null || !tile.IsSafe)
                anyOnDefault = true;
        }

        if (alive.Count < required)
            return anyOnDefault;

        roundSuccess = CheckAllPlayersOnMatchingColor(alive);
        return anyOnDefault;
    }

    /// <summary>
    /// 각 플레이어가 isUniqueColor = true이고,
    /// 자신의 playerColorType에 맞는 안전 칸 위에 있으면 성공.
    /// </summary>
    bool CheckAllPlayersOnMatchingColor(List<Player> alive)
    {
        foreach (Player p in alive)
        {
            if (!p.isUniqueColor) return false;

            GridColorTile tile = GetDominantTileForPlayer(p);
            if (tile == null || !tile.IsSafe) return false;
            if (tile.RequiredColorType != p.playerColorType) return false;
        }
        return alive.Count > 0;
    }

    /// <summary>안전 칸 트리거가 겹칠 때 Safe 우선.</summary>
    GridColorTile GetDominantTileForPlayer(Player p)
    {
        GridColorTile safeMatch = null;
        GridColorTile anyMatch  = null;

        foreach (GridColorTile t in tiles)
        {
            if (t == null || !t.ContainsPlayer(p)) continue;
            anyMatch = t;
            if (t.IsSafe) safeMatch = t;
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
    /// 정산 시 자기 색 Safe 칸에 없는 플레이어에게 개인 데미지 적용.
    /// 조건: isUniqueColor가 true이고, 자기 playerColorType에 맞는 Safe 칸 위에 있어야 통과.
    /// </summary>
    void ApplyIndividualDamage(List<Player> alive)
    {
        if (individualDamageOnFail <= 0 || alive.Count == 0) return;

        foreach (Player p in alive)
        {
            if (p == null || p.IsDead) continue;

            GridColorTile tile = GetDominantTileForPlayer(p);
            bool passed = tile != null
                       && tile.IsSafe
                       && p.isUniqueColor
                       && tile.RequiredColorType == p.playerColorType;

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
