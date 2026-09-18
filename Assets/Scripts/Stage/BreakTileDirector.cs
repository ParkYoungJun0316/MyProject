using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 파괴 타일 지휘자 — T.Stage4 함정 랜덤화 ③.
/// SSOT: Assets/Docs/TStage4TrapRandomization.md §1.4 / §4.1
///
/// [하는 일은 셋뿐이다]
///  1. **판 선택 요청** — `StageVariantPicker`에게 이번 런의 판을 고르게 하고, 그 판의 타일에
///     전 머신 공통 인덱스를 심는다. 뽑기 자체는 Picker가 한다(T5·향후 T2/T.Boss와 공유하는 부품).
///  2. **준비 게이트** — 판이 켜지기 전에 커튼이 걷히지 않도록 씬 준비 게이트를 대신 등록한다.
///     판 5장은 비활성으로 저장돼 있어 스스로 등록할 수 없다(`SceneReadyAndColorMapping.md` §A.3).
///  3. **보고 릴레이** — 타일이 밟혔다는 보고를 Host로 올리고, Host가 정한 붕괴 서버 시각을
///     전 머신의 같은 타일에 꽂아 준다.
///
///  파괴 **개수**는 코드가 정하지 않는다 — 판에 BreakTile을 몇 개 붙였는지가 곧 개수다.
///  (인원별 역스케일도, 후보 중 일부만 뽑는 quota도 없다. §1.3 참고 — 인원에서 수치를 파생시키면
///   그 인원수를 읽는 머신마다 지형이 달라진다.)
///
/// [왜 릴레이가 여기 있나]
///  BreakTile은 NetworkObject가 아니다(판 하나에 수십~백 개라 전부 네트워크 오브젝트로 만들 수 없다).
///  그래서 스테이지 내내 살아있는 StageNetworkState를 상주 릴레이로 쓴다 —
///  TrapProjectile 피격 보고(`ReportTrapHitServerRpc`)가 같은 이유로 같은 자리를 쓰고 있다.
///  타일을 가리키는 것은 **인덱스**이고, 이름순 정렬로 배정하므로 전 머신이 같은 배열을 갖는다
///  (WallLineRandomizer·Breakable의 `_netIndex` 자동 배정과 같은 관례).
///
/// [씬 설정]
///  1. 이 컴포넌트를 씬에 하나 둔다.
///  2. `StageVariantPicker`를 하나 두고 격자 판 후보를 등록한 뒤 boardPicker에 연결한다.
///     T4는 판 5장을 **복도 위 같은 자리에 겹쳐 두므로 Picker의 anchor는 비운다** — 이동이 없으면
///     CapacityTile 감지 기둥이 어긋날 여지 자체가 없다.
///     **판은 전부 비활성으로 저장할 것** — 겹쳐 둔 판이 동시에 켜지면 바닥이 다섯 겹이 된다.
///  3. 파괴시킬 타일에 BreakTile을 붙이고 경고 마커를 달아 준다. 몇 개를 붙이든 그게 그대로 개수다.
/// </summary>
[DefaultExecutionOrder(-100)]
public class BreakTileDirector : MonoBehaviour
{
    [Header("판")]
    [Tooltip("격자 판을 고르는 StageVariantPicker. 뽑기·활성화는 전부 그쪽이 한다.\n" +
             "T4는 판을 겹쳐 두므로 Picker의 anchor는 비울 것(이동 없음).")]
    [SerializeField] StageVariantPicker boardPicker;

    [Header("경고")]
    [Tooltip("밟은 뒤 바닥이 빠지기까지의 시간(초). 이 값으로 Host가 붕괴 서버 시각을 정하므로\n" +
             "전 머신이 같은 순간에 무너진다.")]
    [SerializeField] float warnSeconds = 1f;

    /// <summary>밟은 뒤 붕괴까지의 시간(초). BreakTile이 로컬 경고 연출 길이에 쓴다.</summary>
    public float WarnSeconds => warnSeconds;

    // 판 선택이 끝나기 전에 커튼이 걷히면 판 하나가 눈앞에서 켜지는 것이 그대로 보인다.
    // 이름은 const로 고정한다(SceneReadyAndColorMapping.md §A.3 — 오타가 나면 조용히 통과한다).
    const string BoardGate = "t4.board";

    bool _selected;
    bool _gateHeld;

    // 이름순으로 고정된 타일 배열 — 인덱스가 곧 전 머신 공통 식별자다.
    readonly List<BreakTile> _tiles = new List<BreakTile>();

    // Host 레인: 이미 붕괴 시각을 확정해 뿌린 타일(같은 프레임에 여러 명이 보고해도 한 번만 나간다).
    readonly HashSet<int> _armedOnHost = new HashSet<int>();

    StageNetworkState _netState;

