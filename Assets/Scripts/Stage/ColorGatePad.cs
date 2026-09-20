using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 색 패드 (T.Stage5 2층). 2층을 **3×3 = 9구간**으로 나누고 **구간마다 6색을 반경 4m 링으로** 둔다
/// (9 × 6 = 54개). `TStage5RunnerRedesign.md` §1.5가 SSOT.
///
/// [왜 구간마다 전 색인가]
///  구 배치는 "색당 1개를 2층 곳곳에 흩어"였다. 전환이 12회 이상인 새 구조에서는 안내자가 패드를
///  찾아 뛰어다니는 동안 러너가 문 앞에 서 있게 된다. 구간마다 전 색을 두면 안내자는 **러너를 따라
///  위에서 이동하며 그 자리에서 누른다**(구간 중심 간 40m ≈ 4초 = 전환 1회 예산).
///  협동이 죽지 않는 이유는 **고유색 패드는 그 색 플레이어만 밟을 수 있기 때문**이다 —
///  한 명이 다 처리할 수 없고 역할이 위치가 아니라 색으로 갈린다.
///
/// [동작]
///  밟으면 ColorGateController가 **그 색 문만 Open, 나머지 전부 Close**로 확정한다.
///  올라선 채로는 재발동 없음 — 어차피 배타 게이트라 같은 색을 다시 밟아도 결과가 같다.
///  **`OnTriggerStay` 재발동을 넣지 말 것**: 구간마다 패드가 6개 모여 있어, 두 안내자가 각자
///  패드에 서 있으면 프레임마다 색이 뒤집혀 문이 떨리고 NV가 초당 수십 회 나간다.
///
/// [색 권한 — §1.4 / §1.5]
///  - 고유색 패드(Blue/Purple/Green/Yellow 설계슬롯) : **런타임 실제 색과 같은 고유색 플레이어만**.
///    판정 방식은 PressurePad.IsAllowed와 동일(isUniqueColor + playerColorType 일치).
///  - 흑·백 패드 : **누구나**. 흑백은 플레이어가 수시로 갈아타는 상태라 소유권을 두지 않는다.
///  - **벽이 된 색의 패드는 아예 숨긴다**(ColorGateController가 끈다) — 이번 판에 없는 색과
///    러너 색은 열 대상이 없다. 그래서 **T5에는 Common 패드가 없다**(2026-09-20).
///    구간당 보이는 패드: 4인 5개 · 3인 4개 · 2인 3개 · 솔로 0개.
///
/// [설계슬롯 → 실제 색 — SessionColorSlotMap이 SSOT]
///  designColor는 제작 시점의 설계 색이고, 실제 판정은 `SessionColorSlotMap.Resolve()`가 정하는
///  EffectiveColor로 한다. 시드 고유색 순열이 한 겹 얹히므로 **같은 패드가 판마다 다른 색이 된다**(§1.12).
///  Black/White는 치환 대상이 아니라 designColor 그대로다.
///  값은 **당겨온다**(pull) — 매핑이 언제 확정되든 다음 프레임에 수렴하므로 푸시 순서 버그가 없다.
///
/// [판정] Host 전용 (ContactKnockback·BlackWhiteTogglePad와 동일 — Host가 원격 플레이어 CNT
///        위치로 트리거를 받는다). 루트 캡슐(Player 컴포넌트가 붙은 콜라이더)만 인정.
/// [연출] 밟는 소리는 각 머신 로컬 3D 재생 (PressurePad와 동일).
///
/// [씬 설정] Collider(Is Trigger) + 이 스크립트. controller를 비우면 부모에서 자동 탐색.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ColorGatePad : MonoBehaviour
{
    [Header("색")]
    [Tooltip("제작 시점의 설계 색. 고유색 4종은 판마다 시드 순열로 재매핑되고, Black/White는 고정.")]
    [SerializeField] PlayerColorType designColor = PlayerColorType.Black;

    [Header("게이트")]
    [Tooltip("이 패드가 조작할 색 게이트 컨트롤러. 비우면 부모 계층에서 자동 탐색.")]
    [SerializeField] ColorGateController controller;

    [Header("사운드 (3D)")]
    [SerializeField] SFXId pressSfxId = SFXId.Pad_Press;
    [SerializeField] float pressMinDistance = 5f;
    [SerializeField] float pressMaxDistance = 20f;
    [SerializeField] AudioRolloffMode pressRolloffMode = AudioRolloffMode.Logarithmic;

    /// <summary>제작 시점의 설계 색. 벽 판정(ColorGateController)이 이 값으로 묻는다.</summary>
    public PlayerColorType DesignColor => designColor;

    /// <summary>런타임 실제 색. 재매핑 전에는 designColor와 같다.</summary>
    public PlayerColorType EffectiveColor => _effectiveColor;

    PlayerColorType _effectiveColor;
    int             _appliedMapVersion = -1;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;

        if (controller == null)
            controller = GetComponentInParent<ColorGateController>(true);

        RefreshEffectiveColor();
    }

    void OnEnable() => _appliedMapVersion = -1; // 맵이 라운드마다 켜졌다 꺼지므로 다시 당겨온다

    void Update()
    {
        if (_appliedMapVersion == SessionColorSlotMap.Version) return;
        RefreshEffectiveColor();
    }

    // ── 외부 API ────────────────────────────────────────────────

    /// <summary>ColorGateController가 Awake에서 호출 — 자동 탐색이 실패한 경우의 보정.</summary>
    public void Bind(ColorGateController owner)
    {
        if (owner != null) controller = owner;
    }

    /// <summary>
    /// `SessionColorSlotMap`에서 실제 색을 다시 당겨오고 머티리얼을 맞춘다.
    /// 매핑 버전이 바뀔 때마다 자동으로 호출되므로 밖에서 부를 일은 거의 없다.
    /// </summary>
    public void RefreshEffectiveColor()
    {
        _appliedMapVersion = SessionColorSlotMap.Version;
        _effectiveColor    = SessionColorSlotMap.Resolve(designColor);

        ColoredPadVisual visual = GetComponentInChildren<ColoredPadVisual>(true);
        if (visual != null) visual.Apply(_effectiveColor);
    }

    // ── 판정 ────────────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // 부활 직후 1초 제외 — 생존자 위에 겹쳐 나타난 것만으로 문이 열리지 않게 한다
        // (ReviveSystemDesign.md §3.1).
        Player p = other.GetComponent<Player>();
        if (p == null || !p.CountsForOccupancy) return;
        if (!IsAllowed(p)) return;

        if (pressSfxId != SFXId.None)
            SFXManager.Instance?.PlayAtPoint(pressSfxId, transform.position, pressMinDistance, pressMaxDistance, pressRolloffMode);

        // Host 전권 확정 — Client는 NV 수신으로만 문 연출을 재생한다.
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

        if (controller != null) controller.RequestOpen(_effectiveColor);
    }

    /// <summary>
    /// 흑·백 패드는 누구나, 고유색 패드는 그 색 고유색 플레이어만 (§1.5).
    /// PressurePad.IsAllowed와 같은 판정 — 흑백 상태(Player.isBlack)는 보지 않는다.
    /// Common 가지는 T5에서 쓰이지 않는다(벽이 된 색의 패드는 꺼져 있다) — 공용 판정으로만 남겨 둔다.
    /// </summary>
    bool IsAllowed(Player p)
    {
        if (_effectiveColor == PlayerColorType.Black ||
            _effectiveColor == PlayerColorType.White ||
            _effectiveColor == PlayerColorType.Common) return true;

        return p.isUniqueColor && p.playerColorType == _effectiveColor;
    }
}
