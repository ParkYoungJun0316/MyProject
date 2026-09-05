using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// T.Stage1 — 패드/문 퍼즐을 GameSession 인원·색에 맞게 초기화한다.
///
/// [실행 흐름]
///  Start()
///  1. 씬에서 PressurePad, DoorController, DoorPuzzleGroup 수집
///  2. pad→door 역방향 맵 구성
///  3. designColor(Blue/Purple/Green/Yellow 4슬롯) 단위로 활성 색을 매핑
///     - 같은 designColor는 항상 같은 참가색 (1인=전부 자기색, 2인=2+2, 3인=2+1+1, 4인=그대로)
///     - 같은 문에 묶인 서로 다른 designColor가 매핑 후 겹치면 보정
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

    // 문 네트워크 동기화 (DoorNetworkSync 폐기 — StageNetworkState 공유 슬롯, TStageNetworkBoard.md §3.1).
    // index는 이름순 정렬로 배정 — Host/Client가 항상 동일한 순서로 수집해야 같은 index가
    // 같은 문을 가리킨다 (coloredPads.Sort()와 동일 관례).
    DoorController[] _doorsByIndex = System.Array.Empty<DoorController>();
    StageNetworkState _netState;

    // ── Unity 라이프사이클 ────────────────────────────────────────

    void Start()
    {
        Collect();
        BuildPadToDoorMap();
        BuildDoorIndexMap();

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
        if (_netState != null)
            _netState.OnDoorStateChanged -= HandleDoorStateChanged;
    }

    void ApplySeedAndColors()
    {
        const int salt = 0x050AD5E7;
        UnityEngine.Random.InitState(NetworkSessionData.Seed ^ salt);
        Debug.Log($"[StagePressurePadSetup] 시드 적용 — seed={NetworkSessionData.Seed}");

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
        SetupDoorNetworkSync();
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
    /// 4개 디자인 색 슬롯(Blue/Purple/Green/Yellow) 단위로 활성 색을 매핑하고,
    /// 같은 designColor를 가진 패드는 전부 같은 effectiveColor를 받도록 한다.
    ///
    ///  1인 → 나머지 3색 전부 자기 색으로
    ///  2인 → 각자 자기 색 + 비활성 2색을 1개씩 나눠 받음
    ///  3인 → 각자 자기 색 + 비활성 1색을 그중 1명이 받음
    ///  4인 → 그대로(자기 자신)
    ///
    /// 패드 단위 재셔플은 하지 않는다 — 같은 디자인 그룹은 항상 같은 참가색이어야 하기 때문
    /// (패드 개수 단위로 통짜 셔플하던 구 로직이 그룹을 깨뜨리던 버그의 원인이었음).
    /// </summary>
    void DistributeColors(PlayerColorType[] activeColors)
    {
        // colored 패드 목록
        var coloredPads = new List<PressurePad>();
        foreach (PressurePad pad in pads)
            if (pad != null && pad.designColor != PlayerColorType.Common)
                coloredPads.Add(pad);

        if (coloredPads.Count == 0) return;

        Dictionary<PlayerColorType, PlayerColorType> map = BuildDesignColorMap(activeColors);

        // 같은 문에 묶인 서로 다른 designColor끼리 매핑 후에도 겹치면 보정 (Door.C류 다색 AND 패드 보호)
        FixDoorColorCollisions(map, activeColors);

        // 패드에 effectiveColor 적용 — 같은 designColor는 항상 같은 색
        foreach (PressurePad pad in coloredPads)
            pad.SetEffectiveColor(map.TryGetValue(pad.designColor, out PlayerColorType c) ? c : pad.designColor);
    }

    /// <summary>
    /// ColorOrder 4슬롯(Blue/Purple/Green/Yellow) 각각이 최종적으로 어떤 참가색으로 표시될지
    /// 매핑한다. 활성색은 항상 자기 자신에게 매핑되고, 비활성색만 활성색 중 하나로 재배정된다 —
    /// GameSessionColorDistribution.Distribute의 4슬롯 배정 개수 공식(2인→2+2, 3인→2+1+1,
    /// 4인→1+1+1+1)과 동일한 비율을 만족시킨다.
    /// </summary>
    static Dictionary<PlayerColorType, PlayerColorType> BuildDesignColorMap(PlayerColorType[] activeColors)
    {
        // 4슬롯을 활성색으로 균등 분배한 "가방" — 여분 슬롯 배정 셔플은 내부에서
        // UnityEngine.Random 사용(ApplySeedAndColors에서 이미 InitState된 시드라 Host/Client 동일).
        PlayerColorType[] slotBag = GameSessionColorDistribution.Distribute(activeColors, PlayerColorUtil.ColorOrder.Length);

        var targetCount = new Dictionary<PlayerColorType, int>();
        foreach (PlayerColorType c in slotBag)
            targetCount[c] = targetCount.TryGetValue(c, out int n) ? n + 1 : 1;

        var map       = new Dictionary<PlayerColorType, PlayerColorType>();
        var remaining = new Dictionary<PlayerColorType, int>();
        foreach (PlayerColorType active in activeColors)
        {
            map[active] = active; // 활성색은 항상 자기 자신
            int target = targetCount.TryGetValue(active, out int t) ? t : 0;
            remaining[active] = Mathf.Max(0, target - 1); // 자기 슬롯 1개는 이미 확정
        }

        // 비활성 디자인 색을 remaining 여유가 있는 활성색에 순서대로 채운다.
        foreach (PlayerColorType designSlot in PlayerColorUtil.ColorOrder)
        {
            if (map.ContainsKey(designSlot)) continue; // 이미 활성색(자기 자신)

            PlayerColorType assigned = activeColors.Length > 0 ? activeColors[0] : PlayerColorType.Common;
            foreach (PlayerColorType active in activeColors)
            {
                if (remaining.TryGetValue(active, out int left) && left > 0)
                {
                    assigned = active;
                    remaining[active] = left - 1;
                    break;
                }
            }
            map[designSlot] = assigned;
        }

        return map;
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

    /// <summary>
    /// 씬의 모든 DoorController를 이름순으로 정렬해 index를 배정한다.
    /// Host/Client가 항상 동일한 순서로 수집해야 같은 index가 같은 문을 가리킨다
    /// (coloredPads.Sort()와 동일 관례). ApplySeedAndColors()보다 먼저(Start()에서) 실행돼야
    /// SetupDoorNetworkSync()가 이 배열을 바로 쓸 수 있다.
    /// </summary>
    void BuildDoorIndexMap()
    {
        DoorController[] allDoors = FindObjectsByType<DoorController>(FindObjectsSortMode.None);
        System.Array.Sort(allDoors, (a, b) =>
            string.Compare(a.gameObject.name, b.gameObject.name, System.StringComparison.Ordinal));
        _doorsByIndex = allDoors;
    }

    /// <summary>
    /// 문 개폐를 StageNetworkState 공유 슬롯(_doorOpenStates)에 배선한다 (DoorNetworkSync 폐기,
    /// TStageNetworkBoard.md §3.1). Host: 슬롯 초기화 + 문 이벤트 → SetDoorOpen. 전 머신: 상태
    /// 변경 구독 → Client만 DoorController.Open()/Close() 반영(Host는 이미 로컬 물리로 처리함).
    /// </summary>
    void SetupDoorNetworkSync()
    {
        _netState = StageNetworkState.Instance;
        if (_netState == null)
        {
            Debug.LogWarning("[StagePressurePadSetup] StageNetworkState를 찾을 수 없어 문 네트워크 동기화를 건너뜁니다.");
            return;
        }

        if (!IsClientOnly())
        {
            _netState.InitDoorSlots(_doorsByIndex.Length);
            for (int i = 0; i < _doorsByIndex.Length; i++)
            {
                DoorController door = _doorsByIndex[i];
                if (door == null) continue;
                int index = i; // 클로저 캡처
                door.OnOpened.AddListener(() => _netState.SetDoorOpen(index, true));
                door.OnClosed.AddListener(() => _netState.SetDoorOpen(index, false));
            }
        }

        _netState.OnDoorStateChanged += HandleDoorStateChanged;
    }

    void HandleDoorStateChanged(int index, bool isOpen)
    {
        // Host는 이미 로컬 물리로 문을 움직였으므로 중복 적용하지 않음 (구 DoorNetworkSync와 동일 가드).
        if (!IsClientOnly()) return;
        if (index < 0 || index >= _doorsByIndex.Length) return;

        DoorController door = _doorsByIndex[index];
        if (door == null) return;

        if (isOpen) door.Open();
        else        door.Close();
    }

    static bool IsClientOnly()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && !nm.IsServer;
    }

    // ── 내부 유틸 ────────────────────────────────────────────────

    /// <summary>
    /// 같은 DoorController의 requiredPads가 서로 다른 designColor를 가지는데(Door.C류 다색
    /// AND 패드), 매핑 후 같은 effectiveColor로 겹치면 한쪽을 다른 활성색으로 재배정한다.
    /// 같은 designColor를 가진 패드끼리는 항상 같은 색을 유지해야 하므로, 이 보정은
    /// map(designColor 단위)에만 적용한다 — 패드 인스턴스 단위 스왑은 하지 않는다.
    /// </summary>
    void FixDoorColorCollisions(Dictionary<PlayerColorType, PlayerColorType> map, PlayerColorType[] activeColors)
    {
        // door → 그 문의 requiredPads가 가진 서로 다른 designColor 집합 (Common 제외)
        var doorDesignColors = new Dictionary<DoorController, HashSet<PlayerColorType>>();
        foreach (var kv in _padToDoors)
        {
            if (kv.Key == null || kv.Key.designColor == PlayerColorType.Common) continue;
            foreach (DoorController door in kv.Value)
            {
                if (!doorDesignColors.TryGetValue(door, out HashSet<PlayerColorType> set))
                {
                    set = new HashSet<PlayerColorType>();
                    doorDesignColors[door] = set;
                }
                set.Add(kv.Key.designColor);
            }
        }

        foreach (HashSet<PlayerColorType> designSet in doorDesignColors.Values)
        {
            if (designSet.Count < 2) continue;

            var designs = new List<PlayerColorType>(designSet);
            for (int a = 0; a < designs.Count - 1; a++)
            {
                for (int b = a + 1; b < designs.Count; b++)
                {
                    if (map[designs[a]] != map[designs[b]]) continue;

                    // 충돌 — designs[b]를 이 문에서 아직 안 쓰인 활성색으로 재배정
                    foreach (PlayerColorType candidate in activeColors)
                    {
                        bool usedInDoor = false;
                        foreach (PlayerColorType d in designs)
                            if (map[d] == candidate) { usedInDoor = true; break; }
                        if (usedInDoor) continue;

                        map[designs[b]] = candidate;
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
