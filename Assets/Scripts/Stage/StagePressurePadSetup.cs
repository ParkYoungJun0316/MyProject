using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 패드/문 퍼즐을 이번 판 인원에 맞게 초기화한다 (T.Stage1 · T.Stage4).
///
/// [이 클래스가 하는 일 — 2026-09-18 축소]
///  1. `DoorPuzzleGroup.ApplyScaling(activeCount)` — latch·requiredCount 인원 스케일
///  2. 문 개폐 네트워크 동기화 배선 (StageNetworkState 공유 슬롯)
///  3. 문의 `ColoredDoorVisual`을 그 문의 패드 색에 맞춰 유지
///
/// [더 이상 하지 않는 일 — 색 매핑]
///  설계슬롯 → 실제색 매핑은 **`SessionColorSlotMap`이 SSOT**이고, 패드는 거기서 **직접 당겨간다**.
///  예전엔 이 클래스가 씬 전역을 `FindObjectsByType`으로 훑어 `SetEffectiveColor()`로 밀어넣었는데,
///   · 호출 순서가 곧 정합성이었고(푸시 순서 버그),
///   · 비활성 색을 활성 색에 재배정하는 규칙이라 **뽑기(시드)가 필요**해 `OnPlayersReady`까지
///     매핑을 미뤄야 했으며,
///   · 1회 적용 후 래치라 실패해도 복구 경로가 없었다.
///  새 규칙("빈 슬롯 → Common")은 결정적 치환이라 활성 색만 알면 **씬 로드 즉시** 확정된다.
///  서로 다른 두 설계슬롯이 같은 고유색으로 겹치는 일도 구조적으로 불가능해져,
///  다색 AND 문(Door.C류)을 위한 충돌 보정(`FixDoorColorCollisions`)도 함께 사라졌다.
///
/// [OnPlayersReady를 여전히 기다리는 이유]
///  색이 아니라 **인원수** 때문이다. 토폴로지 스케일링과 문 슬롯 개수 초기화는 확정된 인원이 필요하다.
///
/// [Inspector 설정]
///  puzzleGroups[]     : DoorPuzzleGroup 수동 등록 (비우면 씬에서 자동 수집)
///  autoCollectPads    : true = PressurePad 자동 수집 (false면 pads[] 직접 입력)
///
/// [배치 위치]
///  씬 루트 빈 GameObject 또는 StageManager 자식 오브젝트 1개.
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

    // 문 네트워크 동기화 (DoorNetworkSync 폐기 — StageNetworkState 공유 슬롯, TStageNetworkBoard.md §3.1).
    // index는 이름순 정렬로 배정 — Host/Client가 항상 동일한 순서로 수집해야 같은 index가
    // 같은 문을 가리킨다.
    DoorController[] _doorsByIndex = System.Array.Empty<DoorController>();
    StageNetworkState _netState;

    int _appliedMapVersion = -1;

    // ── Unity 라이프사이클 ────────────────────────────────────────

    void Start()
    {
        Collect();
        BuildDoorIndexMap();

        // 인원수가 필요한 작업만 OnPlayersReady 이후로 미룬다(색은 이제 기다리지 않는다 — 상단 주석).
        PlayerSpawnCoordinator.OnPlayersReady += OnPlayersReadyHandler;
        if (PlayerSpawnCoordinator.IsReady) OnPlayersReadyHandler();
    }

    void OnPlayersReadyHandler()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= OnPlayersReadyHandler;
        ApplyPartyScaling();
    }

    void OnDestroy()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= OnPlayersReadyHandler;
        if (_netState != null)
            _netState.OnDoorStateChanged -= HandleDoorStateChanged;
    }

    // 패드는 스스로 색을 당겨가지만 문 비주얼은 "그 문의 패드가 무슨 색인가"의 파생값이라
    // 여기서 맞춰 준다. 매핑 버전이 바뀔 때만 실제 작업이 일어난다.
    void Update()
    {
        if (_appliedMapVersion == SessionColorSlotMap.Version) return;
        _appliedMapVersion = SessionColorSlotMap.Version;

        SyncDoorVisuals();
        SyncPadCountUIs();
    }

    void ApplyPartyScaling()
    {
        PlayerColorType[] activeColors = PlayerSpawnCoordinator.GetActiveColors();
        int activeCount = activeColors.Length > 0 ? activeColors.Length : 4;
        if (activeColors.Length == 0)
            Debug.LogWarning("[StagePressurePadSetup] PlayerSpawnCoordinator 색 없음 — 4인 fallback");

        ApplyTopologyScaling(activeCount);
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

    /// <summary>
    /// 각 DoorPuzzleGroup의 ApplyScaling을 호출해 latch / requiredCount 를 스케일한다.
    /// activeCount는 PlayerSpawnCoordinator(NetworkList SSOT) 기준이다.
    /// </summary>
    void ApplyTopologyScaling(int activeCount)
    {
        if (puzzleGroups == null || puzzleGroups.Length == 0) return;

        foreach (DoorPuzzleGroup group in puzzleGroups)
            group?.ApplyScaling(activeCount);
    }

    /// <summary>
    /// 씬의 모든 DoorController를 순회해 ColoredDoorVisual을 그 문의 첫 패드 색으로 맞춘다.
    /// 패드가 `SessionColorSlotMap`에서 당겨온 값을 그대로 쓰므로 색의 출처는 여전히 하나다.
    /// </summary>
    void SyncDoorVisuals()
    {
        DoorController[] allDoors = FindObjectsByType<DoorController>(FindObjectsSortMode.None);
        foreach (DoorController door in allDoors)
        {
            if (door == null) continue;
            ColoredDoorVisual visual = door.GetComponent<ColoredDoorVisual>();
            if (visual == null) continue;

            PlayerColorType color = PlayerColorType.Common;
            if (door.requiredPads != null)
                foreach (PressurePad pad in door.requiredPads)
                {
                    if (pad != null) { color = pad.EffectiveColor; break; }
                }

            visual.Apply(color);
        }
    }

    /// <summary>PressurePadCountUI를 스케일된 requiredCount 기준으로 다시 그린다.</summary>
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
    /// Host/Client가 항상 동일한 순서로 수집해야 같은 index가 같은 문을 가리킨다.
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

    // ── 에디터 ──────────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 색·스케일 재적용")]
    void Debug_Rerun()
    {
        Collect();
        _appliedMapVersion = -1;
        ApplyPartyScaling();
        SyncDoorVisuals();
    }
#endif
}
