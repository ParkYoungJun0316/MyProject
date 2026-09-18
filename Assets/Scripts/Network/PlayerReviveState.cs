using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

/// <summary>
/// 사망 / 자동 부활 네트워크 상태 — Player 도메인 소유, 각 플레이어 자신의 NetworkObject 소속.
/// SSOT: `Assets/Docs/ReviveSystemDesign.md` §2(판정) · §3(부활) · §9(동기화).
///
/// [한 줄 규칙]
/// HP가 0이면 죽는다. 죽는 순간 팀 목숨이 0이면 스테이지 실패, 아니면 목숨 1을 쓰고
/// 1초 뒤 **가장 가까운 생존자 위치**에서 자동으로 살아난다.
///
/// [흐름 — Host 단일 레인]
/// NetworkPlayerSetup이 치명타(HP 0 / 낙사 / 즉사)를 확정 → OnDeathFromServer()
///   → 부활 금지 씬(§7.1)이면 아무것도 하지 않는다(목숨도 안 쓴다) — 실패는 그 씬 Objective가 낸다
///   → 생존자 0이면 즉시 스테이지 실패(§2)
///   → 팀 목숨 소진이면 즉시 스테이지 실패(§2·§4)
///   → 아니면 목숨 1 소모 + _reviveAtServerTime = now + 1초 예약
/// Host Update()가 예약 시각을 넘기면 최근접 생존자 좌표를 계산 → HP 복구 → ReviveClientRpc.
///
/// [왜 시전·캔슬이 없나]
/// 구 설계는 "다운 + 팀원이 E를 2초 홀드"였고, 캔슬 판정이 3경로(Host 위치 밀림 + HP 감소 훅 +
/// Owner 입력 감시)로 흩어져 CNT 보간 떨림 보정값과 시전자/대상 1:1 장부, 공유 dedup 번호열까지
/// 필요했다. 전부 유지비였고 2026-09-19에 폐기됐다(§1.2). 팀 긴장은 팀 공유 목숨이 담당한다.
///
/// [배치]
/// Network Player Prefab에 NetworkPlayerSetup과 함께 추가.
/// </summary>
[RequireComponent(typeof(Player))]
[RequireComponent(typeof(NetworkPlayerSetup))]
// 클래스 개명(PlayerDownState → PlayerReviveState, 2026-09-19). 프리팹/씬의 MonoBehaviour 참조 자체는
// .cs.meta의 GUID를 유지한 채 파일명을 바꿔 보존했고, 이 특성은 타입 이름으로 해석되는 경로
// (SerializeReference·API Updater)까지 같이 덮는 보험이다.
[MovedFrom(true, null, null, "PlayerDownState")]
public class PlayerReviveState : NetworkBehaviour
{
    [Header("타이밍")]
    [Tooltip("사망 후 자동 부활까지의 유예(초). §3 = 1초. 3초는 '가볍게'가 안 나와서 기각됐다.")]
    [SerializeField] float reviveDelay = 1f;

    [Header("부활 결과")]
    [Tooltip("부활 직후 HP(칸). §3 = 3. Kkultteok.prefab의 maxHeart는 5라 풀피가 아니다.")]
    [SerializeField] int reviveHeartAmount = 3;

    [Tooltip("부활 직후 무적 시간(초). 이 시간 동안 점유 판정에서도 제외된다(§3.1).")]
    [SerializeField] float reviveGraceInvulnDuration = 1f;

    [Tooltip("생존자 위치에서 위로 띄우는 높이(m). 지면에 박히지 않을 최소치만 — 오프셋·방향 선택·레이캐스트는 없다(§3).")]
    [SerializeField] float reviveHeightOffset = 0.1f;

    /// <summary>예약 없음.</summary>
    const double NoRevive = -1.0;

