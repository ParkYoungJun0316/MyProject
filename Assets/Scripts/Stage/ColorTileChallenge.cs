using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 색상 타일 점수제 (CoopStageAudit.M §3).
///
/// [축 SSOT: NetworkDesign.md §11B — 챌린지 축(C 패턴)]
/// Host Activate → ChallengeStart + Step 0 → 전 머신 GenerateQuotaSession
/// → Host JudgeQuota → 점수마다 ChallengeStepBegin(packed, spawnIndex)로 재스폰
/// → 할당 충족 시 ChallengeCleared + ResetChallengeStep + OnSuccess. 새 NV/RPC 없음.
///
/// 점수 1건 = 공유 슬롯 NV 쓰기 1건이라, 라운드제와 달리 "놓치면 자기수복되는 다음 라운드"가
/// 없다. 그래서 두 장치가 이 축의 전제를 지킨다 — 판정은 서버 틱당 1건으로 예산을 끊고
/// (JudgeQuotaRoutine), 클리어 시 슬롯을 -1로 되돌린다(ResolveRound).
///
/// 동시 타일 = 생존 고유색 1칸씩 + 흑 1 + 백 1. 할당량만큼 미리 깔지 않음.
/// 2초 점유 → 그 색 +1 → 다른 spawn으로 이동. 그 색 할당이 끝나면 그 칸만 사라짐.
/// </summary>
public class ColorTileChallenge : MonoBehaviour
{
    /// <summary>색상별 프리팹 매핑 항목.</summary>
    [System.Serializable]
    public class ColorTilePrefabEntry
    {
        [Tooltip("고유색 또는 점수제 Black/White. Common/Danger는 쓰지 않음.")]
        public PlayerColorType colorType = PlayerColorType.Blue;

        [Tooltip("해당 색상의 ColorTile 프리팹 (머티리얼·시각 포함)")]
        public GameObject prefab;
    }

    [Tooltip("true: 씬/스테이지 시작 즉시 점수제 시작.\n" +
             "false: 외부에서 Activate()를 직접 호출해야 함.")]
    [SerializeField] bool autoStart = true;

    [Tooltip("같은 owner 타입(ColorTile)의 챌린지가 씬에 여러 개 있을 때 서로 구분하는 고유 ID.\n" +
             "같은 씬의 다른 ColorTileChallenge 인스턴스와 절대 겹치지 않게 설정할 것.")]
    [SerializeField] int challengeInstanceId = 0;

    [Header("타일 프리팹 (고유 4색 + Black + White)")]
    [Tooltip("Blue / Purple / Green / Yellow + Black / White ColorTile 프리팹.")]
    [SerializeField] ColorTilePrefabEntry[] tilePrefabs = new ColorTilePrefabEntry[0];

    [Header("타일 생성")]
    [Tooltip("타일이 등장할 수 있는 위치 풀. 고유 인원+흑+백보다 많게.")]
    [SerializeField] Transform[] spawnPoints = new Transform[0];

    [Header("점수제 (CoopStageAudit.M §3)")]
    [Tooltip("고유색마다 채워야 하는 횟수.")]
    [SerializeField] int uniqueQuota = 0;
    [Tooltip("팀 흑 할당. 인덱스 0=1인 … 3=4인. 해당 칸이 없으면 마지막 칸.")]
    [SerializeField] int[] blackQuotaByPlayerCount = new int[4];
    [Tooltip("팀 백 할당. 인덱스 0=1인 … 3=4인. 해당 칸이 없으면 마지막 칸.")]
    [SerializeField] int[] whiteQuotaByPlayerCount = new int[4];
    [SerializeField, HideInInspector] int blackQuota;
    [SerializeField, HideInInspector] int whiteQuota;
    [Tooltip("유효 점유 유지 시간(초). 기본 2. 발 떼면 리셋.")]
    [SerializeField] float occupySeconds = 2f;

    [Header("이벤트")]
    [Tooltip("챌린지 시작 시 호출")]
    public UnityEvent OnChallengeStarted;

    [Tooltip("할당을 모두 채웠을 때 호출")]
    public UnityEvent OnSuccess;

