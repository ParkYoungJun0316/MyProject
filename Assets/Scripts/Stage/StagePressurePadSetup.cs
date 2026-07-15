using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// T.Stage1 — 패드/문 퍼즐을 GameSession 인원·색에 맞게 초기화한다.
///
/// [실행 흐름]
///  Start()
///  1. 씬에서 PressurePad, DoorController, DoorPuzzleGroup 수집
///  2. pad→door 역방향 맵 구성
///  3. designColor != Common인 패드에 활성 색 균등 분배
///     - 같은 문에 묶인 패드끼리는 서로 다른 색 강제
///  4. 각 DoorPuzzleGroup.ApplyScaling(activeCount) 호출 (latch·requiredCount 스케일)
///  5. 문의 ColoredDoorVisual.Apply(effectiveColor) 동기화
///  6. 패드의 ColoredPadVisual.Apply(effectiveColor) 동기화
///
/// [Inspector 설정]
///  puzzleGroups[]     : DoorPuzzleGroup 수동 등록 (비우면 씬에서 자동 수집)
///  autoCollectPads    : true = PressurePad 자동 수집 (false면 pads[] 직접 입력)
///
/// [배치 위치]
///  씬 루트 빈 GameObject 또는 StageManager 자식 오브젝트 1개.
///  StageStartGate 가 있으면 카운트다운 완료 전에 이미 Start()가 실행되므로 별도 연결 불필요.
/// </summary>
public class StagePressurePadSetup : MonoBehaviour
{
    [Header("퍼즐 그룹")]
    [Tooltip("DoorPuzzleGroup이 붙은 오브젝트를 직접 등록.\n" +
             "비워두면 씬 전체에서 FindObjectsByType으로 자동 수집.")]
    [SerializeField] DoorPuzzleGroup[] puzzleGroups = new DoorPuzzleGroup[0];

    [Header("발판 수집")]
    [Tooltip("true: 씬의 모든 PressurePad 자동 수집.\n" +
             "false: 아래 pads[] 배열을 직접 입력 (일부만 관리할 때 사용).")]
    [SerializeField] bool autoCollectPads = true;

    [Tooltip("autoCollectPads = false 일 때 직접 등록하는 PressurePad 목록")]
    [SerializeField] PressurePad[] pads = new PressurePad[0];

    // 패드 → 이 패드를 requiredPads에 포함한 DoorController 목록
    readonly Dictionary<PressurePad, List<DoorController>> _padToDoors = new();

    // ── Unity 라이프사이클 ────────────────────────────────────────

    void Start()
    {
        Collect();
        BuildPadToDoorMap();

        // 사망 리로드 시 시드 RPC(BroadcastNewSeedClientRpc)가
        // StagePressurePadSetup.Start()보다 먼저 도착한다는 보장이 없으므로,
        // LoadEventCompleted → SpawnAllPlayers → OnPlayersReady 이후로 색 배정을 지연.
        // 이 시점은 씬 로드 + 플레이어 스폰 완료 이후로, 시드 RPC가 반드시 선행 처리됨.
        PlayerSpawnCoordinator.OnPlayersReady += OnPlayersReadyHandler;
        if (PlayerSpawnCoordinator.IsReady) OnPlayersReadyHandler();
    }

