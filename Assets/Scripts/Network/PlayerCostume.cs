using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 플레이어 머리 소품(모자·머리장식·선글라스) 표시 + 변경 요청 — CostumeDesign.md.
///
/// [소품 목록 SSOT — 이 컴포넌트의 배열(플레이어 프리팹)]
/// 어떤 소품이 있는지·순서(= 네트워크 번호)·아이콘은 headItems/glassesItems 한 곳에만 있다.
/// 꾸미기 창(<see cref="CostumePanelUI"/>)은 이 배열을 읽어 칸을 만들고, Host 범위 검사도 이 배열 길이로 한다.
///
/// [칸 2개]
/// 머리 칸(headItems: 모자 + 머리장식 — 한 번에 하나)과 선글라스 칸(glassesItems).
/// 값 0 = 없음, n = 배열의 n-1번째. 모자와 머리장식이 같은 칸이라 동시 착용이 구조상 불가능하다.
///
/// [고른 값이 사는 곳]
/// 플레이어는 씬마다 새로 스폰되므로(NetworkDesign.md §11 destroyWithScene:true) 고른 값은 이
/// 컴포넌트가 아니라 DDOL인 <see cref="PlayerSpawnCoordinator"/>의 clientId → 소품 목록에 있다.
/// 여기서는 그 목록을 읽어 자식 오브젝트를 켜고 끄기만 한다. 세션이 끝나면 Coordinator가 Despawn되어
/// 목록도 사라진다 — 저장 없음(접속하면 전원 기본 상태).
///
/// [배치]
/// Network Player Prefab 루트에 부착. 소품 오브젝트는 Head 본 자식으로 두고 배열에 연결.
/// 색 변환 제외는 PlayerVisualController.fixedRenderers에 소품 렌더러를 넣어 처리한다(에디터 설정).
/// </summary>
public class PlayerCostume : NetworkBehaviour
{
    [Serializable]
    public struct Item
    {
        [Tooltip("Head 본 자식 소품 오브젝트(기본 비활성).")]
        public GameObject target;
        [Tooltip("꾸미기 창 칸에 보이는 아이콘.")]
        public Sprite icon;
    }

    /// <summary>이 머신이 조종하는 플레이어의 소품 컴포넌트. 스폰 전·씬 전환 중엔 null.</summary>
    public static PlayerCostume Local { get; private set; }

    [Tooltip("머리 칸 — 모자 + 머리장식. 순서 = 번호(1부터). 새 소품은 끝에만 추가할 것.")]
    [SerializeField] Item[] headItems;

    [Tooltip("선글라스 칸. 순서 = 번호(1부터). 새 소품은 끝에만 추가할 것.")]
    [SerializeField] Item[] glassesItems;

    // 늦게 도착한 옛 요청의 중복본이 최신 선택을 되돌리지 않게 한다(RpcSubmitDedup 주석 참고).
    readonly RpcSubmitDedup _setDedup = new();

    public int HeadCount => headItems != null ? headItems.Length : 0;
    public int GlassesCount => glassesItems != null ? glassesItems.Length : 0;

    /// <summary>번호(1부터)의 아이콘. 범위 밖이면 null.</summary>
    public Sprite GetHeadIcon(int number) => GetIcon(headItems, number);

    /// <summary>번호(1부터)의 아이콘. 범위 밖이면 null.</summary>
    public Sprite GetGlassesIcon(int number) => GetIcon(glassesItems, number);

    static Sprite GetIcon(Item[] items, int number) =>
        items != null && number >= 1 && number <= items.Length ? items[number - 1].icon : null;

    void Awake()
    {
        Apply(0, 0);
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner) Local = this;

        PlayerSpawnCoordinator.OnCostumeChanged += Refresh;
        Refresh();
    }

    public override void OnNetworkDespawn()
    {
        Unhook();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        Unhook(); // Despawn 없이 파괴되는 경로 대비 — static event 구독이 남지 않게.
    }

    void Unhook()
    {
        PlayerSpawnCoordinator.OnCostumeChanged -= Refresh;
        if (Local == this) Local = null;
    }

    /// <summary>현재 확정된 값(Host 목록 기준). 목록에 없으면 둘 다 0(없음).</summary>
    public void GetCurrent(out int head, out int glasses)
    {
        PlayerSpawnCoordinator.TryGetCostume(OwnerClientId, out head, out glasses);
    }

    /// <summary>Owner: 소품 변경 요청. Host가 범위를 검증해 확정하고 전원에게 동기화한다.</summary>
    public void RequestSet(int head, int glasses)
    {
        if (!IsOwner || !IsSpawned) return;
        RequestSetServerRpc((byte)Mathf.Clamp(head, 0, byte.MaxValue), (byte)Mathf.Clamp(glasses, 0, byte.MaxValue),
                            _setDedup.NextSeq());
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    void RequestSetServerRpc(byte head, byte glasses, uint submitSeq, RpcParams rpcParams = default)
    {
        if (_setDedup.IsDuplicate(rpcParams.Receive.SenderClientId, submitSeq)) return;
        if (head > HeadCount || glasses > GlassesCount) return;
        PlayerSpawnCoordinator.Instance?.SetCostume(OwnerClientId, head, glasses);
    }

    void Refresh()
    {
        if (!IsSpawned) return;
        int head, glasses;
        GetCurrent(out head, out glasses);
        Apply(head, glasses);
    }

    void Apply(int head, int glasses)
    {
        SetActiveOnly(headItems, head - 1);
        SetActiveOnly(glassesItems, glasses - 1);
    }

    static void SetActiveOnly(Item[] items, int activeIndex)
    {
        if (items == null) return;
        for (int i = 0; i < items.Length; i++)
        {
            GameObject target = items[i].target;
            if (target == null) continue;
            bool on = i == activeIndex;
            if (target.activeSelf != on) target.SetActive(on);
        }
    }
}