    [Tooltip("점수 진행 변경 — ObjectiveUI Count.")]
    public UnityEvent OnQuotaChanged;

    bool _isRunning;

    readonly List<ColorTile> _activeTiles = new List<ColorTile>();
    readonly List<QuotaSlot> _quotaSlots = new List<QuotaSlot>();
    readonly Dictionary<PlayerColorType, int> _uniqueScores = new Dictionary<PlayerColorType, int>();
    int _blackScore;
    int _whiteScore;
    int _blackQuota;
    int _whiteQuota;
    int _scoreGeneration;
    int _lastScoreTick;
    System.Random _spawnRng;
    Coroutine _judgeCoroutine;

    StageNetworkState _netState;
    bool _subscribed;

    /// <summary>고유·흑·백 할당이 모두 1 이상이면 점수제 시작 가능.</summary>
    public bool UsesQuotaScoring =>
        uniqueQuota > 0 && QuotaForParty(true) > 0 && QuotaForParty(false) > 0;

    public int QuotaProgress { get; private set; }
    public int QuotaRequired { get; private set; }

    struct QuotaSlot
    {
        public ColorTile tile;
        public PlayerColorType color;
        public int spawnIndex;
    }

    // ── 생명주기 ─────────────────────────────────────────────────

    /// <summary>
    /// 레거시 스칼라 할당 → 인원별 배열 마이그레이션은 여기서 1회만. 예전엔 QuotaForParty()가 매
    /// 호출마다 돌려서 UsesQuotaScoring 같은 프로퍼티를 읽는 것만으로 직렬화 필드가 변형됐다.
    /// OnEnable(아래 TryBindAndSubscribe → UsesQuotaScoring)보다 먼저 도는 것이 보장된다.
    /// </summary>
    void Awake()
    {
        MigrateLegacyQuotas();
    }

    void OnEnable()
    {
        TryBindAndSubscribe();
    }

