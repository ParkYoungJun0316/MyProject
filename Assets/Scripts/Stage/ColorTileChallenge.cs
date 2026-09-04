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
/// → 할당 충족 시 OnSuccess. 새 NV/RPC 없음.
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

    /// <summary>챌린지 강제 종료 + 타일 정리. Host 전용 (디버그용).</summary>
    public void Cancel()
    {
        if (IsClientOnly()) return;
        if (_judgeCoroutine != null) { StopCoroutine(_judgeCoroutine); _judgeCoroutine = null; }
        _isRunning = false;
        ClearTiles();
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

        _isRunning = true;
        _scoreGeneration = 0;
        _blackScore = 0;
        _whiteScore = 0;
        _blackQuota = QuotaForParty(true);
        _whiteQuota = QuotaForParty(false);
        _uniqueScores.Clear();
        foreach (PlayerColorType color in colors)
            _uniqueScores[color] = 0;

        int seed = _netState != null ? _netState.ChallengeSeed : 0;
        var rng = new System.Random(seed);
        List<int> order = ShuffledSpawnIndices(rng);
        if (order.Count == 0) return false;

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

    IEnumerator JudgeQuotaRoutine()
    {
        while (_isRunning)
        {
            if (!IsClientOnly())
            {
                for (int i = 0; i < _quotaSlots.Count; i++)
                {
                    QuotaSlot slot = _quotaSlots[i];
                    if (slot.tile == null || !slot.tile.HoldReady) continue;
                    if (!SlotNeedsScore(slot)) continue;

                    int spawnIndex = PickFreeSpawnIndex(i);
                    _scoreGeneration++;
                    int packed = PackScoreStep(i, _scoreGeneration);
                    _netState?.ChallengeStepBegin(packed, spawnIndex);
                    break;
                }

                if (QuotasMet())
                {
                    ResolveRound(true);
                    yield break;
                }
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
        MigrateLegacyQuotas();
        int[] table = black ? blackQuotaByPlayerCount : whiteQuotaByPlayerCount;
        if (table == null || table.Length == 0) return 0;
        int party = GameSession.Instance != null
            ? Mathf.Clamp(GameSession.Instance.ActivePlayerCount, 1, 4)
            : 1;
        int i = Mathf.Min(party - 1, table.Length - 1);
        return Mathf.Max(0, table[i]);
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
        for (int n = 1; n <= spawnPoints.Length; n++)
        {
            int candidate = (current + n) % spawnPoints.Length;
            if (spawnPoints[candidate] == null) continue;
            if (used.Contains(candidate)) continue;
            return candidate;
        }

        return current;
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

    List<PlayerColorType> CollectAliveUniqueColors()
    {
        var aliveColors = new HashSet<PlayerColorType>();
        if (GameSession.Instance != null)
        {
            foreach (Player p in GameSession.Instance.GetActivePlayers())
                if (p != null && !p.IsDead) aliveColors.Add(p.playerColorType);
        }
        else
        {
            foreach (Player p in FindObjectsByType<Player>(FindObjectsSortMode.None))
                if (!p.IsDead) aliveColors.Add(p.playerColorType);
        }

        IReadOnlyList<PlayerColorType> activeColors = GameSession.Instance != null
            ? GameSession.Instance.GetActiveColors()
            : (IReadOnlyList<PlayerColorType>)PlayerColorUtil.ColorOrder;

        var colors = new List<PlayerColorType>();
        foreach (PlayerColorType color in activeColors)
            if (aliveColors.Contains(color)) colors.Add(color);
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
