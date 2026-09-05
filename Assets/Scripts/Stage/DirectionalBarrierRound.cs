using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 방향별 베리어 라운드 매니저.
///
/// [축 SSOT: NetworkDesign.md §11B — 챌린지 축(C 패턴), ColorTileChallenge와 동일 골격 복제]
/// Host만 라운드 시드를 StageNetworkState로 배포하고, 전 머신이 그 시드 기반 System.Random으로
/// 동일한 색 배치를 재생성한다 (버그 수정 2026-07-27 — 이전엔 전 피어가 각자 UnityEngine.Random으로
/// 셔플해 Host/Client 색 배치가 갈라졌음. 위치는 씬 공유 Transform이라 항상 맞았음).
///
/// [흐름 — 2단계로 분리 (2026-09-05)]
///  1. Reveal(): 베리어 스폰 포인트 4곳에 색상 프리팹을 시드 기반으로 배치 + 전부 Open.
///     자동으로 안 닫힌다 — 다이얼로그 등 프리뷰 구간 내내 열어둘 수 있다.
///  2. CloseAndSpawnTiles(): 열려 있던 베리어를 전부 Close(하강) + 타일 스폰 — 진짜 라운드 시작.
///     Reveal()을 건너뛰고 곧장 호출해도 그 자리에서 스폰부터 자동 수행한다(프리뷰 없이 즉시 시작,
///     M.Boss 같은 무프리뷰 씬용).
///  3. 타일 밟으면 해당 색 베리어만 Open, 나머지 Close (HandleTileActivated)
///
/// [베리어 규칙]
///  - 한 번에 하나만 Open (토글 없음)
///
/// [권장 연결 — M.Stage1]
///  Phase0 onPhaseEnter → PhaseDialogueGate.Begin()(대화)과 동시에 → Reveal()(미리보기 시작)
///  PhaseDialogueGate.OnAllReady → StageStartGate.Arm()
///  StageStartGate.OnCountdownComplete → CloseAndSpawnTiles()
///
/// [시작]
///  - autoStart = true: GO가 켜지면 Host가 즉시 Reveal() (프리뷰 없이 바로 배치하고 싶을 때)
///  - autoStart = false: 위 권장 연결대로 Reveal()/CloseAndSpawnTiles()를 외부에서 호출
///
/// [Inspector 필수 설정]
///  barrierSpawnPoints : 동/서/남/북 스폰 위치 4개
///  barrierPrefabs     : Blue/Purple/Green/Yellow + White/Black DoorController 포함 프리팹
///  tileSpawnPoints    : 타일 스폰 위치 (색 종류 수 이상)
///  tilePrefabs        : Blue/Purple/Green/Yellow + White/Black ColorTile 프리팹
///  debugAllTiles      : true = 플레이어 체크 없이 누구나 밟기 가능 (테스트용)
/// </summary>
public class DirectionalBarrierRound : MonoBehaviour
{
    // _challengeStep 슬롯의 stepIndex 의미 — Reveal(배치+Open, 안 닫힘)과 CloseAndSpawnTiles
    // (Close+타일 스폰)를 서로 다른 네트워크 신호로 분리해 Host/Client가 각각 언제 재생할지 구분한다.
    const int RevealStep = 0;
    const int CloseStep  = 1;

    public enum SpawnDirection
    {
        NorthSouth, // 북/남 — 프리팹 회전 그대로
        EastWest,   // 동/서 — 프리팹 회전에 Y -90 추가
    }

    [System.Serializable]
    public class BarrierSpawnPoint
    {
        [Tooltip("베리어가 생성될 위치")]
        public Transform point;

        [Tooltip("NorthSouth: 프리팹 회전 그대로 / EastWest: Y -90 추가 적용")]
        public SpawnDirection direction = SpawnDirection.NorthSouth;
    }

    [System.Serializable]
    public class BarrierPrefabEntry
    {
        [Tooltip("이 프리팹이 대응하는 플레이어 고유색")]
        public PlayerColorType colorType;

        [Tooltip("DoorController가 포함된 베리어 프리팹 (색상 비주얼 포함)")]
        public GameObject prefab;
    }

