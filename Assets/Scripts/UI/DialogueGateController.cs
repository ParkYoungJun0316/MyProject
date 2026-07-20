using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 씬 인트로 대화 UI → StageStartGate Arm 흐름을 제어하는 네트워크 컨트롤러.
///
/// [동작 — 각자 로컬로 읽기]
/// 1. 씬 최초 진입: Host가 DialogueUI StartSequence → ClientRpc로 Client도 열기
/// 2. 각 피어(Host/Client)는 자기 화면에서 각자 Space로 줄을 넘김 (DialogueUI.handleInputLocally=true).
///    줄 넘김 자체는 동기화하지 않음 — 읽는 속도는 순수 로컬.
/// 3. 각 피어가 자기 대화를 끝까지 읽으면 완료를 Host에 보고(Client는 ServerRpc, Host는 로컬 직접 집계).
///    Host가 전원 완료를 확인하면 StageStartGate.Arm() (Host + ArmGateClientRpc)
/// 4. 씬 리로드(사망): GameSession.IsIntroSeen = true → 대화 스킵 → Gate 바로 Arm
///
/// [배치]
/// - M.Stage1 씬 내 StageNetworkState GameObject (NetworkObject) 에 컴포넌트 추가
/// - StageStartGate1.armOnStart = false 로 변경
/// - PhaseManager.onPhaseEnter 에서 기존 StageStartGate.Arm 연결 제거
/// - dialogueUI : UI 프리팹 인스턴스 내 Dialogue_Panel 의 DialogueUI 연결 (handleInputLocally=true 필수)
/// - stageStartGate : StageStartGate1 연결
/// - showOnceKey : 씬별 고유 문자열 (예: "M.Stage1")
/// </summary>
public class DialogueGateController : NetworkBehaviour
{
    [Header("연결")]
    [Tooltip("씬 내 Dialogue_Panel 의 DialogueUI 컴포넌트 (handleInputLocally=true 필수)")]
    [SerializeField] DialogueUI dialogueUI;

    [Tooltip("대화 완료 후 Arm 할 StageStartGate")]
    [SerializeField] StageStartGate stageStartGate;

    [Header("설정")]
    [Tooltip("씬별 고유 키. 타이틀 복귀 전까지 이 키가 seen 이면 대화를 스킵.\n예) M.Stage1 / T.Stage1")]
    [SerializeField] string showOnceKey = "M.Stage1";

    bool _initDone;

    /// <summary>Host: 자기 자신 포함 완료 보고한 clientId 집합. 중복 집계 방지.</summary>
    readonly HashSet<ulong> _doneClientIds = new();

    // ── 진입점 ────────────────────────────────────────────────────

    /// <summary>NetworkObject 스폰 후 호출.</summary>
    public override void OnNetworkSpawn()
    {
        Init(isHost: IsServer);
    }

    public override void OnNetworkDespawn()
    {
        UnsubscribeComplete();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        UnsubscribeComplete();
    }

    // ── 초기화 ────────────────────────────────────────────────────

    void Init(bool isHost)
    {
        if (_initDone) return;
        _initDone = true;

        bool alreadySeen = GameSession.Instance != null && GameSession.Instance.IsIntroSeen(showOnceKey);

        if (alreadySeen)
        {
            // 이미 봤음(사망 리로드 등) → 대화 없이 즉시 Gate Arm
            if (isHost)
            {
                stageStartGate?.Arm();
                ArmGateClientRpc();
            }
            return;
        }

        // 최초 진입: Host가 본 기록 등록 + 대화 시작
        if (isHost)
        {
            GameSession.Instance?.MarkIntroSeen(showOnceKey);
            SubscribeComplete();
            dialogueUI?.StartSequence();
            OpenDialogueClientRpc();
        }
        // Client: OpenDialogueClientRpc 수신 대기
    }

    // ── 완료 집계 (전원이 각자 다 읽어야 Arm) ─────────────────────

    /// <summary>
    /// 이 피어(Host 또는 Client)의 DialogueUI가 마지막 줄까지 완료됐을 때 호출됨.
    /// Host는 직접 집계, Client는 ServerRpc로 Host에 보고.
    /// </summary>
    void OnLocalDialogueComplete()
    {
        UnsubscribeComplete();

        if (IsServer)
            MarkDone(NetworkManager.Singleton.LocalClientId);
        else
            ReportDialogueDoneServerRpc();
    }

    /// <summary>Client → Host: 자기 대화 완료 보고.</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void ReportDialogueDoneServerRpc(RpcParams rpcParams = default)
    {
        MarkDone(rpcParams.Receive.SenderClientId);
    }

    /// <summary>Host: 완료 집계. 참가 인원 전원 완료 시 Gate Arm.</summary>
    void MarkDone(ulong clientId)
    {
        _doneClientIds.Add(clientId);

        int expected = GameSession.Instance != null
            ? GameSession.Instance.ActivePlayerCount
            : NetworkManager.Singleton.ConnectedClientsList.Count;

        if (_doneClientIds.Count < expected) return;

        stageStartGate?.Arm();
        ArmGateClientRpc();
    }

    // ── OnSequenceComplete 구독 ────────────────────────────────────

    void SubscribeComplete()
    {
        if (dialogueUI != null)
            dialogueUI.OnSequenceComplete.AddListener(OnLocalDialogueComplete);
    }

    void UnsubscribeComplete()
    {
        if (dialogueUI != null)
            dialogueUI.OnSequenceComplete.RemoveListener(OnLocalDialogueComplete);
    }

    // ── ClientRpc ────────────────────────────────────────────────

    /// <summary>Host: 최초 진입 시 Client의 DialogueUI를 열어줌. Client는 자기 완료를 스스로 구독.</summary>
    [ClientRpc]
    void OpenDialogueClientRpc()
    {
        if (IsServer) return;
        SubscribeComplete();
        dialogueUI?.StartSequence();
    }

    /// <summary>Host: 전원 완료 확인 후 Gate Arm 을 Client에 브로드캐스트.</summary>
    [ClientRpc]
    void ArmGateClientRpc()
    {
        if (IsServer) return;
        stageStartGate?.Arm();
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

    [ContextMenu("테스트: 이 씬 인트로 본 기록 제거 (재관람)")]
    void Debug_ClearThisKey()
    {
        // 오프라인 에디터에서 _initDone을 리셋해 재실행 가능하게 함
        _initDone = false;
        if (GameSession.Instance != null)
        {
            // GameSession에서 직접 제거 불가(setter 없음)이므로 ResetSession 대신 로그만
            Debug.Log($"[DialogueGateController] 재관람하려면 GameSession.ResetSession() 또는 씬 재시작 필요 (key: {showOnceKey})");
        }
    }
}
