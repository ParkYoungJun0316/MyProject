using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 라운드가 진행될수록 안전 칸 수와 흑/백 보장 개수를 조정하는 단계 설정.
/// afterRound: 이 라운드 인덱스(0부터)부터 적용.
/// 배열은 afterRound 오름차순으로 입력. 비어 있으면 매 라운드 1칸(흑/백 최소 0).
/// </summary>
[System.Serializable]
public class GridSafePhase
{
    [Tooltip("이 라운드 인덱스(0부터)부터 이 단계를 적용")]
    public int afterRound;

    [Tooltip("이번 단계 안전 칸 수 (= 나오는 색 수). 풀 크기보다 크면 풀 크기로 클램프")]
    public int tileCount = 1;

    [Tooltip("그중 흑/백 최소 개수(흑+백 합산). tileCount보다 크면 tileCount로 클램프")]
    public int minBwCount = 0;
}

/// <summary>안전 칸 규칙. 사이클·붕괴·정산 데미지는 두 규칙이 같고, "누가 어느 칸에서 통과하나"만 다르다.</summary>
public enum GridSafeRule
{
    /// <summary>M.Stage5 — 고유색/흑백 칸. 자기 모드에 맞는 칸에 서야 통과(§8).</summary>
    PlayerColor,
    /// <summary>
    /// T.Boss P3 — 공용 칸(색 없음, 누구나). 칸당 1명. 한 칸에 2명 이상이면 그 칸 위 전원 실패
    /// (경계에 걸쳐 두 칸에 등록된 사람도 양쪽 칸에 센다 — 사용자 확정 2026-09-23).
    /// </summary>
    SharedSolo,
}

/// <summary>SharedSolo 전용 — 안전 칸 수 = 인원 + extraTiles. afterRound: 이 라운드 인덱스(0부터)부터 적용.</summary>
[System.Serializable]
public class GridSharedSafePhase
{
    [Tooltip("이 라운드 인덱스(0부터)부터 이 단계를 적용")]
    public int afterRound;

    [Tooltip("인원 수에 더할 여유 칸 수. 0 = 딱 인원 수(한 명만 헷갈려도 누군가 남는다)")]
    public int extraTiles = 0;
}

/// <summary>라운드 경과에 따른 공개~정산 시간. afterRound: 이 라운드 인덱스(0부터)부터 적용.</summary>
[System.Serializable]
public class GridRoundDurationPhase
{
    [Tooltip("이 라운드 인덱스(0부터)부터 적용")]
    public int afterRound;

    [Tooltip("공개~정산 시간(초)")]
    public float duration = 4.5f;
}

/// <summary>
/// 5×5 혼합판(Grid) 챌린지 — M.Stage5.
/// GridColorChallenge(고유색 전용) + GridBWTileChallenge(흑백 전용)를 통합해 한 보드·한 라운드 줄로
/// 합친다 (`CoopStageAudit.M.md` §8, 2026-09-11 확정. Stage5.1 Color / Stage5.2 BW 페이즈 분리는 폐기).
///
/// [축 SSOT: NetworkDesign.md §11B — 챌린지 축(C 패턴), GridColorChallenge/GridBWTileChallenge와 동일
/// 골격을 그대로 재사용] Trigger(Activate, Host만) → RoundStart(Host가 라운드마다 새 시드 NV 배포) →
/// Generate(전 머신 각자 동일 시드로 로컬 재생성) → Judge(Host 레인만) → Resolve(성공/실패 전파 →
/// 다음 라운드/완료 결정). stepIndex를 라운드 번호로 사용 — 라운드마다 Host가 ChallengeStart(newSeed)
/// 뒤 곧바로 ChallengeStepBegin(round)를 같은 프레임에 호출한다(원자적 2단 쓰기).
///
/// [라운드 사이클 — 2026-09-22] 정산 → 멈춤(settleHoldSeconds) → 복구(restoreSeconds) → 휴식(restSeconds)
/// → 선행(preRevealSeconds: 다음 라운드 시드 배포, 안전 칸은 계산만 하고 숨김, OnRoundPreReveal)
/// → 라운드(roundDuration: 안전 칸 공개 OnRoundStarted ~ 정산 OnRoundSettled). 라운드당 챌린지 스텝 2개
/// (짝수 = 선행, 홀수 = 공개)로 전파한다 — 새 RPC/NV 없음. 붕괴·바람은 PreRevealServerTime 기준으로 돈다.
///
/// [풀 구성]
///  - 활성 고유색(GameSession 기준, 2인=2색/4인=4색) + Black + White
///  - 없는 플레이어 고유색은 풀에 안 넣음 (가짜 칸 없음)
///
/// [라운드 생성 — §8 안전 칸 배치]
///  - GridSafePhase(afterRound/tileCount/minBwCount)로 이번 라운드 안전 칸 수 + 흑백 최소 보장을 결정
///  - 흑/백 최소 개수만큼 먼저 풀의 Black/White에서 강제 추첨 → 나머지는 남은 풀(고유+잔여 흑백)에서
///    추첨. 거부 샘플링 없이 결정적으로 제약을 만족(시드 기반 System.Random이므로 전 머신 동일 결과)
///  - 뽑힌 색마다 서로 겹치지 않는 타일 인덱스를 1개씩 배정
///
/// [판정 — §8 정산, 플레이어별 분기]
///  - 내 고유색(Player.playerColorType)이 이번 라운드 풀에 있으면 → 고유색 모드
///    (Player.isUniqueColor == true) + 내 색 안전 칸에 서 있어야 통과. 다른 사람 고유 칸도 실패.
///  - 내 고유색이 없으면 → 흑백 모드(Player.isUniqueColor == false) + 내 Player.isBlack과 일치하는
///    흑/백 안전 칸에 서 있으면 통과(흑백 칸은 여러 명이 공유 가능)
///  - 칸 경계에 걸쳐 서서 칸 두 개를 동시에 밟은 경우는 **관대 판정** — 밟은 칸 중 내 정답 칸이
///    하나라도 있으면 통과 (사용자 확정 2026-09-11, PlayerPassed 주석 참고)
///  - Default 칸 / 칸 밖 / 모드 틀림 → 개인 데미지(NetworkDamageUtil). Objective는 Fail() 호출 안 함
///    — 라운드 실패는 항상 개인 데미지만, HP 0이면 기존 방 리셋 파이프라인
///
/// [SharedSolo — T.Boss P3, 2026-09-23] 위 풀·판정 대신: 안전 칸 = 인원 + 여유(sharedSafePhases) 개의
/// 공용 칸(색 없음, 칸은 Default 그대로 — 표시는 SafeZoneWarnSign 마커). 통과 = 안전 칸 위에 있고, 내가 밟은
/// 안전 칸이 전부 "혼자"일 것. 겹친 칸 위 전원이 같은 개인 데미지를 받는다. 사이클·붕괴는 M5와 동일.
///
/// [씬]
///  - 자식에 GridTile 25개 + Collider(Is Trigger)
///  - 색상별 머티리얼을 각 GridTile Inspector에 연결할 것
///
/// [시작]
///  - autoStart = true: 씬 로드 후 자동 Activate()
///  - autoStart = false: Activate()를 StageStartGate 등에 연결
/// </summary>
public class GridChallenge : MonoBehaviour
{
    public const int ExpectedTileCount = 25;

