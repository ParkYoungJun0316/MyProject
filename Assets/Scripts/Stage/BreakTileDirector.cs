using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 파괴 타일 지휘자 — T.Stage4 함정 랜덤화 ③ / T.Boss P1 붕괴 트랙.
/// SSOT: Assets/Docs/TStage4TrapRandomization.md §1.4 / §4.1
///
/// [하는 일은 둘뿐이다 (2026-09-21 — 판 추첨 폐기로 셋에서 줄었다)]
///  1. **인덱스 배정** — 판의 BreakTile을 모아 월드 좌표순으로 정렬하고 전 머신 공통 인덱스를 심는다.
///  2. **보고 릴레이** — 타일이 밟혔다는 보고를 Host로 올리고, Host가 정한 파괴 서버 시각을
///     전 머신의 같은 타일에 꽂아 준다.
///
///  파괴 **개수**는 코드가 정하지 않는다 — 판에 BreakTile을 몇 개 깔았는지가 곧 개수다.
///  (인원별 역스케일도, 후보 중 일부만 뽑는 quota도 없다. §1.3 참고 — 인원에서 수치를 파생시키면
///   그 인원수를 읽는 머신마다 지형이 달라진다.)
///
/// [판은 한 장이다 (2026-09-21 확정 — 5장 시드 추첨 폐기)]
///  판 N장 중 하나를 시드로 고르던 구조를 버렸다. 그래서 이 컴포넌트에서 `StageVariantPicker`
///  의존과 커튼 준비 게이트(`"t4.board"`)가 함께 사라졌다 — 판이 바뀌지 않으면 커튼 뒤에서
///  감출 것도, 시드 RPC보다 먼저 뽑아 버릴 위험도 없다.
///  **판은 씬에 활성 상태로 저장한다.** 비활성으로 저장하면 아무도 켜 주지 않는다.
///
/// [왜 릴레이가 여기 있나]
///  BreakTile은 NetworkObject가 아니다(판 하나에 수십~백 개라 전부 네트워크 오브젝트로 만들 수 없다).
///  그래서 스테이지 내내 살아있는 StageNetworkState를 상주 릴레이로 쓴다 —
///  TrapProjectile 피격 보고(`ReportTrapHitServerRpc`)가 같은 이유로 같은 자리를 쓰고 있다.
///  타일을 가리키는 것은 **인덱스**이고, 월드 좌표 정렬로 배정하므로 전 머신이 같은 배열을 갖는다
///  (Breakable의 `_netIndex` 자동 배정과 같은 관례).
///
/// [씬 설정]
///  1. 이 컴포넌트를 씬에 하나 둔다.
///  2. `boardRoot`에 타일이 들어 있는 루트를 연결한다(비우면 씬 전체에서 모은다).
///     **루트는 활성으로 저장할 것.**
///  3. 파괴시킬 자리에 `Assets/Prefab/BreakTile.prefab` 인스턴스를 깐다. 파편·파티클·소리 값은
///     전부 프리팹에 있으므로 여기서 손댈 것이 없다 — 몇 개를 깔든 그게 그대로 개수다.
/// </summary>
[DefaultExecutionOrder(-100)]
public class BreakTileDirector : MonoBehaviour
{
    [Header("판")]
    [Tooltip("파괴 타일이 들어 있는 루트. 비우면 씬 전체에서 BreakTile을 모은다.\n" +
             "판은 씬에 활성 상태로 저장할 것 — 비활성으로 두면 아무도 켜 주지 않는다.")]
    [SerializeField] Transform boardRoot;

    [Header("경고")]
    [Tooltip("밟은 뒤 바닥이 부서지기까지의 시간(초). 이 값으로 Host가 파괴 서버 시각을 정하므로\n" +
             "전 머신이 같은 순간에 부서진다.")]
    [SerializeField] float warnSeconds = 1f;

    /// <summary>밟은 뒤 파괴까지의 시간(초). BreakTile이 로컬 경고 연출 길이에 쓴다.</summary>
    public float WarnSeconds => warnSeconds;

