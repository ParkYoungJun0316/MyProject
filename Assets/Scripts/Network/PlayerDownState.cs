using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 다운/부활 네트워크 상태 — Player 도메인 소유, 각 플레이어 자신의 NetworkObject 소속.
/// SSOT: Assets/Docs/DownedReviveSystemDesign.md §9.
///
/// [흐름]
/// NetworkPlayerSetup.ApplyDamageFromServer가 HP 0(즉사 아님)에서 EnterDown() 호출
///   → 부활 가능자(다운·사망 아닌 다른 플레이어)가 없으면 즉시 완전사망(솔로 포함, §2)
///   → 있으면 10초 카운트다운. 근처 플레이어가 RequestStartRevive()로 시전(요청자 본인의 인스턴스에서 호출)
///   → 캔슬 없이 2초 경과 시 CompleteRevive(), 시전자가 실제 HP 감소를 겪으면 CancelIfReviving()
///   → 방치 만료 시 Host Update()가 NetworkPlayerSetup.FinalizeDownDeath()로 기존 사망 파이프라인 재사용.
/// 다운 중 즉사(보울더·낙사·스테이지 실패)는 NetworkPlayerSetup이 ClearForDeath() 후 그대로 적용한다.
///
/// [미구현 — 후속 작업]
/// - 완전사망을 StageManager에 직접 통보 + STAGE FAILED 2초 배너(§6, §9.2) — 지금은 기존 즉시 리로드.
/// - Enemy 도메인의 다운 상태 감지/타겟팅 제외, TeamStatusUI/PlayerHPUI 확장, 부활 파티클(§8).
///
/// [입력]
/// RequestStartRevive 호출부는 PlayerReviveInteract(Player 도메인, Interact/E 액션).
///
/// [배치]
/// Network Player Prefab에 NetworkPlayerSetup과 함께 추가.
/// </summary>
[RequireComponent(typeof(Player))]
[RequireComponent(typeof(NetworkPlayerSetup))]
public class PlayerDownState : NetworkBehaviour
{
    [Header("타이밍")]
    [SerializeField] float downTimeoutDuration = 10f;
    [SerializeField] float reviveCastDuration = 2f;
    [SerializeField] float reviveRange = 2f;

    [Header("부활 결과")]
    [SerializeField] int reviveHeartAmount = 3;
    [SerializeField] float reviveGraceInvulnDuration = 1f;

    const ulong NoReviver = ulong.MaxValue;

