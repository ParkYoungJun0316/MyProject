using Dissonance;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Network Player Prefab에 부착하는 NGO 설정 컴포넌트.
///
/// [역할]
/// - OnNetworkSpawn: Owner / 비오너 분기 설정
///   Owner   : PlayerInput 활성, ThirdPersonCamera 타겟, VoiceBroadcastTrigger 활성, 입력→NV 기록
///   비오너  : PlayerInput 비활성, Rigidbody kinematic, VoiceBroadcastTrigger 비활성
///   Host    : HP·함정·낙사 확정 (Owner ReportFallDeath + Host Y 폴백)
/// - ColorIndex NetworkVariable로 색 동기화 (Host가 스폰 후 설정)
///
/// [배치]
/// Network Player Prefab에 추가.
/// 같은 GameObject에 Player, ClientNetworkTransform(Owner 권한), Rigidbody, PlayerInput 필요.
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

    [SerializeField] LocalPlayerCamera _localCameraPrefab;

    private Player                  _player;
    private Rigidbody               _rb;
    private PlayerInput             _playerInput;
    private PlayerEvents            _events;
    private VoiceBroadcastTrigger   _voiceBroadcast;
    private CheerKeywordEngine      _cheerKeyword;

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
        _cheerKeyword    = GetComponent<CheerKeywordEngine>();
    }

    public override void OnNetworkSpawn()
    {
        // 버그3(Deferred OnSpawn/PurgeTrigger, NetworkDesign.md §9.0.1-b Axis B) 재현 로그.
        // 이 로그가 뜨는 씬/시점(스테이지 전환 직후 · 사망 리로드 직후 · 그 외)과
        // 경고에 찍힌 NetworkObjectId를 대조해 Axis B 발생 지점을 확정하는 용도.
        Debug.Log($"[NetworkPlayerSetup] OnNetworkSpawn — netId={NetworkObjectId} " +
                  $"ownerClientId={OwnerClientId} IsServer={IsServer} IsOwner={IsOwner} " +
                  $"scene={gameObject.scene.name}");

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

        // Client: NV 초기값은 OnValueChanged로 전달되지 않으므로 즉시 적용
        if (!IsServer && _player != null)
            _player.heart = _hp.Value;

        // Owner / 비오너 분기
        if (IsOwner)
        {
            SetupOwner();
            // 오너: 로컬 색 상태 변경 → NetworkVariable에 반영
            if (_events != null)
            {
                _events.OnBlackWhiteChanged  += PushIsBlack;
                _events.OnUniqueColorChanged += PushIsUniqueColor;
            }

            // 카메라 바인드: OnPlayersReady 이후로 타이밍 확정
            // (destroyWithScene:true로 씬마다 새 플레이어가 스폰되므로 매번 re-bind 필요)
            PlayerSpawnCoordinator.OnPlayersReady += BindCameraOnPlayersReady;
            if (PlayerSpawnCoordinator.IsReady) BindCameraOnPlayersReady(); // 늦은 구독 대비
        }
        else
        {
            SetupNonOwner();
            // 비오너: 현재 NetworkVariable 값을 즉시 적용
            ApplyIsBlack(_isBlack.Value);
            ApplyIsUniqueColor(_isUniqueColor.Value);
        }

        // Owner/비오너 설정 이후 Rigidbody 권한을 서버 기준으로 확정
        ApplyPhysicsAuthority();
    }

    public override void OnNetworkDespawn()
    {
        // 버그3(Deferred OnSpawn/PurgeTrigger, NetworkDesign.md §9.0.1-b Axis B) 재현 로그.
        // OnNetworkSpawn 로그와 netId로 대조 — despawn 시점과 이후 뜨는 PurgeTrigger 경고의
        // 타이밍(같은 프레임/씬 언로드 직후 등)을 확인하는 용도.
        Debug.Log($"[NetworkPlayerSetup] OnNetworkDespawn — netId={NetworkObjectId} " +
                  $"ownerClientId={OwnerClientId} IsServer={IsServer} IsOwner={IsOwner} " +
                  $"scene={gameObject.scene.name}");

        _colorIndex.OnValueChanged    -= OnColorIndexChanged;
        _hp.OnValueChanged            -= OnHpChanged;
        _shield.OnValueChanged        -= OnShieldChanged;
        _isBlack.OnValueChanged       -= OnIsBlackChanged;
        _isUniqueColor.OnValueChanged -= OnIsUniqueColorChanged;

        if (IsOwner)
        {
            if (_events != null)
            {
                _events.OnBlackWhiteChanged  -= PushIsBlack;
                _events.OnUniqueColorChanged -= PushIsUniqueColor;
            }
            PlayerSpawnCoordinator.OnPlayersReady -= BindCameraOnPlayersReady;
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

        // 스폰 위치 Writer는 PlayerSpawnManager.SpawnNetworkPlayers()(Host)가 유일하다.
        // Instantiate(prefab, e.SpawnPos, ...) → Spawn() 메시지에 초기 Transform이 포함되어
        // Owner에게도 동일 위치로 복제됨 (NGO 기본 동작). 여기서 재조정하지 않고 검증만 한다.
        VerifySpawnPosition();

        // 로컬 마이크 → Global room 송신은 Owner만 (비오너 인스턴스는 Dissonance가 NGO owner를 모름)
        if (_voiceBroadcast != null) _voiceBroadcast.enabled = true;

        // 키워드 인식도 Owner만 (자기 마이크만 분석)
        if (_cheerKeyword != null) _cheerKeyword.enabled = true;

        Debug.Log($"[NetworkPlayerSetup] Owner 설정 완료 — clientId={OwnerClientId}");
    }

    /// <summary>
    /// 진단용 검증만 — 위치를 다시 쓰지 않는다 (Writer 유일 원칙, NetworkDesign §11).
    /// Host의 Instantiate(e.SpawnPos)와 실제 스폰 위치가 크게 다르면 경고 로그만 남긴다.
    /// 색/PlayerSpawnManager 조회 실패 시에도 위치는 건드리지 않고 조용히 반환.
    /// </summary>
    void VerifySpawnPosition()
    {
        EnablePhysics();

        if (NetworkManager.Singleton == null || PlayerSpawnManager.Instance == null) return;

        ulong myId = NetworkManager.Singleton.LocalClientId;
        if (!PlayerSpawnCoordinator.TryGetColor(myId, out var myColor)) return;

        Vector3 expected = PlayerSpawnManager.Instance.GetFixedSpawnPos(myColor);
        if ((transform.position - expected).sqrMagnitude > 0.25f)
            Debug.LogWarning($"[NetworkPlayerSetup] 스폰 위치 불일치 감지 — clientId={OwnerClientId} " +
                              $"color={myColor} expected={expected} actual={transform.position} " +
                              "(Writer=PlayerSpawnManager만 허용 — 재조정하지 않음)");

        // ===== TEMP DIAG (M.Stage 스폰 위치 버그, 2026-08-13 추가) =====
        // 원인 확정되면 이 줄 + 아래 DiagTrackSpawnPlacement() 코루틴 전체 삭제.
        // Assets/Docs/MStageNetworkBoard.md "M.Stage 스폰 위치 버그" 절 참고.
        StartCoroutine(DiagTrackSpawnPlacement(expected));
        // ===== TEMP DIAG END =====
    }

    // ===== TEMP DIAG (M.Stage 스폰 위치 버그, 2026-08-13 추가) =====
    // Owner 스폰 직후 10프레임만 위치·바닥 레이캐스트를 매 프레임 기록.
    // 씬 전환 직후 그 짧은 순간에 낙사가 나는지가 관심사라 범위를 좁혔음(원래 90프레임 → 10프레임).
    // 최초 콜드 로드 vs 사망 재로드에서 이 로그가 어떻게 다른지 비교하는 용도.
    // 원인 확정되면 이 메서드 전체 삭제.
    const int DiagTrackFrames = 10;

    System.Collections.IEnumerator DiagTrackSpawnPlacement(Vector3 expected)
    {
        float t0 = Time.realtimeSinceStartup;
        for (int frame = 0; frame < DiagTrackFrames; frame++)
        {
            Vector3 pos = transform.position;
            bool grounded = Physics.Raycast(pos + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 5f);
            Debug.Log($"[DIAG-Spawn] clientId={OwnerClientId} scene={gameObject.scene.name} frame={frame} " +
                      $"t={Time.realtimeSinceStartup - t0:F3}s pos=({pos.x:F2},{pos.y:F2},{pos.z:F2}) " +
                      $"expected=({expected.x:F2},{expected.y:F2},{expected.z:F2}) " +
                      $"바닥={(grounded ? $"{hit.collider.name} 거리={hit.distance:F2}" : "없음(NONE)")}");

            // 낙사 임계값 아래로 내려가면 즉시 강조 로그 + 조기 종료 (버그 재현 확정 지점)
            if (_player != null && _player.enableFallDeath && pos.y < _player.fallDeathY)
            {
                Debug.LogWarning($"[DIAG-Spawn] *** 낙사 임계값 이탈 감지 *** clientId={OwnerClientId} " +
                                  $"frame={frame} t={Time.realtimeSinceStartup - t0:F3}s pos={pos} fallDeathY={_player.fallDeathY}");
                yield break;
            }
            yield return null;
        }
    }
    // ===== TEMP DIAG END =====


    void EnablePhysics()
    {
        if (_rb == null) return;
        _rb.linearVelocity  = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        // 스폰 메시지로 이미 확정된 Transform(e.SpawnPos)에 물리 바디를 맞춘다.
        // 안 맞추면 Rigidbody가 프리팹 저장 포즈에 남아있다가 다음 물리 틱에
        // Transform을 그 포즈로 되돌리는 1프레임 워프가 발생할 수 있음(Writer는 여전히
        // PlayerSpawnManager 하나 — 여기서는 물리 동기화만, 좌표 재계산 없음).
        _rb.position = transform.position;
        _rb.rotation = transform.rotation;
        // isKinematic는 ApplyPhysicsAuthority()에서 IsServer 기준으로 설정
    }

    /// <summary>
    /// OnPlayersReady 이후 카메라 바인드.
    /// destroyWithScene:true로 씬마다 플레이어가 새로 스폰되므로 매 씬마다 re-bind.
    /// </summary>
    void BindCameraOnPlayersReady()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= BindCameraOnPlayersReady;
        LocalPlayerCamera.EnsureForOwner(_localCameraPrefab, transform, _player);
        Debug.Log($"[NetworkPlayerSetup] 카메라 바인드 완료 (OnPlayersReady) — clientId={OwnerClientId}");
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
        if (_cheerKeyword  != null) _cheerKeyword.enabled  = false;
    }

    /// <summary>
    /// Owner Authority 확정: Owner/비오너 설정 이후 OnNetworkSpawn 마지막에 호출.
    /// Owner는 본인 캐릭터를 직접 물리 이동시키므로 항상 dynamic.
    /// Host는 비오너 캐릭터도 함정 Trigger 판정을 위해 dynamic으로 유지(이동은 CNT 수신, 시뮬은 안 함).
    /// Client는 비오너 캐릭터를 kinematic으로 두고 CNT 수신 위치만 반영.
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
        // HP가 이미 0이면(사망 처리 중 Respawn 전 재충돌 등) 중복 사망 방지
        if (_hp.Value <= 0) return;

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

    /// <summary>오너 클라이언트에 사망을 확정. 비오너 클라이언트에는 사망 플래그 동기화 + UI 이벤트만 전달.</summary>
    [ClientRpc]
    void ForceKillClientRpc()
    {
        if (IsOwner)
            _player?.ForceKill();
        else
        {
            _player?.SyncDeadFlag();
            _events?.RaiseDied();
        }
    }

    void OnHpChanged(int prev, int next)
    {
        if (_player == null) return;
        _player.heart = next;

        // HP가 실제로 줄었을 때만 피격 이벤트 발행.
        // HP 증가(스폰·리스폰 회복)에서 Hit SFX·연출이 울리는 버그 방지.
        if (next > 0 && next < prev)
            _events?.RaiseDamaged(false);
        // 0 → 양수: 씬 리로드 후 HP 복구 = 리스폰 신호 (비오너만 — Owner는 OnNetworkSpawn에서 처리).
        else if (prev == 0 && next > 0 && !IsOwner)
            _events?.RaiseRespawned();
    }

    // ── 넉백 (순수, HP 미변경) ────────────────────────────────────

    /// <summary>
    /// Host에서 직접 호출해 순수 넉백만 적용. HP·쉴드는 건드리지 않으며,
    /// 기존 피격 무적(_damageInvulnEndTime, isDamage)과도 완전히 분리되어 항상 적용된다.
    /// Punch / Breakable 등 넉백 전용 이벤트에서 사용 (NetworkDamageUtil.ApplyKnockback).
    /// </summary>
    public void ApplyKnockbackFromServer(Vector3 direction, float force)
    {
        if (!IsServer) return;
        if (_player == null || _player.IsDead) return;

        ApplyKnockbackClientRpc(direction, force);
    }

    /// <summary>Owner 클라이언트에서만 실제 AddForce 적용.</summary>
    [ClientRpc]
    void ApplyKnockbackClientRpc(Vector3 direction, float force)
    {
        if (!IsOwner) return;
        _rb?.AddForce(direction * force, ForceMode.Impulse);
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

    /// <summary>오너 클라이언트에 Jammed 즉사 전달. 비오너 클라이언트에는 사망 플래그 동기화 + UI 이벤트만 전달.</summary>
    [ClientRpc]
    void ForceInstantKillClientRpc()
    {
        if (IsOwner)
            _player?.KillInstantly();
        else
        {
            _player?.SyncDeadFlag();
            _events?.RaiseDied();
        }
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
    /// Owner: 로컬 Y가 fallDeathY 미만일 때 1회 호출.
    /// Owner+CNT에서 Host 프록시 Y는 void 낙사를 놓칠 수 있으므로 Owner 실좌표 보고 → Host 확정.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void ReportFallDeathServerRpc()
    {
        ApplyFallDeathFromServer();
    }

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
    /// Server: Host-as-Owner 등 Host 실좌표가 신뢰될 때의 폴백 Y 판정.
    /// Client Owner void 낙사의 주경로는 ReportFallDeathServerRpc.
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
