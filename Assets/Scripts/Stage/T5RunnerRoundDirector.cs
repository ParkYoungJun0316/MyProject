using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// T.Stage5 러너 라운드 디렉터. `TStage5RunnerRedesign.md` §1이 SSOT.
///
/// [역할]
///  맵 7개 중 2개·러너 2명을 시드로 추첨하고, 라운드 2개를 굴린다.
///  라운드 = 3초 카운트다운 → 제한시간 120초 안에 러너가 1층 Goal 도달.
///  2라운드를 모두 통과하면 Complete() → 기존 StageManager 클리어 흐름.
///
/// [StageObjective인 이유]
///  클리어·실패를 자체 경로로 만들지 않기 위해서다. Complete()는 StageManager의 클리어 판정에,
///  Fail()은 StageManager.KillAllPlayersOnFail() → §11 사망 문(NotifyPlayerDeathServerRpc) →
///  기존 리로드에 그대로 얹힌다(NetworkDesign.md §11A.3). 병렬 리로드 경로를 만들지 않는다.
///
/// [실패 2종]
///  - 시간 초과 : 여기서 Fail(). 위 경로로 전원 리로드.
///  - 러너 사망 : **이 클래스가 따로 처리하지 않는다.** StageResetOnPlayerDeath가 이미 모든
///    사망을 사망 문으로 보내고 있어, 러너가 죽으면 그 경로로 리로드된다. 여기서 또 잡으면
///    같은 사건에 리로드 요청이 두 번 들어간다(_resetPending이 막긴 하지만 의미 없는 중복).
///
/// [네트워크 — 축 SSOT NetworkDesign.md §11A]
///  ③Progress/Resolve(추첨·라운드 전환·타임아웃·Goal 판정)는 **Host 레인 하나**.
///  결과는 StageNetworkState의 T5RoundState NV로만 전파되고, 전 머신은 그 NV를 보고
///  로컬 연출(맵 활성화·색 재매핑·문 스냅)을 똑같이 재생한다 — 연출 값을 RPC로 실어보내지 않는다.
///  색 재매핑은 (시드 + 라운드 + NV의 러너 clientId)에서만 나오므로 전 머신 결과가 같다.
///
/// [Inspector]
///  mazesRoot : `StageManager5/T5_Mazes`. 하위 `Map_*` 자식을 이름 순으로 전부 맵 후보로 삼고,
///              각 맵의 ColorGateController / Start1F / Goal1F / Stand2F를 이름으로 자동 해석한다.
///              (맵을 하나하나 끌어다 넣을 필요 없음 — 루트 하나만 배선)
/// </summary>
public class T5RunnerRoundDirector : StageObjective
{
    const int RoundCount = 2;

    // 시드 salt — 추첨 축마다 다른 값을 써서 맵·러너·색이 서로 상관되지 않게 한다.
    const int MapSalt    = 0x54354D50; // "T5MP"
    const int RunnerSalt = 0x54355255; // "T5RU"
    const int ColorSalt  = 0x5435434C; // "T5CL"

    [Header("맵 루트")]
    [Tooltip("StageManager5/T5_Mazes. 하위 Map_* 자식이 맵 후보가 된다.")]
    [SerializeField] Transform mazesRoot;

    [Header("라운드 타이밍 (초)")]
    [Tooltip("라운드 시작 카운트다운. 이 시간이 지나야 제한시간이 돌기 시작한다.")]
    [SerializeField] float introSeconds = 3f;

    [Tooltip("라운드 제한 시간 (§1.1 = 120초)")]
    [SerializeField] float roundSeconds = 120f;

    [Tooltip("라운드 시작 후 체이서가 등장하기까지의 유예 (§1.3 = 3초)")]
    [SerializeField] float chaserGraceSeconds = 3f;

