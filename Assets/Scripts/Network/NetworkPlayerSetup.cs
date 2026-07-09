using Dissonance;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Network Player Prefab에 부착하는 NGO 설정 컴포넌트.
///
/// [역할]
/// - OnNetworkSpawn: Owner / 비오너 분기 설정
///   Owner   : PlayerInput 활성, TopDownCamera 타겟, VoiceBroadcastTrigger 활성, 입력→NV 기록
///   비오너  : PlayerInput 비활성, Rigidbody kinematic, VoiceBroadcastTrigger 비활성
///   Host    : 전 플레이어 물리·HP·함정·낙사 판정 (Update에서 Y 체크)
/// - ColorIndex NetworkVariable로 색 동기화 (Host가 스폰 후 설정)
///
/// [CheerKeywordEngine 관리 안 함]
/// 마이크는 클라이언트 프로세스당 1개뿐이라 Owner/NonOwner 토글이 필요 없다.
/// CheerKeywordEngine은 0.Title의 NetworkManager GameObject에 세션 싱글턴으로 배치되어
/// 스폰과 무관하게 항상 동작한다 (Player 프리팹에는 더 이상 없음).
///
/// [배치]
/// Network Player Prefab에 추가.
/// 같은 GameObject에 Player, ClientNetworkTransform(서버권한), Rigidbody, PlayerInput 필요.
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

    // 쉴드 charge: 서버 쓰기 / 전원 읽기
    private readonly NetworkVariable<int> _shield = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // 색 표시 상태: 오너 쓰기 / 전원 읽기
    private readonly NetworkVariable<bool> _isBlack = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private readonly NetworkVariable<bool> _isUniqueColor = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private Player                  _player;
    private Rigidbody               _rb;
    private PlayerInput             _playerInput;
    private PlayerEvents            _events;
    private VoiceBroadcastTrigger   _voiceBroadcast;

    // 서버 측 피격 무적 타이머 (비오너 플레이어의 isDamage를 서버가 알 수 없으므로 별도 추적)
    private float _damageInvulnEndTime = -1f;

    // Shield duration 만료 코루틴 (서버 전용)
    private Coroutine _shieldExpireCoroutine;

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        _player          = GetComponent<Player>();
        _rb              = GetComponent<Rigidbody>();
        _playerInput     = GetComponent<PlayerInput>();
        _events          = GetComponent<PlayerEvents>();
        _voiceBroadcast  = GetComponent<VoiceBroadcastTrigger>();
    }

    public override void OnNetworkSpawn()
    {
        _colorIndex.OnValueChanged    += OnColorIndexChanged;
        _hp.OnValueChanged            += OnHpChanged;
        _shield.OnValueChanged        += OnShieldChanged;
        _isBlack.OnValueChanged       += OnIsBlackChanged;
        _isUniqueColor.OnValueChanged += OnIsUniqueColorChanged;

        // 색 초기 적용
        ApplyColor(_colorIndex.Value);

        // Host: 플레이어 초기 HP를 NetworkVariable에 설정
        if (IsServer && _player != null)
            _hp.Value = _player.maxHeart;

        // Owner / 비오너 분기
        if (IsOwner)
        {
            SetupOwner();
            // 오너: 로컬 색 상태 변경 → NetworkVariable에 반영
            if (_events == null) _events = GetComponent<PlayerEvents>();
            if (_events != null)
            {
                _events.OnBlackWhiteChanged  += PushIsBlack;
                _events.OnUniqueColorChanged += PushIsUniqueColor;
            }
        }
        else
        {
            SetupNonOwner();
            // 비오너: 현재 NetworkVariable 값을 즉시 적용
            ApplyIsBlack(_isBlack.Value);
            ApplyIsUniqueColor(_isUniqueColor.Value);
        }

        // Phase 2: Owner/비오너 설정 이후 Rigidbody 권한을 서버 기준으로 확정
        ApplyPhysicsAuthority();
    }

    public override void OnNetworkDespawn()
    {
        _colorIndex.OnValueChanged    -= OnColorIndexChanged;
        _hp.OnValueChanged            -= OnHpChanged;
        _shield.OnValueChanged        -= OnShieldChanged;
        _isBlack.OnValueChanged       -= OnIsBlackChanged;
        _isUniqueColor.OnValueChanged -= OnIsUniqueColorChanged;

        if (IsOwner && _events != null)
        {
            _events.OnBlackWhiteChanged  -= PushIsBlack;
            _events.OnUniqueColorChanged -= PushIsUniqueColor;
        }
    }

    // ── Owner 설정 ────────────────────────────────────────────────

    void SetupOwner()
    {
        // 입력 활성
        if (_playerInput != null) _playerInput.enabled = true;
        if (_player != null)      _player.isOwnerControlled = true;

        // 스폰 시 고유색 활성 — PressurePad 인식 조건(isUniqueColor) 충족
        if (_player != null)      _player.isUniqueColor = true;
        _isUniqueColor.Value = true;
        if (_events == null) _events = GetComponent<PlayerEvents>();
        _events?.RaiseUniqueColorChanged(0);

        // PlayerSpawnCoordinator(NetworkList)에서 자신의 색을 조회해 해당 ColoredStartZone
        // 위치로 즉시 이동. OnNetworkSpawn() 내에서 위치를 확정해 (0,0,0) 스폰 문제를 방지.
        MoveToSpawnZone();

        // TopDownCamera → 이 오브젝트를 follow 타겟으로 설정
        var cam = FindAnyObjectByType<TopDownCamera>();
        if (cam != null)
        {
            cam.target = transform;
            if (_player != null)
                _player.followCamera = cam.GetComponent<Camera>();
        }

        // 로컬 마이크 → Global room 송신은 Owner만 (비오너 인스턴스는 Dissonance가 NGO owner를 모름)
        if (_voiceBroadcast != null) _voiceBroadcast.enabled = true;

        Debug.Log($"[NetworkPlayerSetup] Owner 설정 완료 — clientId={OwnerClientId}");
    }

    /// <summary>
    /// PlayerSpawnCoordinator(NetworkList)에서 자신의 색을 읽어 일치하는 ColoredStartZone으로 이동.
    /// 존이 없거나 색 매핑이 없으면, 이미 Netcode가 보정해 둔 현재 위치를 스폰 앵커로 확정한다.
    /// </summary>
    void MoveToSpawnZone()
    {
        if (NetworkManager.Singleton == null) { EnablePhysics(); return; }

        ulong myId = NetworkManager.Singleton.LocalClientId;
        if (!PlayerSpawnCoordinator.TryGetColor(myId, out var myColor))
        {
            Debug.LogWarning($"[NetworkPlayerSetup] 색 정보 없음 — clientId={myId} 현재 위치를 스폰 앵커로 확정");
            UseCurrentPositionAsSpawnAnchor();
            return;
        }

        // 비활성 존 포함 전체 탐색 (ColoredStartZone.Start()에서 비활성화된 것도 위치는 유효)
        var zones = FindObjectsByType<ColoredStartZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var zone in zones)
        {
            if (zone.ColorType != myColor) continue;

            Vector3    pos = zone.SpawnPosition;
            Quaternion rot = zone.SpawnRotation;

            transform.SetPositionAndRotation(pos, rot);
            _player?.ForceSetSpawnPoint(pos, rot);
            EnablePhysics();

            Debug.Log($"[NetworkPlayerSetup] 스폰 위치 결정 — clientId={OwnerClientId} color={myColor} pos={pos}");
            return;
        }

        Debug.LogWarning($"[NetworkPlayerSetup] color={myColor}에 해당하는 ColoredStartZone 없음 — 현재 위치를 스폰 앵커로 확정");
        UseCurrentPositionAsSpawnAnchor();
    }

    /// <summary>
    /// 스폰 존 매칭에 실패했을 때의 폴백.
    /// 이 시점의 transform.position은 Netcode가 이미 서버 스폰 좌표로 보정해 둔 값이라
    /// (Player.Awake()가 캐싱한 값보다 신뢰 가능) 이를 그대로 리스폰 앵커로 확정한다.
    /// 이렇게 해야 나중에 사망 → Respawn() 시 (0,0,0) 등 잘못된 좌표로 밀리지 않는다.
    /// </summary>
    void UseCurrentPositionAsSpawnAnchor()
    {
        _player?.ForceSetSpawnPoint(transform.position, transform.rotation);
        EnablePhysics();
    }

    void EnablePhysics()
    {
        if (_rb == null) return;
        _rb.linearVelocity  = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        // isKinematic는 ApplyPhysicsAuthority()에서 IsServer 기준으로 설정
    }

    // ── 비오너 설정 ───────────────────────────────────────────────

    void SetupNonOwner()
    {
        // 입력 비활성 — 타인의 입력이 이 클라이언트에서 처리되지 않도록
        if (_playerInput != null) _playerInput.enabled = false;
        if (_player != null)      _player.isOwnerControlled = false;

        // 속도 초기화 (isKinematic는 ApplyPhysicsAuthority에서 IsServer 기준으로 처리)
        if (_rb != null)
        {
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        if (_voiceBroadcast != null) _voiceBroadcast.enabled = false;
    }

    /// <summary>
    /// Phase 2 — Host Authority: 서버면 Rigidbody 동적(물리 시뮬), 클라이언트면 kinematic(NetworkTransform 수신).
    /// Owner/비오너 설정 이후 OnNetworkSpawn 마지막에 호출해 최종 권한을 확정한다.
    /// Host는 전 플레이어의 Rigidbody를 직접 시뮬레이션하므로 모두 동적으로 유지.
    /// </summary>
    void ApplyPhysicsAuthority()
    {
        if (_rb == null) return;
        _rb.linearVelocity  = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        // Owner: dynamic (직접 물리 이동)
        // Host 비오너: dynamic (함정 Trigger 판정 유지)
        // Client 비오너: kinematic (NT 수신 전용)
        _rb.isKinematic = (!IsOwner && !IsServer);
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

        PlayerColorUtil.ApplyToPlayer(_player, LobbyNetworkManager.ColorOrder[index]);
        // 색 동기화 완료 이벤트 발행 (TeamStatusUI 등 UI 갱신)
        if (_events == null) _events = GetComponent<PlayerEvents>();
        _events?.RaiseColorTypeChanged(LobbyNetworkManager.ColorOrder[index]);
    }

    // ── 색 상태 동기화 (isBlack / isUniqueColor) ──────────────────

    /// <summary>오너 클라이언트: PlayerEvents 수신 → NetworkVariable 갱신.</summary>
    void PushIsBlack(bool value)
    {
        if (!IsOwner) return;
        _isBlack.Value = value;
    }

    void PushIsUniqueColor(int colorIndex)
    {
        if (!IsOwner) return;
        _isUniqueColor.Value = colorIndex >= 0;
    }

    /// <summary>비오너: NetworkVariable 변경 수신 → 로컬 Player 상태·비주얼 갱신.</summary>
    void OnIsBlackChanged(bool prev, bool next)
    {
        if (IsOwner) return;
        ApplyIsBlack(next);
    }

    void OnIsUniqueColorChanged(bool prev, bool next)
    {
        if (IsOwner) return;
        ApplyIsUniqueColor(next);
    }

    void ApplyIsBlack(bool value)
    {
        if (_player == null) return;
        _player.isBlack = value;
        if (_events == null) _events = GetComponent<PlayerEvents>();
        _events?.RaiseBlackWhiteChanged(value);
    }

    void ApplyIsUniqueColor(bool value)
    {
        if (_player == null) return;
        _player.isUniqueColor = value;
        if (_events == null) _events = GetComponent<PlayerEvents>();
        _events?.RaiseUniqueColorChanged(value ? 0 : -1);
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

        // 비오너 플레이어는 isDamage가 서버에서 갱신되지 않으므로
        // 서버 자체 무적 타이머로 연속 피격을 차단
        if (Time.time < _damageInvulnEndTime) return;

        // Shield 선차감 — charge 소모 후 남은 데미지만 HP에 적용
        if (_shield.Value > 0)
        {
            int absorbed = Mathf.Min(_shield.Value, amount);
            _shield.Value -= absorbed;   // OnShieldChanged → 전 클라이언트 PlayerBuffSystem 동기화
            amount -= absorbed;
        }

        int newHp = Mathf.Max(0, _hp.Value - amount);
        _hp.Value = newHp;

        _damageInvulnEndTime = Time.time + (_player?.InvulnerabilityDuration ?? 0.5f);

        if (newHp > 0)
            NotifyHitClientRpc(knockback);
        else
            ForceKillClientRpc();
    }

    /// <summary>오너 클라이언트에 피격 연출(애니·무적)만 요청. HP/heart 수정은 OnHpChanged에서 담당.</summary>
    [ClientRpc]
    void NotifyHitClientRpc(bool knockback)
    {
        if (!IsOwner) return;
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

        // HP가 실제로 줄었을 때만 피격 이벤트 발행.
        // HP 증가(스폰·리스폰 회복)에서 Hit SFX·연출이 울리는 버그 방지.
        if (next > 0 && next < prev)
            _player.GetComponent<PlayerEvents>()?.RaiseDamaged(false);
    }

    // ── 문 즉사 (Jammed 애니) ──────────────────────────────────────

    /// <summary>
    /// 서버에서 즉사 확정. HP를 0으로 내리고 Owner에게 KillInstantly() 전달.
    /// _hp.Value <= 0 가드로 중복 호출(Host 물리 + Owner 신고 동시) 방지.
    /// </summary>
    public void ApplyInstantKillFromServer()
    {
        if (!IsServer) return;
        if (_player == null || _player.IsDead || _hp.Value <= 0) return;
        _hp.Value = 0;
        ForceInstantKillClientRpc();
    }

    /// <summary>Owner에게 Jammed 애니 즉사 전달.</summary>
    [ClientRpc]
    void ForceInstantKillClientRpc()
    {
        if (!IsOwner) return;
        _player?.KillInstantly();
    }

    // ── 응원 버프 동기화 ──────────────────────────────────────────

    /// <summary>
    /// CheerService (Host)가 호출. 전 클라이언트에 버프 적용을 전달.
    /// Shield: _shield NV 설정 + 서버 측 duration 만료 코루틴 시작.
    /// SpeedUp: ClientRpc만으로 처리 (기존 방식).
    /// </summary>
    public void ApplyCheerBuff(PlayerBuffSystem.BuffType type, float duration)
    {
        if (!IsServer) return;

        var setting = _player?.GetComponent<PlayerBuffSystem>()?.GetSetting(type);
        float value = setting?.value ?? 0f;

        if (type == PlayerBuffSystem.BuffType.Shield)
        {
            _shield.Value = Mathf.Max(1, Mathf.RoundToInt(value));
            // 서버에서 duration 만료 시 _shield 리셋 → 클라이언트 동기화
            if (_shieldExpireCoroutine != null) StopCoroutine(_shieldExpireCoroutine);
            _shieldExpireCoroutine = StartCoroutine(ExpireShieldAfter(duration));
        }

        ApplyCheerBuffClientRpc((int)type, duration, value);
    }

    /// <summary>전 클라이언트에서 이 플레이어의 PlayerBuffSystem에 버프를 적용 (타이머·SFX·UI용).</summary>
    [ClientRpc]
    void ApplyCheerBuffClientRpc(int buffTypeIndex, float duration, float value)
    {
        GetComponent<PlayerBuffSystem>()?.ApplyBuff(
            (PlayerBuffSystem.BuffType)buffTypeIndex, duration, value);
    }

    /// <summary>Shield NV sync — 전 클라이언트의 PlayerBuffSystem charge 갱신.</summary>
    void OnShieldChanged(int prev, int next)
    {
        GetComponent<PlayerBuffSystem>()?.SetShieldCharges(next);
    }

    /// <summary>서버: duration 만료 후 _shield 리셋. 클라이언트 OnShieldChanged → 아이콘 즉시 숨김.</summary>
    System.Collections.IEnumerator ExpireShieldAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        _shield.Value = 0;
        _shieldExpireCoroutine = null;
    }

    // ── 추락 사망 ─────────────────────────────────────────────────

    /// <summary>
    /// 서버에서 낙사 확정. HP를 0으로 내리고 Owner에게 일반 Die()를 전달 (doDie 애니).
    /// doFall 애니는 Owner Update에서 이미 재생됐으므로 여기서는 기본 사망 처리만.
    /// </summary>
    void ApplyFallDeathFromServer()
    {
        if (!IsServer) return;
        if (_player == null || _player.IsDead || _hp.Value <= 0) return;
        _hp.Value = 0;
        ForceKillClientRpc();
    }

    /// <summary>
    /// Server: 낙사 Y 판정.
    /// </summary>
    void Update()
    {
        if (!IsServer) return;
        if (_player == null || _player.IsDead || !_player.enableFallDeath) return;
        if (transform.position.y < _player.fallDeathY)
            ApplyFallDeathFromServer();
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
