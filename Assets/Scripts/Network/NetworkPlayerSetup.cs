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
    // 색 인덱스: 서버 쓰기 / 전원 읽기
    private readonly NetworkVariable<int> _colorIndex = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // HP: 서버 쓰기 / 전원 읽기
    private readonly NetworkVariable<int> _hp = new(
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
        _hp.OnValueChanged         += OnHpChanged;

        // 색 초기 적용
        ApplyColor(_colorIndex.Value);

        // Host: 플레이어 초기 HP를 NetworkVariable에 설정
        if (IsServer && _player != null)
            _hp.Value = _player.maxHeart;

        // Owner / 비오너 분기
        if (IsOwner)
            SetupOwner();
        else
            SetupNonOwner();
    }

    public override void OnNetworkDespawn()
    {
        _colorIndex.OnValueChanged -= OnColorIndexChanged;
        _hp.OnValueChanged         -= OnHpChanged;
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

    // ── HP 동기화 ─────────────────────────────────────────────────

    /// <summary>
    /// Host에서 직접 호출해 HP를 차감.
    /// ArrowTrap·DropTrap 등 함정이 Host에서 플레이어 충돌을 감지했을 때 사용.
    /// </summary>
    public void ApplyDamageFromServer(int amount, bool knockback = false)
    {
        if (!IsServer) return;
        if (_player == null || _player.IsDead) return;
        if (_player.IsDamageInvulnerable) return;

        int newHp = Mathf.Max(0, _hp.Value - amount);
        _hp.Value = newHp;

        NotifyHitClientRpc(knockback);

        if (newHp <= 0)
            ForceKillClientRpc();
    }

    /// <summary>오너 클라이언트에 피격 연출(애니·무적)을 요청.</summary>
    [ClientRpc]
    void NotifyHitClientRpc(bool knockback)
    {
        if (!IsOwner) return;
        // NetworkVariable(_hp) 변경과 ClientRpc가 같은 틱에 전송되더라도
        // 처리 순서가 보장되지 않으므로 여기서 heart를 명시적으로 맞춘다.
        if (_player != null) _player.heart = _hp.Value;
        _player?.TakeDamageVisualOnly(knockback);
    }

    /// <summary>오너 클라이언트에 사망을 확정.</summary>
    [ClientRpc]
    void ForceKillClientRpc()
    {
        if (!IsOwner) return;
        _player?.ForceKill();
    }

    void OnHpChanged(int prev, int next)
    {
        if (_player == null) return;
        _player.heart = next;

        // 비오너: 다른 플레이어의 HP UI도 갱신 (오너는 NotifyHitClientRpc에서 이미 처리)
        if (!IsOwner)
            _player.GetComponent<PlayerEvents>()?.RaiseDamaged(false);
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: Owner 여부 출력")]
    void Debug_Status()
    {
        Debug.Log($"[NetworkPlayerSetup] IsOwner={IsOwner} IsServer={IsServer} " +
                  $"colorIndex={_colorIndex.Value} HP={_hp.Value} colorType={_player?.playerColorType}");
    }

    [ContextMenu("테스트: 데미지 1 적용 (Host 전용)")]
    void Debug_Damage1() => ApplyDamageFromServer(1);
#endif
}