    [Header("Goal 판정")]
    [Tooltip("러너가 Goal1F 마커에 이 거리(m) 안으로 들어오면 도달로 친다. 통로 폭 8 기준 4 권장.")]
    [SerializeField] float goalRadius = 4f;

    [Header("라운드 전환 연출 (§1.1)")]
    [Tooltip("암전/페이드인 각각의 시간(초). 이 시간이 지난 뒤 화면이 덮인 상태에서 텔레포트한다.")]
    [SerializeField] float coverFadeSeconds = 0.35f;

    [Tooltip("텔레포트 후 화면을 덮은 채 유지하는 시간(초).")]
    [SerializeField] float coveredHoldSeconds = 0.3f;

    [Tooltip("텔레포트 목적지를 바닥에서 이 높이(m)만큼 띄운다. 겹침·끼임 방지.")]
    [SerializeField] float dropHeight = 3f;

    [Header("이벤트 (UI 연결용)")]
    [Tooltip("라운드/남은 시간 표시가 갱신되어야 할 때 호출. ObjectiveUI가 자동 구독.\n" +
             "표시 문구가 실제로 바뀔 때만(라운드 변경 또는 올림 초 변경) 발동한다.")]
    public UnityEngine.Events.UnityEvent OnProgressChanged;

    /// <summary>진행(또는 완료)한 라운드 수 — ObjectiveUI Count 표시용. 라운드 진입 전이면 0.</summary>
    public int PlayedRounds
    {
        get
        {
            var net = StageNetworkState.Instance;
            int r = net != null ? net.T5CurrentRound : -1;
            return r < 0 ? 0 : Mathf.Min(r + 1, RoundCount);
        }
    }

    /// <summary>전체 라운드 수 (2).</summary>
    public int TotalRounds => RoundCount;

    /// <summary>이번 라운드 제한시간 잔여(초). 라운드 진입 전이면 0.</summary>
    public float Remaining
    {
        get
        {
            var net = StageNetworkState.Instance;
            if (net == null || net.T5CurrentRound < 0) return 0f;
            return Mathf.Max(0f, (float)(net.T5Round.roundEndServerTime - ServerNow()));
        }
    }

    /// <summary>맵 하나의 구성 요소. mazesRoot 하위에서 이름으로 자동 해석된다.</summary>
    class MapEntry
    {
        public GameObject          root;
        public ColorGateController gate;
        public Transform           start1F;
        public Transform           goal1F;
        public Transform           stand2F;
        public Stage5ChaserSpawner spawner;
    }

    readonly List<MapEntry> _maps = new List<MapEntry>();

    // Host 레인 전용 진행 상태 — Client는 NV만 본다.
    enum RoundPhase { Idle, Intro, Active, Done }
    RoundPhase _phase = RoundPhase.Idle;
    bool       _chasersSpawned;

    // 전 머신 로컬: 카운트다운 이동 잠금의 마지막 적용값
    bool _movementLocked;

    // 전 머신 로컬: 마지막으로 UI에 흘려보낸 표시값(중복 갱신 방지)
    int _shownRound   = -1;
    int _shownSeconds = -1;

    // 전 머신 공통: 마지막으로 연출을 적용한 라운드(중복 적용 방지)
    int _presentedRound = int.MinValue;

    // ── StageObjective ──────────────────────────────────────────

    void Awake() => CollectMaps();

    public override void Begin()
    {
        var net = StageNetworkState.Instance;
        if (net != null) net.OnT5RoundChanged += OnRoundStateChanged;

        if (!IsHostLane()) return;

        if (_maps.Count < RoundCount)
        {
            Debug.LogError($"[T5RunnerRoundDirector] 맵이 {_maps.Count}개뿐이라 라운드 {RoundCount}개를 구성할 수 없습니다.");
            return;
        }

        DrawAndWriteNv();
        BeginRound(0);
        TeleportForRound(0); // 시작 홀 → 1라운드 맵. 2라운드 전환과 같은 코드를 탄다.
    }

