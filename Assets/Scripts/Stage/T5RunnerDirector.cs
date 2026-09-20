using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// T.Stage5 러너 디렉터 — 한 판짜리 스테이지의 시작 절차를 담당한다.
/// SSOT: `Assets/Docs/TStage5RunnerRedesign.md` §1.1 / §1.2 / §4.3
///
/// [하는 일 — 넷뿐이다]
///  1. **뽑기** — 시드로 **러너 1명 + 고유색 순열**을 정한다. **맵은 1장이라 안 뽑는다**(§1.1).
///  2. **준비 게이트** — 색이 확정되기 전에 커튼이 걷히지 않도록 `"t5.map"`을 건다.
///     **문 색이 곧 이번 판의 지형**이라 커튼이 걷힐 때는 이미 확정돼 있어야 한다
///     (`SceneReadyAndColorMapping.md` §A.3). 맵 선택이 사라져도 게이트 문자열은 그대로 쓴다 —
///     뜻이 "이번 판 확정 완료"로 같다.
///  3. **시작 절차** — 카운트다운 완료 시 러너 것을 뺀 발판을 켜고, 안내자가 **각자** 2층에 닿을 때마다
///     **그 사람 몫의 2층 바닥 판을 닫는다**. **전원이 닿으면**(또는 안전망 시간) 남은 판을 닫고,
///     그 다음에 발판을 끄고 barrier를 푼다 — 순서가 뒤집히면 러너가 구멍 뚫린 2층을 등지고 출발한다.
///  4. **체이서** — 러너가 시작 존을 벗어나면 1회 스폰시킨다.
///
///  **라운드도 맵 뽑기도 없다.**
///
/// [매판 판이 달라지는 축 둘 — 맵이 1장인데 어디서 나오나 (§1.12)]
///  · **러너 로테이션** — 러너 색 문 약 30개가 통째로 **벽**이 되므로 누가 러너냐에 따라 격자가 바뀐다.
///  · **시드 고유색 순열** — 문에 칠해진 고유 4색을 섞어 매판 다른 사람이 열쇠를 쥔다.
///    흑·백은 안 건드리므로 생성기가 보장한 연결성·최소 전환 횟수는 순열에 대해 불변이다.
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
///  · 판이 **레인별**인 이유: 하나로 된 뚜껑을 "전원 도착"에 닫으면, 한 명이 발사에 실패했을 때
///    이미 올라간 사람들이 **아직 안 닫힌 구멍으로 도로 떨어져** 무한히 튀어오른다. 그때 뚜껑이
///    닫히면 비행 중이던 사람이 밑에 갇히고, 갇힘을 피하려 기다리면 영영 안 닫힌다 — 교착이다.
///    레인별이면 성공한 사람은 **자기가 올라간 그 비행에서** 자기 판이 닫혀 그 위에 착지하고,
///    실패한 사람만 혼자 남는다.
///
/// [판의 초기 상태가 "닫힘"인 이유 — 러너·없는 색 구멍 문제]
///  판을 열어두고 도착할 때 닫는 방식이면 **아무도 올라오지 않는 레인이 영영 열린 채로 남는다** —
///  러너는 1층에 남고(자기 발판이 꺼져 있다), 3인·2인이면 없는 색 자리도 비기 때문이다.
///  그래서 **전부 닫힌 채로 시작해서 이번 판에 실제로 발사될 안내자 색만 연다.**
///  솔로는 안내자가 0명이라 넷 다 닫힌 채고, 2층은 처음부터 완전한 바닥이 된다.
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
///  2. `launchPads` 4개(색별) · `floorPlates` 4개(색별 `Lid_*`) · `barriers`(대각 칸막이).
///     **발판과 판은 색이 서로 맞아야 한다** — Blue 발판에서 올라간 사람은 Blue 판 밑으로 나온다.
///  3. `arrivalZone2F`(2층 상공 트리거) · `startZone`(러너 이탈 트리거) · `chaserSpawner`.
///  4. `ColorGateController`는 배선하지 않는다 — 씬에 하나뿐이라 자동으로 찾는다.
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

    /// <summary>
    /// 색 하나에 대응하는 2층 바닥 판. 발사통 위 구멍을 넷으로 나눈 삼각형 한 장이고,
    /// 그 색 자리에서 발사된 사람의 **머리 위를 덮는 몫**이다.
    /// **켜짐 = 닫힘(바닥 있음)**, 꺼짐 = 열림(구멍)이라 활성 상태와 의미가 반대인 것에 주의.
    /// </summary>
    [System.Serializable]
    public class FloorPlate
    {
        [Tooltip("이 판이 덮는 시작 자리의 색.")]
        public PlayerColorType color = PlayerColorType.Blue;

        [Tooltip("2층 바닥 판 오브젝트(Lid_*). 켜져 있으면 바닥, 꺼져 있으면 구멍이다.")]
        public GameObject plate;
    }


    [Header("시작 발판 / barrier")]
    [Tooltip("색별 시작 발판. 러너 색 발판만 빼고 카운트다운 완료 시 켜진다.")]
    [SerializeField] LaunchPad[] launchPads = new LaunchPad[0];

    [Tooltip("시작 자리를 칸칸이 막는 투명 barrier. 안내자가 2층에 닿으면 전부 꺼진다.")]
    [SerializeField] GameObject[] barriers = new GameObject[0];

    [Tooltip("2층 도착 감지 존(트리거 콜라이더). 안내자가 여기 들어오면 시작 절차가 끝난다.")]
    [SerializeField] Collider arrivalZone2F;

    [Tooltip("색별 2층 바닥 판(Lid_*). 전부 닫힌 채로 시작해 안내자 색 것만 열리고,\n" +
             "그 사람이 2층에 닿으면 다시 닫힌다. 러너 색·없는 색은 끝까지 닫힌 채다.")]
    [SerializeField] FloorPlate[] floorPlates = new FloorPlate[0];

    [Tooltip("2층 도착이 감지되지 않아도 이 시간(초) 뒤에는 남은 판을 닫고 발판을 끄고 barrier를 푼다.\n" +
             "누군가 발사에 실패해 판이 통째로 멈추는 것을 막는 안전망이다.")]
    [SerializeField] float arrivalFallbackSeconds = 8f;

    [Header("러너 출발 / 체이서")]
    [Tooltip("러너 시작 존(트리거 콜라이더). 러너가 여기를 벗어나면 체이서가 나온다.")]
    [SerializeField] Collider startZone;

    [Tooltip("씬에 하나 있는 Stage5ChaserSpawner.")]
    [SerializeField] Stage5ChaserSpawner chaserSpawner;

    /// <summary>러너 시작 존(읽기 전용). 지도 UI가 start 점 위치로 쓴다 — 좌표를 따로 적어두지 않기 위해서다.</summary>
    public Collider RunnerStartZone => startZone;

    /// <summary>이번 판의 러너 clientId. 뽑기 전이면 <see cref="StageNetworkState.NoRunner"/>.</summary>
    public ulong RunnerClientId { get; private set; } = StageNetworkState.NoRunner;

    bool _picked;
    bool _gateHeld;
    bool _started;      // 카운트다운 완료 → 발판 켜짐
    bool _released;     // 2층 도착(또는 안전망) → 발판 끄고 barrier 해제
    bool _chasersSent;

    float _startedAt;

    /// <summary>이미 2층에 닿은 안내자 clientId. 한 번 들어오면 빠지지 않는다(래치).</summary>
    readonly HashSet<ulong> _arrivedGuides = new HashSet<ulong>();


    // ── 수명 주기 ──────────────────────────────────────────────

    void Start()
    {
        // 게이트는 뽑기보다 먼저 걸어야 한다 — 아래 핸들러가 같은 프레임에 바로 돌 수 있다.
        RegisterMapGate();

        SetPadsActive(false);
        SetActiveAll(barriers, true);
        SetAllPlatesClosed();   // 뽑기 전에는 2층에 구멍이 없다 — 열 레인은 러너가 정해진 뒤에 고른다

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
        PickRunnerAndColors();
    }

    // ── 1. 뽑기 ────────────────────────────────────────────────

    /// <summary>
    /// 이번 판의 러너와 색 배치를 확정한다. 순서가 중요하다 —
    /// **순열을 먼저 얹고 그 다음 러너 제외**다. 러너 제외는 "순열이 끝난 뒤의 색"을 기준으로
    /// 벽을 정하므로, 뒤집으면 엉뚱한 색이 벽이 된다.
    /// </summary>
    void PickRunnerAndColors()
    {
        if (_picked) return;
        _picked = true;

        RunnerClientId = DrawRunner();

        // ① 시드 고유색 순열 — 매판 다른 사람이 열쇠를 쥔다 (§1.12).
        SessionColorSlotMap.SetDesignPermutation(NetworkSessionData.Seed);

        // ② 러너 색은 **벽**이 된다 — 러너는 1층이라 2층 패드를 밟을 수 없다 (§1.4).
        //    전 머신이 같은 러너를 계산하므로 로컬 호출이다.
        SessionColorSlotMap.SetRunnerExclusion(RunnerColor());

        var net = StageNetworkState.Instance;
        if (net != null && IsHostLane())
        {
            net.SetT5Runner(RunnerClientId); // 체이서가 읽을 단일 진실
            net.CloseAllGates();             // 문은 시작 시 전부 닫힘 (§1.4)
        }

        // 애니메이션 없이 닫힌 위치로 — 시작 순간 문 180개가 스르륵 닫히는 걸 보여줄 이유가 없다.
        FindFirstObjectByType<ColorGateController>(FindObjectsInactive.Include)?.SnapAllClosed();

        SetRunnerPadActive(false);
        OpenGuidePlates();

        NetLog.Transition("T5RunnerDirector", "Draw",
            $"runner={RunnerClientId} runnerColor={RunnerColor()} seed={NetworkSessionData.Seed}");

        MarkMapGateReady();
    }

    /// <summary>
    /// **이번 사이클에 아직 러너를 안 한 사람** 중에서, 정렬된 clientId를 시드로 섞어 맨 앞을 쓴다.
    /// 솔로는 후보가 본인 1명이라 매번 본인이 된다.
    ///
    /// 후보를 걸러내는 이유: 순수 랜덤이면 연속 2판에서 같은 사람이 뽑힌다(4인 1/4). T5는
    /// 실패 → 리로드 → 재추첨이 **곧 러너 교대 기회**라(§1.8 "부활을 안 주는 이유"), 그 확률이
    /// 그대로 설계 구멍이 된다. 기록은 씬을 넘어 살아남아야 하므로 `GameSession`이 들고 있다(§1.10).
    ///
    /// 입력이 명단·기록·시드뿐이라 전 머신이 같은 답을 낸다.
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

        GameSession session = GameSession.Instance;
        List<ulong> pool = session != null ? session.GetT5RunnerCandidates(ids) : new List<ulong>(ids);
        if (pool.Count == 0) pool = new List<ulong>(ids);  // 방어 — 빌 수 없는 구조지만 여기서 멈추면 안 된다
        pool.Sort();

        var rng = new System.Random(NetworkSessionData.Seed ^ RunnerSalt);
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        ulong picked = pool[0];
        session?.MarkT5Runner(picked);
        return picked;
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
    /// 안내자가 **각자** 2층 도착 존에 들어왔는지 본다. 한 명 잡힐 때마다 그 사람 색 판을 닫고,
    /// 전원이 잡히면(또는 안전망 시간이 지나면) 시작 절차를 끝낸다.
    ///
    /// 도착 판정선이 2층 바닥(50.25)이 아니라 그보다 위인 이유: 판은 **그 사람이 아직 올라가는
    /// 중일 때** 발밑에서 닫혀야 한다. 착지한 뒤에 닫으면 이미 구멍으로 떨어진 뒤다.
    ///
    /// 원격 플레이어 위치는 CNT로 수렴하므로 머신마다 최대 보간 지연만큼 시점이 다를 수 있는데,
    /// 내 머신에서 타이밍이 중요한 판은 **내 판 하나뿐**이고 내 위치는 내 머신에서 정확하다.
    /// 남의 판이 몇 프레임 늦게 닫혀도 남의 몸은 어차피 그 머신에서 움직인다(클래스 주석 권한 항목).
    /// </summary>
    void TickArrival()
    {
        if (arrivalZone2F != null)
        {
            foreach (Player p in FindObjectsByType<Player>(FindObjectsSortMode.None))
            {
                if (p == null || p.IsDead) continue;
                if (IsRunner(p)) continue;

                NetworkObject no = p.GetComponent<NetworkObject>();
                if (no == null || _arrivedGuides.Contains(no.OwnerClientId)) continue;
                if (!arrivalZone2F.bounds.Contains(p.transform.position)) continue;

                _arrivedGuides.Add(no.OwnerClientId);

                if (PlayerSpawnCoordinator.TryGetColor(no.OwnerClientId, out PlayerColorType c))
                    SetPlateClosed(c, true);

                NetLog.Transition("T5RunnerDirector", "GuideArrived",
                    $"clientId={no.OwnerClientId} color={c} {_arrivedGuides.Count}/{GuideCount()}");
            }

            if (_arrivedGuides.Count >= GuideCount())
            {
                ReleaseStart("arrival");
                return;
            }
        }

        if (arrivalFallbackSeconds > 0f && Time.time - _startedAt >= arrivalFallbackSeconds)
            ReleaseStart("fallback");
    }

    /// <summary>
    /// 시작 절차 종료 — **순서가 사양이다.** 남은 판을 먼저 닫고, 그 다음에 발판을 끄고 barrier를 푼다.
    /// barrier가 먼저 풀리면 러너가 구멍이 아직 남은 2층을 등지고 출발한다.
    ///
    /// 안전망으로 들어온 경우에도 **남은 판을 닫는다**(2026-09-21 확정). 그 시각이면 정상 발사는
    /// 이미 끝났고, 안 올라왔다는 것은 발판을 아예 못 밟았다는 뜻이라 더 기다려도 안 올라온다.
    /// 구멍을 남겨 두면 잘못이 없는 다른 안내자가 패드 사이를 뛰다 빠진다 — 그쪽 피해가 더 크다.
    /// </summary>
    void ReleaseStart(string reason)
    {
        if (_released) return;
        _released = true;

        SetAllPlatesClosed();
        SetPadsActive(false);
        SetActiveAll(barriers, false);

        NetLog.Transition("T5RunnerDirector", "StartReleased",
            $"reason={reason} arrived={_arrivedGuides.Count}/{GuideCount()}");
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

    /// <summary>판 전부 닫기(= 2층에 구멍 없음). 시작 시점과 절차 종료 시점 양쪽에서 쓴다.</summary>
    void SetAllPlatesClosed()
    {
        foreach (FloorPlate fp in floorPlates)
            if (fp?.plate != null) fp.plate.SetActive(true);
    }

    /// <summary>판 한 장의 상태를 바꾼다. <paramref name="closed"/> = true면 바닥, false면 구멍.</summary>
    void SetPlateClosed(PlayerColorType color, bool closed)
    {
        foreach (FloorPlate fp in floorPlates)
            if (fp?.plate != null && fp.color == color) fp.plate.SetActive(closed);
    }

    /// <summary>
    /// **이번 판에 실제로 발사될 사람**의 레인만 연다 — 명단에 있고 러너가 아닌 색.
    /// 명단에 없는 색(3인·2인의 빈 자리)은 애초에 순회에 안 걸리므로 닫힌 채로 남고,
    /// 솔로는 안내자가 0명이라 한 장도 열리지 않는다.
    /// </summary>
    void OpenGuidePlates()
    {
        foreach ((ulong clientId, PlayerColorType color) in PlayerSpawnCoordinator.GetAllEntries())
            if (clientId != RunnerClientId) SetPlateClosed(color, false);
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
