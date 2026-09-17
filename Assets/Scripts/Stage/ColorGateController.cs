using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 색 게이트 컨트롤러 (T.Stage5 러너 미로). 맵(Map_XX) 루트마다 1개.
/// `TStage5RunnerRedesign.md` §1.2가 SSOT.
///
/// [동작 — 배타 색 게이트]
///  - 한 번에 **한 색만** 열린다. 패드를 밟으면 그 색 문만 Open, 나머지는 전부 Close.
///  - 라운드 시작 상태는 **전부 닫힘**(NV 초기값 -1). 흑·백도 같은 규칙의 한 색일 뿐이다.
///  - 문 이동·닫힘 넉백은 각 문의 DoorController가 담당(openMode = SlideUp, latchOnOpen = false).
///
/// [설계슬롯 vs 실제 색]
///  맵은 Blue/Purple/Green/Yellow 4개의 **설계슬롯**으로 제작돼 있고, 런타임에 그 슬롯들이
///  2층 안내자의 실제 색으로 재매핑된다(라운드 디렉터가 ApplySlotMapping 호출).
///  Black/White는 재매핑 없이 고정. 2인이면 4슬롯이 전부 같은 안내자 색이 되므로
///  그 색 패드 하나가 고유색 문 8개를 한꺼번에 여는데, 이는 §1.2가 의도한 동작이다
///  (맵 BFS 검증도 "2인 = 실질 3색" 전제로 통과시킨 것 — §2-R).
///
/// [네트워크]
///  상태는 StageNetworkState 전용 슬롯(OpenGateColor) 하나. Host만 쓰고, 전 머신은 매 프레임
///  이 값과 마지막 적용값을 비교해 다르면 DoorController.Open()/Close()를 재생한다
///  (스폰 초기 동기화 순서와 무관하게 항상 최종값으로 수렴).
///
/// [솔로]
///  §1.2에 따라 문을 전부 Open으로 고정하고 2층 패드를 숨긴다 — 안내자가 없으므로
///  색 게이트 자체가 성립하지 않는다.
///
/// [Inspector]
///  doorGroups : 설계슬롯 6색 각각의 문 부모(Doors/Blue … Doors/White). 비활성 자식 포함 수집.
///  pads       : 비우면 이 맵 하위의 ColorGatePad를 자동 수집.
/// </summary>
public class ColorGateController : MonoBehaviour
{
    /// <summary>재매핑 대상인 고유색 설계슬롯. 이 순서가 ApplySlotMapping 인자의 순서다.</summary>
    public static readonly PlayerColorType[] DesignSlots =
    {
        PlayerColorType.Blue,
        PlayerColorType.Purple,
        PlayerColorType.Green,
        PlayerColorType.Yellow,
    };

    [System.Serializable]
    public class DoorGroup
    {
        [Tooltip("이 묶음의 설계 색. Blue/Purple/Green/Yellow는 런타임 재매핑 대상, Black/White는 고정.")]
        public PlayerColorType designColor = PlayerColorType.Common;

        [Tooltip("이 색 문(DoorController)들의 부모. 비활성 자식 포함 수집.")]
        public Transform root;

        [System.NonSerialized] public DoorController[]   doors;
        [System.NonSerialized] public ColoredDoorVisual[] visuals;

        /// <summary>런타임 실제 적용 색. 재매핑 전에는 designColor와 같다.</summary>
        [System.NonSerialized] public PlayerColorType effectiveColor;
    }

    [Header("색별 문 묶음")]
    [SerializeField] DoorGroup[] doorGroups = new DoorGroup[]
    {
        new DoorGroup { designColor = PlayerColorType.Blue   },
        new DoorGroup { designColor = PlayerColorType.Purple },
        new DoorGroup { designColor = PlayerColorType.Green  },
        new DoorGroup { designColor = PlayerColorType.Yellow },
        new DoorGroup { designColor = PlayerColorType.Black  },
        new DoorGroup { designColor = PlayerColorType.White  },
    };

    [Header("패드")]
    [Tooltip("이 맵의 2층 색 패드. 비우면 Awake에서 하위 ColorGatePad를 자동 수집한다.")]
    [SerializeField] ColorGatePad[] pads;

    // StageNetworkState가 없는 씬(에디터 단독 테스트)용 로컬 상태
    int _localOpenColor = -1;

    bool _hasApplied;
    int  _appliedOpenColor;
    bool _appliedSolo;

    void Awake()
    {
        foreach (DoorGroup g in doorGroups)
        {
            if (g == null) continue;
            g.effectiveColor = g.designColor;
            g.doors   = g.root != null ? g.root.GetComponentsInChildren<DoorController>(true)   : new DoorController[0];
            g.visuals = g.root != null ? g.root.GetComponentsInChildren<ColoredDoorVisual>(true) : new ColoredDoorVisual[0];
        }

        if (pads == null || pads.Length == 0)
            pads = GetComponentsInChildren<ColorGatePad>(true);

        foreach (ColorGatePad pad in pads)
            if (pad != null) pad.Bind(this);
    }

    // 맵이 라운드마다 켜졌다 꺼지므로, 다시 켜질 때 마지막 적용값을 버리고 현재 NV로 재수렴시킨다.
    void OnEnable() => _hasApplied = false;

    void Update()
    {
        bool solo      = IsSolo();
        int  wantColor = CurrentOpenColor();

        if (_hasApplied && wantColor == _appliedOpenColor && solo == _appliedSolo) return;
        Apply(wantColor, solo);
    }