    void OnDestroy()
    {
        var net = StageNetworkState.Instance;
        if (net != null) net.OnT5RoundChanged -= OnRoundStateChanged;
    }

    public override void Tick()
    {
        // 연출(맵 활성화·색 재매핑)과 카운트다운 이동 잠금은 전 머신 로컬.
        // NV 콜백을 놓친 늦은 스폰도 여기서 수렴한다.
        PresentCurrentRoundIfNeeded();
        UpdateLocalMovementLock();
        RaiseProgressIfDisplayChanged();

        if (!IsHostLane()) return;
        if (_phase != RoundPhase.Intro && _phase != RoundPhase.Active) return;

        var net = StageNetworkState.Instance;
        if (net == null) return;

        double now   = ServerNow();
        T5RoundState st = net.T5Round;

        if (_phase == RoundPhase.Intro)
        {
            if (now < st.roundStartServerTime + introSeconds) return;
            _phase = RoundPhase.Active;
            NetLog.Transition("T5RunnerRoundDirector", "RoundActive", $"round={st.roundIndex} map={st.MapIndexOf(st.roundIndex)}");
        }

        // 체이서 등장 — 라운드 시작(카운트다운 종료) 후 chaserGraceSeconds 경과 시 1회
        if (!_chasersSpawned && now >= st.roundStartServerTime + introSeconds + chaserGraceSeconds)
        {
            _chasersSpawned = true;
            SpawnChasers(st.roundIndex);
        }

        // 시간 초과 → 실패(전원 즉사 → §11 사망 문 → 리로드)
        if (now >= st.roundEndServerTime)
        {
            NetLog.Transition("T5RunnerRoundDirector", "RoundTimeout", $"round={st.roundIndex}");
            _phase = RoundPhase.Done;
            Fail();
            return;
        }

        if (HasRunnerReachedGoal(st)) AdvanceRound(st.roundIndex);
    }

    // ── Host: 추첨 ──────────────────────────────────────────────

    void DrawAndWriteNv()
    {
        int seed = NetworkSessionData.Seed;

        int[] mapPick = PickDistinct(_maps.Count, RoundCount, new System.Random(seed ^ MapSalt));

        // 러너: clientId를 시드로 섞은 뒤 앞에서 2개.
        //  1인 → 같은 사람이 두 번 / 2인 → 각 1회 / 3~4인 → 서로 다른 2명. 한 규칙으로 전부 커버된다.
        List<ulong> clients = SortedClientIds();
        if (clients.Count == 0)
        {
            Debug.LogError("[T5RunnerRoundDirector] 활성 플레이어 명단이 비어 러너를 뽑을 수 없습니다.");
            return;
        }
        Shuffle(clients, new System.Random(seed ^ RunnerSalt));

        ulong runner0 = clients[0];
        ulong runner1 = clients.Count >= 2 ? clients[1] : clients[0];

        StageNetworkState.Instance?.SetT5Draw(mapPick[0], mapPick[1], runner0, runner1);
        NetLog.Transition("T5RunnerRoundDirector", "Draw",
            $"seed={seed} maps={mapPick[0]},{mapPick[1]} runners={runner0},{runner1}");
    }

    // ── Host: 라운드 진행 ───────────────────────────────────────

    void BeginRound(int round)
    {
        var net = StageNetworkState.Instance;
        if (net == null) return;

        // 문은 라운드 시작 시 전부 닫힘 (§1.2)
        net.CloseAllGates();

        double start = ServerNow();
        net.BeginT5Round(round, start, start + introSeconds + roundSeconds);

        _phase          = RoundPhase.Intro;
        _chasersSpawned = false;

        NetLog.Transition("T5RunnerRoundDirector", "RoundBegin",
            $"round={round} map={net.T5Round.MapIndexOf(round)} runner={net.T5Round.RunnerOf(round)}");
    }