    readonly NetworkVariable<bool> _isDowned = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<double> _downDeadlineServerTime = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<bool> _isBeingRevived = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<ulong> _reviverClientId = new(
        NoReviver, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool IsDowned => _isDowned.Value;
    public bool IsBeingRevived => _isBeingRevived.Value;
    public ulong ReviverClientId => _reviverClientId.Value;

    /// <summary>완전사망까지 남은 시간(초). 부활 시전 중에는 정지된 값을 반환(§3). TeamStatusUI 표시용.</summary>
    public float RemainingDownTime
    {
        get
        {
            if (!_isDowned.Value || NetworkManager == null) return 0f;
            if (_isBeingRevived.Value) return Mathf.Max(0f, (float)_remainingOnReviveStart);
            return Mathf.Max(0f, (float)(_downDeadlineServerTime.Value - NetworkManager.ServerTime.Time));
        }
    }

    /// <summary>이 플레이어가 지금 다른 누군가를 부활 시전 중인지. 전 머신에서 NV로 판단 — 시전자 이동 잠금(§9.3)용.</summary>
    public bool IsRevivingOther
    {
        get
        {
            if (!IsSpawned) return false;
            foreach (var s in _spawned)
                if (s != this && s._isBeingRevived.Value && s._reviverClientId.Value == OwnerClientId)
                    return true;
            return false;
        }
    }

    Player _player;
    NetworkPlayerSetup _netSetup;

    // 부활 시전 시작 시점의 "완전사망까지 남은 시간". Host는 BeginRevive에서 정확히 기록(캔슬 복원용, §9.2),
    // Client는 _isBeingRevived 변경 콜백에서 표시용으로 근사 기록.
    double _remainingOnReviveStart;
    Coroutine _reviveTimerRoutine;

    // 중복 수신 방어(RpcSubmitDedup SSOT). 발신·수신 모두 시전자 본인 인스턴스라 수명이 같다.
    readonly RpcSubmitDedup _reviveRequestDedup = new();

    static readonly List<PlayerDownState> _spawned = new();

    /// <summary>스폰된 모든 PlayerDownState(자기 자신 포함). PlayerReviveInteract의 근접 대상 탐색용.</summary>
    public static IReadOnlyList<PlayerDownState> AllSpawned => _spawned;

    // 서버 전용: reviverClientId → 그가 지금 시전 중인 대상 (캔슬 조회 + 시전자 측 1:1 강제).
    static readonly Dictionary<ulong, PlayerDownState> _activeReviveByReviver = new();

    void Awake()
    {
        _player = GetComponent<Player>();
        _netSetup = GetComponent<NetworkPlayerSetup>();
    }

    public override void OnNetworkSpawn()
    {
        _spawned.Add(this);
        _isBeingRevived.OnValueChanged += OnIsBeingRevivedChanged;
    }

    public override void OnNetworkDespawn()
    {
        _spawned.Remove(this);
        _isBeingRevived.OnValueChanged -= OnIsBeingRevivedChanged;

        if (!IsServer) return;
        ReleaseReviveBookkeeping();
    }

    void OnIsBeingRevivedChanged(bool prev, bool next)
    {
        if (IsServer || !next) return;
        _remainingOnReviveStart = _downDeadlineServerTime.Value - NetworkManager.ServerTime.Time;
    }

    /// <summary>Host 전용: 방치 타임아웃 감지. TutorialNetworkManager.UpdateGate와 동일한 단일 레인 방식(§9).</summary>
    void Update()
    {
        if (!IsServer) return;
        if (!_isDowned.Value || _isBeingRevived.Value) return;
        if (NetworkManager.ServerTime.Time < _downDeadlineServerTime.Value) return;

        ClearForDeath();
        _netSetup.FinalizeDownDeath();
    }

    // ── 다운 진입 (Host 전용, NetworkPlayerSetup.ApplyDamageFromServer에서 호출) ──────

    public void EnterDown()
    {
        if (!IsServer) return;
        if (_isDowned.Value || (_player != null && _player.IsDead)) return;

        // 나를 살릴 사람이 없으면 카운트다운 없이 즉시 완전사망(솔로·마지막 생존자 다운, §2).
        if (!HasPotentialReviver())
        {
            _netSetup.FinalizeDownDeath();
            return;
        }

        _isDowned.Value = true;
        _downDeadlineServerTime.Value = NetworkManager.ServerTime.Time + downTimeoutDuration;
        _isBeingRevived.Value = false;
        _reviverClientId.Value = NoReviver;

        PlayerDownedClientRpc();
    }

    bool HasPotentialReviver()
    {
        foreach (var s in _spawned)
        {
            if (s == this || s._player == null) continue;
            if (!s._player.IsDead && !s._isDowned.Value) return true;
        }
        return false;
    }

    [ClientRpc]
    void PlayerDownedClientRpc() => _player?.EnterDownState();

    /// <summary>
    /// Host 전용: 다운 중 즉사/방치 만료로 완전사망 확정 직전 호출. 진행 중 부활(대상·시전자 양쪽)과
    /// 다운 플래그를 정리해 Update()의 방치 만료가 한 번 더 발동하지 않게 한다.
    /// </summary>
    public void ClearForDeath()
    {
        if (!IsServer) return;

        CancelIfReviving(OwnerClientId);
        if (_isBeingRevived.Value) CancelRevive();

        _isDowned.Value = false;
    }

    // ── 부활 요청 (호출부 = 시전자 본인의 인스턴스) ──────────────────────────

    /// <summary>Owner: 다운된 대상 근접 + 홀드 입력에서 호출. targetNetId = 대상 NetworkObjectId.</summary>
    public void RequestStartRevive(ulong targetNetId) =>
        RequestStartReviveServerRpc(targetNetId, _reviveRequestDedup.NextSeq());

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    void RequestStartReviveServerRpc(ulong targetNetId, uint submitSeq, RpcParams rpcParams = default)
    {
        if (_reviveRequestDedup.IsDuplicate(rpcParams.Receive.SenderClientId, submitSeq)) return;

        // this = 요청자(시전자) 본인의 PlayerDownState 인스턴스. 검증 순서는 §9.4 기재 순서.
        if (NetworkManager.SpawnManager == null ||
            !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetNetId, out var targetObj))
            return;

        var target = targetObj.GetComponent<PlayerDownState>();
        if (target == null || target == this) return;

        // ① 요청자-대상 간 거리
        float dist = Vector3.Distance(transform.position, target.transform.position);
        if (dist > reviveRange) return;

        // ② 대상이 실제로 다운 상태인지
        if (!target._isDowned.Value) return;

        // ③ 대상이 이미 다른 시전자에게 시전받고 있지 않은지 (대상 측 1:1)
        if (target._isBeingRevived.Value) return;

        // ④ 요청자 본인이 다운/사망 상태가 아닌지
        if (_isDowned.Value || (_player != null && _player.IsDead)) return;

        // ⑤ 요청자가 이미 다른 대상을 시전 중이 아닌지 (시전자 측 1:1)
        if (_activeReviveByReviver.ContainsKey(OwnerClientId)) return;

        target.BeginRevive(OwnerClientId);
    }

