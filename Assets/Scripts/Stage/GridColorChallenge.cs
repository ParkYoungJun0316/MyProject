using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 5×5 고유색 보드 챌린지 (모드 B).
///
/// [축 SSOT: NetworkDesign.md §11B — 챌린지 축(C 패턴), GridBWTileChallenge/ColorTileChallenge와 동일 골격 복제]
/// Trigger(Activate, Host만) → RoundStart(Host가 라운드마다 새 시드 NV 배포) → Generate(전 머신 각자
/// 동일 시드로 로컬 재생성) → Judge(Host 레인만) → Resolve(성공/실패 전파 → 다음 라운드/완료 결정).
/// GridBW와 동일하게 stepIndex를 라운드 번호로 사용 — 라운드마다 Host가 ChallengeStart(newSeed) 뒤
/// 곧바로 ChallengeStepBegin(round)를 같은 프레임에 호출한다(원자적 2단 쓰기).
///
/// [라운드]
///  - 25칸 중 활성 색 수만큼 Safe 칸 배치 (위치 랜덤), 나머지 Default
///  - 4인: 4칸 / 2인: 2칸 (GameSession 기준, 없으면 4색 fallback)
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
    bool _subscribed;
    StageNetworkState _netState;
    Coroutine _judgeCoroutine;

    // 현재 라운드: 색 → 타일 인덱스
    readonly Dictionary<PlayerColorType, int> _currentSafeIndices =
        new Dictionary<PlayerColorType, int>();

    public bool IsRunning => _isRunning;
    public int TotalRounds => totalRounds;
    public int CurrentRoundIndex { get; private set; } = -1;

    void Awake()
    {
        if (tiles == null || tiles.Length == 0)
            CollectTilesFromChildren();
    }

    void OnEnable()
    {
        TryBindAndSubscribe();
    }

    /// <summary>
    /// Unity가 실제로 보장하는 건 "씬의 모든 Awake가 끝난 뒤에야 모든 Start가 실행된다"뿐이고,
    /// OnEnable은 이 보장 밖이라 다른 오브젝트(StageNetworkState)의 Awake보다 먼저 돌 수 있다.
    /// 그래서 OnEnable에서만 _netState를 캐시하면 최초 활성화 시점에 null로 굳어버리는 레이스가
    /// 있었다 (2026-07-28 버그 — M.Stage5 GridRound 미진행, Activate()가 _netState null로 조기
    /// 반환). Start()는 전역 Awake→Start 순서가 보장되므로(OXQuizManager와 동일 원칙) 여기서
    /// 최초 바인딩의 안전망을 맡는다.
    /// </summary>
    void Start()
    {
        TryBindAndSubscribe();
    }

    void OnDisable()
    {
        Unsubscribe();

        if (_judgeCoroutine != null) { StopCoroutine(_judgeCoroutine); _judgeCoroutine = null; }
        _isRunning = false;
    }

    /// <summary>
    /// _netState 바인딩 + 구독 + autoStart 트리거를 한 곳에 모은 진입점. OnEnable과 Start 양쪽에서
    /// 호출되지만 _subscribed 가드로 중복 구독을 막는다.
    /// - 최초 활성화: OnEnable이 StageNetworkState.Awake보다 먼저 돌면 _netState가 아직 null이라
    ///   건너뛰고, 뒤이어 실행되는 Start()(순서 보장)가 안전하게 완료한다. 반대로 OnEnable이 이미
    ///   늦게 돌아 바인딩에 성공했다면 Start()는 _subscribed=true를 보고 그냥 스킵한다.
    /// - Phase 재활성화(2번째 이후 OnEnable): Start는 생애 1회만 실행되므로 이 경우엔 OnEnable이
    ///   재구독 + autoStart 재트리거를 전담한다 (Phase가 이 GameObject를 껐다 켤 때마다 다시
    ///   Activate()해야 하는 원래 동작 유지).
    /// </summary>
    void TryBindAndSubscribe()
    {
        if (_subscribed) return;

        _netState ??= StageNetworkState.Instance;
        if (_netState == null) return;

        _netState.OnChallengeStepChanged    += HandleChallengeStepChanged;
        _netState.OnChallengeClearedChanged += HandleChallengeClearedChanged;
        _netState.OnChallengeOutcome        += HandleChallengeOutcome;
        _netState.OnDeathReloadStarted      += HandleDeathReloadStarted;
        _subscribed = true;

        if (!autoStart || IsClientOnly()) return;

        if (autoStartDelay > 0f)
            StartCoroutine(AutoStartRoutine());
        else
            Activate();
    }

    void Unsubscribe()
    {
        if (_netState != null)
        {
            _netState.OnChallengeStepChanged    -= HandleChallengeStepChanged;
            _netState.OnChallengeClearedChanged -= HandleChallengeClearedChanged;
            _netState.OnChallengeOutcome        -= HandleChallengeOutcome;
            _netState.OnDeathReloadStarted      -= HandleDeathReloadStarted;
        }
        _subscribed = false;
    }

    IEnumerator AutoStartRoutine()
    {
        yield return new WaitForSeconds(autoStartDelay);
        Activate();
    }

    /// <summary>Client/Host 공통. Host 레인 여부만 다르게 취급 (OXQuizManager와 동일).</summary>
    static bool IsClientOnly()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && !nm.IsServer;
    }

    void CollectTilesFromChildren()
    {
        GridColorTile[] found = GetComponentsInChildren<GridColorTile>(true);
        System.Array.Sort(found, (a, b) => a.GridIndex.CompareTo(b.GridIndex));

        for (int i = 0; i < found.Length; i++)
            found[i].SetGridIndex(i);

        tiles = found;
    }

    /// <summary>
    /// 챌린지 시작 (totalRounds회). 이미 실행 중이면 무시.
    /// Host 레인만 실제로 진행 — Client의 직접 호출은 무시된다 (§11B ①Trigger).
    /// </summary>
    public void Activate()
    {
        if (IsClientOnly()) return;
        if (_isRunning) return;
        if (_netState == null) return;

        if (tiles == null || tiles.Length == 0)
            CollectTilesFromChildren();

        if (tiles.Length == 0)
        {
            Debug.LogWarning("[GridColorChallenge] GridColorTile이 없습니다.", this);
            return;
        }

        if (tiles.Length != ExpectedTileCount)
            Debug.LogWarning($"[GridColorChallenge] 타일 수 {tiles.Length} (권장 {ExpectedTileCount}).", this);

        if (totalRounds <= 0)
        {
            Debug.LogWarning("[GridColorChallenge] totalRounds는 1 이상이어야 합니다.", this);
            return;
        }

        if (roundDuration <= 0f)
        {
            Debug.LogWarning("[GridColorChallenge] roundDuration은 0보다 커야 합니다.", this);
            return;
        }

        StartRound(0);
    }

    /// <summary>진행 중단 + 모든 칸 Default. Host 전용 (§11B ①Trigger와 동일 권한).</summary>
    public void Cancel()
    {
        if (IsClientOnly()) return;
        if (_judgeCoroutine != null) { StopCoroutine(_judgeCoroutine); _judgeCoroutine = null; }

        _isRunning = false;
        CurrentRoundIndex = -1;
        _currentSafeIndices.Clear();
        SetAllTilesDefault();
        OnChallengeCancelled?.Invoke();
    }

    /// <summary>
    /// Host: 라운드 시작. 새 라운드 시드를 생성해 배포한다 — Activate()의 최초 호출과 동일한
    /// 원자적 2단 쓰기(ChallengeStart 뒤 곧바로 ChallengeStepBegin)이므로 Client는 항상 최종
    /// 커밋된 값(새 시드 + 이번 라운드 인덱스)만 관찰한다 (§11B ②RoundStart).
    /// </summary>
    void StartRound(int round)
    {
        int seed = Random.Range(int.MinValue, int.MaxValue);
        _netState.ChallengeStart(seed, ChallengeOwnerType.GridColor);
        _netState.ChallengeStepBegin(round);
    }

    // ── 라운드 생성 (전 머신 공통 — StageNetworkState NV 구독, §11B ③Generate) ──

    /// <summary>
    /// StageNetworkState.OnChallengeStepChanged 구독 핸들러. Host/Client 동일 코드로 라운드를 생성한다.
    /// 판정(JudgeRoutine)은 이 메서드 끝에서 Host만 시작한다 (§11B ④Judge).
    /// </summary>
    void HandleChallengeStepChanged(int stepIndex)
    {
        // [버그 수정 2026-07-28] _challengeStep 공유 슬롯 owner 가드 — 내 것(GridColor)이 아니면 무시
        // (ChallengeOwnerType 정의부 참고, A-B-C-A 회귀의 근본 원인).
        if (_netState == null || _netState.ChallengeOwner != ChallengeOwnerType.GridColor) return;
        if (stepIndex < 0) return; // ChallengeStart()의 초기화 신호 — 무시
        if (!isActiveAndEnabled) return; // OnDisable에서 구독 해제하지만, 해제 타이밍 레이스 방어용 가드
        if (tiles == null || tiles.Length == 0) return;

        if (!_isRunning)
        {
            _isRunning = true;
            OnChallengeStarted?.Invoke();
        }

        CurrentRoundIndex = stepIndex;

        int seed = _netState != null ? _netState.ChallengeSeed : 0;
        var rng  = new System.Random(seed);
        PickRandomColorTiles(rng);
        ApplyTileStates();

        OnRoundStarted?.Invoke(stepIndex);

        // 판정은 Host 레인에서만 (§11B ④Judge) — Client는 결과를 ClientRpc로만 관찰
        if (IsClientOnly()) return;

        if (_judgeCoroutine != null) StopCoroutine(_judgeCoroutine);
        _judgeCoroutine = StartCoroutine(JudgeRoutine(stepIndex));
    }

    // ── 판정 (Host 전용, §11B ④Judge) ─────────────────────────────

    IEnumerator JudgeRoutine(int round)
    {
        yield return new WaitForSeconds(roundDuration);

        List<Player> alive = GatherAlivePlayers();
        EvaluateRound(alive, out bool roundSuccess);
        ApplyIndividualDamage(alive);

        HandleRoundOutcome(round, roundSuccess);
        _netState?.NotifyChallengeOutcomeClientRpc(roundSuccess);

        if (round < totalRounds - 1)
        {
            if (cooldownBetweenRounds > 0f)
                yield return new WaitForSeconds(cooldownBetweenRounds);

            StartRound(round + 1);
        }
        else
        {
            _netState?.ChallengeCleared(true);
        }
    }

    // ── 결과 반영 (전 머신 공통 — Host는 직접 호출, Client는 ClientRpc로 수신) ──

    /// <summary>Host는 JudgeRoutine에서 직접 호출하므로 이 핸들러는 Client에서만 의미 있음.</summary>
    void HandleChallengeOutcome(bool success)
    {
        if (_netState == null || _netState.ChallengeOwner != ChallengeOwnerType.GridColor) return; // owner 가드 — HandleChallengeStepChanged와 동일 이유
        HandleRoundOutcome(CurrentRoundIndex, success);
    }

    void HandleRoundOutcome(int round, bool success)
    {
        OnRoundSettled?.Invoke(round, success);
        SetAllTilesDefault();
    }

    /// <summary>
    /// §11 사망 문 진입 확정(StageNetworkState.OnDeathReloadStarted) — Host/Client 공통 구독.
    /// 사망은 이 챌린지의 판정(JudgeRoutine)이 감지할 수 없는 챌린지 축 밖의 사건이라, 여기서 즉시
    /// 판정 코루틴을 끊어 뒤이어 Despawn된 _netState에 NotifyChallengeOutcomeClientRpc를 쏘는 것을
    /// 원천 차단한다 (SequenceRingMinigame.HandleDeathReloadStarted와 동일 원칙).
    /// </summary>
    void HandleDeathReloadStarted()
    {
        if (_judgeCoroutine != null) { StopCoroutine(_judgeCoroutine); _judgeCoroutine = null; }
        _isRunning = false;
    }

    /// <summary>ChallengeCleared NV 변경 시 Host/Client 공통으로 OnChallengeComplete를 1회 재생 (OX의 OnAllCleared와 동일 패턴).</summary>
    void HandleChallengeClearedChanged(bool cleared)
    {
        if (_netState == null || _netState.ChallengeOwner != ChallengeOwnerType.GridColor) return; // owner 가드 — HandleChallengeStepChanged와 동일 이유
        if (!cleared) return;

        _isRunning = false;
        CurrentRoundIndex = -1;
        _currentSafeIndices.Clear();
        OnChallengeComplete?.Invoke();
    }

    /// <summary>
    /// 활성 색(GameSession 기준)마다 Safe 칸 1개씩 배정. GameSession 없으면 ColorOrder 전체(4색) fallback.
    /// rng는 ChallengeSeed 기반 System.Random — 전 머신이 동일 시드로 호출하면 항상 같은 결과가 나온다
    /// (UnityEngine.Random 전역 상태 오염 방지 — OXQuizManager와 동일 원칙).
    /// </summary>
    void PickRandomColorTiles(System.Random rng)
    {
        _currentSafeIndices.Clear();

        var pool = new List<int>(tiles.Length);
        for (int i = 0; i < tiles.Length; i++) pool.Add(i);

        IReadOnlyList<PlayerColorType> activeColors = GameSession.Instance != null
            ? GameSession.Instance.GetActiveColors()
            : (IReadOnlyList<PlayerColorType>)ColorOrder;

        foreach (PlayerColorType color in activeColors)
        {
            if (pool.Count == 0) break;
            int pick = rng.Next(0, pool.Count);
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

        if (GameSession.Instance != null)
        {
            foreach (Player p in GameSession.Instance.GetActivePlayers())
                if (p != null && !p.IsDead) list.Add(p);
        }
        else
        {
            foreach (Player p in FindObjectsByType<Player>(FindObjectsSortMode.None))
                if (p != null && !p.IsDead) list.Add(p);
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

            // NetworkDamageUtil이 데미지 파이프라인 단일 진입점 — Player.ReceiveDamage() 직접 호출은
            // 온라인 모드에서 Player.TakeDamage()가 즉시 반환(no-op)해 데미지가 전혀 적용되지 않는
            // 버그였음 (architecture.mdc: NetworkDamageUtil 단일 진입점 규칙 위반, 2026-07-19 수정).
            if (!passed)
                NetworkDamageUtil.ApplyDamage(p, individualDamageOnFail);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: Activate")]
    void Debug_Activate() => Activate();

    [ContextMenu("테스트: Cancel")]
    void Debug_Cancel() => Cancel();
#endif
}