    /// <summary>
    /// Unity가 실제로 보장하는 건 "씬의 모든 Awake가 끝난 뒤에야 모든 Start가 실행된다"뿐이고,
    /// OnEnable은 이 보장 밖이라 다른 오브젝트(StageNetworkState)의 Awake보다 먼저 돌 수 있다.
    /// 그래서 OnEnable에서만 _netState를 캐시하면 최초 활성화 시점에 null로 굳어버리는 레이스가
    /// 있었다 (2026-07-28 버그 — GridColorChallenge와 동일 원인·동일 골격). Start()는 전역
    /// Awake→Start 순서가 보장되므로(OXQuizManager와 동일 원칙) 여기서 최초 바인딩의 안전망을 맡는다.
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
        ClearTiles();
    }

    void TryBindAndSubscribe()
    {
        if (_subscribed) return;

        _netState ??= StageNetworkState.Instance;
        if (_netState == null) return;

        _netState.OnChallengeStepChanged += HandleChallengeStepChanged;
        _netState.OnChallengeOutcome     += HandleNetChallengeOutcome;
        _netState.OnDeathReloadStarted   += HandleDeathReloadStarted;
        _subscribed = true;

        // Host는 자기 자신이 Writer라(쓰기 시점에 로컬 콜백이 동기로 돌아간다) 이 경로가 필요 없다.
        if (IsClientOnly()) CatchUpClientStep();

        if (autoStart && !IsClientOnly())
        {
            if (UsesQuotaScoring)
                Activate();
            else
                Debug.LogWarning("[ColorTileChallenge] uniqueQuota와 인원별 흑·백 할당이 모두 1 이상이어야 시작합니다.");
        }
    }

    void Unsubscribe()
    {
        if (_netState != null)
        {
            _netState.OnChallengeStepChanged -= HandleChallengeStepChanged;
            _netState.OnChallengeOutcome     -= HandleNetChallengeOutcome;
            _netState.OnDeathReloadStarted   -= HandleDeathReloadStarted;
        }
        _subscribed = false;
    }

    static bool IsClientOnly()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && !nm.IsServer;
    }

    /// <summary>현재 서버 틱. NGO가 안 돌면 -1 — 그 경우 틱 예산 없이 프레임당 1건으로 동작한다.</summary>
    static int CurrentServerTick()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening ? nm.ServerTime.Tick : -1;
    }

    // ── 외부 호출 (Host 전용 — §11B ①Trigger) ─────────────────────

    /// <summary>
    /// 챌린지 즉시 시작. Host 레인만 실제로 진행 — Client의 직접 호출은 무시된다.
    /// </summary>
    public void Activate()
    {
        if (IsClientOnly()) return;
        if (_isRunning) return;
        if (_netState == null) return;
        if (!UsesQuotaScoring)
        {
            Debug.LogWarning("[ColorTileChallenge] uniqueQuota와 인원별 흑·백 할당이 모두 1 이상이어야 시작합니다.");
            return;
        }

        int seed = Random.Range(int.MinValue, int.MaxValue);
        _netState.ChallengeStart(seed, ChallengeOwnerType.ColorTile, challengeInstanceId);
        _netState.ChallengeStepBegin(0);
    }

    /// <summary>
    /// 챌린지 강제 종료 + 타일 정리. Host 전용 (디버그용).
    /// Client에도 같은 정리를 보내고 공유 슬롯을 -1로 되돌린다 — 안 그러면 Host만 판을 치우고
    /// Client엔 타일이 남으며, 낡은 packed 스텝이 슬롯에 남아 다음 시작을 오염시킨다.
    /// </summary>
    public void Cancel()
    {
        if (IsClientOnly()) return;
        if (_judgeCoroutine != null) { StopCoroutine(_judgeCoroutine); _judgeCoroutine = null; }
        bool wasRunning = _isRunning;
        _isRunning = false;
        ClearTiles();

        if (!wasRunning) return;
        _netState?.NotifyChallengeOutcomeClientRpc(false, challengeInstanceId);
        _netState?.ResetChallengeStep();
    }

    // ── 라운드 생성 (전 머신 공통 — StageNetworkState NV 구독, §11B ③Generate) ──

    void HandleChallengeStepChanged(int stepIndex)
    {
        if (_netState == null || _netState.ChallengeOwner != ChallengeOwnerType.ColorTile) return;
        if (_netState.ChallengeInstanceId != challengeInstanceId) return;
        if (stepIndex < 0) return;
        if (!isActiveAndEnabled) return;

        HandleQuotaStepChanged(stepIndex);
    }

    /// <summary>
    /// 늦은 구독 캐치업 (Client 전용). 이 GO가 Phase 컨테이너로 켜지는 시점이 _challengeStep NV
    /// 도착보다 늦으면, 구독 전에 지나간 값 변경은 C# 이벤트라 재생되지 않아 그 스텝을 영구히
    /// 놓친다(§11B.9의 NV 도착 순서 미보장 — SequenceRingMinigame과 같은 원인·같은 처방).
    /// 라운드제는 다음 라운드에 자기수복되지만 점수제는 step 0이 세션당 1회뿐이라 복구 지점이 없다.
    /// 새 경로가 아니라 같은 핸들러를 1회 재실행하는 것.
    ///
    /// 복구 가능한 건 step 0(세션 생성)까지다. packed 점수 스텝(>0)이면 그 시점 seed 칸이 이미
    /// 마지막 재스폰 인덱스로 덮여 있어(ChallengeStepBegin(stepIndex, seed) 규약) 원래 세션 시드를
    /// 되찾을 수 없다 — 조용히 빈 판이 되지 않도록 경고만 남긴다.
    /// </summary>
    void CatchUpClientStep()
    {
        if (_netState.ChallengeOwner != ChallengeOwnerType.ColorTile) return;
        if (_netState.ChallengeInstanceId != challengeInstanceId) return;

        int stepIndex = _netState.ChallengeStepIndex;
        if (stepIndex == 0)
        {
            HandleChallengeStepChanged(stepIndex);
            return;
        }

        if (stepIndex > 0)
            Debug.LogWarning($"[ColorTileChallenge] 진행 중인 점수제 세션(step {stepIndex})에 늦게 합류 — 이 머신은 타일을 재구성할 수 없습니다.");
    }

    void HandleDeathReloadStarted()
    {
        if (_judgeCoroutine != null) { StopCoroutine(_judgeCoroutine); _judgeCoroutine = null; }
        _isRunning = false;
    }

    void HandleNetChallengeOutcome(bool success)
    {
        HandleChallengeOutcome(success, _netState != null ? _netState.LastChallengeOutcomeInstanceId : 0);
    }

    void HandleChallengeOutcome(bool success, int instanceId)
    {
        if (_netState == null || _netState.ChallengeOwner != ChallengeOwnerType.ColorTile) return;
        if (instanceId != challengeInstanceId) return;

        ClearTiles();
        _isRunning = false;

        if (success)
            OnSuccess?.Invoke();
    }

    void ResolveRound(bool success)
    {
        _judgeCoroutine = null;
        HandleChallengeOutcome(success, challengeInstanceId);
        _netState?.NotifyChallengeOutcomeClientRpc(success, challengeInstanceId);

        if (!success) return;

        // ⑤Resolve 클리어 신호 — 형제 챌린지(GridBW/GridColor/SideSplit/SequenceRing)와 동일 계약.
        // ResetChallengeStep()까지 부르는 이유는 SequenceRing과 같다: 공유 슬롯에 packed 점수
        // stepIndex(≫0)가 남아 있으면 같은 슬롯을 쓰는 다음 챌린지가 늦게 활성화될 때 그 값을
        // "이미 진행 중"으로 오인한다(StageNetworkState.ResetChallengeStep 주석). 위
        // CatchUpClientStep이 step 0만 복구 대상으로 삼는 것도 이 리셋을 전제로 성립한다.
        _netState?.ChallengeCleared(true);
        _netState?.ResetChallengeStep();
    }

    void ClearTiles()
    {
        foreach (ColorTile t in _activeTiles)
        {
            if (t == null) continue;
            t.gameObject.SetActive(false);
            Destroy(t.gameObject);
        }
        _activeTiles.Clear();
        _quotaSlots.Clear();
    }

    GameObject GetPrefabForColor(PlayerColorType colorType)
    {
        foreach (ColorTilePrefabEntry entry in tilePrefabs)
        {
            if (entry.colorType == colorType)
                return entry.prefab;
        }
        return null;
    }

    // ── 점수제 (Host 판정 + ChallengeStep NV 재스폰, 새 RPC 없음) ──

    void HandleQuotaStepChanged(int stepIndex)
    {
        if (stepIndex == 0)
        {
            if (!GenerateQuotaSession())
                return;
            OnChallengeStarted?.Invoke();
            RefreshQuotaProgress();
            if (!IsClientOnly())
            {
                if (_judgeCoroutine != null) StopCoroutine(_judgeCoroutine);
                _judgeCoroutine = StartCoroutine(JudgeQuotaRoutine());
            }
            return;
        }

        ApplyQuotaScore(stepIndex);
    }

    bool GenerateQuotaSession()
    {
        List<PlayerColorType> colors = CollectAliveUniqueColors();
        if (colors.Count == 0 || spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[ColorTileChallenge] 점수제: 생존 색 또는 spawnPoints가 없습니다.");
            return false;
        }

        int needed = colors.Count + 2;
        if (spawnPoints.Length < needed)
            Debug.LogWarning($"[ColorTileChallenge] 점수제 spawnPoints {spawnPoints.Length}개 — 고유+흑+백 {needed}개 이상 권장.");

        int seed = _netState != null ? _netState.ChallengeSeed : 0;
        var rng = new System.Random(seed);
        List<int> order = ShuffledSpawnIndices(rng);
        if (order.Count == 0)
        {
            Debug.LogWarning("[ColorTileChallenge] 점수제: spawnPoints 슬롯이 전부 비어 있습니다(참조 미연결).");
            return false;
        }

        if (!HasPrefabsForQuota(colors)) return false;

        // ── 여기부터 상태 변경 ────────────────────────────────────
        // 위 실패 경로가 이 아래에 있으면 _isRunning이 true로 굳은 채 판정 코루틴은 안 돌고,
        // Activate()의 `if (_isRunning) return;`이 재시도까지 막아 비활성화될 때까지 영구 정지한다.
        _isRunning = true;
        _scoreGeneration = 0;
        _lastScoreTick = int.MinValue;
        _spawnRng = rng; // 초기 셔플에 쓴 스트림을 재스폰 추첨까지 이어 쓴다 (PickFreeSpawnIndex 참고)
        _blackScore = 0;
        _whiteScore = 0;
        _blackQuota = QuotaForParty(true);
        _whiteQuota = QuotaForParty(false);
        _uniqueScores.Clear();
        foreach (PlayerColorType color in colors)
            _uniqueScores[color] = 0;

        ClearTiles();

        var used = new HashSet<int>();
        int cursor = 0;
        int TakeSpawn()
        {
            for (int n = 0; n < order.Count; n++)
            {
                int idx = order[(cursor + n) % order.Count];
                if (used.Add(idx))
                {
                    cursor += n + 1;
                    return idx;
                }
            }

            return order[cursor % order.Count];
        }

        foreach (PlayerColorType color in colors)
            SpawnQuotaTile(color, TakeSpawn());

        SpawnQuotaTile(PlayerColorType.Black, TakeSpawn());
        SpawnQuotaTile(PlayerColorType.White, TakeSpawn());
        return true;
    }

    /// <summary>
    /// 할당량이 걸린 색(생존 고유색 + 흑 + 백) 전부에 프리팹이 등록돼 있는지.
    /// 하나라도 없으면 그 색은 타일이 안 깔리는데 QuotasMet()은 계속 그 할당을 요구해서, 판이
    /// 승리 불가인 채로 targetTime 초과 Fail까지 조용히 흘러간다. 할당을 빼는 건 §3 잠금("흑백
    /// 의무 0 금지") 위반이므로 대신 시작 자체를 막고 무엇이 빠졌는지 로그로 남긴다.
    /// </summary>
    bool HasPrefabsForQuota(List<PlayerColorType> colors)
    {
        var missing = new List<PlayerColorType>();

        foreach (PlayerColorType color in colors)
            if (GetPrefabForColor(color) == null) missing.Add(color);

        if (QuotaForParty(true) > 0 && GetPrefabForColor(PlayerColorType.Black) == null)
            missing.Add(PlayerColorType.Black);
        if (QuotaForParty(false) > 0 && GetPrefabForColor(PlayerColorType.White) == null)
            missing.Add(PlayerColorType.White);

        if (missing.Count == 0) return true;

        Debug.LogWarning($"[ColorTileChallenge] 점수제 프리팹 없음 ({string.Join(", ", missing)}) — " +
                         "그 색 할당은 아무도 채울 수 없어 시작하지 않습니다. tilePrefabs에 등록하세요.");
        return false;
    }

    /// <summary>
    /// §11B ④Judge — Host 레인 전용. 이 코루틴은 HandleQuotaStepChanged가 Host에서만 시작하므로
    /// 루프 안에 IsClientOnly() 가드를 다시 두지 않는다(호스트 마이그레이션이 없어 레인이 도중에
    /// 바뀌지 않는다 — NetworkDesign.md §12).
    /// </summary>
    IEnumerator JudgeQuotaRoutine()
    {
        while (_isRunning)
        {
            // 서버 틱당 최대 1점수. NGO는 네트워크 틱마다 "그 시점의 최종값"만 스냅샷으로
            // 보내므로(StageNetworkState.ChallengeCleared 주석의 그 성질), 프레임당 1건으로
            // 끊는 것만으로는 부족하다 — 60~144fps에 틱 30이면 연속 두 프레임이 같은 틱에
            // 들어가 앞 점수가 Client에 아예 도착하지 않는다. 그러면 그 클라의 타일은 옛 자리에
            // 남아 유령 타일이 되고(판정은 Host 타일 위치 기준이라 Host만 정상으로 보임) 점수
            // HUD도 영구히 어긋난다. generation은 유실을 감지도 복구도 하지 않으므로 애초에
            // 유실이 불가능하게 만든다. 2초 점유 게임에 1틱(≈33ms) 지연은 체감이 없다.
            int tick = CurrentServerTick();
            if (tick < 0 || tick != _lastScoreTick)
            {
                for (int i = 0; i < _quotaSlots.Count; i++)
                {
                    QuotaSlot slot = _quotaSlots[i];
                    if (slot.tile == null || !slot.tile.HoldReady) continue;
                    if (!SlotNeedsScore(slot)) continue;

                    int spawnIndex = PickFreeSpawnIndex(i);
                    _scoreGeneration++;
                    int packed = PackScoreStep(i, _scoreGeneration);
                    _lastScoreTick = tick;
                    _netState?.ChallengeStepBegin(packed, spawnIndex);
                    break;
                }
            }

            if (QuotasMet())
            {
                ResolveRound(true);
                yield break;
            }

            yield return null;
        }
    }

    void ApplyQuotaScore(int packed)
    {
        UnpackScoreStep(packed, out int slotIndex, out int generation);
        if (slotIndex < 0 || slotIndex >= _quotaSlots.Count) return;

        QuotaSlot slot = _quotaSlots[slotIndex];
        if (slot.tile != null)
            slot.tile.PlayScoreSfx();

        switch (slot.color)
        {
            case PlayerColorType.Black:
                _blackScore++;
                break;
            case PlayerColorType.White:
                _whiteScore++;
                break;
            default:
                if (_uniqueScores.ContainsKey(slot.color))
                    _uniqueScores[slot.color]++;
                else
                    _uniqueScores[slot.color] = 1;
                break;
        }

        _scoreGeneration = Mathf.Max(_scoreGeneration, generation);
        RefreshQuotaProgress();

        int newSpawn = _netState != null ? _netState.ChallengeSeed : slot.spawnIndex;
        if (newSpawn < 0 || spawnPoints == null || newSpawn >= spawnPoints.Length)
            newSpawn = slot.spawnIndex;

        if (!SlotNeedsScore(_quotaSlots[slotIndex]))
        {
            DespawnQuotaSlot(slotIndex);
            return;
        }

        MoveQuotaTile(slotIndex, newSpawn);
    }

    bool SlotNeedsScore(QuotaSlot slot)
    {
        switch (slot.color)
        {
            case PlayerColorType.Black:
                return _blackScore < _blackQuota;
            case PlayerColorType.White:
                return _whiteScore < _whiteQuota;
            default:
                _uniqueScores.TryGetValue(slot.color, out int scored);
                return scored < uniqueQuota;
        }
    }

    bool QuotasMet()
    {
        if (_blackScore < _blackQuota || _whiteScore < _whiteQuota) return false;
        foreach (var kv in _uniqueScores)
        {
            if (kv.Value < uniqueQuota) return false;
        }
        return _uniqueScores.Count > 0;
    }

    void RefreshQuotaProgress()
    {
        int required = _blackQuota + _whiteQuota;
        int progress = Mathf.Min(_blackScore, _blackQuota) + Mathf.Min(_whiteScore, _whiteQuota);
        foreach (var kv in _uniqueScores)
        {
            required += uniqueQuota;
            progress += Mathf.Min(kv.Value, uniqueQuota);
        }

        QuotaRequired = required;
        QuotaProgress = progress;
        OnQuotaChanged?.Invoke();
    }

    void SpawnQuotaTile(PlayerColorType color, int spawnIndex)
    {
        if (spawnPoints == null || spawnIndex < 0 || spawnIndex >= spawnPoints.Length) return;
        Transform point = spawnPoints[spawnIndex];
        if (point == null) return;

        GameObject prefab = GetPrefabForColor(color);
        if (prefab == null)
        {
            Debug.LogWarning($"[ColorTileChallenge] 점수제 프리팹 없음 ({color}). tilePrefabs에 등록하세요.");
            return;
        }

        GameObject tileObj = Instantiate(prefab, point.position, Quaternion.identity);
        ColorTile tile = tileObj.GetComponent<ColorTile>();
        if (tile == null) tile = tileObj.AddComponent<ColorTile>();
        tile.SetupQuota(color, occupySeconds);

        _activeTiles.Add(tile);
        _quotaSlots.Add(new QuotaSlot
        {
            tile = tile,
            color = color,
            spawnIndex = spawnIndex,
        });
    }

    void MoveQuotaTile(int slotIndex, int spawnIndex)
    {
        QuotaSlot slot = _quotaSlots[slotIndex];
        if (slot.tile == null || spawnPoints == null || spawnIndex < 0 || spawnIndex >= spawnPoints.Length)
            return;
        Transform point = spawnPoints[spawnIndex];
        if (point == null) return;

        slot.tile.transform.position = point.position;
        slot.tile.ResetHold();
        slot.spawnIndex = spawnIndex;
        _quotaSlots[slotIndex] = slot;
    }

    void DespawnQuotaSlot(int slotIndex)
    {
        QuotaSlot slot = _quotaSlots[slotIndex];
        if (slot.tile != null)
        {
            _activeTiles.Remove(slot.tile);
            slot.tile.gameObject.SetActive(false);
            Destroy(slot.tile.gameObject);
        }

        slot.tile = null;
        _quotaSlots[slotIndex] = slot;
    }

    int QuotaForParty(bool black)
    {
        int[] table = black ? blackQuotaByPlayerCount : whiteQuotaByPlayerCount;
        if (table == null || table.Length == 0) return 0;
        int i = Mathf.Min(PartySize() - 1, table.Length - 1);
        return Mathf.Max(0, table[i]);
    }

    /// <summary>
    /// 인원(1~4). 흑·백 할당은 전 머신이 같은 값을 봐야 한다 — 각 머신이 SlotNeedsScore로 같은
    /// 시점에 그 칸을 없애기 때문에, 값이 갈리면 타일 배치가 어긋난다. 그래서 CollectAliveUniqueColors와
    /// 같은 우선순위로 PSC NetworkList를 먼저 본다(GameSession.ActivePlayerCount는 그 파생값이라
    /// 씬 로드 직후 아직 0일 수 있다).
    /// </summary>
    static int PartySize()
    {
        int registered = PlayerSpawnCoordinator.EntryCount;
        if (registered > 0) return Mathf.Clamp(registered, 1, 4);

        return GameSession.Instance != null
            ? Mathf.Clamp(GameSession.Instance.ActivePlayerCount, 1, 4)
            : 1;
    }

    void MigrateLegacyQuotas()
    {
        MigrateScalarQuota(ref blackQuotaByPlayerCount, ref blackQuota);
        MigrateScalarQuota(ref whiteQuotaByPlayerCount, ref whiteQuota);
    }

    static void MigrateScalarQuota(ref int[] table, ref int legacy)
    {
        if (legacy <= 0) return;
        bool any = false;
        if (table != null)
        {
            for (int i = 0; i < table.Length; i++)
            {
                if (table[i] > 0)
                {
                    any = true;
                    break;
                }
            }
        }

        if (any)
        {
            legacy = 0;
            return;
        }

        if (table == null || table.Length == 0)
            table = new int[4];
        for (int i = 0; i < table.Length; i++)
            table[i] = legacy;
        legacy = 0;
    }