    void AdvanceRound(int finishedRound)
    {
        NetLog.Transition("T5RunnerRoundDirector", "GoalReached", $"round={finishedRound}");

        StopChasers(finishedRound);

        if (finishedRound + 1 >= RoundCount)
        {
            _phase = RoundPhase.Done;
            Complete();
            return;
        }

        // 순서 중요: BeginRound가 NV를 써야 전 머신이 다음 맵을 활성화하고 이동 잠금이 걸린다.
        // 텔레포트는 암전(coverFadeSeconds) 뒤에 착지하므로, 그때는 목적지 맵이 이미 켜져 있다.
        // 반대로 하면 아직 비활성인 맵으로 떨어져 바닥을 통과한다.
        BeginRound(finishedRound + 1);
        TeleportForRound(finishedRound + 1);
    }

    /// <summary>
    /// Host: round 맵으로 전원 이동 (`NetworkDesign.md` §11.9).
    /// 러너는 Start1F, 안내자는 Stand2F 주변으로. 실제 좌표 쓰기는 각자 Owner 머신이 한다
    /// (CNT는 Owner 권한 — §7.3). 여기서는 목적지만 계산해서 넘긴다.
    /// </summary>
    void TeleportForRound(int round)
    {
        var net = StageNetworkState.Instance;
        MapEntry map = MapOf(round);
        if (net == null || map == null) return;

        ulong runner = net.T5Round.RunnerOf(round);

        var ids  = new List<ulong>();
        var dest = new List<Vector3>();

        foreach ((ulong clientId, PlayerColorType color) in PlayerSpawnCoordinator.GetAllEntries())
        {
            Vector3? pos = clientId == runner
                ? RunnerDestination(map)
                : GuideDestination(map, color);

            if (pos == null) continue;
            ids.Add(clientId);
            dest.Add(pos.Value);
        }

        if (ids.Count == 0) return;

        net.BeginT5Transition(ids.ToArray(), dest.ToArray(), coverFadeSeconds, coveredHoldSeconds);
        NetLog.Transition("T5RunnerRoundDirector", "Teleport", $"round={round} count={ids.Count}");
    }

    Vector3? RunnerDestination(MapEntry map) =>
        map.start1F != null ? map.start1F.position + Vector3.up * dropHeight : (Vector3?)null;

    /// <summary>
    /// 안내자 목적지 = Stand2F + "시작 홀에서 쓰던 색별 XZ 오프셋" + dropHeight.
    /// 시작 홀 고정 좌표((0,0,5)/(5,0,0)/(-5,0,0)/(0,0,-5))가 원점 기준이라 그 XZ가 곧 오프셋이다 —
    /// 같은 배치를 2층에서 재사용하면 색마다 자리가 달라 겹치지 않는다(사용자 확정 2026-09-18).
    /// </summary>
    Vector3? GuideDestination(MapEntry map, PlayerColorType color)
    {
        if (map.stand2F == null) return null;

        Vector3 offset = Vector3.zero;
        if (PlayerSpawnManager.Instance != null)
        {
            Vector3 hall = PlayerSpawnManager.Instance.GetFixedSpawnPos(color);
            offset = new Vector3(hall.x, 0f, hall.z);
        }

        return map.stand2F.position + offset + Vector3.up * dropHeight;
    }

    /// <summary>
    /// 전 머신 로컬: 라운드 카운트다운 동안 이동을 잠근다.
    /// NV(roundStartServerTime) + introSeconds에서만 나오는 값이라 Host/Client가 같은 순간에
    /// 풀린다 — 해제용 RPC가 필요 없다.
    /// </summary>
    void UpdateLocalMovementLock()
    {
        var net = StageNetworkState.Instance;
        if (net == null) return;

        T5RoundState st = net.T5Round;
        bool shouldLock = st.roundIndex >= 0 && ServerNow() < st.roundStartServerTime + introSeconds;
        if (shouldLock == _movementLocked) return;

        _movementLocked = shouldLock;

        Player local = LocalPlayer();
        if (local != null) local.SetMovementLocked(shouldLock);
    }