    // 활성 고유색 fallback (GameSession 없을 때만 사용)
    static readonly PlayerColorType[] FallbackColorOrder =
    {
        PlayerColorType.Blue,
        PlayerColorType.Purple,
        PlayerColorType.Green,
        PlayerColorType.Yellow,
    };

    [Header("타일 (비우면 자식 GridTile 자동 수집)")]
    [SerializeField] GridTile[] tiles = new GridTile[0];

    [Header("시작 설정")]
    [Tooltip("true: 씬 로드 후 자동 Activate()\n" +
             "false: Activate()를 StageStartGate 등에 연결")]
    [SerializeField] bool autoStart = false;

    [Tooltip("autoStart=true일 때 Activate()까지 대기(초). 0이면 Start() 직후")]
    [SerializeField] float autoStartDelay = 0f;

    [Header("라운드")]
    [Tooltip("안전 칸 공개 ~ 정산(초). 끝에 한 번 판정. roundDurationPhases가 있으면 그쪽이 우선")]
    [SerializeField] float roundDuration = 0f;

    [Tooltip("라운드 경과에 따라 공개 시간을 바꾼다(후반 압박). afterRound 오름차순. 비어 있으면 roundDuration 고정")]
    [SerializeField] GridRoundDurationPhase[] roundDurationPhases = new GridRoundDurationPhase[0];

    [Tooltip("Activate() 1회 시 진행할 라운드 수")]
    [SerializeField] int totalRounds = 0;

    [Header("라운드 사이클 (정산 → 멈춤 → 복구 → 휴식 → 선행 → 라운드)")]
    [Tooltip("정산 직후 아무것도 바뀌지 않고 결과만 보여주는 시간(초). 이후 복구 시작")]
    [SerializeField] float settleHoldSeconds = 0.5f;

    [Tooltip("복구 전용 시간(초). GridTileCollapse의 복구 연출이 이 안에 끝나야 한다")]
    [SerializeField] float restoreSeconds = 1.5f;

    [Tooltip("복구 후 정말 아무 일도 없는 숨 고르기(초)")]
    [SerializeField] float restSeconds = 2f;

    [Tooltip("다음 라운드 시드를 먼저 배포하고 안전 칸은 숨긴 채 보내는 시간(초). GridTileCollapse는 " +
             "이때부터 경고·파괴를 시작한다. 0이면 공개와 동시에 시작(선행 없음)")]
    [SerializeField] float preRevealSeconds = 0f;