#if UNITY_EDITOR
    void OnValidate() => MigrateLegacyQuotas();
#endif

    /// <summary>
    /// 점수 후 옮겨갈 자리 — 지금 자리와 다른 살아있는 타일이 쓰는 자리를 뺀 빈 자리 중 무작위 1개.
    /// 빈 자리가 없으면(스폰 포인트가 필요 개수보다 적은 배치) 제자리 유지.
    ///
    /// Host 레인에서만 호출되고 결과는 ChallengeStepBegin(packed, spawnIndex)로 전 머신에 그대로
    /// 전달된다 — 즉 여기서 뽑는 난수는 머신 간에 일치할 필요가 없다(③Generate의 시드 재현이 아니라
    /// ④Judge의 산출물). 그래도 전역 UnityEngine.Random을 오염시키지 않도록 세션 시드로 만든
    /// System.Random 스트림을 이어 쓴다 — GridColor/SequenceRing과 동일 원칙.
    /// </summary>
    int PickFreeSpawnIndex(int slotIndex)
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return 0;

        var used = new HashSet<int>();
        for (int i = 0; i < _quotaSlots.Count; i++)
        {
            if (i == slotIndex) continue;
            if (_quotaSlots[i].tile == null) continue;
            used.Add(_quotaSlots[i].spawnIndex);
        }

        int current = _quotaSlots[slotIndex].spawnIndex;

        var free = new List<int>(spawnPoints.Length);
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (i == current) continue;
            if (spawnPoints[i] == null) continue;
            if (used.Contains(i)) continue;
            free.Add(i);
        }

        if (free.Count == 0) return current;

        return free[_spawnRng != null ? _spawnRng.Next(0, free.Count) : 0];
    }

    List<int> ShuffledSpawnIndices(System.Random rng)
    {
        var list = new List<int>(spawnPoints.Length);
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] != null)
                list.Add(i);
        }

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }

    /// <summary>
    /// 이번 세션의 고유색 슬롯 목록. 슬롯 인덱스가 PackScoreStep으로 네트워크를 타므로 이 목록은
    /// 전 머신에서 개수와 순서가 반드시 같아야 한다 — 그래서 순서는 소스가 무엇이든
    /// PlayerColorUtil.ColorOrder(colorIndex) 기준으로 정규화한다(PSC의 GetActiveColors()는
    /// HashSet 열거 결과라 그 배열 순서 자체는 계약이 아니다).
    ///
    /// 색 소스 우선순위는 GameSessionColorDistribution과 같다 — ① PSC NetworkList(레이스 없는
    /// SSOT) → ② GameSession 활성색(그 파생) → ③ ColorOrder 4색(최종 폴백).
    /// </summary>
    List<PlayerColorType> CollectAliveUniqueColors()
    {
        var activeColors = new HashSet<PlayerColorType>();
        foreach (PlayerColorType color in PlayerSpawnCoordinator.GetActiveColors())
            activeColors.Add(color);

        if (activeColors.Count == 0 && GameSession.Instance != null)
            foreach (PlayerColorType color in GameSession.Instance.GetActiveColors())
                activeColors.Add(color);

        if (activeColors.Count == 0)
            foreach (PlayerColorType color in PlayerColorUtil.ColorOrder)
                activeColors.Add(color);

        var aliveColors = new HashSet<PlayerColorType>();
        if (GameSession.Instance != null)
        {
            foreach (Player p in GameSession.Instance.GetActivePlayers())
                if (p != null && !p.IsDead) aliveColors.Add(p.playerColorType);
        }
        else
        {
            foreach (Player p in FindObjectsByType<Player>(FindObjectsSortMode.None))
                if (p != null && !p.IsDead) aliveColors.Add(p.playerColorType);
        }

        // Player 목록이 아직 안 채워진 머신은 활성색 전체를 생존으로 본다. step 0에는 아무도 죽지
        // 않았으므로 이게 정답이고, 무엇보다 "한쪽 머신만 슬롯이 적은" 상태를 만들지 않는다 —
        // GameSession._activePlayers는 OnPlayersReady로 채워지는 파생값이라 머신마다 시점이 다르다
        // (2026-07-28 M.Stage3 ColorTile 미생성 버그와 같은 뿌리, GameSession.OnSceneLoaded 주석).
        if (aliveColors.Count == 0)
            aliveColors = activeColors;

        var colors = new List<PlayerColorType>();
        foreach (PlayerColorType color in PlayerColorUtil.ColorOrder)
            if (activeColors.Contains(color) && aliveColors.Contains(color)) colors.Add(color);
        return colors;
    }

    static int PackScoreStep(int slot, int generation) => (generation << 8) | (slot & 0xFF);

    static void UnpackScoreStep(int packed, out int slot, out int generation)
    {
        slot = packed & 0xFF;
        generation = packed >> 8;
    }

    // ── 에디터 ──────────────────────────────────────────────────

    [ContextMenu("테스트: 챌린지 시작")]
    void Debug_Activate() => Activate();

    [ContextMenu("테스트: 챌린지 취소")]
    void Debug_Cancel() => Cancel();

    void OnDrawGizmos()
    {
        if (spawnPoints == null) return;
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.6f);
        foreach (Transform sp in spawnPoints)
        {
            if (sp == null) continue;
            Gizmos.DrawWireCube(sp.position, Vector3.one * 0.8f);
        }
    }
}
