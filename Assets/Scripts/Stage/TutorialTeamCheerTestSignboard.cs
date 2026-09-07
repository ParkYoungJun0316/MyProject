using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Tutorial 팀 응원 연습 표지판(구 은신 자리). `TutorialCheerNameSignboard`와 동일한 상호작용
/// 패턴(E키, 근접 트리거, 로컬 프롬프트)을 따르되, 여는 대상이 UI 패널이 아니라 씬에 실제로
/// 배치된 <see cref="MouthController"/>(mouth0, teamCheerHazard=true)라는 점만 다르다.
///
/// [왜 "연습"이 아니라 진짜 함정인가]
/// `CheerAndTutorialDesign.md`의 "말해보기 = 진짜 응원 제출 그 자체" 원칙과 동일하게, 이 표지판도
/// 가짜 UI 시뮬레이션을 만들지 않는다. E를 누르면 mouth0가 실제로 Warning → Close → Hold까지
/// 진행하고, 팀원이 실제로 팀워드를 외치면 `SubmitTeamCheerServerRpc` → 투표 →
/// `MouthController.Revert()` → Open → `TeamCheerCleared` 배너까지 전부 실제 경로 그대로
/// 발동한다. 이 스크립트는 그 창을 "여는" 신호만 전원에게 동시에 쏴 준다.
///
/// [동기화가 필요한 이유]
/// 실제 함정은 전 머신이 같은 시드(`PhaseStartServerTime` 등)로 스스로 같은 순간에 창을 열어
/// "동시성"이 저절로 생긴다. E 상호작용은 누른 사람 로컬 입력이라 그 동시성이 없다 — 그래서
/// Host를 거쳐 전원에게 브로드캐스트하고, 각자 로컬 mouth0에 `StartSingleHazardWindow()`를
/// 호출한다(MouthController 자체는 NetworkBehaviour가 아니라 RPC가 없다).
///
/// [설정 방법]
/// 1. 이 스크립트 + Collider(Is Trigger) + NetworkObject를 씬 배치 오브젝트에 부착(구 은신 자리)
/// 2. mouthController에 씬의 mouth0(MouthController) 연결
///    - mouth0 쪽: teamCheerHazard = true, startOnAwake = false(자동 랜덤 사이클 금지, E로만 시작)
///    - screenFader는 비워둘 것(테스트에서 화면 암전 없이 입 애니메이션만 보이게)
/// 3. promptRoot에 "[E] 팀 응원 연습" 안내 UI 연결 — 기본 비활성 권장
/// 4. CheerService가 이미 이 씬(Tutorial)에 배치돼 있어야 동작(Phase D0 완료 전제)
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(NetworkObject))]
public class TutorialTeamCheerTestSignboard : NetworkBehaviour
{
    [Tooltip("Tutorial 씬의 mouth0(MouthController). teamCheerHazard=true, startOnAwake=false로 설정.")]
    [SerializeField] MouthController mouthController;

    [Tooltip("근처에 있을 때만 보이는 \"[E] 팀 응원 연습\" 프롬프트. 비워도 동작(프롬프트 없이 상호작용만).")]
    [SerializeField] GameObject promptRoot;

    // E를 누른 뒤 Host 왕복(RTT)이 끝나기 전까진 로컬 입이 아직 Idle이라 연타가 그대로 중복
    // 요청이 된다. Host도 창 상태로 거르지만(권한 판정) 불필요한 트래픽은 여기서 끊는다.
    // 요청이 거절·유실됐을 때 프롬프트가 영구히 잠기지 않도록 짧은 만료를 둔다.
    const float RequestGrace = 1f;

    bool _localPlayerInRange;
    float _requestExpiresAt = -1f;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        SetPromptVisible(false);

        if (mouthController == null)
            Debug.LogWarning("[TutorialTeamCheerTestSignboard] mouthController가 비어 있어 연습 창을 열 수 없습니다. " +
                             "씬의 mouth0(MouthController)를 연결하세요.", this);
    }

    void OnTriggerEnter(Collider other) => TrySetRange(other, true);

    void OnTriggerExit(Collider other) => TrySetRange(other, false);

    void TrySetRange(Collider other, bool inRange)
    {
        Player p = other.GetComponentInParent<Player>();
        if (p == null) return;

        NetworkObject netObj = p.GetComponent<NetworkObject>();
        if (netObj == null || !netObj.IsOwner) return; // 남의 캐릭터는 내 화면 프롬프트와 무관

        _localPlayerInRange = inRange;
        if (!inRange) SetPromptVisible(false);
    }

    void Update()
    {
        if (!_localPlayerInRange || mouthController == null) return;

        // 창이 열려 있는 동안(Warning~Open)엔 프롬프트를 숨겨 중복 상호작용을 막는다.
        bool windowOpen = mouthController.IsHazardWindowOpen;
        if (windowOpen) _requestExpiresAt = -1f;

        bool waitingForHost = Time.time < _requestExpiresAt;
        SetPromptVisible(!windowOpen && !waitingForHost);
        if (windowOpen || waitingForHost) return;

        // 채팅·치어네임 입력창이 열려 있으면 타이핑한 'e'가 상호작용으로 새지 않게 양보
        // (CheerDigitInput·MicMuteHotkeyUI·PlayerEmoteMenuUI와 동일 게이팅).
        if (InGameChatUI.IsChatOpen || TutorialCheerNameUI.IsOpen) return;

        // 씬 언로드·미스폰 중엔 Rpc 호출 자체가 예외가 된다(TutorialNetworkManager와 동일 가드).
        if (!IsSpawned) return;

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            _requestExpiresAt = Time.time + RequestGrace;
            RequestStartRpc();
        }
    }

    /// <summary>Client(전원) → Host. 창을 열어도 되는지는 Host가 판정한다(함정 스케줄 권한 = Host).</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void RequestStartRpc()
    {
        if (mouthController == null || mouthController.IsHazardWindowOpen) return;
        BroadcastStartClientRpc();
    }

    [ClientRpc]
    void BroadcastStartClientRpc()
    {
        if (mouthController != null)
            mouthController.StartSingleHazardWindow();
    }

    void SetPromptVisible(bool visible)
    {
        if (promptRoot != null) promptRoot.SetActive(visible);
    }
}
