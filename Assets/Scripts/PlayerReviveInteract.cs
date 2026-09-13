using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 다운된 팀원 근접 + Interact(E) 입력 → 부활 시전 요청. Player 도메인.
/// SSOT: Assets/Docs/DownedReviveSystemDesign.md §4.
///
/// [설계]
/// 실제 2초 시전·캔슬 판정은 전부 Host(PlayerDownState)가 갖고 있다(§9.2) — 이 컴포넌트는
/// "버튼을 눌렀다 + 그 순간 근처에 부활 가능한 다운된 팀원이 있다"만 로컬로 판단해 요청 1회를
/// 전송하는 얇은 입력 어댑터일 뿐이다. 판정 결과가 아니라 "시도"만 보내므로 클라이언트를
/// 신뢰하지 않는다 — Host가 거리·상태를 다시 검증한다(RequestStartReviveServerRpc).
///
/// [Interact 액션 주의]
/// InputSystem_Actions.inputactions의 "Interact"(E키) 액션에 Hold 인터랙션이 걸려 있다.
/// 여기서는 PlayerPunch.OnAttack과 동일하게 value.isPressed로 눌림 시작만 판정하므로 동작은
/// 하지만, Hold의 기본 유예시간(~0.4초)만큼 버튼을 누르고 있어야 요청이 나간다 — 부활 자체의
/// 2초 시전과는 무관한 추가 지연이다. 즉시 반응을 원하면 Input Actions 에디터에서 Interact의
/// Interactions를 Hold → (없음)으로 바꿔야 한다(에디터 작업).
///
/// [배치]
/// Network Player Prefab에 Player, PlayerDownState와 함께 추가.
/// </summary>
[RequireComponent(typeof(Player))]
[RequireComponent(typeof(PlayerDownState))]
public class PlayerReviveInteract : NetworkBehaviour
{
    [Tooltip("이 범위 안의 다운된 팀원만 후보로 본다. Host가 PlayerDownState.reviveRange로 다시 검증하므로 " +
        "여기 값은 UX용(대략 맞으면 됨) — 서버 값보다 넉넉하게 잡아 '눌렀는데 씹힘'을 방지한다.")]
    [SerializeField] float detectRange = 2.5f;

    Player _player;
    PlayerDownState _downState;

    void Awake()
    {
        _player = GetComponent<Player>();
        _downState = GetComponent<PlayerDownState>();
    }

    /// <summary>PlayerInput SendMessages — Interact 액션과 매핑.</summary>
    public void OnInteract(InputValue value)
    {
        if (!value.isPressed) return; // 눌림 시작만 (PlayerPunch.OnAttack과 동일 패턴)
        if (!IsOwner || _player == null || _player.IsDead || _player.IsDowned) return;
        if (InGameChatUI.IsChatOpen || TutorialCheerNameUI.IsOpen || CursorUnlockRequestUtil.IsRequested) return;

        var target = FindNearestRevivableTeammate();
        if (target == null) return;

        _downState.RequestStartRevive(target.NetworkObjectId);
    }

    PlayerDownState FindNearestRevivableTeammate()
    {
        PlayerDownState nearest = null;
        float nearestSqr = detectRange * detectRange;

        foreach (var other in PlayerDownState.AllSpawned)
        {
            if (other == null || other == _downState) continue;
            if (!other.IsDowned || other.IsBeingRevived) continue;

            float sqr = (other.transform.position - transform.position).sqrMagnitude;
            if (sqr > nearestSqr) continue;

            nearest = other;
            nearestSqr = sqr;
        }

        return nearest;
    }
}