    void OnPlayersReadyHandler()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= OnPlayersReadyHandler;
        ApplySeedAndColors();
    }

    void OnDestroy()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= OnPlayersReadyHandler;
    }

    void ApplySeedAndColors()
    {
        if (LobbyContext.IsOnline)
        {
            const int salt = 0x050AD5E7;
            UnityEngine.Random.InitState(NetworkSessionData.Seed ^ salt);
            Debug.Log($"[StagePressurePadSetup] 시드 적용 — seed={NetworkSessionData.Seed}");
        }

        // PlayerSpawnCoordinator(NetworkList)가 SSOT — OnPlayersReady 시점에 레이스 없음.
        // GameSession 경유를 없애고 activeColors를 한 번만 결정해 하위에 주입한다.
        PlayerColorType[] activeColors = PlayerSpawnCoordinator.GetActiveColors();
        if (activeColors.Length == 0)
        {
            activeColors = new[]
            {
                PlayerColorType.Blue, PlayerColorType.Purple,
                PlayerColorType.Green, PlayerColorType.Yellow,
            };
            Debug.LogWarning("[StagePressurePadSetup] PlayerSpawnCoordinator 색 없음 — 4색 fallback");
        }
        Debug.Log($"[StagePressurePadSetup] activeColors({activeColors.Length}): {string.Join(", ", activeColors)}");

        DistributeColors(activeColors);
        ApplyTopologyScaling(activeColors.Length);
        SyncDoorVisuals();
        SyncPadVisuals();
        SyncPadCountUIs();
    }

    // ── 단계별 처리 ──────────────────────────────────────────────

    void Collect()
    {
        if (autoCollectPads)
            pads = FindObjectsByType<PressurePad>(FindObjectsSortMode.None);

        if (puzzleGroups == null || puzzleGroups.Length == 0)
            puzzleGroups = FindObjectsByType<DoorPuzzleGroup>(FindObjectsSortMode.None);
    }

    /// <summary>DoorController.requiredPads 기준으로 pad → door 역방향 맵을 구성한다.</summary>
    void BuildPadToDoorMap()
    {
        _padToDoors.Clear();

        DoorController[] allDoors = FindObjectsByType<DoorController>(FindObjectsSortMode.None);
        foreach (DoorController door in allDoors)
        {
            if (door == null || door.requiredPads == null) continue;
            foreach (PressurePad pad in door.requiredPads)
            {
                if (pad == null) continue;
                if (!_padToDoors.TryGetValue(pad, out List<DoorController> list))
                {
                    list = new List<DoorController>();
                    _padToDoors[pad] = list;
                }
                if (!list.Contains(door))
                    list.Add(door);
            }
        }
    }

    /// <summary>
    /// designColor != Common인 패드에 활성 색을 균등 분배하고 effectiveColor를 설정한다.
    /// 같은 문에 묶인 패드끼리는 서로 다른 색이 배정되도록 보정한다.
    /// </summary>
    void DistributeColors(PlayerColorType[] activeColors)
    {
        // colored 패드 목록
        var coloredPads = new List<PressurePad>();
        foreach (PressurePad pad in pads)
            if (pad != null && pad.designColor != PlayerColorType.Common)
                coloredPads.Add(pad);

        if (coloredPads.Count == 0) return;

        // 이름 기준 오름차순 정렬 → Host/Client 양측에서 동일 순서 보장
        coloredPads.Sort((a, b) =>
            string.Compare(a.gameObject.name, b.gameObject.name, System.StringComparison.Ordinal));

        // 색 분배 + 셔플 — activeColors를 직접 주입해 GameSession 경유를 없앤다
        PlayerColorType[] distributed = GameSessionColorDistribution.Distribute(activeColors, coloredPads.Count);
        Shuffle(distributed);

        // 같은 문에 묶인 패드끼리 다른 색 강제
        FixSiblingPadColors(coloredPads, distributed);

        // 패드에 effectiveColor 적용
        for (int i = 0; i < coloredPads.Count; i++)
            coloredPads[i].SetEffectiveColor(distributed[i]);
    }

    /// <summary>
    /// 각 DoorPuzzleGroup의 ApplyScaling을 호출해 latch / requiredCount 를 스케일한다.
    /// activeCount는 ApplySeedAndColors()에서 PlayerSpawnCoordinator 기준으로 확정된 값을 받는다.
    /// </summary>
    void ApplyTopologyScaling(int activeCount)
    {
        if (puzzleGroups == null || puzzleGroups.Length == 0) return;

        foreach (DoorPuzzleGroup group in puzzleGroups)
            group?.ApplyScaling(activeCount);
    }

    /// <summary>
    /// 씬의 모든 DoorController를 순회해 ColoredDoorVisual을 effectiveColor로 동기화한다.
    /// ApplyTopologyScaling 이후에 실행하므로 Simultaneous 축소 결과도 반영된다.
    /// </summary>
    void SyncDoorVisuals()
    {
        DoorController[] allDoors = FindObjectsByType<DoorController>(FindObjectsSortMode.None);
        foreach (DoorController door in allDoors)
        {
            if (door == null) continue;
            ColoredDoorVisual visual = door.GetComponent<ColoredDoorVisual>();
            if (visual == null) continue;

            // 문의 첫 번째 유효 패드 effectiveColor를 비주얼에 반영
            PlayerColorType color = PlayerColorType.Common;
            if (door.requiredPads != null)
                foreach (PressurePad pad in door.requiredPads)
                {
                    if (pad != null) { color = pad.EffectiveColor; break; }
                }

            visual.Apply(color);
        }
    }

    /// <summary>
    /// 씬의 모든 PressurePad를 순회해 ColoredPadVisual을 effectiveColor로 동기화한다.
    /// ApplyTopologyScaling 이후에 실행하므로 Simultaneous 축소 결과도 반영된다.
    /// </summary>
    void SyncPadVisuals()
    {
        foreach (PressurePad pad in pads)
        {
            if (pad == null) continue;
            pad.GetComponent<ColoredPadVisual>()?.Apply(pad.EffectiveColor);
        }
    }

    /// <summary>
    /// 씬의 모든 PressurePad를 순회해 PressurePadCountUI를 초기화한다.
    /// ApplyTopologyScaling 이후 스케일된 requiredCount 기준으로 표시.
    /// </summary>
    void SyncPadCountUIs()
    {
        foreach (PressurePad pad in pads)
        {
            if (pad == null) continue;
            pad.GetComponent<PressurePadCountUI>()?.Refresh();
        }
    }

    // ── 내부 유틸 ────────────────────────────────────────────────

    static void Shuffle<T>(T[] arr)
    {
        for (int i = arr.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
    }

    /// <summary>
    /// 같은 DoorController의 requiredPads에 묶인 패드끼리 다른 색이 배정되도록 보정한다.
    /// Door.C처럼 2색 AND 패드에서 동일 색이 배정되는 상황을 방지한다.
    /// </summary>
    void FixSiblingPadColors(List<PressurePad> coloredPads, PlayerColorType[] distributed)
    {
        // door → 해당 문에 묶인 패드의 distributed 인덱스 목록
        var doorIndices = new Dictionary<DoorController, List<int>>();
        for (int i = 0; i < coloredPads.Count; i++)
        {
            if (!_padToDoors.TryGetValue(coloredPads[i], out List<DoorController> doors)) continue;
            foreach (DoorController door in doors)
            {
                if (!doorIndices.ContainsKey(door))
                    doorIndices[door] = new List<int>();
                if (!doorIndices[door].Contains(i))
                    doorIndices[door].Add(i);
            }
        }

        foreach (List<int> siblings in doorIndices.Values)
        {
            if (siblings.Count < 2) continue;

            // 형제 패드끼리 색 충돌 확인
            for (int a = 0; a < siblings.Count - 1; a++)
            {
                for (int b = a + 1; b < siblings.Count; b++)
                {
                    if (distributed[siblings[a]] != distributed[siblings[b]]) continue;

                    // 충돌: 형제가 아닌 패드 중 다른 색을 가진 패드와 스왑
                    for (int c = 0; c < distributed.Length; c++)
                    {
                        if (siblings.Contains(c)) continue;
                        if (distributed[c] == distributed[siblings[b]]) continue;

                        (distributed[siblings[b]], distributed[c]) =
                            (distributed[c], distributed[siblings[b]]);
                        break;
                    }
                }
            }
        }
    }

    // ── 에디터 ──────────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 색 배정 재실행")]
    void Debug_Rerun()
    {
        Collect();
        BuildPadToDoorMap();

        PlayerColorType[] activeColors = PlayerSpawnCoordinator.GetActiveColors();
        if (activeColors.Length == 0)
            activeColors = new[]
            {
                PlayerColorType.Blue, PlayerColorType.Purple,
                PlayerColorType.Green, PlayerColorType.Yellow,
            };

        DistributeColors(activeColors);
        ApplyTopologyScaling(activeColors.Length);
        SyncDoorVisuals();
        SyncPadVisuals();
        SyncPadCountUIs();
    }
#endif
}
