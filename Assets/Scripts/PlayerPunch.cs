using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 다른 플레이어를 때려 순수 넉백만 가하는 Punch 컴포넌트 (HP 데미지 0).
/// Player.cs 확장이 아닌 별도 관심사로 분리 (NetworkPlayerSetup과 동일한 배치 원칙).
///
/// [흐름]
/// Owner 입력(Attack) → PunchServerRpc → Host가 쿨다운·생존 여부만 체크
/// → 스윙 판정 윈도우를 염 → 자식 PlayerPunchHitbox의 OnTriggerEnter(Host 로컬 물리)가
/// 실제 피격을 판정 → TryRegisterHit()로 1스윙 1히트 넉백 적용.
/// 스윙을 여는 순간 PlayerPunchHitbox.CheckAlreadyOverlapping()도 함께 호출해
/// 이미 겹쳐 있던 대상(가만히 붙어 있다가 펀치)까지 즉시 판정한다
/// (OnTriggerEnter는 새로 겹치는 순간에만 발생해 이 케이스를 놓치기 때문).
///
/// 사거리 재검증 없음: 실제 피격 판정은 Host의 물리 히트박스가 하므로
/// 클라이언트가 보낸 요청 자체를 신뢰할 필요가 없다 (Stage5ChaserHitbox와 동일 원칙).
///
/// [배치]
/// Network Player Prefab 루트에 추가. 자식 오브젝트에 Trigger Collider +
/// PlayerPunchHitbox를 붙여야 한다 (위치/오프셋은 에디터 작업).
/// </summary>
[RequireComponent(typeof(Player))]
public class PlayerPunch : NetworkBehaviour
{
    [Header("쿨다운")]
    [Tooltip("펀치 재사용 대기시간(초)")]
    [Range(0f, 2f)]
    [SerializeField] float cooldown = 1f;

    [Header("판정 윈도우")]
    [Tooltip("Host가 PunchServerRpc를 승인한 뒤 히트박스가 유효한 시간(초). 1스윙 1히트 가드용")]
    [SerializeField] float swingActiveDuration = 0.2f;

    [Header("히트박스")]
    [Tooltip("자식 오브젝트의 PlayerPunchHitbox. 스윙 시작 시 CheckAlreadyOverlapping() 호출용")]
    [SerializeField] PlayerPunchHitbox hitbox;

    [Header("넉백 (세기만 랜덤, 방향은 맞은 지점 기준)")]
    [Tooltip("넉백 힘 최소값")]
    [SerializeField] float knockbackForceMin = 5f;
    [Tooltip("넉백 힘 최대값")]
    [SerializeField] float knockbackForceMax = 10f;

    Player _player;
    Animator _anim;
    float _nextPunchTime;
    float _nextLocalPunchTime;
    bool _swingActive;
    bool _hitThisSwing;
    Coroutine _swingRoutine;

    void Awake()
    {
        _player = GetComponent<Player>();
        _anim = GetComponentInChildren<Animator>();
    }

    /// <summary>
    /// PlayerInput SendMessages — InputSystem_Actions의 Attack 액션과 매핑.
    /// 쿨다운 게이트를 Owner 로컬에서도 먼저 걸어 애니메이션/SFX가 서버 판정과 별개로
    /// 연타마다 나가는 것을 막는다 (실제 넉백 판정 쿨다운은 PunchServerRpc가 별도로 검증).
    /// </summary>
    public void OnAttack(InputValue value)
    {
        if (!value.isPressed) return;
        if (!IsOwner || _player == null || _player.IsDead) return;
        if (Time.time < _nextLocalPunchTime) return;

        _nextLocalPunchTime = Time.time + cooldown;

        SFXManager.Instance?.Play(SFXId.Player_Punch);
        // Owner 로컬에서 직접 트리거 — NetworkAnimator(Owner Authority)가 다른 클라이언트에 자동 동기화
        // (Player.cs의 doHit/doDie와 동일한 방식. 실제 피격 판정은 별도로 PunchServerRpc가 담당)
        _anim?.SetTrigger("doPunch");
        PunchServerRpc();
    }

    /// <summary>Host: 쿨다운·생존 여부만 체크. 사거리는 Host 물리 히트박스가 판정하므로 재검증하지 않는다.</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    void PunchServerRpc()
    {
        if (_player == null || _player.IsDead) return;
        if (Time.time < _nextPunchTime) return;

        _nextPunchTime = Time.time + cooldown;
        StartSwingWindow();
    }

    void StartSwingWindow()
    {
        if (_swingRoutine != null) StopCoroutine(_swingRoutine);
        _swingRoutine = StartCoroutine(SwingWindowRoutine());
    }

    IEnumerator SwingWindowRoutine()
    {
        _hitThisSwing = false;
        _swingActive = true;

        // OnTriggerEnter는 새로 겹치는 순간에만 발생하므로,
        // 스윙 시작 전부터 이미 겹쳐 있던 대상은 여기서 즉시 보완 판정한다.
        hitbox?.CheckAlreadyOverlapping();

        yield return new WaitForSeconds(swingActiveDuration);
        _swingActive = false;
        _swingRoutine = null;
    }

    /// <summary>
    /// PlayerPunchHitbox(자식)가 호출. Host에서만 유효.
    /// 스윙 윈도우 밖 또는 이번 스윙에 이미 맞은 대상이면 무시 (1스윙 1히트).
    /// </summary>
    public void TryRegisterHit(Player target, Vector3 hitPoint)
    {
        if (!IsServer) return;
        if (!_swingActive || _hitThisSwing) return;
        if (target == null || target == _player || target.IsDead) return;

        _hitThisSwing = true;

        Vector3 dir = target.transform.position - hitPoint;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        float force = Random.Range(knockbackForceMin, knockbackForceMax);
        NetworkDamageUtil.ApplyKnockback(target, dir, force);
    }
}