    /// <summary>
    /// 표시 문구가 실제로 바뀔 때만 OnProgressChanged를 쏜다.
    /// ObjectiveUI는 올림 초(`Mathf.CeilToInt`)로 표시하므로 매 프레임 쏘면 같은 글자를 60번 다시 쓴다.
    /// </summary>
    void RaiseProgressIfDisplayChanged()
    {
        int round   = PlayedRounds;
        int seconds = Mathf.CeilToInt(Remaining);
        if (round == _shownRound && seconds == _shownSeconds) return;

        _shownRound   = round;
        _shownSeconds = seconds;
        OnProgressChanged?.Invoke();
    }

    static Player LocalPlayer()
    {
        var nm = NetworkManager.Singleton;
        NetworkObject netObj = nm?.LocalClient?.PlayerObject;
        return netObj != null ? netObj.GetComponent<Player>() : null;
    }

    bool HasRunnerReachedGoal(T5RoundState st)
    {
        MapEntry map = MapOf(st.roundIndex);
        if (map?.goal1F == null) return false;

        Player runner = FindPlayer(st.RunnerOf(st.roundIndex));
        if (runner == null || runner.IsDead) return false;

        // 2층 안내자가 Goal 위를 지나가도 반응하지 않도록 러너 한 명만 본다 (§1.1).
        return (runner.transform.position - map.goal1F.position).sqrMagnitude <= goalRadius * goalRadius;
    }

    void SpawnChasers(int round)
    {
        Stage5ChaserSpawner spawner = MapOf(round)?.spawner;
        if (spawner == null) return; // C7 전까지는 맵에 스포너가 없을 수 있다
        spawner.StartSpawning();
    }

    void StopChasers(int round)
    {
        Stage5ChaserSpawner spawner = MapOf(round)?.spawner;
        if (spawner != null) spawner.StopAndClear();
    }

    // ── 전 머신: 라운드 연출 ────────────────────────────────────

    void OnRoundStateChanged(T5RoundState _) => PresentCurrentRoundIfNeeded();

    /// <summary>
    /// NV가 가리키는 라운드의 맵만 켜고, 색 슬롯을 재매핑하고, 문을 닫힌 위치로 스냅한다.
    /// Host/Client 모두 같은 입력(NV + 시드)으로 같은 결과를 만든다.
    /// </summary>
    void PresentCurrentRoundIfNeeded()
    {
        var net = StageNetworkState.Instance;
        if (net == null) return;

        T5RoundState st = net.T5Round;
        if (st.roundIndex < 0 || st.roundIndex == _presentedRound) return;
        _presentedRound = st.roundIndex;

        int activeMap = st.MapIndexOf(st.roundIndex);
        for (int i = 0; i < _maps.Count; i++)
        {
            GameObject root = _maps[i].root;
            if (root != null && root.activeSelf != (i == activeMap))
                root.SetActive(i == activeMap);
        }

        MapEntry map = MapOf(st.roundIndex);
        if (map?.gate == null) return;

        // 순서 주의: 스냅이 "적용 완료" 표식을 세우므로 재매핑을 뒤에 둬야 다음 프레임에
        // 새 매핑으로 다시 수렴한다.
        map.gate.SnapAllClosed();
        map.gate.ApplySlotMapping(BuildSlotColors(st));
    }

