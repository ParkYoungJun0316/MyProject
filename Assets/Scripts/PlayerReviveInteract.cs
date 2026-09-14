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
/// [시작 판정 — 매 프레임 폴링 (2026-09-15)]
/// 예전엔 Interact press 순간(OnInteract) 1회만 판정해서, W를 누른 채 E를 누르면 조용히 버려지고
/// 이후 W를 떼도 E를 다시 누르기 전까지 시작되지 않았다. 지금은 "E 누르는 중 + 다른 버튼 없음 + 후보 있음 +
/// 시전 중 아님"이 되는 첫 프레임에 요청한다. 한 번 보낸 요청은 E를 뗄 때까지 재전송하지 않는다
/// (Host 측 캔슬·거절 뒤 자동 재시작 방지 — 다시 하려면 E를 새로 누른다).
///
/// [후보 사거리]
/// Host 검증 사거리(PlayerDownState.ReviveRange)보다 clientRangeMargin만큼 좁게 잡는다. 클라이언트가 더 넓으면
/// "보냈는데 Host가 거절"하는 구간이 생기고, 그 구간에서 안내 UI가 떠 있으면 누르는데도 안 되는 것처럼 보인다.
///
/// [판정 방식]
/// 2초 시전·완료는 Host가 판정한다. 여기서는 요청 1회와 캔슬 신고 1회만 보낸다. 서버 수락 여부를
/// 기다리지 않고 요청 직후부터 입력을 감시하는데, 시작·캔슬 RPC는 같은 신뢰 채널로 순서가 보장되므로
/// 수락 전 캔슬도 "시작 → 캔슬" 순으로 처리된다. 거절되거나 이미 끝난 시전에 대한 캔슬은 Host에서 no-op.
///
/// [UI]
/// ReviveInteractPromptUI가 CurrentTarget / CastTarget을 매 프레임 읽어 안내·진행·캔슬 문구를 표시한다(읽기 전용).
///
/// [배치]
/// Network Player Prefab에 Player, PlayerDownState, PlayerInput과 함께 추가.
/// </summary>
[RequireComponent(typeof(Player))]
[RequireComponent(typeof(PlayerDownState))]
public class PlayerReviveInteract : NetworkBehaviour
{
    [Tooltip("후보 탐색 사거리 = Host의 PlayerDownState.reviveRange − 이 값(m). 클라이언트를 Host보다 좁게 잡아 " +
        "CNT 보간 오차가 있어도 '안내가 떴는데 거절됨'이 생기지 않게 한다.")]
    [SerializeField] float clientRangeMargin = 0.3f;

    Player _player;
    PlayerDownState _downState;
    InputAction _interactAction;

    // 요청을 보낸 뒤 캔슬 신고 전까지 true. 서버 수락과 무관하게 입력 감시 구간을 뜻한다.
    bool _casting;

    // 시작 요청 후 E를 뗄 때까지 true — 같은 홀드 안에서 재요청하지 않는다.
    bool _requestedThisHold;

    /// <summary>Owner 전용: 지금 E를 누르면 시전을 요청할 수 있는 가장 가까운 다운 팀원. 없으면 null.</summary>
    public PlayerDownState CurrentTarget { get; private set; }

    /// <summary>Owner 전용: 이번 홀드에서 시전을 요청한 대상. 입력 감시 구간(캔슬 신고 전)에만 non-null.</summary>
    public PlayerDownState CastTarget { get; private set; }

    void Awake()
    {
        _player = GetComponent<Player>();
        _downState = GetComponent<PlayerDownState>();
        _interactAction = GetComponent<PlayerInput>()?.actions?.FindAction("Interact");
    }

    void Update()
    {
        if (!IsOwner)
        {
            CurrentTarget = null;
            return;
        }

        bool canAct = _player != null && !_player.IsDead && !_player.IsDowned && !IsBlockedByUI();
        CurrentTarget = canAct ? FindNearestRevivableTeammate() : null;

        bool interactHeld = _interactAction != null && _interactAction.IsPressed();

        if (_casting)
        {
            var otherButton = FindOtherHeldButton();
            if (interactHeld && canAct && otherButton == null) return;

            string reason = !interactHeld ? "E 해제"
                : otherButton != null ? $"다른 버튼 입력: {otherButton.path}"
                : _player == null || _player.IsDead ? "본인 사망"
                : _player.IsDowned ? "본인 다운"
                : $"UI 차단(chat={InGameChatUI.IsChatOpen} cheerName={TutorialCheerNameUI.IsOpen} cursor={CursorUnlockRequestUtil.IsRequested})";
            Debug.Log($"[PlayerReviveInteract] 부활 캔슬 신고 — reason={reason}");

            _casting = false;
            CastTarget = null;
            _downState.RequestCancelRevive();
        }

        if (!interactHeld)
        {
            _requestedThisHold = false;
            return;
        }

        if (_requestedThisHold || CurrentTarget == null) return;

        // 다른 입력을 누른 채로 시작하면 곧바로 캔슬되므로 보내지 않고 기다린다(떼는 순간 시작).
        if (IsAnyOtherButtonHeld()) return;

        _downState.RequestStartRevive(CurrentTarget.NetworkObjectId);
        CastTarget = CurrentTarget;
        _casting = true;
        _requestedThisHold = true;
    }

    static bool IsBlockedByUI() =>
        InGameChatUI.IsChatOpen || TutorialCheerNameUI.IsOpen || CursorUnlockRequestUtil.IsRequested;

    /// <summary>
    /// Interact에 바인딩된 컨트롤을 뺀 모든 물리 버튼(키보드 키·마우스 클릭·패드 버튼) 중 눌린 게 있는지.
    /// synthetic(anyKey, 스틱/마우스 델타의 방향 버튼 등)과 noisy는 제외 — 마우스 이동은 여기서 걸리지 않는다.
    /// </summary>
    bool IsAnyOtherButtonHeld() => FindOtherHeldButton() != null;

    ButtonControl FindOtherHeldButton()
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
                return button;
            }
        }
        return null;
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
        float range = Mathf.Max(0f, _downState.ReviveRange - clientRangeMargin);
        PlayerDownState nearest = null;
        float nearestSqr = range * range;

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