    [Header("안전 칸 단계 (GridSafePhase)")]
    [Tooltip("라운드 경과에 따라 안전 칸 수 + 흑백 최소 보장을 조정.\n" +
             "afterRound 오름차순 입력. 비어 있으면 매 라운드 1칸, 흑백 최소 0.")]
    [SerializeField] GridSafePhase[] safeTilePhases = new GridSafePhase[0];

    [Header("안전 칸 규칙")]
    [Tooltip("PlayerColor = M.Stage5(고유색/흑백 칸, safeTilePhases 사용)\n" +
             "SharedSolo = T.Boss P3(공용 칸 1명, sharedSafePhases 사용 — safeTilePhases 무시)")]
    [SerializeField] GridSafeRule safeRule = GridSafeRule.PlayerColor;

    [Tooltip("SharedSolo 전용. 안전 칸 수 = 인원 + extraTiles. afterRound 오름차순. 비어 있으면 여유 0(딱 인원 수)")]
    [SerializeField] GridSharedSafePhase[] sharedSafePhases = new GridSharedSafePhase[0];

    [Header("플레이어")]
    [Tooltip("0이면 생존한 모든 Player를 검사. 4인 플레이 시 4 권장")]
    [SerializeField] int requiredAliveCount = 0;

    [Header("정산 데미지 (개인)")]
    [Tooltip("정산 시 통과 못 한 플레이어 개인에게 적용. PlayerColor = 자기 모드 칸에 없음 / " +
             "SharedSolo = 안전 칸에 없음 또는 2명 이상 겹친 칸 위")]
    [SerializeField] int individualDamageOnFail = 0;

    [Header("이벤트")]
    public UnityEvent OnChallengeStarted;
    public UnityEvent OnChallengeComplete;
    public UnityEvent OnChallengeCancelled;

    [Tooltip("라운드 인덱스(0부터). 선행 시작 — 이번 라운드 안전 칸은 계산만 됐고 아직 숨김. " +
             "preRevealSeconds가 0이면 OnRoundStarted 직전 같은 프레임에 발생")]
    public UnityEvent<int> OnRoundPreReveal;

    [Tooltip("라운드 인덱스(0부터). 안전 칸 공개 순간")]
    public UnityEvent<int> OnRoundStarted;

    [Tooltip("라운드 인덱스, 성공 여부")]
    public UnityEvent<int, bool> OnRoundSettled;

    bool _isRunning;
    bool _subscribed;
    bool _warnedNoGameSession;
    StageNetworkState _netState;
    Coroutine _judgeCoroutine;

    // 현재 라운드: 타일 인덱스 → 배정된 색(Blue/Purple/Green/Yellow/Black/White)
    readonly Dictionary<int, PlayerColorType> _currentSafeTiles = new Dictionary<int, PlayerColorType>();

    // 현재 라운드 풀에 뽑힌 고유색 집합 — 플레이어별 모드 분기(§8 정산 1/2)에 사용
    readonly HashSet<PlayerColorType> _uniqueColorsThisRound = new HashSet<PlayerColorType>();

    public bool IsRunning => _isRunning;
    public int TotalRounds => totalRounds;
    public float RoundDuration => roundDuration;

    /// <summary>이번 라운드(CurrentRoundIndex)의 공개~정산 시간.</summary>
    public float CurrentRoundDuration => RoundDurationFor(CurrentRoundIndex);

    public float RoundDurationFor(int round)
    {
        float d = roundDuration;
        if (roundDurationPhases != null)
            foreach (GridRoundDurationPhase p in roundDurationPhases)
                if (p != null && round >= p.afterRound && p.duration > 0f)
                    d = p.duration;
        return d;
    }
    public float SettleHoldSeconds => settleHoldSeconds;
    public float RestoreSeconds => restoreSeconds;
    public float RestSeconds => restSeconds;
    public float PreRevealSeconds => Mathf.Max(0f, preRevealSeconds);
    public int CurrentRoundIndex { get; private set; } = -1;

    /// <summary>이번 라운드 보드를 만든 시드(전 머신 동일). 라운드 연동 연출(GridTileCollapse)이 같은 시드를 쓴다.</summary>
    public int CurrentRoundSeed { get; private set; }

    /// <summary>
    /// 이번 라운드 선행 시작 ServerTime(전 머신 동일 절대 시각). 라운드 연동 연출(붕괴·바람)의 시간 앵커 —
    /// 각 머신이 이벤트를 받은 로컬 시각이 아니라 이 값 기준으로 돌아 Host/Client가 같은 순간에 맞는다.
    /// </summary>
    public double PreRevealServerTime { get; private set; } = -1.0;

    /// <summary>이번 라운드 안전 칸 공개 ServerTime = PreRevealServerTime + PreRevealSeconds.</summary>
    public double RevealServerTime => PreRevealServerTime + PreRevealSeconds;

    /// <summary>보드 칸 배열(인덱스 = GridIndex). 읽기 전용 — 상태 변경은 이 챌린지만.</summary>
    public IReadOnlyList<GridTile> Tiles => tiles;