    [System.Serializable]
    public class TilePrefabEntry
    {
        [Tooltip("이 프리팹이 대응하는 플레이어 고유색")]
        public PlayerColorType colorType;

        [Tooltip("ColorTile 프리팹")]
        public GameObject prefab;
    }

    [Header("베리어 스폰 위치 (동/서/남/북 4개)")]
    [Tooltip("point: 스폰 위치 / direction: NorthSouth=프리팹 그대로, EastWest=Y-90 추가")]
    [SerializeField] BarrierSpawnPoint[] barrierSpawnPoints = new BarrierSpawnPoint[4];

    [Header("베리어 프리팹 (색상별 4개)")]
    [Tooltip("Blue / Purple / Green / Yellow — DoorController + 색상 비주얼이 포함된 프리팹")]
    [SerializeField] BarrierPrefabEntry[] barrierPrefabs = new BarrierPrefabEntry[4];

    [Header("타일 스폰 위치 (4개 이상)")]
    [SerializeField] Transform[] tileSpawnPoints = new Transform[0];

    [Header("타일 프리팹 (색상별 4개)")]
    [Tooltip("Blue / Purple / Green / Yellow ColorTile 프리팹")]
    [SerializeField] TilePrefabEntry[] tilePrefabs = new TilePrefabEntry[4];

    [Header("라운드 시작")]
    [Tooltip("true: GO가 켜지면 Host가 즉시 Reveal() (프리뷰 없이 바로 배치+Open)\n" +
             "false: Reveal()/CloseAndSpawnTiles()를 외부에서 호출 (M.Stage1 권장 연결 참고)")]
    [SerializeField] bool autoStart = false;

    [Header("테스트")]
    [Tooltip("true: 플레이어 색/isUniqueColor 체크 없이 누구든 타일 밟기 가능")]
    [SerializeField] bool debugAllTiles = true;

    [Header("이벤트")]
    public UnityEvent OnRoundStarted;

    // 색 → 이번 라운드에 스폰된 DoorController 목록 (동일 색이 여러 슬롯에 배정될 수 있음)
    readonly Dictionary<PlayerColorType, List<DoorController>> _colorToDoors = new();

    readonly List<GameObject> _spawnedBarriers = new();
    readonly List<ColorTile>  _activeTiles      = new();

    // 이번 라운드 4슬롯에 배정된 색 목록 (§2.1 확정 표 — BuildBarrierSlots. 균등 분배 아님)
    PlayerColorType[] _roundColors;

    StageNetworkState _netState;
    bool _subscribed;

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
        // Phase가 이 GameObject를 끌 때 구독 해제 + 스폰 정리 — 다른 챌린지가 그 뒤에
        // _challengeStep을 쓸 때 이 컴포넌트가 반응하지 않도록 반드시 여기서 해제해야 한다.
        Unsubscribe();

