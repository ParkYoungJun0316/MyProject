using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 씬 인트로 대화 UI → StageStartGate Arm 흐름을 제어하는 네트워크 컨트롤러.
///
/// [동작]
/// 1. 씬 최초 진입: Host가 DialogueUI StartSequence → ClientRpc로 Client도 열기
/// 2. Host만 Space로 줄 넘김 → SyncLineClientRpc → Client 동일 줄 표시
/// 3. 마지막 줄 완료 → StageStartGate.Arm() (Host + ArmGateClientRpc)
/// 4. 씬 리로드(사망): GameSession.IsIntroSeen = true → 대화 스킵 → Gate 바로 Arm
///
/// [배치]
/// - M.Stage1 씬 내 StageNetworkState GameObject (NetworkObject) 에 컴포넌트 추가
/// - StageStartGate1.armOnStart = false 로 변경
/// - PhaseManager.onPhaseEnter 에서 기존 StageStartGate.Arm 연결 제거
/// - dialogueUI : UI 프리팹 인스턴스 내 Dialogue_Panel 의 DialogueUI 연결
/// - stageStartGate : StageStartGate1 연결
/// - showOnceKey : 씬별 고유 문자열 (예: "M.Stage1")
/// </summary>
public class DialogueGateController : NetworkBehaviour
{
    [Header("연결")]
    [Tooltip("씬 내 Dialogue_Panel 의 DialogueUI 컴포넌트")]
    [SerializeField] DialogueUI dialogueUI;

    [Tooltip("대화 완료 후 Arm 할 StageStartGate")]
    [SerializeField] StageStartGate stageStartGate;

    [Header("설정")]
    [Tooltip("씬별 고유 키. 타이틀 복귀 전까지 이 키가 seen 이면 대화를 스킵.\n예) M.Stage1 / T.Stage1")]
    [SerializeField] string showOnceKey = "M.Stage1";

    bool _initDone;

    // ── 진입점 ────────────────────────────────────────────────────

    /// <summary>오프라인 진입점 (NGO 없음).</summary>
    void Start()
    {
        if (!LobbyContext.IsOffline) return;
        Init(isHost: true);
    }

    /// <summary>온라인 진입점. NetworkObject 스폰 후 호출.</summary>
    public override void OnNetworkSpawn()
    {
        if (LobbyContext.IsOffline) return;
        Init(isHost: IsServer);
    }

    public override void OnNetworkDespawn()
    {
        UnsubscribeComplete();
    }

    void OnDestroy()
    {
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
                if (!LobbyContext.IsOffline)
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
            if (!LobbyContext.IsOffline)
                OpenDialogueClientRpc();
        }
        // Client: OpenDialogueClientRpc 수신 대기
    }

    // ── 입력 (Host / 오프라인 전용) ──────────────────────────────

    void Update()
    {
        if (dialogueUI == null || !dialogueUI.IsPlaying) return;

        bool isHostOrOffline = LobbyContext.IsOffline
            || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);
        if (!isHostOrOffline) return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            AdvanceLine();
    }

    void AdvanceLine()
    {
        int next = dialogueUI.CurrentLineIndex + 1;

        if (next >= dialogueUI.LineCount)
        {
            // 마지막 줄 완료
            dialogueUI.Hide();
            UnsubscribeComplete();

            stageStartGate?.Arm();

            if (!LobbyContext.IsOffline)
            {
                CompleteDialogueClientRpc();
                ArmGateClientRpc();
            }
        }
        else
        {
            dialogueUI.ShowLine(next);
            if (!LobbyContext.IsOffline)
                SyncLineClientRpc(next);
        }
    }

    // ── OnSequenceComplete 구독 (handleInputLocally 경로 안전망) ──

    void SubscribeComplete()
    {
        if (dialogueUI != null)
            dialogueUI.OnSequenceComplete.AddListener(OnDialogueComplete);
    }

    void UnsubscribeComplete()
    {
        if (dialogueUI != null)
            dialogueUI.OnSequenceComplete.RemoveListener(OnDialogueComplete);
    }

    void OnDialogueComplete()
    {
        // AdvanceLine 에서 이미 처리하므로 중복 방지
        // handleInputLocally=true 경로로 완료됐을 경우 Gate Arm 안전망
        stageStartGate?.Arm();
        UnsubscribeComplete();

        if (!LobbyContext.IsOffline)
            ArmGateClientRpc();
    }

    // ── ClientRpc ────────────────────────────────────────────────

    /// <summary>Host: 최초 진입 시 Client의 DialogueUI를 열어줌.</summary>
    [ClientRpc]
    void OpenDialogueClientRpc()
    {
        if (IsServer) return;
        dialogueUI?.StartSequence();
    }

    /// <summary>Host: Space로 줄 넘길 때 Client 동기화.</summary>
    [ClientRpc]
    void SyncLineClientRpc(int index)
    {
        if (IsServer) return;
        dialogueUI?.ShowLine(index);
    }

    /// <summary>Host: 대화 완료 시 Client DialogueUI 숨김.</summary>
    [ClientRpc]
    void CompleteDialogueClientRpc()
    {
        if (IsServer) return;
        dialogueUI?.Hide();
    }

    /// <summary>Host: Gate Arm 을 Client에 브로드캐스트.</summary>
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
