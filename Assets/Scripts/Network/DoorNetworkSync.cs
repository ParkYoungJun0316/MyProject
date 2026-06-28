using Unity.Netcode;
using UnityEngine;

/// <summary>
/// DoorController 열림·닫힘 상태를 네트워크로 동기화.
/// NetworkBehaviour — DoorController와 같은 GameObject에 부착.
/// 같은 GameObject에 NetworkObject도 필요.
///
/// [동작]
/// Host: DoorController.OnOpened/OnClosed 이벤트를 구독 → _isOpen NetworkVariable 갱신
/// Client: _isOpen 변경 감지 → DoorController.Open()/Close() 호출 (시각 연출)
///
/// [배치]
/// T.Stage1 씬의 각 DoorController GameObject에 추가:
///   - NetworkObject 추가
///   - DoorNetworkSync 추가
///   - door 필드에 DoorController 연결 (같은 GameObject이면 자동 검색)
/// </summary>
[RequireComponent(typeof(DoorController))]
public class DoorNetworkSync : NetworkBehaviour
{
    [Tooltip("비워두면 같은 GameObject의 DoorController를 자동 연결.")]
    [SerializeField] private DoorController door;

    private readonly NetworkVariable<bool> _isOpen = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        if (door == null)
            door = GetComponent<DoorController>();
    }

    public override void OnNetworkSpawn()
    {
        _isOpen.OnValueChanged += OnOpenStateChanged;

        if (IsServer)
        {
            // Host: 문 이벤트 구독 → NetworkVariable 갱신
            door.OnOpened.AddListener(OnDoorOpened);
            door.OnClosed.AddListener(OnDoorClosed);
        }
    }

    public override void OnNetworkDespawn()
    {
        _isOpen.OnValueChanged -= OnOpenStateChanged;

        if (IsServer)
        {
            door.OnOpened.RemoveListener(OnDoorOpened);
            door.OnClosed.RemoveListener(OnDoorClosed);
        }
    }

    // ── Host 측 이벤트 ────────────────────────────────────────────

    void OnDoorOpened() => _isOpen.Value = true;
    void OnDoorClosed() => _isOpen.Value = false;

    // ── Client 측 반응 ────────────────────────────────────────────

    void OnOpenStateChanged(bool prev, bool current)
    {
        // Server(Host)는 이미 물리 처리로 문이 움직이므로 제외
        if (IsServer) return;
        if (door == null) return;

        if (current) door.Open();
        else         door.Close();
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 열기")]
    void Debug_Open()  { if (door != null) door.Open(); }

    [ContextMenu("테스트: 닫기")]
    void Debug_Close() { if (door != null) door.Close(); }
#endif
}