        ClearBarriers();
        ClearTiles();
    }

    /// <summary>
    /// _netState 바인딩 + 구독 + Host 라운드 발동을 한 곳에 모은 진입점. OnEnable과 Start 양쪽에서
    /// 호출되지만 _subscribed 가드로 중복 구독을 막는다 (GridColorChallenge.TryBindAndSubscribe와
    /// 동일 원칙 — 최초 활성화는 Start가 안전망, Phase 재활성화는 OnEnable이 재구독 전담).
    /// </summary>
    void TryBindAndSubscribe()
    {
        if (_subscribed) return;

        _netState ??= StageNetworkState.Instance;
        if (_netState == null) return;

        _netState.OnChallengeStepChanged += HandleChallengeStepChanged;
        _subscribed = true;

        // late-subscribe catch-up: Host는 이 GameObject가 켜지자마자 Reveal()로 _challengeStep을
        // 쓰지만, Client는 접속·씬 로드·StageNetworkState replica 스폰을 거쳐야 해서 항상 Host보다
        // 늦게 이 시점이 온다. 그 시점엔 이미 stepIndex가 "초기 스폰값"으로 도착해 있어
        // OnValueChanged(변경 이벤트)가 발동하지 않는 NGO 표준 동작 때문에 HandleChallengeStepChanged가
        // 영원히 안 불렸다(Client 화면에 베리어·타일이 아예 안 나오던 증상). 구독 직후 현재 NV 값으로
        // 1회 강제 재실행해 놓친 이벤트를 보정 — Host는 이 시점에 아직 -1이라 중복 발동 없음.
        if (_netState.ChallengeStepIndex >= 0)
            HandleChallengeStepChanged(_netState.ChallengeStepIndex);

        // Host 레인만 라운드를 발동(§11B ①Trigger+②RoundStart) — Client는 NV 전파만 관찰.
        // autoStart=false면 Reveal()/CloseAndSpawnTiles()를 외부(다이얼로그·게이트)에서 기다린다.
        if (autoStart && !IsClientOnly())
            Reveal();
    }

    void Unsubscribe()
    {
        if (_netState != null)
            _netState.OnChallengeStepChanged -= HandleChallengeStepChanged;
        _subscribed = false;
    }

    /// <summary>Client/Host 공통. Host 레인 여부만 다르게 취급 (OXQuizManager/ColorTileChallenge와 동일).</summary>
    static bool IsClientOnly()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && !nm.IsServer;
    }

    // ── 외부 호출 (Host 전용 — §11B ①Trigger) ─────────────────────

    /// <summary>
    /// 베리어를 시드 기반으로 배치하고 전부 Open한 채로 유지한다 — 자동으로 안 닫힌다.
    /// 씬 진입(다이얼로그 등) 시점에 호출해 색↔슬롯 매핑을 미리 보여주는 용도. Host 레인만
    /// 실제로 새 시드를 배포한다 — Client의 직접 호출은 무시된다.
    /// </summary>
    public void Reveal()
    {
        if (IsClientOnly()) return;
        if (_netState == null) return;

        int seed = Random.Range(int.MinValue, int.MaxValue);
        _netState.ChallengeStart(seed, ChallengeOwnerType.DirectionalBarrier);
        _netState.ChallengeStepBegin(RevealStep);
    }

    /// <summary>
    /// Reveal()로 계속 열려 있던 베리어를 전부 Close(하강)하고 타일을 스폰한다 — 진짜 라운드 시작.
    /// StageStartGate.OnCountdownComplete에 연결. Host 레인만 실제로 신호를 보낸다 — Client의
    /// 직접 호출은 무시된다. Reveal()이 먼저 호출된 적 없으면(ChallengeOwner 불일치) 여기서
    /// 새로 소유권+시드를 잡는다 — Reveal() 없이 이 메서드만 호출해도 항상 동작한다(M.Boss 등
    /// 무프리뷰 씬용, CloseBarriersAndSpawnTiles의 late-subscribe 복구 경로와 같은 이유).
    /// </summary>
    public void CloseAndSpawnTiles()
    {
        if (IsClientOnly()) return;
        if (_netState == null) return;

        if (_netState.ChallengeOwner != ChallengeOwnerType.DirectionalBarrier)
        {
            int seed = Random.Range(int.MinValue, int.MaxValue);
            _netState.ChallengeStart(seed, ChallengeOwnerType.DirectionalBarrier);
        }

        _netState.ChallengeStepBegin(CloseStep);
    }

    // ── 라운드 흐름 (전 머신 공통 — StageNetworkState NV 구독, §11B ③Generate) ──

    /// <summary>
    /// StageNetworkState.OnChallengeStepChanged 구독 핸들러. Host/Client 동일 코드로 재생한다.
    /// 색 배치는 ChallengeSeed 기반 System.Random이라 전 머신이 항상 같은 결과를 낸다.
    /// </summary>
    void HandleChallengeStepChanged(int stepIndex)
    {
        // [버그 수정 2026-07-28] _challengeStep 공유 슬롯 owner 가드 — 내 것(DirectionalBarrier)이
        // 아니면 무시(ChallengeOwnerType 정의부 참고, A-B-C-A 회귀의 근본 원인).
        if (_netState == null || _netState.ChallengeOwner != ChallengeOwnerType.DirectionalBarrier) return;
        if (stepIndex < 0) return; // ChallengeStart()의 초기화 신호 — 무시
        if (!isActiveAndEnabled) return; // OnDisable에서 구독 해제하지만, 해제 타이밍 레이스 방어용 가드

        if (stepIndex == RevealStep)
            RevealBarriers();
        else if (stepIndex == CloseStep)
            CloseBarriersAndSpawnTiles();
    }

    void RevealBarriers()
    {
        ClearTiles();

        int seed = _netState != null ? _netState.ChallengeSeed : 0;
        SpawnBarriers(new System.Random(seed));

        foreach (List<DoorController> doors in _colorToDoors.Values)
            foreach (DoorController door in doors)
                door?.Open();
    }

    void CloseBarriersAndSpawnTiles()
    {
        // 늦게 구독한 Client가 RevealStep 신호를 놓치고 곧장 CloseStep으로 캐치업하는 경우
        // (late-subscribe catch-up, TryBindAndSubscribe 참고) — 베리어가 아직 없으므로 여기서
        // 스폰부터 복구한다. Reveal()을 건너뛰고 CloseAndSpawnTiles()만 호출한 씬(M.Boss 등
        // 무프리뷰)도 항상 이 경로로 들어온다 — 즉시 스폰+Close+타일까지 한 번에 끝난다.
        if (_colorToDoors.Count == 0)
            RevealBarriers();

        foreach (List<DoorController> doors in _colorToDoors.Values)
            foreach (DoorController door in doors)
                door?.Close();

        // Reveal의 SpawnBarriers와는 독립된 새 System.Random 인스턴스 — 같은 시드로 시작해도
        // 서로 다른 함수 안에서만 rng.Next()를 쓰므로 두 스텝이 시간차를 두고 갈라져도(다이얼로그
        // 대기 등) 전 머신이 항상 같은 타일 배치를 재생한다.
        int seed = _netState != null ? _netState.ChallengeSeed : 0;
        SpawnTiles(new System.Random(seed));

        OnRoundStarted?.Invoke();
    }

    // ── 베리어 스폰 ──────────────────────────────────────────────

    void SpawnBarriers(System.Random rng)
    {
        ClearBarriers();

        if (barrierSpawnPoints == null || barrierSpawnPoints.Length == 0 || barrierPrefabs.Length == 0)
        {
            Debug.LogWarning("[DirectionalBarrierRound] barrierSpawnPoints 또는 barrierPrefabs가 비어 있습니다.");
            return;
        }

        // §2.1 확정 표 기반 4슬롯 (CoopStageAudit.M.md §2.1) — GameSessionColorDistribution.Distribute의
        // 균등 분배(2인→2+2 등)는 더 이상 쓰지 않는다. 1인 4면 전부 고유색 배정 금지.
        _roundColors = BuildBarrierSlots(GameSessionColorDistribution.GetActiveColorsOrFallback());

        // 어떤 방향 슬롯에 어떤 색이 배치될지 셔플
        PlayerColorType[] shuffledForBarriers = (PlayerColorType[])_roundColors.Clone();
        for (int i = shuffledForBarriers.Length - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (shuffledForBarriers[i], shuffledForBarriers[j]) = (shuffledForBarriers[j], shuffledForBarriers[i]);
        }

        int count = Mathf.Min(shuffledForBarriers.Length, barrierSpawnPoints.Length);
        for (int i = 0; i < count; i++)
        {
            PlayerColorType   color  = shuffledForBarriers[i];
            BarrierSpawnPoint entry  = barrierSpawnPoints[i];
            GameObject        prefab = GetBarrierPrefabForColor(color);

            if (prefab == null)
            {
                Debug.LogWarning($"[DirectionalBarrierRound] {color} 베리어 프리팹이 등록되지 않았습니다.");
                continue;
            }
            if (entry == null || entry.point == null)
            {
                Debug.LogWarning($"[DirectionalBarrierRound] barrierSpawnPoints[{i}]가 null입니다.");
                continue;
            }

            // NorthSouth: 프리팹 회전 그대로 / EastWest: 월드 Y축 기준 -90 추가
            Quaternion baseRot  = prefab.transform.rotation;
            Quaternion spawnRot = entry.direction == SpawnDirection.EastWest
                ? Quaternion.Euler(0f, -90f, 0f) * baseRot
                : baseRot;

            GameObject     obj  = Instantiate(prefab, entry.point.position, spawnRot);
            DoorController door = obj.GetComponent<DoorController>();

            if (door == null)
            {
                Debug.LogWarning($"[DirectionalBarrierRound] {color} 베리어 프리팹에 DoorController가 없습니다.");
                Destroy(obj);
                continue;
            }

            if (!_colorToDoors.TryGetValue(color, out List<DoorController> list))
            {
                list = new List<DoorController>();
                _colorToDoors[color] = list;
            }
            list.Add(door);
            _spawnedBarriers.Add(obj);
        }
    }

    // ── 타일 스폰 ────────────────────────────────────────────────

    void SpawnTiles(System.Random rng)
    {
        ClearTiles();

        if (tileSpawnPoints.Length == 0 || tilePrefabs.Length == 0)
        {
            Debug.LogWarning("[DirectionalBarrierRound] tileSpawnPoints 또는 tilePrefabs가 비어 있습니다.");
            return;
        }
        if (_colorToDoors.Count == 0)
        {
            Debug.LogWarning("[DirectionalBarrierRound] _colorToDoors가 없습니다. SpawnBarriers를 먼저 호출하세요.");
            return;
        }

        // 타일은 슬롯이 아니라 "색"당 1개만 스폰한다 — §2.1 "고유 패드 1개 → 고유 문 2개":
        // 1인은 같은 고유색이 barrierSpawnPoints 2칸(_roundColors 중복)에 배정되지만, 그 색 문은
        // _colorToDoors에 이미 함께 묶여 있으므로(HandleTileActivated가 색 단위로 open/close) 타일도
        // 색 단위로 하나만 있어야 한다. _roundColors(중복 포함)로 스폰하면 같은 색 타일이 2개 생겨버림.
        List<PlayerColorType> distinctColors = new List<PlayerColorType>(_colorToDoors.Keys);
        for (int i = distinctColors.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (distinctColors[i], distinctColors[j]) = (distinctColors[j], distinctColors[i]);
        }

        // 스폰 포인트 셔플
        List<Transform> shuffledPoints = new List<Transform>(tileSpawnPoints);
        for (int i = shuffledPoints.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (shuffledPoints[i], shuffledPoints[j]) = (shuffledPoints[j], shuffledPoints[i]);
        }

        int count = Mathf.Min(distinctColors.Count, shuffledPoints.Count);
        for (int i = 0; i < count; i++)
        {
            PlayerColorType color  = distinctColors[i];
            GameObject      prefab = GetTilePrefabForColor(color);

            if (prefab == null)
            {
                Debug.LogWarning($"[DirectionalBarrierRound] {color} 타일 프리팹이 등록되지 않았습니다.");
                continue;
            }

            GameObject obj  = Instantiate(prefab, shuffledPoints[i].position, Quaternion.identity);
            ColorTile  tile = obj.GetComponent<ColorTile>() ?? obj.AddComponent<ColorTile>();

            tile.Setup(color);
            tile.ignorePlayerCheck   = debugAllTiles;
            tile.OnActivatedCallback = HandleTileActivated;
            _activeTiles.Add(tile);
        }
    }

    // ── 타일 활성화 처리 ─────────────────────────────────────────

    void HandleTileActivated(PlayerColorType color)
    {
        if (!_colorToDoors.ContainsKey(color)) return;

        foreach (KeyValuePair<PlayerColorType, List<DoorController>> pair in _colorToDoors)
        {
            bool isTarget = pair.Key == color;
            foreach (DoorController door in pair.Value)
            {
                if (door == null) continue;
                if (isTarget) door.Open();
                else          door.Close();
            }
        }
    }

    // ── 정리 ─────────────────────────────────────────────────────

    void ClearBarriers()
    {
        foreach (GameObject obj in _spawnedBarriers)
        {
            if (obj != null) Destroy(obj);
        }
        _spawnedBarriers.Clear();
        _colorToDoors.Clear();
    }

    void ClearTiles()
    {
        foreach (ColorTile t in _activeTiles)
        {
            if (t != null) Destroy(t.gameObject);
        }
        _activeTiles.Clear();
    }

    // ── 유틸 ─────────────────────────────────────────────────────

    /// <summary>
    /// §2.1 확정 표(CoopStageAudit.M.md) — 4슬롯 고정 배정. 균등 분배가 아니다.
    ///
    ///  1인: 고유, 고유, 백, 흑  (고유 패드 1개가 고유 문 2개를 같이 열게 됨 — 흑·백은 따로)
    ///  2인: A, B, 백, 흑
    ///  3인: 고유3 + 백 1  (백은 공용 — 흑 없음)
    ///  4인: 고유4  (흑백 없음)
    ///
    /// activeColors 순서는 GameSessionColorDistribution.GetActiveColorsOrFallback()이 이미
    /// ColorIndex 기준으로 정렬해서 주므로 Host/Client가 항상 같은 순서를 본다 — 여기선 추가로
    /// 셔플하지 않는다(어떤 색이 몇 번 인덱스로 배정되는지는 표에서 고정, 물리적 스폰 위치 셔플은
    /// SpawnBarriers의 Fisher-Yates가 별도로 담당).
    /// </summary>
    static PlayerColorType[] BuildBarrierSlots(IReadOnlyList<PlayerColorType> activeColors)
    {
        int n = activeColors != null ? activeColors.Count : 0;

        switch (n)
        {
            case 0:
                // 활성색 정보가 아예 없을 때의 최종 폴백 — 정상 플레이에서는 도달하지 않음.
                return new[] { PlayerColorType.Blue, PlayerColorType.Blue, PlayerColorType.White, PlayerColorType.Black };
            case 1:
                return new[] { activeColors[0], activeColors[0], PlayerColorType.White, PlayerColorType.Black };
            case 2:
                return new[] { activeColors[0], activeColors[1], PlayerColorType.White, PlayerColorType.Black };
            case 3:
                return new[] { activeColors[0], activeColors[1], activeColors[2], PlayerColorType.White };
            default: // 4명 이상 — 4슬롯 전부 고유색
                return new[] { activeColors[0], activeColors[1], activeColors[2], activeColors[3] };
        }
    }

    GameObject GetBarrierPrefabForColor(PlayerColorType color)
    {
        foreach (BarrierPrefabEntry e in barrierPrefabs)
        {
            if (e.colorType == color) return e.prefab;
        }
        return null;
    }

    GameObject GetTilePrefabForColor(PlayerColorType color)
    {
        foreach (TilePrefabEntry e in tilePrefabs)
        {
            if (e.colorType == color) return e.prefab;
        }
        return null;
    }

    // ── 에디터 ──────────────────────────────────────────────────

    [ContextMenu("테스트: Reveal (미리보기)")]
    void Debug_Reveal() => Reveal();

    [ContextMenu("테스트: Close + 타일 스폰 (라운드 시작)")]
    void Debug_CloseAndSpawnTiles() => CloseAndSpawnTiles();

    void OnDrawGizmos()
    {
        if (barrierSpawnPoints != null)
        {
            foreach (BarrierSpawnPoint entry in barrierSpawnPoints)
            {
                if (entry == null || entry.point == null) continue;
                Gizmos.color = entry.direction == SpawnDirection.EastWest
                    ? new Color(0.3f, 0.6f, 1f, 0.7f)
                    : new Color(1f, 0.3f, 0.3f, 0.7f);
                Gizmos.DrawWireCube(entry.point.position, new Vector3(1f, 2f, 0.2f));
            }
        }

        if (tileSpawnPoints != null)
        {
            Gizmos.color = new Color(0.8f, 0.4f, 1f, 0.6f);
            foreach (Transform sp in tileSpawnPoints)
            {
                if (sp != null)
                    Gizmos.DrawWireCube(sp.position, Vector3.one * 0.8f);
            }
        }
    }
}
