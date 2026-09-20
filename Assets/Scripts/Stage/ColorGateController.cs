using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 색 게이트 컨트롤러 (T.Stage5). **`T5_Maze` 루트에 1개** — 맵이 1장이라 씬에 하나뿐이다.
/// `TStage5RunnerRedesign.md` §1.4 / §1.5 / §1.6이 SSOT.
///
/// [동작 — 배타 색 게이트]
///  - 한 번에 **한 색만** 열린다. 패드를 밟으면 그 색 문만 Open, 나머지는 전부 Close.
///  - 시작 상태는 **전부 닫힘**(NV 초기값 -1). 흑·백도 같은 규칙의 한 색일 뿐이다.
///  - 격자의 **모든 변이 문**이라 문은 180개고, 전환 1회에 실제로 움직이는 것은 60개다
///    (열려 있던 30개가 닫히고 새 색 30개가 열린다. 나머지는 이미 닫혀 있어 Close()가 무동작).
///  - 문 이동·닫힘 넉백은 각 문의 DoorController가 담당(openMode = SlideUp, latchOnOpen = false).
///
/// [설계슬롯 → 실제 색 — SessionColorSlotMap이 SSOT]
///  문은 Blue/Purple/Green/Yellow 4개의 **설계슬롯** + 흑·백으로 제작돼 있고, 거기에 두 겹이 얹힌다.
///   ① **시드 순열**(`SetDesignPermutation`) — 매판 다른 사람이 그 문의 열쇠를 쥔다(§1.12).
///   ② **러너 제외**(`SetRunnerExclusion`) — 러너 색은 **벽**이 된다(아래).
///  Black/White는 두 겹 모두의 대상이 아니라 그대로 고정.
///
/// [⚠️ T5에는 Common이 없다 — 열 수 없는 색은 전부 **벽** (2026-09-20 §1.4)]
///  벽이 되는 색은 둘이다:
///   · **이번 판에 없는 색** — 들고 있는 사람이 아예 없다.
///   · **러너 색** — 러너는 1층이라 2층 패드를 밟을 수 없다.
///  둘 다 `SessionColorSlotMap.IsSlotAbsent()`가 판정하고, 그 묶음은 **영영 안 열리며 패드도 숨긴다.**
///
///  구 규칙(없는 색·러너 색 → Common = 누구나 엶)을 문 180개에 그대로 쓰면 **2인에서 문의 절반이
///  패드 하나로 열려** 러너가 거의 걸어서 골인한다(실측 최소 전환 5회). 벽 규칙에서는 인원이 줄수록
///  격자가 **촘촘해진다** — seed 7036 실측 최소 전환: 4인 12~13 · 3인 13~14 · 2인 14~15 · 솔로 27.
///  연결성은 §1.6의 **흑 ∪ 백 조건**이 전 인원에서 보장하므로 어떤 인원에서도 막히지 않는다.
///
/// [솔로 — 흑·백 동시 열림 고정 (§1.6)]
///  고유색·Common 문은 **전부 닫힘 고정**(열어줄 안내자가 없다), **흑·백만 동시에 열어** 고정 미로로
///  만들고 2층 패드는 전부 숨긴다. 구 규칙("솔로는 문 전부 Open")은 폐기됐다 — 모든 변이 문인
///  구조에서 전부 열면 **벽이 하나도 없는 120×120 빈 들판**이 되어 대각선으로 달리면 끝난다.
///
/// [네트워크]
///  상태는 StageNetworkState 전용 슬롯(OpenGateColor) 하나. Host만 쓰고, 전 머신은 매 프레임
///  이 값과 마지막 적용값을 비교해 다르면 DoorController.Open()/Close()를 재생한다
///  (스폰 초기 동기화 순서와 무관하게 항상 최종값으로 수렴).
///
/// [Inspector]
///  doorGroups : 설계슬롯 6색 각각의 문 부모(Doors/Blue … Doors/White). 비활성 자식 포함 수집.
///  pads       : 비우면 이 하위의 ColorGatePad를 자동 수집(9구간 × 6 = 54개).
/// </summary>
public class ColorGateController : MonoBehaviour
{
    // 고유색 설계슬롯 목록은 SessionColorSlotMap.DesignSlots가 SSOT — 여기 복사본을 두지 않는다.

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

        /// <summary>이번 판에 아무도 열 수 없어 **영영 안 열리는** 묶음인가(§1.4 벽 규칙 — 없는 색·러너 색).</summary>
        [System.NonSerialized] public bool isWall;
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
    int  _appliedMapVersion = -1;

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
    void OnEnable()
    {
        _hasApplied        = false;
        _appliedMapVersion = -1;
    }

    void Update()
    {
        PullMappingIfChanged();

        bool solo      = IsSolo();
        int  wantColor = CurrentOpenColor();

        if (_hasApplied && wantColor == _appliedOpenColor && solo == _appliedSolo) return;
        Apply(wantColor, solo);
    }

    /// <summary>
    /// `SessionColorSlotMap`에서 각 묶음의 실제 색을 당겨온다(push 아님 — 매핑이 언제 확정되든 수렴).
    /// 매핑이 바뀌면 "어느 묶음이 열려야 하는가"의 답도 바뀌므로 개폐 적용도 강제로 다시 태운다.
    /// </summary>
    void PullMappingIfChanged()
    {
        if (_appliedMapVersion == SessionColorSlotMap.Version) return;
        _appliedMapVersion = SessionColorSlotMap.Version;

        foreach (DoorGroup g in doorGroups)
        {
            if (g == null) continue;

            g.effectiveColor = SessionColorSlotMap.Resolve(g.designColor);
            g.isWall         = SessionColorSlotMap.IsSlotAbsent(g.designColor);

            if (g.visuals == null) continue;
            foreach (ColoredDoorVisual v in g.visuals)
                if (v != null) v.Apply(g.effectiveColor);
        }

        _hasApplied = false;
    }

    // ── 외부 API ────────────────────────────────────────────────

    /// <summary>designColor 슬롯의 런타임 실제 색. 패드가 조회한다.</summary>
    public PlayerColorType GetEffectiveColor(PlayerColorType designColor) =>
        SessionColorSlotMap.Resolve(designColor);

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

            bool open;
            if (g.isWall)   open = false;                    // 없는 색·러너 색 = 벽. 무엇을 밟아도 안 열린다 (§1.4)
            else if (solo)  open = IsBlackOrWhite(g.effectiveColor); // 솔로는 흑·백만 동시 열림 (§1.6)
            else            open = openColor >= 0 && (int)g.effectiveColor == openColor;

            SetDoors(g.doors, open);
        }

        // 솔로는 안내자가 없어 색 게이트가 성립하지 않으므로 전부 숨긴다(§1.6).
        // 다인승에서도 **벽이 된 색(없는 색·러너 색)의 패드는 숨긴다** — 열 대상이 없다(§1.5).
        // 구간당 보이는 패드: 4인 5개(고유 3 + 흑 + 백) · 3인 4개 · 2인 3개 · 솔로 0개.
        foreach (ColorGatePad pad in pads)
        {
            if (pad == null) continue;

            bool visible = !solo && !SessionColorSlotMap.IsSlotAbsent(pad.DesignColor);
            if (pad.gameObject.activeSelf == visible) continue;
            pad.gameObject.SetActive(visible);
        }
    }

    static bool IsBlackOrWhite(PlayerColorType c) =>
        c == PlayerColorType.Black || c == PlayerColorType.White;

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