    void Start()
    {
        // 게이트는 판 선택보다 먼저 걸어야 한다 — 아래 OnPlayersReadyHandler가 같은 프레임에
        // 바로 돌 수 있고, 그러면 등록도 해제도 없이 지나가 버린다.
        RegisterBoardGate();

        // 사망 리로드 시 시드 RPC가 Start()보다 먼저 도착한다는 보장이 없으므로
        // 판 선택을 OnPlayersReady 이후로 미룬다(StagePressurePadSetup과 동일 관례).
        PlayerSpawnCoordinator.OnPlayersReady += OnPlayersReadyHandler;
        if (PlayerSpawnCoordinator.IsReady) OnPlayersReadyHandler();

        _netState = StageNetworkState.Instance;
        if (_netState != null)
        {
            _netState.OnBreakTileStepReported += HandleStepReportedOnHost;
            _netState.OnBreakTileArmed        += HandleArmed;
        }
        else
        {
            Debug.LogWarning("[BreakTileDirector] StageNetworkState를 찾지 못해 파괴 타일 보고가 동작하지 않습니다.", this);
        }
    }

    void OnDestroy()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= OnPlayersReadyHandler;

        // 판 선택 전에 씬이 내려가도 게이트는 풀고 나간다 — 커튼은 씬을 넘어 살아남으므로
        // 들고 나가면 다음 씬이 타임아웃(5초)까지 덮인 채 멈춘다.
        MarkBoardGateReady();

        if (_netState != null)
        {
            _netState.OnBreakTileStepReported -= HandleStepReportedOnHost;
            _netState.OnBreakTileArmed        -= HandleArmed;
        }
    }

    void OnPlayersReadyHandler()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= OnPlayersReadyHandler;
        SelectBoard();
    }

    // ── 판 선택 ────────────────────────────────────────────────

    void SelectBoard()
    {
        if (_selected) return;
        _selected = true;

        if (boardPicker == null)
        {
            Debug.LogWarning("[BreakTileDirector] boardPicker가 비어 있다 — StageVariantPicker를 연결할 것.", this);
        }
        else
        {
            Transform board = boardPicker.Pick();
            if (board != null)
            {
                BindTiles(board);
                Debug.Log($"[BreakTileDirector] 판={board.name} 파괴타일={_tiles.Count}");
            }
        }

        // 판을 골랐든 못 골랐든 게이트는 반드시 푼다. 실패했다고 들고 있으면 커튼이 타임아웃까지
        // 덮여 있게 되는데, 그건 "판이 없다"보다 더 나쁜 증상이다(경고 로그로 이미 시끄럽다).
        MarkBoardGateReady();
    }

    // ── 준비 게이트 ────────────────────────────────────────────

    void RegisterBoardGate()
    {
        var curtain = LoadingCurtain.Instance;
        if (curtain == null || !curtain.IsCovered) return; // 덮여 있지 않으면 등록해봐야 의미가 없다

        curtain.RegisterGate(BoardGate);
        _gateHeld = true;
    }

    void MarkBoardGateReady()
    {
        if (!_gateHeld) return;
        _gateHeld = false;

        LoadingCurtain.Instance?.MarkGateReady(BoardGate);
    }

    void BindTiles(Transform board)
    {
        _tiles.Clear();
        board.GetComponentsInChildren(true, _tiles);

        // 전 머신이 같은 인덱스로 같은 타일을 가리켜야 한다. 이름은 T_{행}_{열}_{역할} 규약이라
        // 그 자체로 결정적이고 sibling 순서에 의존하지 않는다.
        _tiles.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        for (int i = 0; i < _tiles.Count; i++)
            _tiles[i].Bind(this, i);
    }

    // ── 보고 릴레이 ────────────────────────────────────────────

    /// <summary>BreakTile이 호출 — 밟은 당사자의 Owner 머신에서만 올라온다.</summary>
    public void ReportStep(int tileIndex)
    {
        if (_netState == null) return;
        _netState.ReportBreakTileSteppedServerRpc(tileIndex);
    }

    /// <summary>Host 레인: 보고 수신 → 붕괴 서버 시각 확정 → 전 머신에 배포.</summary>
    void HandleStepReportedOnHost(int tileIndex)
    {
        if (tileIndex < 0 || tileIndex >= _tiles.Count) return;
        if (!_armedOnHost.Add(tileIndex)) return; // 같은 타일 중복 보고 — 첫 보고만 인정

        BreakTile tile = _tiles[tileIndex];
        if (tile == null) return;

        _netState?.BroadcastBreakTileArm(tileIndex, ServerNow() + warnSeconds);
    }

    /// <summary>전 머신: Host가 확정한 붕괴 시각을 그 타일에 꽂는다.</summary>
    void HandleArmed(int tileIndex, double collapseServerTime)
    {
        if (tileIndex < 0 || tileIndex >= _tiles.Count) return;
        _tiles[tileIndex]?.ArmFromServer(collapseServerTime);
    }

    static double ServerNow()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening ? nm.ServerTime.Time : Time.timeAsDouble;
    }

    void OnValidate()
    {
        warnSeconds = Mathf.Max(0.1f, warnSeconds);
    }
}
