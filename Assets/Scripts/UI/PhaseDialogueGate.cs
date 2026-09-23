using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Phase 진입 시 대화(DialogueUI)를 띄우고, 전원이 다 읽으면(또는 스킵되면) OnAllReady를
/// 발동하는 범용 게이트.
///
/// [범용 대화 게이트 — 씬 인트로 / Phase 중간 대화 공용 SSOT]
/// 트리거를 OnNetworkSpawn 1회로 고정하지 않고 **`Begin()`을 호출하는 시점**을 자유롭게 둬서,
/// 씬 최초 진입(예: Phase0 onPhaseEnter에서 1회) / Phase 중간 전환(각 Phase onPhaseEnter마다)
/// 양쪽 모두를 이 컴포넌트 하나로 커버한다. 완료 동작도 하드코딩하지 않고 Inspector에서
/// 배선하는 OnAllReady UnityEvent로 둬서, Phase마다 다음 단계(Gate Arm / StageManager.StartStage
/// 등)를 다르게 연결할 수 있다.
///
/// [권한]
/// "이미 봤는가" 판정은 Host의 GameSession 상태만 신뢰한다 (Client 로컬 상태는 사용하지 않음).
/// Host가 열지/스킵할지 결정해 ClientRpc로 알린다. 완료 집계도 Host에서만: Client는 ServerRpc로
/// 보고, Host가 전원 완료 확인 후 ClientRpc로 OnAllReady를 전파한다. Host 자신은 로컬에서 직접 발동.
///
/// [씬 설정]
/// 1. 스폰된 NetworkObject(예: StageNetworkState가 붙은 오브젝트)에 씬 인트로/Phase별로 하나씩 부착
/// 2. dialogueUI : 이 대화 전용 Dialogue_Panel (handleInputLocally=true 필수). 비우면 대화 없이 즉시 OnAllReady
/// 3. showOnceKey : 씬 안에서 고유한 키 (예: "Stage4.1") — 사망 리로드 후 재관람 방지. 비우면 매번 다시 표시.
///    실제 기록 키는 코드가 씬 이름을 앞에 붙인다("M.Stage4/Stage4.1") — M/T가 같은 키를 써도 안 겹친다
/// 4. 씬 최초 진입이면 Phase0 onPhaseEnter / Phase 중간이면 해당 Phase onPhaseEnter → 이 컴포넌트의 Begin() 연결
/// 5. OnAllReady → StageStartGate.Arm 또는 StageManager.StartStage 등 다음 단계 연결
/// </summary>
public class PhaseDialogueGate : NetworkBehaviour
{
    [Header("연결")]
    [Tooltip("이 Phase 전용 Dialogue_Panel (handleInputLocally=true 필수). 비우면 대화 없이 즉시 OnAllReady.")]
    [SerializeField] DialogueUI dialogueUI;

    [Header("설정")]
    [Tooltip("Phase별 고유 키. 사망 리로드 후 이 키가 seen이면 대화를 스킵.\n예) M.Stage4.1. 비우면 항상 다시 표시.")]
    [SerializeField] string showOnceKey = "";

    [Header("이벤트")]
    [Tooltip("전원이 대화를 다 읽었을 때(또는 스킵됐을 때) 발동. 이 Phase의 다음 단계를 연결.")]
    public UnityEvent OnAllReady;

    bool _started;
    readonly HashSet<ulong> _doneClientIds = new();

    public override void OnNetworkDespawn() => UnsubscribeComplete();

    public override void OnDestroy()
    {
        base.OnDestroy();
        UnsubscribeComplete();
    }

    // ── 진입점 ────────────────────────────────────────────────────

    /// <summary>
    /// PhaseManager의 해당 Phase onPhaseEnter에 연결. Host/Client 양쪽에서 각자 로컬로 호출됨
    /// (PhaseManager.EnterPhase/EnterPhaseOnClient 둘 다 onPhaseEnter를 발동하므로) —
    /// 실제 판정·진행은 Host 레인에서만 하고, Client는 Host의 RPC만 따른다.
    /// </summary>
    public void Begin()
    {
        if (_started) return;
        _started = true;

        if (!IsServer) return;

        // [2026-09-24] 씬 이름을 붙인다 — M.Stage1·T.Stage1이 둘 다 "Stage1"을 써서 한 판을 이어 하면
        // T 쪽 인트로가 "이미 봄"으로 스킵되던 충돌 제거.
        string seenKey = string.IsNullOrEmpty(showOnceKey)
            ? null
            : SceneManager.GetActiveScene().name + "/" + showOnceKey;

        bool alreadySeen = seenKey != null
            && GameSession.Instance != null
            && GameSession.Instance.IsIntroSeen(seenKey);

        if (alreadySeen || dialogueUI == null)
        {
            InvokeAllReadyClientRpc();
            OnAllReady?.Invoke();
            return;
        }

        if (seenKey != null)
            GameSession.Instance?.MarkIntroSeen(seenKey);

        SubscribeComplete();
        dialogueUI.StartSequence();
        OpenDialogueClientRpc();
    }

    // ── 완료 집계 (전원이 각자 다 읽어야 OnAllReady) ────────────────

    /// <summary>이 피어(Host 또는 Client)의 DialogueUI가 마지막 줄까지 완료됐을 때 호출됨.</summary>
    void OnLocalDialogueComplete()
    {
        UnsubscribeComplete();

        if (IsServer)
            MarkDone(NetworkManager.Singleton.LocalClientId);
        else
            ReportDoneServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void ReportDoneServerRpc(RpcParams rpcParams = default)
    {
        MarkDone(rpcParams.Receive.SenderClientId);
    }

    void MarkDone(ulong clientId)
    {
        _doneClientIds.Add(clientId);

        int expected = GameSession.Instance != null
            ? GameSession.Instance.ActivePlayerCount
            : NetworkManager.Singleton.ConnectedClientsList.Count;

        if (_doneClientIds.Count < expected) return;

        InvokeAllReadyClientRpc();
        OnAllReady?.Invoke();
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

    /// <summary>Host: 대화가 필요한 경우 Client의 DialogueUI를 열어줌. Client는 자기 완료를 스스로 구독.</summary>
    [ClientRpc]
    void OpenDialogueClientRpc()
    {
        if (IsServer) return;
        SubscribeComplete();
        dialogueUI?.StartSequence();
    }

    /// <summary>Host: 스킵 또는 전원 완료 확인 후 OnAllReady를 Client에 전파.</summary>
    [ClientRpc]
    void InvokeAllReadyClientRpc()
    {
        if (IsServer) return;
        OnAllReady?.Invoke();
    }
}