    /// <summary>
    /// 이번 라운드 안전 칸이면 true. 선행 구간(OnRoundPreReveal~공개 전)에는 화면엔 숨겨진 **계획된** 안전 칸을
    /// 돌려준다 — GridTileCollapse가 공개 전에 깰 칸을 고를 때 안전 칸을 피하기 위해서. 정산 후~다음 선행 전엔 전부 false.
    /// </summary>
    public bool IsSafeTileThisRound(int index) => _currentSafeTiles.ContainsKey(index);

    public GridSafeRule SafeRule => safeRule;

    /// <summary>이번 라운드 안전 칸 인덱스. IsSafeTileThisRound와 같은 계약(선행 구간엔 숨겨진 계획값).</summary>
    public IEnumerable<int> SafeTileIndices => _currentSafeTiles.Keys;

    /// <summary>
    /// SharedSolo: 이 안전 칸 위 생존자가 2명 이상인가. 정산 판정(Host)과 안전 연출(각 머신 로컬)이 같은 규칙을 쓴다 —
    /// 점유는 트리거 기반 로컬 값이라 머신 간 차이는 CNT 보간 지연만큼뿐이다(CapacityTile과 같은 판단).
    /// </summary>
    public bool IsSharedSafeTileOverloaded(int index) => SafeTileOccupantCount(index) >= 2;

    /// <summary>이번 라운드 안전 칸 위 생존자 수. 안전 칸이 아니면 0. 연출(0명/1명/2명 이상)용.</summary>
    public int SafeTileOccupantCount(int index)
    {
        if (!_currentSafeTiles.ContainsKey(index) || index < 0 || index >= tiles.Length || tiles[index] == null)
            return 0;
        return CountAliveOccupants(tiles[index]);
    }

    static int CountAliveOccupants(GridTile t)
    {
        int n = 0;
        foreach (Player p in t.Occupants)
            if (p != null && !p.IsDead) n++;
        return n;
    }

