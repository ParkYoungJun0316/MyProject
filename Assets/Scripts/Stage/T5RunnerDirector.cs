using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// T.Stage5 러너 디렉터 — 한 판짜리 스테이지의 시작 절차를 담당한다.
/// SSOT: `Assets/Docs/TStage5RunnerRedesign.md` §1.1 / §1.2 / §4.3
///
/// [하는 일 — 넷뿐이다]
///  1. **뽑기** — 시드로 맵 1장과 러너 1명을 정한다. 맵 활성화는 `StageVariantPicker`가 한다.
///  2. **준비 게이트** — 맵이 켜지기 전에 커튼이 걷히지 않도록 `"t5.map"`을 대신 건다
///     (맵 7장이 비활성이라 스스로 등록할 수 없다 — `SceneReadyAndColorMapping.md` §A.3).
///  3. **시작 절차** — 카운트다운 완료 시 러너 것을 뺀 발판을 켜고, 안내자가 2층에 닿으면
///     발판을 끄고 barrier를 푼다.
///  4. **체이서** — 러너가 시작 존을 벗어나면 1회 스폰시킨다.
///
///  **라운드는 없다.** 목표 판정·제한시간은 <see cref="T5RunnerObjective"/>가 따로 들고 있다.
///
/// [왜 텔레포트가 없나]
///  플레이어는 스폰한 자리에서 `ContactKnockback.VerticalUp` 발판으로 2층에 올라간다.
///  러너는 자기 발판이 꺼져 있어 1층에 남는다 — `ContactKnockback`에는 대상을 고르는 기능이
///  없으므로 "발판을 끄는 것"이 유일한 선별 수단이다.
///
/// [순서가 왜 이런가 — 셋이 이 순서에 걸려 있다]
///  · 맵 활성화가 `OnPlayersReady`인 이유: 사망 리로드 때 새 시드가 RPC로 오는데 `Start()`보다
///    먼저 도착한다는 보장이 없다(T4 판 선택과 같은 이유). 커튼이 덮고 있어 안 보인다.
///  · barrier를 2층 도착까지 유지하는 이유: 먼저 내리면 러너가 **옆 칸 발판**을 밟아 올라간다.
///  · 2층 하향 방지 트리거를 미리 켜지 않는 이유: `ContactKnockback`은 **속도 방향을 보지 않아**,
///    올라가던 사람이 통과하며 한 번 더 가속돼 난간을 넘는다.
///
/// [권한]
///  러너는 시드 파생이라 **각 머신이 로컬로 같은 값**을 계산한다. Host는 그 값을 NV에도 적어
///  두는데, Host 전권인 체이서가 "타겟의 진실"을 한 곳에서 읽어야 하기 때문이다. 두 값은 같은
///  규칙(정렬된 clientId + 시드)에서만 나오므로 어긋날 입력이 없다.
///  발판·barrier 토글은 **각 머신 로컬**이다 — 넉백은 밟은 본인(Owner)이 판정하고 이동도 Owner
///  권한이라, 효과가 갈리는 지점이 애초에 그 사람 자신의 머신이다(`CapacityTile`과 같은 사상).
///
/// [씬 설정]
///  1. 씬 루트에 이 컴포넌트 하나.
///  2. `StageVariantPicker`에 맵 7장을 등록(전부 비활성, **anchor는 비움** — 원점에 겹쳐 둔다).
///  3. `StageStartGate.OnCountdownComplete` → <see cref="BeginStage"/> 연결.
///  4. 색별 발판 4개·barrier·2층 도착 존·2층 하향 방지 트리거·시작 존·체이서 스포너 배선.
/// </summary>
[DefaultExecutionOrder(-100)]
public class T5RunnerDirector : MonoBehaviour
{
    const int RunnerSalt = 0x54355255; // "T5RU"

    // 맵이 켜지기 전에 커튼이 걷히면 미로가 눈앞에서 나타나는 것이 그대로 보인다.
    const string MapGate = "t5.map";

