using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 다운/부활 네트워크 상태 — Player 도메인 소유, 각 플레이어 자신의 NetworkObject 소속.
/// SSOT: Assets/Docs/DownedReviveSystemDesign.md §9.
///
/// [흐름]
/// NetworkPlayerSetup.ApplyDamageFromServer가 HP 0(즉사 아님)에서 EnterDown() 호출
///   → 부활 가능자(다운·사망 아닌 다른 플레이어)가 없거나 팀 목숨 0이면 즉시 완전사망(솔로 포함, §2·§4B)
///   → 있으면 10초 카운트다운. 근처 플레이어가 RequestStartRevive()로 시전(요청자 본인의 인스턴스에서 호출)
///   → 시전 중 캔슬: Host가 시전자의 위치 밀림·사거리 이탈을 매 프레임 판정, HP 감소는 CancelIfReviving(),
///     E 해제·다른 입력은 Owner가 RequestCancelRevive()로 신고(§4)
///   → 캔슬 없이 2초 경과 시 부활(팀 목숨 1 소모, 0이 되면 다운 중인 나머지 즉시 완전사망), 방치 만료 시 NetworkPlayerSetup.FinalizeDownDeath()로 기존 사망 파이프라인 재사용.
///
/// [연출 = NV 구동]
/// 다운 여부는 지속 상태라 _isDowned.OnValueChanged에서 Player.EnterDownState/ExitDownState를 호출한다
/// (NetworkDesign 동기화 규약). ClientRpc로 하면 중복 수신 시 서버와 무관하게 로컬만 다시 다운되고,
/// HP NV보다 먼저 도착해 연출 시점의 heart가 이전 값이 된다.
/// _isDowned는 부활로만 false가 된다 — 완전사망은 _deathFinalized(서버 전용)로 표시하고 씬 리로드로 정리.
///
/// [미구현 — 후속 작업]
/// - 완전사망을 StageManager에 직접 통보 + STAGE FAILED 2초 배너(§6, §9.2) — 지금은 기존 즉시 리로드.
/// 부활 꿀물 파티클은 ReviveHoneyVfx(IsBeingRevived NV 구독, RPC 없음).
///
/// [입력]
/// RequestStartRevive/RequestCancelRevive 호출부는 PlayerReviveInteract(Player 도메인, Interact/E 액션).
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

    [Header("부활 캔슬")]
    [Tooltip("시전 시작 위치에서 시전자가 이 거리(m) 이상 벗어나면 캔슬(걷기·넉백·바람·미끄러짐 등 모든 밀림). " +
        "Host가 보는 원격 시전자 위치는 ClientNetworkTransform 보간값이라 떨림을 흡수할 여유가 필요하다.")]
    [SerializeField] [Min(MinReviveMoveTolerance)] float reviveMoveTolerance = 0.3f;

    // 0이면 정지 상태의 부동소수 떨림(0.00m)만으로 매번 캔슬된다 — 프리팹 값이 0이어도 이 아래로는 내려가지 않게 한다.
    const float MinReviveMoveTolerance = 0.05f;
    [Tooltip("시전 시작 직후 이 시간(초) 동안은 밀림 판정을 하지 않고 시작 위치를 계속 갱신한다. " +
        "원격 시전자가 멈춘 직후 E를 누르면 Host의 CNT 보간 위치가 아직 따라오는 중이라 즉시 캔슬되던 문제 방지. " +
        "사거리 이탈 판정은 이 구간에도 적용된다.")]
    [SerializeField] float reviveMoveGraceDuration = 0.2f;

    [Header("부활 결과")]
    [SerializeField] int reviveHeartAmount = 2;
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

    /// <summary>지금 부활 시전을 시작할 수 있는 대상인지. 클라이언트 후보 탐색용 — Host가 다시 검증한다.</summary>
    public bool IsRevivable => _isDowned.Value && !_isBeingRevived.Value && _player != null && !_player.IsDead;

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

    /// <summary>다운 후 완전사망까지의 전체 시간(초). LocalDownOverlayUI 회색 막 진행도 계산용.</summary>
    public float DownTimeoutDuration => downTimeoutDuration;

    /// <summary>Host가 검증하는 부활 사거리(m). PlayerReviveInteract가 이보다 좁게 후보를 잡는 기준.</summary>
    public float ReviveRange => reviveRange;

    /// <summary>스폰된 모든 PlayerDownState(자기 자신 포함). PlayerReviveInteract의 근접 대상 탐색용.</summary>
    public static IReadOnlyList<PlayerDownState> AllSpawned => _spawned;

    Player _player;
    NetworkPlayerSetup _netSetup;

    // 서버 전용 — 진행 중인 부활 시전(this = 다운된 대상).
    PlayerDownState _reviver;
    Vector3 _reviverStartPos;
    float _reviverStartPosLockTime;
    float _reviveCompleteTime;

    // 부활 시전 시작 시점의 "완전사망까지 남은 시간". Host는 BeginRevive에서 정확히 기록(캔슬 복원용, §9.2),
    // Client는 _isBeingRevived 변경 콜백에서 표시용으로 근사 기록.
    double _remainingOnReviveStart;

    // 서버 전용: 완전사망 확정. 방치 만료 재발동·사망자 부활 시전을 막는다.
    bool _deathFinalized;

    // 중복 수신 방어(RpcSubmitDedup SSOT). 시작·캔슬이 같은 번호열을 공유해야 늦게 도착한 중복 캔슬이
    // 그 뒤에 새로 시작한 시전을 캔슬하지 못한다. 발신·수신 모두 시전자 본인 인스턴스라 수명이 같다.
    readonly RpcSubmitDedup _reviveRequestDedup = new();

    static readonly List<PlayerDownState> _spawned = new();

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
        _isDowned.OnValueChanged += OnIsDownedChanged;
        _isBeingRevived.OnValueChanged += OnIsBeingRevivedChanged;
    }

    public override void OnNetworkDespawn()
    {
        _spawned.Remove(this);
        _isDowned.OnValueChanged -= OnIsDownedChanged;
        _isBeingRevived.OnValueChanged -= OnIsBeingRevivedChanged;

        if (IsServer) ReleaseReviveBookkeeping();
    }

    void OnIsDownedChanged(bool prev, bool next)
    {
        if (_player == null) return;
        if (next) _player.EnterDownState();
        else _player.ExitDownState();
    }

    void OnIsBeingRevivedChanged(bool prev, bool next)
    {
        if (IsServer || !next) return;
        _remainingOnReviveStart = _downDeadlineServerTime.Value - NetworkManager.ServerTime.Time;
    }

    /// <summary>Host 전용: 시전 캔슬·완료와 방치 타임아웃 판정. TutorialNetworkManager.UpdateGate와 동일한 단일 레인(§9).</summary>
    void Update()
    {
        if (!IsServer || !_isDowned.Value || _deathFinalized) return;

        if (_isBeingRevived.Value)
        {
            if (IsReviveInterrupted(out string reason))
            {
                Debug.Log($"[PlayerDownState] 부활 캔슬(Host 판정) — target={OwnerClientId} reviver={_reviverClientId.Value} " +
                    $"elapsed={reviveCastDuration - (_reviveCompleteTime - Time.time):F2}s reason={reason}");
                CancelRevive();
            }
            else if (Time.time >= _reviveCompleteTime) CompleteRevive();
            return;
        }

        if (NetworkManager.ServerTime.Time < _downDeadlineServerTime.Value) return;

        ClearForDeath();
        _netSetup.FinalizeDownDeath();
    }

    /// <summary>시전자가 사라졌거나, 시작 위치에서 밀렸거나, 사거리를 벗어났으면 true(§4).</summary>
    bool IsReviveInterrupted(out string reason)
    {
        reason = null;
        var r = _reviver;
        if (r == null || r._player == null) { reason = "시전자 없음"; return true; }
        if (r._player.IsDead) { reason = "시전자 사망"; return true; }
        if (r._isDowned.Value) { reason = "시전자 다운"; return true; }

        Vector3 pos = r.transform.position;
        if (Time.time < _reviverStartPosLockTime)
            _reviverStartPos = pos; // 유예 구간: 보간 위치가 멈출 때까지 시작 위치를 따라간다.
        else
        {
            float moved = Vector3.Distance(pos, _reviverStartPos);
            float tolerance = Mathf.Max(reviveMoveTolerance, MinReviveMoveTolerance);
            if (moved > tolerance)
            {
                reason = $"시전자 위치 밀림 {moved:F3}m > {tolerance}m (start={_reviverStartPos} now={pos})";
                return true;
            }
        }

        float dist = Vector3.Distance(pos, transform.position);
        if (dist > reviveRange)
        {
            reason = $"사거리 이탈 {dist:F2}m > {reviveRange}m";
            return true;
        }
        return false;
    }

    // ── 다운 진입 (Host 전용, NetworkPlayerSetup.ApplyDamageFromServer에서 호출) ──────

    public void EnterDown()
    {
        if (!IsServer) return;
        if (_isDowned.Value || _deathFinalized || (_player != null && _player.IsDead)) return;

        // 나를 살릴 사람이 없거나(솔로·마지막 생존자, §2) 팀 목숨이 0이면(§4B) 카운트다운 없이 즉시 완전사망.
        // StageNetworkState가 없는 씬(튜토리얼)은 목숨 제한 없음.
        var stage = StageNetworkState.Instance;
        if (!HasPotentialReviver() || (stage != null && stage.AreTeamLivesExhausted()))
        {
            _deathFinalized = true;
            _netSetup.FinalizeDownDeath();
            return;
        }

        _downDeadlineServerTime.Value = NetworkManager.ServerTime.Time + downTimeoutDuration;
        _isBeingRevived.Value = false;
        _reviverClientId.Value = NoReviver;
        _isDowned.Value = true;
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

    /// <summary>
    /// Host 전용: 완전사망 확정 직전 호출(다운 중 즉사·방치 만료·일반 즉사 공통). 진행 중 부활(대상·시전자
    /// 양쪽)을 정리하고 _deathFinalized를 세워 방치 만료 재발동과 사망자 대상 시전을 막는다.
    /// </summary>
    public void ClearForDeath()
    {
        if (!IsServer) return;

        _deathFinalized = true;
        CancelIfReviving(OwnerClientId, "시전자 완전사망");
        if (_isBeingRevived.Value) CancelRevive();
    }

    // ── 부활 요청 (호출부 = 시전자 본인의 인스턴스) ──────────────────────────

    /// <summary>Owner: 다운된 대상 근접 + E 입력에서 호출. targetNetId = 대상 NetworkObjectId.</summary>
    public void RequestStartRevive(ulong targetNetId) =>
        RequestStartReviveServerRpc(targetNetId, _reviveRequestDedup.NextSeq());

    /// <summary>Owner: 시전 중 E 해제·다른 입력 시 호출. 시전 중이 아니면 Host에서 no-op.</summary>
    public void RequestCancelRevive() =>
        RequestCancelReviveServerRpc(_reviveRequestDedup.NextSeq());

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
        if ((transform.position - target.transform.position).sqrMagnitude > reviveRange * reviveRange) return;

        // ② 대상이 실제로 다운 상태인지 (완전사망 확정 전)
        if (!target._isDowned.Value || target._deathFinalized) return;
        if (target._player != null && target._player.IsDead) return;

        // ③ 대상이 이미 다른 시전자에게 시전받고 있지 않은지 (대상 측 1:1)
        if (target._isBeingRevived.Value) return;

        // ④ 요청자 본인이 다운/사망 상태가 아닌지
        if (_isDowned.Value || _deathFinalized || (_player != null && _player.IsDead)) return;

        // ⑤ 요청자가 이미 다른 대상을 시전 중이 아닌지 (시전자 측 1:1)
        if (_activeReviveByReviver.ContainsKey(OwnerClientId)) return;

        target.BeginRevive(this);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    void RequestCancelReviveServerRpc(uint submitSeq, RpcParams rpcParams = default)
    {
        if (_reviveRequestDedup.IsDuplicate(rpcParams.Receive.SenderClientId, submitSeq)) return;
        CancelIfReviving(OwnerClientId, "Owner 입력 캔슬 신고(원인은 시전자 콘솔의 [PlayerReviveInteract] 로그)");
    }

    /// <summary>Host 전용: 검증 통과 후 실제 부활 시전 시작. this = 다운된 대상.</summary>
    void BeginRevive(PlayerDownState reviver)
    {
        _remainingOnReviveStart = _downDeadlineServerTime.Value - NetworkManager.ServerTime.Time;
        _reviver = reviver;
        _reviverStartPos = reviver.transform.position;
        _reviverStartPosLockTime = Time.time + reviveMoveGraceDuration;
        _reviveCompleteTime = Time.time + reviveCastDuration;
        _activeReviveByReviver[reviver.OwnerClientId] = this;

        _reviverClientId.Value = reviver.OwnerClientId;
        _isBeingRevived.Value = true;
    }

    /// <summary>Host 전용: this = 다운된 대상. 잔여 시간 복원 후 방치 카운트다운 재개.</summary>
    void CancelRevive()
    {
        if (!_isBeingRevived.Value) return;

        ReleaseReviveBookkeeping();

        // 시전 시작 시점에 보관해둔 잔여 시간으로 완전사망 데드라인 복원(§9.2).
        _downDeadlineServerTime.Value = NetworkManager.ServerTime.Time + _remainingOnReviveStart;
        _reviverClientId.Value = NoReviver;
        _isBeingRevived.Value = false;
    }

    void CompleteRevive()
    {
        if (!_isBeingRevived.Value) return;

        ReleaseReviveBookkeeping();
        _reviverClientId.Value = NoReviver;
        _isBeingRevived.Value = false;

        // 팀 공유 목숨 1개 소모(§4B). StageNetworkState가 없는 씬(튜토리얼)은 목숨 제한 없음.
        var stage = StageNetworkState.Instance;
        if (stage != null) stage.ConsumeTeamLife();

        // HP를 먼저 복구해야 Host에서 _isDowned 콜백(ExitDownState)이 동기 발동할 때 heart가 이미 새 값이다.
        _netSetup.ReviveFromServer(reviveHeartAmount, reviveGraceInvulnDuration);
        _isDowned.Value = false;

        // 방금 소모로 목숨이 0이면 다운 중인 나머지는 더 살릴 수 없다 — 10초를 기다리지 않고 즉시 완전사망.
        if (stage != null && stage.TeamLivesRemaining == 0)
            FailAllOtherDowned(this);
    }

    /// <summary>Host 전용: 팀 목숨 소진 확정 시, 이 인스턴스를 제외하고 현재 다운 중인 나머지 전원을 즉시 완전사망 처리.</summary>
    static void FailAllOtherDowned(PlayerDownState exclude)
    {
        foreach (var s in _spawned)
        {
            if (s == exclude || s._player == null) continue;
            if (!s._isDowned.Value || s._deathFinalized) continue;
            s.ClearForDeath();
            s._netSetup.FinalizeDownDeath();
        }
    }

    void ReleaseReviveBookkeeping()
    {
        if (_reviverClientId.Value != NoReviver &&
            _activeReviveByReviver.TryGetValue(_reviverClientId.Value, out var t) && t == this)
            _activeReviveByReviver.Remove(_reviverClientId.Value);

        _reviver = null;
    }

    /// <summary>Host 전용: 이 clientId가 현재 누군가를 부활 시전 중이면 캔슬. HP 감소·Owner 캔슬 신고에서 호출.</summary>
    public static void CancelIfReviving(ulong reviverClientId, string reason = null)
    {
        if (_activeReviveByReviver.TryGetValue(reviverClientId, out var target) && target != null)
        {
            Debug.Log($"[PlayerDownState] 부활 캔슬 — target={target.OwnerClientId} reviver={reviverClientId} " +
                $"elapsed={target.reviveCastDuration - (target._reviveCompleteTime - Time.time):F2}s reason={reason ?? "(미지정)"}");
            target.CancelRevive();
        }
    }
}