    // 챌린지 스텝 NV 인코딩: 라운드당 스텝 2개 — 짝수 = 선행 시작(새 시드), 홀수 = 안전 칸 공개(시드 유지).
    // 새 RPC/NV 없이 기존 _challengeStep 한 슬롯으로 두 순간을 전파한다.
    static int PreRevealStep(int round) => round * 2;
    static int RevealStep(int round) => round * 2 + 1;
    int _preRevealedRound = -1; // 이 머신이 선행 처리를 끝낸 라운드 — 짝수 스텝을 못 받고 홀수만 온 경우 보충용

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
    /// 있었다 (GridColorChallenge/GridBWTileChallenge와 동일 원인 — 2026-07-28 버그). Start()는
    /// 전역 Awake→Start 순서가 보장되므로(OXQuizManager와 동일 원칙) 여기서 최초 바인딩의 안전망을
    /// 맡는다.
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
        _preRevealedRound = -1; // 재활성화 후 라운드 0 선행이 "이미 처리됨"으로 건너뛰어지지 않게
    }

    /// <summary>
    /// _netState 바인딩 + 구독 + autoStart 트리거를 한 곳에 모은 진입점. OnEnable과 Start 양쪽에서
    /// 호출되지만 _subscribed 가드로 중복 구독을 막는다 (GridColorChallenge.TryBindAndSubscribe와
    /// 동일 원칙 — 최초 활성화는 Start가 안전망, Phase 재활성화는 OnEnable이 재구독 전담).
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
        GridTile[] found = GetComponentsInChildren<GridTile>(true);
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
            Debug.LogWarning("[GridChallenge] GridTile이 없습니다.", this);
            return;
        }

        if (tiles.Length != ExpectedTileCount)
            Debug.LogWarning($"[GridChallenge] 타일 수 {tiles.Length} (권장 {ExpectedTileCount}).", this);

        int emptySlots = 0;
        foreach (GridTile t in tiles)
            if (t == null) emptySlots++;
        if (emptySlots > 0)
            Debug.LogWarning($"[GridChallenge] tiles에 빈 슬롯 {emptySlots}개 — 안전 칸 배정에서 제외됩니다. 인스펙터 확인.", this);

        if (safeRule == GridSafeRule.PlayerColor && (safeTilePhases == null || safeTilePhases.Length == 0))
            Debug.LogWarning("[GridChallenge] safeTilePhases가 비어 있습니다 — 매 라운드 1칸/흑백 최소 0으로 돕니다. " +
                             "그 1칸이 고유색으로 뽑히면 나머지 인원은 설 곳이 없어 라운드를 통과할 수 없습니다 " +
                             "(CoopStageAudit.M.md §8 커브 표 입력 필요).", this);

        if (totalRounds <= 0)
        {
            Debug.LogWarning("[GridChallenge] totalRounds는 1 이상이어야 합니다.", this);
            return;
        }

        if (roundDuration <= 0f)
        {
            Debug.LogWarning("[GridChallenge] roundDuration은 0보다 커야 합니다.", this);
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
        _preRevealedRound = -1;
        PreRevealServerTime = -1.0;
        _currentSafeTiles.Clear();
        _uniqueColorsThisRound.Clear();
        SetAllTilesDefault();
        OnChallengeCancelled?.Invoke();
    }

    /// <summary>
    /// Host: 라운드 시작 = 선행 시작. 새 라운드 시드와 선행 스텝을 배포한다 — ChallengeStart 뒤 곧바로
    /// ChallengeStepBegin을 같은 프레임에 쓰는 원자적 2단 쓰기이므로 Client는 항상 최종 커밋된 값
    /// (새 시드 + 이번 스텝)만 관찰한다 (§11B ②RoundStart). 선행이 0초면 공개 스텝을 바로 쓴다.
    /// 이후 공개·판정·다음 라운드는 RoundRoutine이 이어서 진행한다.
    /// </summary>
    void StartRound(int round)
    {
        if (_judgeCoroutine != null) StopCoroutine(_judgeCoroutine);
        _judgeCoroutine = StartCoroutine(RoundRoutine(round));
    }

    // ── 라운드 진행 (Host 전용 — §11B ②RoundStart / ④Judge / ⑤Resolve) ─────────

    /// <summary>
    /// 한 라운드 전체: 선행 → 공개 → 판정 → (멈춤 + 복구 + 휴식 → 다음 라운드) 또는 (멈춤 + 복구 → 완료).
    /// 복구 연출 자체는 GridTileCollapse가 OnRoundSettled를 받아 settleHoldSeconds 뒤에 재생한다 —
    /// 여기서는 그 시간을 비워 두기만 한다.
    /// </summary>
    IEnumerator RoundRoutine(int round)
    {
        int seed = Random.Range(int.MinValue, int.MaxValue);
        _netState.ChallengeStart(seed, ChallengeOwnerType.Grid);

        if (PreRevealSeconds > 0f)
        {
            _netState.ChallengeStepBegin(PreRevealStep(round));
            yield return new WaitForSeconds(PreRevealSeconds);
            if (_netState == null) yield break;
        }

        _netState.ChallengeStepBegin(RevealStep(round));

        yield return new WaitForSeconds(RoundDurationFor(round));

        List<Player> alive = GatherAlivePlayers();
        EvaluateRound(alive, out bool roundSuccess);
        ApplyIndividualDamage(alive);

        HandleRoundOutcome(round, roundSuccess);
        // Rpc 송신은 캐시(_netState)가 아니라 Instance로 — 캐시는 Despawn 이후에도 살아있어 씬 언로드·
        // 사망 리로드 구간에서 낡은 NetworkObjectId로 메시지가 나간다(수신 측 라우팅 실패 → purge 경고).
        // Instance는 OnNetworkDespawn에서 null이 되므로 `?.`가 그 창구를 닫는다.
        StageNetworkState.Instance?.NotifyChallengeOutcomeClientRpc(roundSuccess);

        // 마지막 라운드도 복구 연출이 보이도록 멈춤 + 복구까지 기다린 뒤 완료한다
        // (완료 순간 GridTileCollapse는 연출 없이 즉시 복구하므로, 먼저 끝나면 복구가 안 보인다).
        float afterSettle = Mathf.Max(0f, settleHoldSeconds) + Mathf.Max(0f, restoreSeconds);
        bool last = round >= totalRounds - 1;
        if (!last) afterSettle += Mathf.Max(0f, restSeconds);

        if (afterSettle > 0f)
            yield return new WaitForSeconds(afterSettle);

        _judgeCoroutine = null;
        if (last) _netState?.ChallengeCleared(true);
        else      StartRound(round + 1);
    }

    // ── 라운드 생성 (전 머신 공통 — StageNetworkState NV 구독, §11B ③Generate) ──

    /// <summary>
    /// StageNetworkState.OnChallengeStepChanged 구독 핸들러. Host/Client 동일 코드.
    /// 짝수 스텝 = 선행 시작(안전 칸 계산만, 숨김), 홀수 스텝 = 공개(칸 색 적용).
    /// 판정은 Host의 RoundRoutine이 담당한다 (§11B ④Judge).
    /// </summary>
    void HandleChallengeStepChanged(int stepIndex)
    {
        // [owner 가드 — GridColorChallenge/GridBWTileChallenge와 동일 원인]
        // _challengeStep 공유 슬롯이 내 것(Grid)이 아니면 무시 (ChallengeOwnerType 정의부 참고,
        // A-B-C-A 회귀의 근본 원인).
        if (_netState == null || _netState.ChallengeOwner != ChallengeOwnerType.Grid) return;
        if (stepIndex < 0) return; // ChallengeStart()의 초기화 신호 — 무시
        if (!isActiveAndEnabled) return; // OnDisable에서 구독 해제하지만, 해제 타이밍 레이스 방어용 가드
        if (tiles == null || tiles.Length == 0) return;

        if (!_isRunning)
        {
            _isRunning = true;
            OnChallengeStarted?.Invoke();
        }

        int round = stepIndex / 2;
        bool reveal = (stepIndex & 1) == 1;
        double stepTime = _netState.ChallengeStepStartServerTime;

        // 선행 처리. 홀수(공개)만 도착한 경우 — 선행 0초이거나 두 NV 쓰기가 한 번에 합쳐져 도착 — 에도
        // 여기서 보충해 OnRoundPreReveal이 항상 먼저 한 번 발생하게 한다.
        if (_preRevealedRound != round)
        {
            _preRevealedRound = round;
            CurrentRoundIndex = round;

            int seed = _netState.ChallengeSeed;
            CurrentRoundSeed = seed;
            PickRandomTiles(new System.Random(seed));

            PreRevealServerTime = reveal ? stepTime - PreRevealSeconds : stepTime;
            OnRoundPreReveal?.Invoke(round);
        }

        if (!reveal) return;

        ApplyTileStates();
        OnRoundStarted?.Invoke(round);
    }

    // ── 결과 반영 (전 머신 공통 — Host는 직접 호출, Client는 ClientRpc로 수신) ──

    /// <summary>Host는 JudgeRoutine에서 직접 호출하므로 이 핸들러는 Client에서만 의미 있음.</summary>
    void HandleChallengeOutcome(bool success)
    {
        if (_netState == null || _netState.ChallengeOwner != ChallengeOwnerType.Grid) return; // owner 가드 — HandleChallengeStepChanged와 동일 이유
        HandleRoundOutcome(CurrentRoundIndex, success);
    }

    void HandleRoundOutcome(int round, bool success)
    {
        OnRoundSettled?.Invoke(round, success);
        SetAllTilesDefault();
        // 정산 후~다음 선행 전엔 안전 칸 없음(IsSafeTileThisRound 계약). 판정·데미지는 이미 끝났다.
        _currentSafeTiles.Clear();
        _uniqueColorsThisRound.Clear();
    }

    /// <summary>
    /// §11 사망 문 진입 확정(StageNetworkState.OnDeathReloadStarted) — Host/Client 공통 구독.
    /// 사망은 이 챌린지의 판정(JudgeRoutine)이 감지할 수 없는 챌린지 축 밖의 사건이라, 여기서 즉시
    /// 판정 코루틴을 끊어 뒤이어 Despawn된 _netState에 NotifyChallengeOutcomeClientRpc를 쏘는 것을
    /// 원천 차단한다 (GridColorChallenge.HandleDeathReloadStarted와 동일 원칙).
    /// </summary>
    void HandleDeathReloadStarted()
    {
        if (_judgeCoroutine != null) { StopCoroutine(_judgeCoroutine); _judgeCoroutine = null; }
        _isRunning = false;
    }

    /// <summary>ChallengeCleared NV 변경 시 Host/Client 공통으로 OnChallengeComplete를 1회 재생 (OX의 OnAllCleared와 동일 패턴).</summary>
    void HandleChallengeClearedChanged(bool cleared)
    {
        if (_netState == null || _netState.ChallengeOwner != ChallengeOwnerType.Grid) return; // owner 가드 — HandleChallengeStepChanged와 동일 이유
        if (!cleared) return;

        _isRunning = false;
        CurrentRoundIndex = -1;
        _preRevealedRound = -1;
        PreRevealServerTime = -1.0;
        _currentSafeTiles.Clear();
        _uniqueColorsThisRound.Clear();
        OnChallengeComplete?.Invoke();
    }

    /// <summary>현재 라운드에 적용할 단계(GridSafePhase)를 배열에서 읽어 반환. 비어 있으면 {1, 0}.</summary>
    GridSafePhase GetCurrentPhase()
    {
        GridSafePhase current = new GridSafePhase { afterRound = 0, tileCount = 1, minBwCount = 0 };
        foreach (GridSafePhase phase in safeTilePhases)
            if (CurrentRoundIndex >= phase.afterRound)
                current = phase;
        return current;
    }

    /// <summary>
    /// §8 풀 구성 + 안전 칸 배치: 활성 고유색 + Black/White 풀에서, 이번 단계의 흑/백 최소 개수를
    /// 먼저 강제 추첨한 뒤 나머지를 전체 풀에서 채운다(거부 샘플링 없이 결정적으로 제약 만족).
    /// 뽑힌 색마다 서로 겹치지 않는 타일 인덱스를 1개씩 배정.
    /// rng는 ChallengeSeed 기반 System.Random — 전 머신이 동일 시드로 호출하면 항상 같은 결과가
    /// 나온다 (UnityEngine.Random 전역 상태 오염 방지 — OXQuizManager와 동일 원칙).
    /// </summary>
    void PickRandomTiles(System.Random rng)
    {
        _currentSafeTiles.Clear();
        _uniqueColorsThisRound.Clear();

        IReadOnlyList<PlayerColorType> activeColors = GetActiveColorsForBoard();

        if (safeRule == GridSafeRule.SharedSolo)
        {
            // 인원 = 활성 고유색 수(PlayerColor 풀과 같은 소스 — 전 머신 동일 보드).
            PickSharedTiles(rng, activeColors.Count + GetSharedExtraTiles());
            return;
        }

        var colorPool = new List<PlayerColorType>(activeColors);
        colorPool.Add(PlayerColorType.Black);
        colorPool.Add(PlayerColorType.White);

        GridSafePhase phase = GetCurrentPhase();
        int tileCount  = Mathf.Clamp(phase.tileCount, 0, colorPool.Count);
        int minBwCount = Mathf.Clamp(phase.minBwCount, 0, tileCount);

        var chosen = new List<PlayerColorType>(tileCount);

        // 1단계: 흑/백 최소 개수를 풀에서 먼저 강제 추첨 (풀에 Black/White가 각 1개뿐이므로 최대 2개)
        var bwAvailable = new List<PlayerColorType>();
        if (colorPool.Contains(PlayerColorType.Black)) bwAvailable.Add(PlayerColorType.Black);
        if (colorPool.Contains(PlayerColorType.White)) bwAvailable.Add(PlayerColorType.White);

        for (int i = 0; i < minBwCount && bwAvailable.Count > 0; i++)
        {
            int pick = rng.Next(0, bwAvailable.Count);
            PlayerColorType c = bwAvailable[pick];
            bwAvailable.RemoveAt(pick);
            colorPool.Remove(c);
            chosen.Add(c);
        }

        // 2단계: 나머지는 남은 풀(고유 + 잔여 흑백) 전체에서 추첨
        while (chosen.Count < tileCount && colorPool.Count > 0)
        {
            int pick = rng.Next(0, colorPool.Count);
            chosen.Add(colorPool[pick]);
            colorPool.RemoveAt(pick);
        }

        // 3단계: 뽑힌 색마다 겹치지 않는 타일 인덱스 배정.
        // null 슬롯은 후보에서 제외한다 — 배정해도 ApplyTileStates가 건너뛰어 화면엔 그 칸이 안 뜨는데
        // _uniqueColorsThisRound에는 그 색이 들어가서, 해당 색 플레이어가 존재하지도 않는 칸을 찾아야
        // 하는 "조용히 통과 불가능한 라운드"가 된다.
        var tileIndexPool = new List<int>(tiles.Length);
        for (int i = 0; i < tiles.Length; i++)
            if (tiles[i] != null) tileIndexPool.Add(i);

        foreach (PlayerColorType c in chosen)
        {
            if (tileIndexPool.Count == 0) break;
            int pick = rng.Next(0, tileIndexPool.Count);
            int tileIndex = tileIndexPool[pick];
            tileIndexPool.RemoveAt(pick);

            _currentSafeTiles[tileIndex] = c;
            if (c == PlayerColorType.Blue || c == PlayerColorType.Purple ||
                c == PlayerColorType.Green || c == PlayerColorType.Yellow)
                _uniqueColorsThisRound.Add(c);
        }
    }

    IReadOnlyList<PlayerColorType> GetActiveColorsForBoard()
    {
        if (GameSession.Instance != null)
            return GameSession.Instance.GetActiveColors();

        // 폴백은 Host/Client가 서로 다른 색 풀을 만들 수 있는 자리다 — Host엔 GameSession이 있고
        // 이 머신엔 없으면 2인 세션인데 여기만 4색 풀이 돼 보드가 조용히 갈라진다(같은 시드를
        // 써도 풀이 다르면 결과가 다름). 추적 가능하도록 1회만 경고한다(라운드마다 찍으면 묻힘).
        if (!_warnedNoGameSession)
        {
            _warnedNoGameSession = true;
            Debug.LogWarning("[GridChallenge] GameSession이 없어 고유색 4색 폴백으로 보드를 만듭니다 — " +
                             "Host와 색 풀이 다르면 Host/Client 보드가 어긋납니다.", this);
        }
        return FallbackColorOrder;
    }

    int GetSharedExtraTiles()
    {
        int extra = 0;
        if (sharedSafePhases != null)
            foreach (GridSharedSafePhase phase in sharedSafePhases)
                if (phase != null && CurrentRoundIndex >= phase.afterRound)
                    extra = phase.extraTiles;
        return Mathf.Max(0, extra);
    }

    /// <summary>SharedSolo: 겹치지 않는 칸 count개를 공용 안전 칸으로. 칸 상태는 Common(색 없음) — 표시는 마커 몫.</summary>
    void PickSharedTiles(System.Random rng, int count)
    {
        var tileIndexPool = new List<int>(tiles.Length);
        for (int i = 0; i < tiles.Length; i++)
            if (tiles[i] != null) tileIndexPool.Add(i);

        for (int n = 0; n < count && tileIndexPool.Count > 0; n++)
        {
            int pick = rng.Next(0, tileIndexPool.Count);
            _currentSafeTiles[tileIndexPool[pick]] = PlayerColorType.Common;
            tileIndexPool.RemoveAt(pick);
        }
    }

    void ApplyTileStates()
    {
        for (int i = 0; i < tiles.Length; i++)
        {
            if (tiles[i] == null) continue;
            tiles[i].SetState(
                _currentSafeTiles.TryGetValue(i, out PlayerColorType state)
                    ? state
                    : PlayerColorType.Common);
        }
    }

    void SetAllTilesDefault()
    {
        foreach (GridTile t in tiles)
            t?.SetState(PlayerColorType.Common);
    }

    /// <summary>
    /// 정산 시점의 라운드 성공 여부. 판정 전에 유령 점유(사망·리셋 잔여)를 먼저 털어낸다.
    /// 라운드 실패 자체는 팀 데미지가 아니라 개인 데미지로만 처리되므로(§8) 여기선 성공 여부만 낸다.
    /// </summary>
    void EvaluateRound(List<Player> alive, out bool roundSuccess)
    {
        roundSuccess = false;

        int required = requiredAliveCount > 0 ? requiredAliveCount : alive.Count;
        if (required <= 0) return;

        foreach (GridTile t in tiles)
            t?.RefreshOccupants();

        if (alive.Count < required) return;

        roundSuccess = CheckAllPlayersPassed(alive);
    }

    /// <summary>
    /// §8 정산: 내 고유색이 이번 라운드 풀에 있으면 고유색 모드 + 내 색 칸, 없으면 흑백 모드 + 내
    /// isBlack과 일치하는 흑/백 칸. 전원 통과해야 라운드 성공.
    /// </summary>
    bool CheckAllPlayersPassed(List<Player> alive)
    {
        foreach (Player p in alive)
            if (!PlayerPassed(p)) return false;
        return alive.Count > 0;
    }

    /// <summary>
    /// 플레이어 1명 판정 (§8 정산).
    ///
    /// [겹침 = 관대 판정, 사용자 확정 2026-09-11] 칸 경계에 걸쳐 서면 캡슐 콜라이더가 트리거 두 개에
    /// 동시에 닿아 양쪽 _occupants에 모두 등록된다. 예전처럼 "대표 칸" 하나를 골라 검사하면 배열
    /// 순서에 따라 내 정답 칸을 밟고 있는데도 옆 칸 기준으로 실패 처리되는 억울한 판정이 났다.
    /// 그래서 대표 칸을 고르지 않고 "내가 밟은 칸 중 내 정답 칸이 하나라도 있으면 통과"로 본다.
    /// 잘못된 칸만 밟고 있으면 정답 칸이 없으니 그대로 실패 — "내 색이 나왔는데 흑백에 서면 데미지"
    /// 규칙은 변하지 않는다.
    /// </summary>
    bool PlayerPassed(Player p)
    {
        if (safeRule == GridSafeRule.SharedSolo)
            return PlayerPassedShared(p);

        bool colorInPool = _uniqueColorsThisRound.Contains(p.playerColorType);

        // 모드가 어긋나면 어느 칸에 서 있든 실패 — 내 색이 나왔는데 흑백 모드이거나, 안 나왔는데
        // 고유색 모드인 경우 (bool 두 개가 같아야 통과).
        if (colorInPool != p.isUniqueColor) return false;

        PlayerColorType required = colorInPool
            ? p.playerColorType
            : (p.isBlack ? PlayerColorType.Black : PlayerColorType.White);

        foreach (GridTile t in tiles)
            if (t != null && t.State == required && t.ContainsPlayer(p)) return true;

        return false;
    }

    /// <summary>
    /// SharedSolo 판정: 안전 칸 하나 이상을 밟고 있고, 밟은 안전 칸이 전부 혼자일 때만 통과.
    /// 경계에 걸쳐 두 칸에 등록된 사람은 양쪽 칸에 세므로, 한쪽 칸이 겹치면 그 사람도 그 칸의 원래 주인도 실패
    /// (사용자 확정 2026-09-23 — M5 관대 판정과 달리 "어느 한 칸이라도 통과"로 보지 않는다).
    /// </summary>
    bool PlayerPassedShared(Player p)
    {
        bool onSafe = false;
        foreach (int index in _currentSafeTiles.Keys)
        {
            GridTile t = tiles[index];
            if (t == null || !t.ContainsPlayer(p)) continue;
            if (CountAliveOccupants(t) >= 2) return false;
            onSafe = true;
        }
        return onSafe;
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

    /// <summary>정산 시 자기 모드에 맞는 안전 칸에 없는 플레이어에게 개인 데미지 적용.</summary>
    void ApplyIndividualDamage(List<Player> alive)
    {
        if (individualDamageOnFail <= 0 || alive.Count == 0) return;

        foreach (Player p in alive)
        {
            if (p == null || p.IsDead) continue;

            // NetworkDamageUtil이 데미지 파이프라인 단일 진입점 — Player.ReceiveDamage() 직접 호출은
            // 온라인 모드에서 Player.TakeDamage()가 즉시 반환(no-op)해 데미지가 전혀 적용되지 않는
            // 버그였음 (architecture.mdc: NetworkDamageUtil 단일 진입점 규칙 위반, GridColorChallenge/
            // GridBWTileChallenge와 동일 원 사례 2026-07-19).
            if (!PlayerPassed(p))
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