    // 부활 예정 서버 시각(절대). Host만 쓴다. 지속 상태라 ClientRpc가 아니라 NV(§9.1).
    readonly NetworkVariable<double> _reviveAtServerTime = new(
        NoRevive, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>부활 예정 서버 시각. 예약이 없으면 음수.</summary>
    public double ReviveAtServerTime => _reviveAtServerTime.Value;

    /// <summary>스폰된 모든 PlayerReviveState(자기 자신 포함). 최근접 생존자 탐색용.</summary>
    public static IReadOnlyList<PlayerReviveState> AllSpawned => _spawned;

    static readonly List<PlayerReviveState> _spawned = new();

    Player _player;
    NetworkPlayerSetup _netSetup;
    NetworkTransform _netTransform;
    Rigidbody _rigid;

    // Host 전용 사망 표식. Player.IsDead는 ForceKillClientRpc가 전 머신에 퍼뜨리는 값이라
    // Host 레인에서 "지금 누가 생존자인가"를 판정할 때 RPC 왕복에 기대게 된다 — 판정은 Host가
    // 자기 필드로만 한다(§9 Host 단일 레인).
    bool _isDeadServer;

    void Awake()
    {
        _player       = GetComponent<Player>();
        _netSetup     = GetComponent<NetworkPlayerSetup>();
        _netTransform = GetComponent<NetworkTransform>();
        _rigid        = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()   => _spawned.Add(this);
    public override void OnNetworkDespawn() => _spawned.Remove(this);

    // ── 사망 (Host 전용) ──────────────────────────────────────────

    /// <summary>
    /// Host 전용: 치명타가 확정된 직후 NetworkPlayerSetup이 호출(§2 판정표 전체).
    /// ForceKillClientRpc 전파와 순서를 가리지 않는다 — 생존자 판정은 이 클래스의 _isDeadServer로만 한다.
    /// </summary>
    public void OnDeathFromServer()
    {
        if (!IsServer || _isDeadServer) return;
        _isDeadServer = true;
        _reviveAtServerTime.Value = NoRevive;

        var stage = StageNetworkState.Instance;

        // 이미 실패가 확정돼 리로드 대기 중이면 목숨을 더 깎지 않는다 — 곧 씬이 갈린다.
        if (stage != null && stage.IsStageFailing) return;

        // ① 부활 금지 스테이지(§7.1 — T.Stage5). 목숨도 소모하지 않고 예약도 하지 않는다.
        //    "누가 죽든 그 판 실패"는 그 씬의 Objective가 사망을 보고 Fail()로 낸다
        //    (ColorTileChallenge·SequenceRing의 "실수 1회 = 판 다시"와 같은 형태).
        if (stage != null && stage.IsReviveDisabled) return;

        // ② 생존자 0 → 즉시 실패(§2). 부활 대기 중인 사람은 생존자가 아니다.
        if (FindNearestSurvivor() == null)
        {
            stage?.FailStageFromServer($"생존자 없음 (clientId={OwnerClientId} 사망)");
            return;
        }

        // ③ 팀 목숨 소진 → 즉시 실패(§2·§4). 목숨 슬롯이 없는 씬(튜토리얼)은 제한 없음(§8.1).
        //    솔로는 목숨이 0(인원−1)이라 여기서 항상 실패한다 — 별도 솔로 분기가 없는 이유.
        if (stage != null && !stage.TryConsumeTeamLife())
        {
            stage.FailStageFromServer($"팀 목숨 소진 (clientId={OwnerClientId} 사망)");
            return;
        }

        _reviveAtServerTime.Value = NetworkManager.ServerTime.Time + reviveDelay;
    }

    // ── 부활 (Host 전용 예약 만료 판정) ───────────────────────────

    /// <summary>Host 전용: 예약 시각이 지나면 부활시킨다. 단일 레인 — Client는 ReviveClientRpc만 받는다.</summary>
    void Update()
    {
        if (!IsServer || !_isDeadServer) return;

        double at = _reviveAtServerTime.Value;
        if (at < 0 || NetworkManager.ServerTime.Time < at) return;

        // 실패 확정 시 진행 중인 부활 예약을 취소한다(§9.4). 안 하면 "실패 배너(2초) → 리로드" 사이에
        // 1초 예약이 먼저 터져 죽은 사람이 살아난다. 지금은 배너가 2초라 우연히 안전하지만
        // 시간 관계에 기대지 않는다.
        var stage = StageNetworkState.Instance;
        if (stage != null && stage.IsStageFailing)
        {
            _reviveAtServerTime.Value = NoRevive;
            return;
        }

        // 유예 1초 사이에 나머지가 전부 죽었을 수 있다(§2 — 전원이 유예 안에 죽으면 생존자 0).
        PlayerReviveState survivor = FindNearestSurvivor();
        if (survivor == null)
        {
            _reviveAtServerTime.Value = NoRevive;
            stage?.FailStageFromServer($"부활 시점 생존자 없음 (clientId={OwnerClientId})");
            return;
        }

        _reviveAtServerTime.Value = NoRevive;
        _isDeadServer = false;

        // 생존자가 지금 서 있는 자리 그대로 — 사람이 서 있는 곳이라 구멍일 수도 벽 안일 수도 없다.
        // 그래서 지면 레이캐스트도 8방향 탐색도 구조적으로 불필요하다(§3).
        Vector3 pos = survivor.transform.position + Vector3.up * reviveHeightOffset;

        // HP를 먼저 복구해야 부활 직후 연출(PlayerHPUI의 OnHealed)이 이미 새 값을 본다.
        _netSetup.ReviveFromServer(reviveHeartAmount, reviveGraceInvulnDuration);
        ReviveClientRpc(pos, reviveGraceInvulnDuration);

        Debug.Log($"[PlayerReviveState] 부활 — clientId={OwnerClientId} " +
            $"생존자={survivor.OwnerClientId} pos={pos} 남은목숨={stage?.TeamLivesRemaining ?? -1}");
    }

    /// <summary>
    /// Host 전용: 지금 살아 있는 가장 가까운 다른 플레이어. 없으면 null.
    /// **부활 대기 중인 사람은 생존자가 아니다**(§2) — _isDeadServer가 그대로 그 조건이다.
    /// </summary>
    PlayerReviveState FindNearestSurvivor()
    {
        PlayerReviveState best = null;
        float bestSqr = float.MaxValue;
        Vector3 from = transform.position;

        foreach (var s in _spawned)
        {
            if (s == this || s == null || s._isDeadServer) continue;
            if (s._player == null) continue;

            float sqr = (s.transform.position - from).sqrMagnitude;
            if (sqr >= bestSqr) continue;
            bestSqr = sqr;
            best = s;
        }
        return best;
    }

    // ── 부활 전파 (전 머신) ───────────────────────────────────────

    /// <summary>
    /// 전 머신: 사망 상태를 풀고 부활 그레이스 창을 연다. 좌표 이동은 **Owner 머신에서만** —
    /// ClientNetworkTransform은 Owner 권한이라 남의 좌표를 쓸 수 없다(§9.3 / NetworkDesign.md §11.9).
    /// </summary>
    [ClientRpc]
    void ReviveClientRpc(Vector3 pos, float graceDuration)
    {
        _player?.Revive(graceDuration);

        if (!IsOwner) return;

        TeleportSelf(pos);

        // 텔레포트하면 ThirdPersonCamera의 positionDamping이 맵을 가로질러 날아간다 — 1프레임 스냅.
        LocalPlayerCamera.Instance?.ThirdPersonCam?.SnapToTarget();
    }

    /// <summary>
    /// Owner 전용 좌표 이동. `transform.position` 단순 대입 금지 — NetworkDesign.md §11.9가 SSOT.
    ///
    /// [왜 Teleport()여야 하나] 그냥 대입하면 ① CNT가 Interpolate라 원격 화면에서 맵을 가로질러
    /// 미끄러지고 ② Host 비오너 레인이 rb.MovePosition()으로 적용해 그 거리를 물리로 쓸어
    /// 벽에 끼거나 터널링한다. 부활은 죽은 자리에서 생존자까지 거리가 길 수 있어 둘 다 현실적이다.
    /// Teleport()는 보간을 리셋하며, 권한(Owner) 인스턴스가 아닌 곳에서 호출하면 예외를 던진다.
    /// </summary>
    void TeleportSelf(Vector3 pos)
    {
        if (_netTransform != null)
            _netTransform.Teleport(pos, transform.rotation, transform.localScale);
        else
            transform.position = pos; // CNT 미부착 폴백(튜토리얼 등 비네트워크 구성)

        if (_rigid != null)
        {
            _rigid.position = pos;
            if (!_rigid.isKinematic)
            {
                _rigid.linearVelocity  = Vector3.zero;
                _rigid.angularVelocity = Vector3.zero;
            }
        }
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: 즉시 부활 (Host 전용)")]
    void Debug_ReviveNow()
    {
        if (!IsServer || !_isDeadServer) return;
        _reviveAtServerTime.Value = NetworkManager.ServerTime.Time;
    }
#endif
}