    /// <summary>색 하나에 대응하는 시작 발판. 러너 색 발판만 꺼진 채로 시작한다.</summary>
    [System.Serializable]
    public class LaunchPad
    {
        [Tooltip("이 발판이 있는 시작 자리의 색.")]
        public PlayerColorType color = PlayerColorType.Blue;

        [Tooltip("ContactKnockback(VerticalUp)이 붙은 발판 오브젝트. 카운트다운 완료 시 켜진다.")]
        public GameObject pad;
    }

    [Header("맵")]
    [Tooltip("맵 7장을 등록한 StageVariantPicker. 뽑기·활성화는 전부 그쪽이 한다.\n" +
             "맵을 원점에 겹쳐 두므로 Picker의 anchor는 비울 것(이동 없음).")]
    [SerializeField] StageVariantPicker mapPicker;

    [Header("시작 발판 / barrier")]
    [Tooltip("색별 시작 발판. 러너 색 발판만 빼고 카운트다운 완료 시 켜진다.")]
    [SerializeField] LaunchPad[] launchPads = new LaunchPad[0];

    [Tooltip("시작 자리를 칸칸이 막는 투명 barrier. 안내자가 2층에 닿으면 전부 꺼진다.")]
    [SerializeField] GameObject[] barriers = new GameObject[0];

    [Tooltip("2층 도착 감지 존(트리거 콜라이더). 안내자가 여기 들어오면 시작 절차가 끝난다.")]
    [SerializeField] Collider arrivalZone2F;

    [Tooltip("2층 발사 구멍의 하향 방지 트리거. 처음엔 꺼져 있다가 도착 시 켜진다.\n" +
             "미리 켜면 올라가던 사람이 한 번 더 가속된다(클래스 주석).")]
    [SerializeField] GameObject descentBlocker;

    [Tooltip("2층 도착이 감지되지 않아도 이 시간(초) 뒤에는 발판을 끄고 barrier를 푼다.\n" +
             "누군가 발사에 실패해 시작 홀에 갇히는 것을 막는 안전망이다.")]
    [SerializeField] float arrivalFallbackSeconds = 5f;

    [Header("러너 출발 / 체이서")]
    [Tooltip("러너 시작 존(트리거 콜라이더). 러너가 여기를 벗어나면 체이서가 나온다.")]
    [SerializeField] Collider startZone;

    [Tooltip("씬에 하나 있는 Stage5ChaserSpawner.")]
    [SerializeField] Stage5ChaserSpawner chaserSpawner;

    /// <summary>이번 판의 러너 clientId. 뽑기 전이면 <see cref="StageNetworkState.NoRunner"/>.</summary>
    public ulong RunnerClientId { get; private set; } = StageNetworkState.NoRunner;

    bool _picked;
    bool _gateHeld;
    bool _started;      // 카운트다운 완료 → 발판 켜짐
    bool _released;     // 2층 도착(또는 안전망) → 발판 끄고 barrier 해제
    bool _chasersSent;

    float _startedAt;

    Transform _map;

    // ── 수명 주기 ──────────────────────────────────────────────

    void Start()
    {
        // 게이트는 뽑기보다 먼저 걸어야 한다 — 아래 핸들러가 같은 프레임에 바로 돌 수 있다.
        RegisterMapGate();

        SetPadsActive(false);
        SetActiveAll(barriers, true);
        if (descentBlocker != null) descentBlocker.SetActive(false);

        PlayerSpawnCoordinator.OnPlayersReady += OnPlayersReadyHandler;
        if (PlayerSpawnCoordinator.IsReady) OnPlayersReadyHandler();
    }

