using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// 다운된 팀원 근접 + Interact(E) 홀드 → 부활 시전 요청/캔슬 신고. Player 도메인.
/// SSOT: Assets/Docs/DownedReviveSystemDesign.md §4.
///
/// [규칙]
/// 시전은 "E만 누르고 있는 2초"다. E를 떼거나 E 외의 키·마우스 클릭·패드 버튼이 하나라도 들어오면 캔슬.
/// 마우스 이동(카메라 회전)은 허용. 위치 밀림·HP 감소 캔슬은 Host(PlayerDownState)가 따로 판정한다 —
/// Host는 키 입력을 볼 수 없으므로 입력 캔슬만 이 컴포넌트가 신고한다.
///
/// [판정 방식]
/// 2초 시전·완료는 Host가 판정한다. 여기서는 요청 1회와 캔슬 신고 1회만 보낸다. 서버 수락 여부를
/// 기다리지 않고 요청 직후부터 입력을 감시하는데, 시작·캔슬 RPC는 같은 신뢰 채널로 순서가 보장되므로
/// 수락 전 캔슬도 "시작 → 캔슬" 순으로 처리된다. 거절되거나 이미 끝난 시전에 대한 캔슬은 Host에서 no-op.
///
/// [Interact 액션]
/// InputSystem_Actions의 Interact는 인터랙션 없는 Button이라 E 누름 즉시 OnInteract가 호출된다.
/// 액션에 Hold 등을 다시 걸면 요청 전송이 그만큼 늦어진다.
///
/// [배치]
/// Network Player Prefab에 Player, PlayerDownState, PlayerInput과 함께 추가.
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
    InputAction _interactAction;

    // 요청을 보낸 뒤 캔슬 신고 전까지 true. 서버 수락과 무관하게 입력 감시 구간을 뜻한다.
    bool _casting;

    void Awake()
    {
        _player = GetComponent<Player>();
        _downState = GetComponent<PlayerDownState>();
        _interactAction = GetComponent<PlayerInput>()?.actions?.FindAction("Interact");
    }

    /// <summary>PlayerInput SendMessages — Interact 액션과 매핑.</summary>
    public void OnInteract(InputValue value)
    {
        if (!value.isPressed || _casting) return; // 눌림만 (PlayerPunch.OnAttack과 동일 패턴)
        if (!IsOwner || _player == null || _player.IsDead || _player.IsDowned) return;
        if (InGameChatUI.IsChatOpen || TutorialCheerNameUI.IsOpen || CursorUnlockRequestUtil.IsRequested) return;

        // 다른 입력을 누른 채로 시작하면 곧바로 캔슬되므로 애초에 보내지 않는다.
        if (IsAnyOtherButtonHeld()) return;

        var target = FindNearestRevivableTeammate();
        if (target == null) return;

        _downState.RequestStartRevive(target.NetworkObjectId);
        _casting = true;
    }

    void Update()
    {
        if (!_casting) return;

        bool keepCasting = _interactAction != null && _interactAction.IsPressed()
            && !IsAnyOtherButtonHeld()
            && !_player.IsDead && !_player.IsDowned;
        if (keepCasting) return;

        _casting = false;
        _downState.RequestCancelRevive();
    }

    /// <summary>
    /// Interact에 바인딩된 컨트롤을 뺀 모든 물리 버튼(키보드 키·마우스 클릭·패드 버튼) 중 눌린 게 있는지.
    /// synthetic(anyKey, 스틱/마우스 델타의 방향 버튼 등)과 noisy는 제외 — 마우스 이동은 여기서 걸리지 않는다.
    /// </summary>
    bool IsAnyOtherButtonHeld()
    {
        var devices = InputSystem.devices;
        for (int d = 0; d < devices.Count; d++)
        {
            var controls = devices[d].allControls;
            for (int i = 0; i < controls.Count; i++)
            {
                if (controls[i] is not ButtonControl button) continue;
                if (button.synthetic || button.noisy || !button.isPressed) continue;
                if (IsInteractControl(button)) continue;
                return true;
            }
        }
        return false;
    }

    bool IsInteractControl(InputControl control)
    {
        if (_interactAction == null) return false;
        var bound = _interactAction.controls;
        for (int i = 0; i < bound.Count; i++)
            if (bound[i] == control) return true;
        return false;
    }

    PlayerDownState FindNearestRevivableTeammate()
    {
        PlayerDownState nearest = null;
        float nearestSqr = detectRange * detectRange;

        foreach (var other in PlayerDownState.AllSpawned)
        {
            if (other == null || other == _downState || !other.IsRevivable) continue;

            float sqr = (other.transform.position - transform.position).sqrMagnitude;
            if (sqr > nearestSqr) continue;

            nearest = other;
            nearestSqr = sqr;
        }

        return nearest;
    }
}