    // ── 외부 API ────────────────────────────────────────────────

    /// <summary>
    /// 라운드 디렉터(C4)가 라운드 시작 시 호출. 고유색 설계슬롯 4개(DesignSlots 순서)를
    /// 2층 안내자 실제 색으로 재매핑한다. Black/White 묶음은 건드리지 않는다.
    /// slotColors는 GameSessionColorDistribution.Distribute(안내자색목록, 4, rng) 결과를 그대로 넘긴다
    /// (러너 색·빈 슬롯이 안내자 색으로 시드 랜덤하게 채워지는 것도 그 유틸이 처리).
    /// </summary>
    public void ApplySlotMapping(IReadOnlyList<PlayerColorType> slotColors)
    {
        if (slotColors == null || slotColors.Count == 0)
        {
            Debug.LogWarning($"[ColorGateController] {name}: slotColors가 비어 재매핑을 건너뜁니다.");
            return;
        }

        for (int i = 0; i < DesignSlots.Length; i++)
        {
            PlayerColorType slot   = DesignSlots[i];
            PlayerColorType actual = slotColors[i % slotColors.Count];

            DoorGroup g = FindGroup(slot);
            if (g == null) continue;

            g.effectiveColor = actual;
            foreach (ColoredDoorVisual v in g.visuals)
                if (v != null) v.Apply(actual);
        }

        foreach (ColorGatePad pad in pads)
            if (pad != null) pad.RefreshEffectiveColor();

        // 매핑이 바뀌면 "어느 묶음이 열려야 하는가"의 답도 바뀌므로 다음 프레임에 강제 재적용.
        _hasApplied = false;
    }

    /// <summary>designColor 슬롯의 런타임 실제 색. 매핑 전이면 designColor 그대로. 패드가 조회한다.</summary>
    public PlayerColorType GetEffectiveColor(PlayerColorType designColor)
    {
        DoorGroup g = FindGroup(designColor);
        return g != null ? g.effectiveColor : designColor;
    }

    /// <summary>
    /// Host 전용: 패드가 밟혔을 때 호출. color 문만 열고 나머지는 전부 닫는다.
    /// 인자는 **실제 색**(패드의 effectiveColor) — 설계슬롯이 아니다.
    /// </summary>
    public void RequestOpen(PlayerColorType color)
    {
        if (IsClientOnly()) return;

        var net = StageNetworkState.Instance;
        if (net != null) net.SetOpenGateColor(color);
        else _localOpenColor = (int)color;
    }

    /// <summary>Host 전용: 전부 닫힘으로 되돌린다. 라운드 시작·리셋 시 디렉터가 호출.</summary>
    public void CloseAll()
    {
        if (IsClientOnly()) return;

        var net = StageNetworkState.Instance;
        if (net != null) net.CloseAllGates();
        else _localOpenColor = -1;
    }

    /// <summary>
    /// 라운드 리셋: 전 머신이 문을 닫힌 위치로 **즉시 텔레포트**시킨다(연출·넉백 없음).
    /// 다음 맵으로 텔레포트하는 순간 문이 스르륵 닫히는 걸 보여줄 이유가 없고,
    /// 애니메이션 중이던 문이 어중간한 높이로 남는 것도 막는다.
    /// </summary>
    public void SnapAllClosed()
    {
        foreach (DoorGroup g in doorGroups)
        {
            if (g?.doors == null) continue;
            foreach (DoorController d in g.doors)
                if (d != null) d.ResetDoorState();
        }

        _hasApplied      = true;
        _appliedOpenColor = -1;
        _appliedSolo      = false;
    }

    // ── 내부 ────────────────────────────────────────────────────

    DoorGroup FindGroup(PlayerColorType designColor)
    {
        foreach (DoorGroup g in doorGroups)
            if (g != null && g.designColor == designColor) return g;
        return null;
    }

    int CurrentOpenColor()
    {
        var net = StageNetworkState.Instance;
        return net != null ? net.OpenGateColor : _localOpenColor;
    }

    static bool IsSolo() =>
        GameSession.Instance != null && GameSession.Instance.ActivePlayerCount <= 1;

    static bool IsClientOnly()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && !nm.IsServer;
    }

    void Apply(int openColor, bool solo)
    {
        _hasApplied       = true;
        _appliedOpenColor = openColor;
        _appliedSolo      = solo;

        foreach (DoorGroup g in doorGroups)
        {
            if (g?.doors == null) continue;

            // 솔로는 전부 열림 고정. 그 외에는 실제 색이 열린 색과 같은 묶음만 열린다
            // (여러 슬롯이 같은 색으로 매핑되면 그 묶음들이 함께 열리는 게 정상 — 상단 주석 참고).
            bool open = solo || (openColor >= 0 && (int)g.effectiveColor == openColor);
            SetDoors(g.doors, open);
        }

        // 솔로는 안내자가 없어 색 게이트가 성립하지 않으므로 2층 패드를 숨긴다 (§1.2).
        bool padsVisible = !solo;
        foreach (ColorGatePad pad in pads)
        {
            if (pad == null || pad.gameObject.activeSelf == padsVisible) continue;
            pad.gameObject.SetActive(padsVisible);
        }
    }

    static void SetDoors(DoorController[] doors, bool open)
    {
        foreach (DoorController d in doors)
        {
            if (d == null || !d.isActiveAndEnabled) continue;
            if (open) d.Open();
            else      d.Close();
        }
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: 전부 닫기")]
    void Debug_CloseAll() => CloseAll();
#endif
}