    void OnDestroy()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= OnPlayersReadyHandler;
        MarkMapGateReady(); // 뽑기 전에 씬이 내려가도 게이트는 풀고 나간다(다음 씬이 멈춘다)
    }

    void OnPlayersReadyHandler()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= OnPlayersReadyHandler;
        PickMapAndRunner();
    }

    // ── 1. 뽑기 ────────────────────────────────────────────────

    void PickMapAndRunner()
    {
        if (_picked) return;
        _picked = true;

        RunnerClientId = DrawRunner();

        if (mapPicker != null) _map = mapPicker.Pick();
        else Debug.LogWarning("[T5RunnerDirector] mapPicker가 비어 있다 — StageVariantPicker를 연결할 것.", this);

        // 러너 색은 2층 패드를 밟을 수 없으므로 슬롯에서 빼 Common으로 떨어뜨린다
        // (`SceneReadyAndColorMapping.md` §B.1). 전 머신이 같은 러너를 계산하므로 로컬 호출이다.
        SessionColorSlotMap.SetRunnerExclusion(RunnerColor());

        var net = StageNetworkState.Instance;
        if (net != null && IsHostLane())
        {
            net.SetT5Runner(RunnerClientId); // 체이서가 읽을 단일 진실
            net.CloseAllGates();             // 문은 시작 시 전부 닫힘 (§1.4)
        }

        // 애니메이션 없이 닫힌 위치로 — 시작 순간 문이 스르륵 닫히는 걸 보여줄 이유가 없다.
        _map?.GetComponentInChildren<ColorGateController>(true)?.SnapAllClosed();

        SetRunnerPadActive(false);

        NetLog.Transition("T5RunnerDirector", "Draw",
            $"map={_map?.name} runner={RunnerClientId} seed={NetworkSessionData.Seed}");

        MarkMapGateReady();
    }

    /// <summary>
    /// 정렬된 clientId를 시드로 섞어 맨 앞을 쓴다 — 솔로는 자동으로 본인이 된다.
    /// 입력이 명단과 시드뿐이라 전 머신이 같은 답을 낸다.
    /// </summary>
    ulong DrawRunner()
    {
        var ids = new List<ulong>();
        foreach ((ulong clientId, PlayerColorType _) in PlayerSpawnCoordinator.GetAllEntries())
            ids.Add(clientId);

        if (ids.Count == 0)
        {
            Debug.LogError("[T5RunnerDirector] 활성 플레이어 명단이 비어 러너를 뽑을 수 없습니다.", this);
            return StageNetworkState.NoRunner;
        }

        ids.Sort();

        var rng = new System.Random(NetworkSessionData.Seed ^ RunnerSalt);
        for (int i = ids.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (ids[i], ids[j]) = (ids[j], ids[i]);
        }
        return ids[0];
    }

    PlayerColorType RunnerColor() =>
        PlayerSpawnCoordinator.TryGetColor(RunnerClientId, out PlayerColorType c)
            ? c
            : PlayerColorType.Common;

    // ── 2. 준비 게이트 ─────────────────────────────────────────

    void RegisterMapGate()
    {
        var curtain = LoadingCurtain.Instance;
        if (curtain == null || !curtain.IsCovered) return; // 덮여 있지 않으면 등록해봐야 의미가 없다

        curtain.RegisterGate(MapGate);
        _gateHeld = true;
    }

    void MarkMapGateReady()
    {
        if (!_gateHeld) return;
        _gateHeld = false;

        LoadingCurtain.Instance?.MarkGateReady(MapGate);
    }

    // ── 3. 시작 절차 ───────────────────────────────────────────

    /// <summary>`StageStartGate.OnCountdownComplete`에 연결. 러너 것을 뺀 발판을 켠다.</summary>
    public void BeginStage()
    {
        if (_started) return;
        _started   = true;
        _startedAt = Time.time;

        SetPadsActive(true);
        SetRunnerPadActive(false);

        NetLog.Transition("T5RunnerDirector", "LaunchPadsOn", $"runner={RunnerClientId}");

        // 솔로는 올라갈 안내자가 없어 도착 감지가 영영 오지 않는다 — 바로 푼다.
        if (GuideCount() == 0) ReleaseStart("solo");
    }

    void Update()
    {
        if (_started && !_released) TickArrival();
        if (_released && !_chasersSent) TickRunnerExit();
    }

    /// <summary>
    /// 안내자가 2층 도착 존에 들어왔는지(또는 안전망 시간이 지났는지). 원격 플레이어 위치는
    /// CNT로 수렴하므로 머신마다 최대 보간 지연만큼 시점이 다를 수 있는데, 이 토글의 효과
    /// (발판·barrier)는 어차피 각자 자기 머신에서 자기 몸에만 적용된다(클래스 주석 권한 항목).
    /// </summary>
    void TickArrival()
    {
        if (arrivalZone2F != null)
        {
            foreach (Player p in FindObjectsByType<Player>(FindObjectsSortMode.None))
            {
                if (p == null || p.IsDead) continue;
                if (IsRunner(p)) continue;
                if (!arrivalZone2F.bounds.Contains(p.transform.position)) continue;

                ReleaseStart("arrival");
                return;
            }
        }

        if (arrivalFallbackSeconds > 0f && Time.time - _startedAt >= arrivalFallbackSeconds)
            ReleaseStart("fallback");
    }

    /// <summary>발판을 끄고 barrier를 풀고 2층 하향 방지를 켠다 — 셋이 한 스위치다.</summary>
    void ReleaseStart(string reason)
    {
        if (_released) return;
        _released = true;

        SetPadsActive(false);
        SetActiveAll(barriers, false);
        if (descentBlocker != null) descentBlocker.SetActive(true);

        NetLog.Transition("T5RunnerDirector", "StartReleased", $"reason={reason}");
    }

    // ── 4. 체이서 ──────────────────────────────────────────────

    /// <summary>
    /// 러너가 시작 존을 벗어나면 1회 스폰. Host 레인에서만 판정한다(스폰이 Host 전권이라
    /// 판정을 Client가 내릴 이유가 없다).
    ///
    /// 러너가 끝까지 안 나가면 체이서도 안 나온다 — 교착이 아니라 제한시간이 실패로 끝낸다.
    /// </summary>
    void TickRunnerExit()
    {
        if (!IsHostLane() || chaserSpawner == null || startZone == null) return;

        Player runner = FindRunner();
        if (runner == null || runner.IsDead) return;
        if (startZone.bounds.Contains(runner.transform.position)) return;

        _chasersSent = true;
        chaserSpawner.StartSpawning();

        NetLog.Transition("T5RunnerDirector", "ChasersSpawned", $"runner={RunnerClientId}");
    }

    // ── 유틸 ───────────────────────────────────────────────────

    void SetPadsActive(bool on)
    {
        foreach (LaunchPad lp in launchPads)
            if (lp?.pad != null) lp.pad.SetActive(on);
    }

    /// <summary>러너 색 발판만 따로 끈다. 켜는 경우는 없다 — 러너는 끝까지 1층이다.</summary>
    void SetRunnerPadActive(bool on)
    {
        if (RunnerClientId == StageNetworkState.NoRunner) return;

        PlayerColorType runnerColor = RunnerColor();
        foreach (LaunchPad lp in launchPads)
            if (lp?.pad != null && lp.color == runnerColor) lp.pad.SetActive(on);
    }

    static void SetActiveAll(GameObject[] objects, bool on)
    {
        foreach (GameObject go in objects)
            if (go != null) go.SetActive(on);
    }

    int GuideCount()
    {
        int n = 0;
        foreach ((ulong clientId, PlayerColorType _) in PlayerSpawnCoordinator.GetAllEntries())
            if (clientId != RunnerClientId) n++;
        return n;
    }

    bool IsRunner(Player p)
    {
        NetworkObject no = p.GetComponent<NetworkObject>();
        return no != null && no.OwnerClientId == RunnerClientId;
    }

    Player FindRunner()
    {
        foreach (Player p in FindObjectsByType<Player>(FindObjectsSortMode.None))
            if (IsRunner(p)) return p;
        return null;
    }

    static bool IsHostLane()
    {
        var nm = NetworkManager.Singleton;
        return nm == null || !nm.IsListening || nm.IsServer;
    }
}
