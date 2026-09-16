using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 흑/백 토글 문 컨트롤러 (T.Stage5 미로). 씬에 1개.
///
/// [동작]
///  - 흑 열림 ↔ 백 열림 두 상태만 존재. 시작은 흑 열림.
///  - BlackWhiteTogglePad가 밟힐 때마다 Host가 상태를 뒤집는다 (쿨다운 없음).
///  - 문 이동·닫힘 넉백은 각 문의 DoorController가 담당 (openMode = SlideDown 권장).
///
/// [네트워크]
///  상태는 StageNetworkState 전용 슬롯(IsBlackDoorOpen) 하나. Host만 쓰고, 전 머신은 매 프레임
///  이 값과 마지막 적용값을 비교해 다르면 DoorController.Open()/Close()를 재생한다
///  (스폰 초기 동기화 순서와 무관하게 항상 최종값으로 수렴).
///
/// [Inspector]
///  blackDoorsRoot / whiteDoorsRoot : 각 색 문(DoorController)들의 부모. 비활성 자식 포함 수집.
/// </summary>
public class BlackWhiteDoorToggle : MonoBehaviour
{
    [Header("문 묶음")]
    [Tooltip("흑 문(DoorController)들의 부모")]
    [SerializeField] Transform blackDoorsRoot;

    [Tooltip("백 문(DoorController)들의 부모")]
    [SerializeField] Transform whiteDoorsRoot;

    DoorController[] _blackDoors;
    DoorController[] _whiteDoors;

    bool _localBlackOpen = true;   // StageNetworkState가 없는 씬(테스트)용
    bool _hasApplied;
    bool _appliedBlackOpen;

    void Awake()
    {
        _blackDoors = blackDoorsRoot != null ? blackDoorsRoot.GetComponentsInChildren<DoorController>(true) : new DoorController[0];
        _whiteDoors = whiteDoorsRoot != null ? whiteDoorsRoot.GetComponentsInChildren<DoorController>(true) : new DoorController[0];
    }

    void Update()
    {
        bool want = CurrentBlackOpen();
        if (_hasApplied && want == _appliedBlackOpen) return;
        Apply(want);
    }

    bool CurrentBlackOpen()
    {
        var net = StageNetworkState.Instance;
        return net != null ? net.IsBlackDoorOpen : _localBlackOpen;
    }

    /// <summary>Host 전용: 패드가 밟혔을 때 호출.</summary>
    public void RequestToggle()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

        bool next = !CurrentBlackOpen();
        var net = StageNetworkState.Instance;
        if (net != null) net.SetBlackDoorOpen(next);
        else _localBlackOpen = next;
    }

    void Apply(bool blackOpen)
    {
        _hasApplied = true;
        _appliedBlackOpen = blackOpen;
        SetDoors(_blackDoors, blackOpen);
        SetDoors(_whiteDoors, !blackOpen);
    }

    static void SetDoors(DoorController[] doors, bool open)
    {
        foreach (DoorController d in doors)
        {
            if (d == null || !d.isActiveAndEnabled) continue;
            if (open) d.Open();
            else d.Close();
        }
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: 토글")]
    void Debug_Toggle() => RequestToggle();
#endif
}