    /// <summary>
    /// 고유색 설계슬롯 4개에 들어갈 실제 색. 러너 색을 뺀 **2층 안내자 색**만 후보다 (§1.2).
    /// 시드 + 라운드로만 결정되므로 전 머신이 같은 배열을 얻는다.
    /// </summary>
    PlayerColorType[] BuildSlotColors(T5RoundState st)
    {
        var guides = new List<PlayerColorType>();
        ulong runner = st.RunnerOf(st.roundIndex);

        foreach ((ulong clientId, PlayerColorType color) in PlayerSpawnCoordinator.GetAllEntries())
        {
            if (clientId == runner) continue;
            guides.Add(color);
        }

        var rng = new System.Random(NetworkSessionData.Seed ^ ColorSalt ^ st.roundIndex);

        // guides가 비면(솔로) Distribute가 기본 4색으로 fallback한다 — 솔로는 어차피
        // ColorGateController가 문을 전부 열어두므로 매핑 값 자체는 의미가 없다.
        return GameSessionColorDistribution.Distribute(guides, ColorGateController.DesignSlots.Length, rng);
    }

    // ── 맵 수집 ────────────────────────────────────────────────

    void CollectMaps()
    {
        _maps.Clear();
        if (mazesRoot == null)
        {
            Debug.LogError("[T5RunnerRoundDirector] mazesRoot가 비어 있습니다 (StageManager5/T5_Mazes를 배선하세요).");
            return;
        }

        var roots = new List<Transform>();
        foreach (Transform child in mazesRoot)
            if (child.name.StartsWith("Map_")) roots.Add(child);

        roots.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        foreach (Transform root in roots)
        {
            var entry = new MapEntry
            {
                root    = root.gameObject,
                gate    = root.GetComponentInChildren<ColorGateController>(true),
                start1F = root.Find("Start1F"),
                goal1F  = root.Find("Goal1F"),
                stand2F = root.Find("Stand2F"),
                spawner = root.GetComponentInChildren<Stage5ChaserSpawner>(true),
            };

            if (entry.gate == null)    Debug.LogWarning($"[T5RunnerRoundDirector] {root.name}: ColorGateController가 없습니다.");
            if (entry.goal1F == null)  Debug.LogWarning($"[T5RunnerRoundDirector] {root.name}: Goal1F를 찾지 못했습니다 — Goal 판정 불가.");
            if (entry.start1F == null) Debug.LogWarning($"[T5RunnerRoundDirector] {root.name}: Start1F를 찾지 못했습니다.");

            _maps.Add(entry);
        }
    }

    MapEntry MapOf(int round)
    {
        var net = StageNetworkState.Instance;
        if (net == null) return null;

        int index = net.T5Round.MapIndexOf(round);
        return index >= 0 && index < _maps.Count ? _maps[index] : null;
    }

    // ── 유틸 ────────────────────────────────────────────────────

    static bool IsHostLane()
    {
        var nm = NetworkManager.Singleton;
        return nm == null || !nm.IsListening || nm.IsServer;
    }

    static double ServerNow()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening ? nm.ServerTime.Time : Time.timeAsDouble;
    }

    /// <summary>clientId 순 정렬 — 셔플 입력 순서를 머신과 무관하게 고정한다.</summary>
    static List<ulong> SortedClientIds()
    {
        var ids = new List<ulong>();
        foreach ((ulong clientId, PlayerColorType _) in PlayerSpawnCoordinator.GetAllEntries())
            ids.Add(clientId);
        ids.Sort();
        return ids;
    }

    static Player FindPlayer(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsServer &&
            nm.ConnectedClients.TryGetValue(clientId, out NetworkClient client) &&
            client.PlayerObject != null)
            return client.PlayerObject.GetComponent<Player>();

        foreach (Player p in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            NetworkObject no = p.GetComponent<NetworkObject>();
            if (no != null && no.OwnerClientId == clientId) return p;
        }
        return null;
    }

    static int[] PickDistinct(int poolSize, int count, System.Random rng)
    {
        var pool = new List<int>(poolSize);
        for (int i = 0; i < poolSize; i++) pool.Add(i);

        for (int i = poolSize - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        var result = new int[count];
        for (int i = 0; i < count; i++) result[i] = pool[i % poolSize];
        return result;
    }

    static void Shuffle<T>(List<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