    /// <summary>Host 전용: 검증 통과 후 실제 부활 시전 시작. this = 다운된 대상.</summary>
    void BeginRevive(ulong reviverClientId)
    {
        _remainingOnReviveStart = _downDeadlineServerTime.Value - NetworkManager.ServerTime.Time;
        _isBeingRevived.Value = true;
        _reviverClientId.Value = reviverClientId;
        _activeReviveByReviver[reviverClientId] = this;

        if (_reviveTimerRoutine != null) StopCoroutine(_reviveTimerRoutine);
        _reviveTimerRoutine = StartCoroutine(ReviveTimerRoutine());
    }

    IEnumerator ReviveTimerRoutine()
    {
        yield return new WaitForSeconds(reviveCastDuration);
        _reviveTimerRoutine = null;
        CompleteRevive();
    }

    /// <summary>Host 전용: this = 다운된 대상. 잔여 시간 복원 후 방치 카운트다운 재개.</summary>
    void CancelRevive()
    {
        if (!_isBeingRevived.Value) return;

        ReleaseReviveBookkeeping();

        // 시전 시작 시점에 보관해둔 잔여 시간으로 완전사망 데드라인 복원(§9.2).
        _downDeadlineServerTime.Value = NetworkManager.ServerTime.Time + _remainingOnReviveStart;
        _isBeingRevived.Value = false;
        _reviverClientId.Value = NoReviver;

        ReviveCancelledClientRpc();
    }

    void CompleteRevive()
    {
        if (!_isBeingRevived.Value) return; // 이미 캔슬된 뒤 코루틴이 뒤늦게 실행된 경우 방어

        ReleaseReviveBookkeeping();
        _isDowned.Value = false;
        _isBeingRevived.Value = false;
        _reviverClientId.Value = NoReviver;

        _netSetup.ReviveFromServer(reviveHeartAmount, reviveGraceInvulnDuration);
        ReviveCompletedClientRpc();
    }

    void ReleaseReviveBookkeeping()
    {
        if (_reviverClientId.Value != NoReviver &&
            _activeReviveByReviver.TryGetValue(_reviverClientId.Value, out var t) && t == this)
            _activeReviveByReviver.Remove(_reviverClientId.Value);

        if (_reviveTimerRoutine != null)
        {
            StopCoroutine(_reviveTimerRoutine);
            _reviveTimerRoutine = null;
        }
    }

    [ClientRpc]
    void ReviveCancelledClientRpc()
    {
        // 파티클 중단 등 실패 피드백 자리 — 세부 미정(DownedReviveSystemDesign.md §8), 추후 구현.
    }

    [ClientRpc]
    void ReviveCompletedClientRpc() => _player?.ExitDownState();

    /// <summary>Host 전용: 이 clientId가 현재 누군가를 부활 시전 중이면 캔슬. NetworkPlayerSetup이 실제 HP 감소 시 호출.</summary>
    public static void CancelIfReviving(ulong reviverClientId)
    {
        if (_activeReviveByReviver.TryGetValue(reviverClientId, out var target) && target != null)
            target.CancelRevive();
    }
}
