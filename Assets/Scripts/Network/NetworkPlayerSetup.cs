using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Network Player Prefab에 부착하는 NGO 설정 컴포넌트.
///
/// [역할]
/// - OnNetworkSpawn: Owner / 비오너 분기 설정
///   Owner   : PlayerInput 활성, Rigidbody 물리 사용, TopDownCamera 타겟 설정
///   비오너  : PlayerInput 비활성, Rigidbody kinematic (NetworkTransform이 위치 제어)
/// - ColorIndex NetworkVariable로 색 동기화 (Host가 스폰 후 설정)
///
/// [배치]
/// Network Player Prefab에 추가.
/// 같은 GameObject에 Player, ClientNetworkTransform, Rigidbody, PlayerInput 필요.
/// </summary>
[RequireComponent(typeof(Player))]
public class NetworkPlayerSetup : NetworkBehaviour
{
    // 서버가 색 인덱스를 쓰고, 모든 클라이언트가 읽음
    private readonly NetworkVariable<int> _colorIndex = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Player      _player;
    private Rigidbody   _rb;
    private PlayerInput _playerInput;

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        _player      = GetComponent<Player>();
        _rb          = GetComponent<Rigidbody>();
        _playerInput = GetComponent<PlayerInput>();
    }

    public override void OnNetworkSpawn()
    {
        _colorIndex.OnValueChanged += OnColorIndexChanged;

        // 색 초기 적용
        ApplyColor(_colorIndex.Value);

        // Owner / 비오너 분기
        if (IsOwner)
            SetupOwner();
        else
            SetupNonOwner();
    }

    public override void OnNetworkDespawn()
    {
        _colorIndex.OnValueChanged -= OnColorIndexChanged;
    }

    // ── Owner 설정 ────────────────────────────────────────────────

    void SetupOwner()
    {
        // 입력 활성
        if (_playerInput != null) _playerInput.enabled = true;
        if (_player != null)      _player.isOwnerControlled = true;

        // Rigidbody 물리 활성 (ClientNetworkTransform이 위치를 브로드캐스트)
        if (_rb != null) _rb.isKinematic = false;

        // TopDownCamera → 이 오브젝트를 follow 타겟으로 설정
        var cam = FindAnyObjectByType<TopDownCamera>();
        if (cam != null)
        {
            cam.target = transform;
            if (_player != null)
                _player.followCamera = cam.GetComponent<Camera>();
        }

        Debug.Log($"[NetworkPlayerSetup] Owner 설정 완료 — clientId={OwnerClientId}");
    }

    // ── 비오너 설정 ───────────────────────────────────────────────

    void SetupNonOwner()
    {
        // 입력 비활성 — 타인의 입력이 이 클라이언트에서 처리되지 않도록
        if (_playerInput != null) _playerInput.enabled = false;
        if (_player != null)      _player.isOwnerControlled = false;

        // Rigidbody kinematic — ClientNetworkTransform이 위치 제어
        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }

    // ── 색 동기화 ─────────────────────────────────────────────────

    /// <summary>Host가 스폰 후 호출해 색 인덱스를 설정. 전원에 동기화됨.</summary>
    public void SetColorIndex(int index)
    {
        if (!IsServer) return;
        _colorIndex.Value = index;
    }

    void OnColorIndexChanged(int prev, int next) => ApplyColor(next);

    void ApplyColor(int index)
    {
        if (_player == null) return;
        if (index < 0 || index >= LobbyNetworkManager.ColorOrder.Length) return;

        _player.playerColorType = LobbyNetworkManager.ColorOrder[index];
        // uniqueColor 시각 색상은 PlayerVisualController 등에서 playerColorType 기반으로 별도 처리
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: Owner 여부 출력")]
    void Debug_Status()
    {
        Debug.Log($"[NetworkPlayerSetup] IsOwner={IsOwner} IsServer={IsServer} " +
                  $"colorIndex={_colorIndex.Value} colorType={_player?.playerColorType}");
    }
#endif
}