    // 월드 좌표순으로 고정된 타일 배열 — 인덱스가 곧 전 머신 공통 식별자다.
    readonly List<BreakTile> _tiles = new List<BreakTile>();

    // Host 레인: 이미 파괴 시각을 확정해 뿌린 타일(같은 프레임에 여러 명이 보고해도 한 번만 나간다).
    // 타일 쪽 상태로는 대신할 수 없다 — Host 본인이 밟으면 보고가 도착하기 전에 이미 경고 중이다.
    readonly HashSet<int> _armedOnHost = new HashSet<int>();

    StageNetworkState _netState;

    void Start()
    {
        BindTiles();

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
        if (_netState != null)
        {
            _netState.OnBreakTileStepReported -= HandleStepReportedOnHost;
            _netState.OnBreakTileArmed        -= HandleArmed;
        }
    }

    // ── 인덱스 배정 ────────────────────────────────────────────

    void BindTiles()
    {
        _tiles.Clear();

        if (boardRoot != null)
        {
            boardRoot.GetComponentsInChildren(true, _tiles);

            if (!boardRoot.gameObject.activeInHierarchy)
            {
                Debug.LogWarning(
                    $"[BreakTileDirector] 판 '{boardRoot.name}'이 비활성으로 저장돼 있다 — " +
                    "판 추첨이 없어진 뒤로는 아무도 켜 주지 않는다. 활성으로 저장할 것.", this);
            }
        }
        else
        {
            _tiles.AddRange(FindObjectsByType<BreakTile>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        }

        // 전 머신이 같은 인덱스로 같은 타일을 가리켜야 한다. 기준은 **월드 좌표**다 —
        // 씬 파일에 저장된 고정 좌표라 Host/Client가 언제나 같은 순서를 얻고, 이름에 의존하지
        // 않는다(타일은 전부 BreakTile 프리팹 인스턴스라 이름이 서로 같다).
        // Breakable.CompareByWorldPosition과 같은 규약 — 그쪽과 비교식을 어긋나게 두지 말 것.
        _tiles.Sort(CompareByWorldPosition);

        for (int i = 0; i < _tiles.Count; i++)
            _tiles[i].Bind(this, i);

        Debug.Log($"[BreakTileDirector] 판={(boardRoot != null ? boardRoot.name : "(씬 전체)")} 파괴타일={_tiles.Count}");
    }

    /// <summary>x→y→z 순으로 비교. 씬에 고정된 좌표라 전 머신이 항상 같은 순서를 얻는다.</summary>
    static int CompareByWorldPosition(BreakTile a, BreakTile b)
    {
        Vector3 pa = a.transform.position;
        Vector3 pb = b.transform.position;

        int c = pa.x.CompareTo(pb.x);
        if (c != 0) return c;

        c = pa.y.CompareTo(pb.y);
        if (c != 0) return c;

        return pa.z.CompareTo(pb.z);
    }

    // ── 보고 릴레이 ────────────────────────────────────────────

    /// <summary>BreakTile이 호출 — 밟은 당사자의 Owner 머신에서만 올라온다.</summary>
    public void ReportStep(int tileIndex)
    {
        if (_netState == null) return;
        _netState.ReportBreakTileSteppedServerRpc(tileIndex);
    }

    /// <summary>Host 레인: 보고 수신 → 파괴 서버 시각 확정 → 전 머신에 배포.</summary>
    void HandleStepReportedOnHost(int tileIndex)
    {
        if (tileIndex < 0 || tileIndex >= _tiles.Count) return;
        if (!_armedOnHost.Add(tileIndex)) return; // 같은 타일 중복 보고 — 첫 보고만 인정

        BreakTile tile = _tiles[tileIndex];
        if (tile == null) return;

        _netState?.BroadcastBreakTileArm(tileIndex, ServerNow() + warnSeconds);
    }

    /// <summary>전 머신: Host가 확정한 파괴 시각을 그 타일에 꽂는다.</summary>
    void HandleArmed(int tileIndex, double breakServerTime)
    {
        if (tileIndex < 0 || tileIndex >= _tiles.Count) return;
        _tiles[tileIndex]?.ArmFromServer(breakServerTime);
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
