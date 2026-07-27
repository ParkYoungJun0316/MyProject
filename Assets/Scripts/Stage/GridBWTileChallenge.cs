using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
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
/// [축 SSOT: NetworkDesign.md §11B — 챌린지 축(C 패턴), ColorTileChallenge/OXQuizManager와 동일 골격 복제]
/// Trigger(Activate, Host만) → RoundStart(Host가 라운드마다 새 시드 NV 배포) → Generate(전 머신 각자
/// 동일 시드로 로컬 재생성) → Judge(Host 레인만) → Resolve(성공/실패 전파 → 다음 라운드/완료 결정).
/// GridBW는 totalRounds회 반복 라운드라 OX/ColorTile(1샷)과 달리 스텝 인덱스 자체를 라운드 번호로
/// 사용한다 — 라운드마다 Host가 ChallengeStart(newSeed) 뒤 곧바로 ChallengeStepBegin(round)를 같은
/// 프레임에 호출해(Activate()의 최초 호출과 동일한 원자적 2단 쓰기 — NV는 마지막 값만 전송되므로
/// 중간 상태가 Client에 노출되지 않는다) 다음 라운드로 진행한다(OX의 NextQuestionAfterDelay와 동일 구조).
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
    Coroutine _judgeCoroutine;
    StageNetworkState _netState;

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
        // [버그 수정] 구독을 Start/OnDestroy가 아니라 OnEnable/OnDisable로 건다 —
        // _challengeStep은 씬당 공유 슬롯이라 Phase로 이 GameObject가 비활성화된 뒤에도
        // Start에서 건 구독이 살아있으면 다른 챌린지(GridColorChallenge 등)의 ChallengeStepBegin에도
        // 계속 반응해 "Coroutine couldn't be started because the game object is inactive" 에러가
        // 났다 (DirectionalBarrierRound.OnEnable과 동일 원칙).
        _netState = StageNetworkState.Instance;
        if (_netState != null)
        {
            _netState.OnChallengeStepChanged    += HandleChallengeStepChanged;
            _netState.OnChallengeClearedChanged += HandleChallengeClearedChanged;
            _netState.OnChallengeOutcome        += HandleChallengeOutcome;
            _netState.OnDeathReloadStarted      += HandleDeathReloadStarted;
        }

        if (!autoStart || IsClientOnly()) return;

        if (autoStartDelay > 0f)
            StartCoroutine(AutoStartRoutine());
        else
            Activate();
    }

    void OnDisable()
    {
        if (_netState != null)
        {
            _netState.OnChallengeStepChanged    -= HandleChallengeStepChanged;
            _netState.OnChallengeClearedChanged -= HandleChallengeClearedChanged;
            _netState.OnChallengeOutcome        -= HandleChallengeOutcome;
            _netState.OnDeathReloadStarted      -= HandleDeathReloadStarted;
        }

        if (_judgeCoroutine != null) { StopCoroutine(_judgeCoroutine); _judgeCoroutine = null; }
        _isRunning = false;
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
        GridBWTile[] found = GetComponentsInChildren<GridBWTile>(true);
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
            Debug.LogWarning("[GridBWTileChallenge] GridBWTile이 없습니다.", this);
            return;
        }

        if (tiles.Length != ExpectedTileCount)
            Debug.LogWarning($"[GridBWTileChallenge] 타일 수 {tiles.Length} (권장 {ExpectedTileCount}).", this);

        if (totalRounds <= 0)
        {
            Debug.LogWarning("[GridBWTileChallenge] totalRounds는 1 이상이어야 합니다.", this);
            return;
        }

        if (roundDuration <= 0f)
        {
            Debug.LogWarning("[GridBWTileChallenge] roundDuration은 0보다 커야 합니다.", this);
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
        _currentSafeTiles.Clear();
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
        _netState.ChallengeStart(seed);
        _netState.ChallengeStepBegin(round);
    }

    // ── 라운드 생성 (전 머신 공통 — StageNetworkState NV 구독, §11B ③Generate) ──

    /// <summary>
    /// StageNetworkState.OnChallengeStepChanged 구독 핸들러. Host/Client 동일 코드로 라운드를 생성한다.
    /// GridBW는 stepIndex 자체를 라운드 번호로 사용 — 라운드마다 새로 배포된 시드로 안전 칸 위치+색을
    /// 재생성한다. 판정(JudgeRoutine)은 이 메서드 끝에서 Host만 시작한다 (§11B ④Judge).
    /// </summary>
    void HandleChallengeStepChanged(int stepIndex)
    {
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
        PickRandomSafeTiles(GetCurrentSafeCount(), rng);
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
    void HandleChallengeOutcome(bool success) => HandleRoundOutcome(CurrentRoundIndex, success);

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
        if (!cleared) return;

        _isRunning = false;
        CurrentRoundIndex = -1;
        _currentSafeTiles.Clear();
        OnChallengeComplete?.Invoke();
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
    /// tiles 중 count개를 서로 겹치지 않게 선택. 각 칸의 Black/White 색도 함께 결정.
    /// rng는 ChallengeSeed 기반 System.Random — 전 머신이 동일 시드로 호출하면 항상
    /// 같은 결과가 나온다 (UnityEngine.Random 전역 상태 오염 방지 — OXQuizManager와 동일 원칙).
    /// </summary>
    void PickRandomSafeTiles(int count, System.Random rng)
    {
        _currentSafeTiles.Clear();

        var pool = new List<int>(tiles.Length);
        for (int i = 0; i < tiles.Length; i++) pool.Add(i);

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int pick = rng.Next(0, pool.Count);
            _currentSafeTiles.Add(new SafeTileEntry
            {
                tileIndex = pool[pick],
                isBlack   = rng.NextDouble() >= 0.5,
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

            // NetworkDamageUtil이 데미지 파이프라인 단일 진입점 — Player.ReceiveDamage() 직접 호출은
            // 온라인 모드에서 no-op이던 버그였음 (GridColorChallenge와 동일 수정, 2026-07-19 원 사례).
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
