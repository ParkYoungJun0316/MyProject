using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Interlude 씬(M.Boss ↔ T.Stage1 사이 인터미션) 네트워크 매니저. NetworkBehaviour.
/// CheerAndTutorialDesign.md §1.1 / §3.4 SSOT — CheerName/TeamCheerWord **2차 변경** 게이트만 담당.
///
/// [TutorialNetworkManager와의 차이 — §3.4.1]
/// 스폰·색배정·시드·세션시각·DisplayName·VoiceId 세션 확정은 전부 제외한다 — 이미 Tutorial 게이트
/// 통과 시점에 1회 확정되어 세션 전체(M1~M.Boss~T.Boss)에 재사용되는 값들이라 Interlude에서
/// 다시 손댈 필요가 없다. PlayerSpawnManager가 모든 스테이지 씬(Interlude 포함,
/// §3.4 코드 변경 #1 — PlayerSpawnManager.IsStageScene)에 공통 배치 스폰을 담당하므로
/// 이 클래스는 스폰을 직접 하지 않는다.
///
/// [역할]
/// - Host 전용 헤드카운트 게이트(TutorialGatherZone 재사용, 색 무관 단일 존, Tutorial과 동일 원칙)
///   → 카운트다운 완료 시:
///   ① PlayerCheerNameSync.BuildSessionCheerNames() → GameSession.SetSessionCheerNames + ClientRpc
///   ② CheerService.Instance.TeamCheerWord → GameSession.SetSessionTeamCheerWord + ClientRpc
///   ③ SceneFlowManager.Instance.LoadNextScene() → T.Stage1
///
/// [이탈 정책 — §3.4.3]
/// Interlude는 인게임으로 취급한다. 이 클래스는 이탈을 다루지 않음 — 씬에 DisconnectManager를
/// 반드시 별도 배치해서 누구든 이탈 시 방 전체를 종료한다(Tutorial식 "슬롯만 제거, 방 유지"
/// 관용 이탈 로직은 여기 없음 — 과거 버그2의 반대 방향 실수를 막는 지점).
///
/// [배치 — 사용자 에디터 작업]
/// Interlude 씬 빈 GameObject → NetworkObject + InterludeNetworkManager 추가.
/// 씬에 TutorialGatherZone을 1개 재배치(색 무관 단일 존) — Instance로 자동 참조, 별도 연결 불필요.
/// TutorialCheerNameUI 패널 + TutorialCheerNameSignboard 표지판도 그대로 재배치(코드 수정 없음).
/// </summary>
public class InterludeNetworkManager : NetworkBehaviour
{
    public static InterludeNetworkManager Instance { get; private set; }

    [Header("게이트 카운트다운 (Tutorial과 동일 패턴, §3.4)")]
    [Tooltip("전원 존 점유 유지 후 게이트 통과까지 걸리는 시간(초). 중간에 이탈/인원 변경 시 리셋.")]
    [SerializeField] private float gateCountdownDuration = 3f;

    [Header("게이트 이벤트 (UI 연결용)")]
    [Tooltip("매 프레임 카운트다운 남은 시간(0~gateCountdownDuration) 전달. 카운트다운 중 아니면 duration 값.")]
    public UnityEvent<float> OnGateCountdownTick;
    [Tooltip("카운트다운 리셋 시 호출 (이탈/인원 변경 등).")]
    public UnityEvent OnGateCountdownReset;
    [Tooltip("카운트다운 완료 직후, T.Stage1 로드 직전에 발동.")]
    public UnityEvent OnGateCountdownComplete;

    // ── 게이트 상태 (Host 전용) ──────────────────────────────────
    bool  _gateCompleted;
    bool  _isCounting;
    float _countdown;

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // 오설정 조기 경고(Host만). sceneSequence에 이 씬이 없으면 SceneFlowManager.CurrentSceneIndex가
        // -1이 되고, LoadNextScene()은 -1+1=0번 씬(M.Stage1)을 로드한다 — Tutorial은 바로 그 동작에
        // 의존하지만 Interlude에서는 M 구역으로 되돌아가는 무한 루프가 된다. 게이트까지 기다리지 않고
        // 씬 진입 즉시 드러낸다(2026-09-06 리뷰).
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsHost || IsInSceneSequence()) return;

        Debug.LogError("[InterludeNetworkManager] 이 씬이 SceneFlowManager.sceneSequence에 없습니다 — " +
                       "M.Boss와 T.Stage1 사이에 \"Interlude\"를 삽입하세요. " +
                       "지금 상태로 게이트를 통과하면 T.Stage1이 아니라 M.Stage1로 되돌아갑니다.", this);
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;
    }

    static bool IsInSceneSequence() =>
        SceneFlowManager.Instance != null && SceneFlowManager.Instance.CurrentSceneIndex >= 0;

    // ── 사전 게이트 (Host 전용, TutorialNetworkManager.UpdateGate와 동일 원칙) ──

    void Update()
    {
        // IsSpawned 가드 — 미스폰 상태로 CompleteGate에 들어가면 세션 재확정 ClientRpc 2개가
        // 유실되어 Client만 옛 CheerName/TeamCheerWord를 들고 T.Stage1에 들어가는 조용한 desync가
        // 된다(CheerService.ApplyTeamBuff와 동일한 방어 패턴).
        if (!IsSpawned || !IsHost || _gateCompleted) return;
        UpdateGate();
    }

    /// <summary>
    /// 존 안 인원 == 세션 접속 인원(헤드카운트, 색 무관)이면 카운트다운 진행.
    /// 도중 인원이 안 맞게 되면 즉시 리셋 — TutorialNetworkManager.UpdateGate와 동일.
    /// </summary>
    void UpdateGate()
    {
        var zone = TutorialGatherZone.Instance;
        if (zone == null) return;

        int connected = PlayerSpawnCoordinator.EntryCount;
        bool allIn = connected > 0 && zone.OccupantCount == connected;

        if (!allIn)
        {
            if (_isCounting) ResetGateCountdown();
            return;
        }

        if (!_isCounting)
        {
            _isCounting = true;
            _countdown  = gateCountdownDuration;
            OnGateCountdownTick?.Invoke(_countdown);
        }

        _countdown -= Time.deltaTime;
        OnGateCountdownTick?.Invoke(Mathf.Max(0f, _countdown));

        if (_countdown <= 0f)
            CompleteGate();
    }

    void ResetGateCountdown()
    {
        _isCounting = false;
        _countdown  = gateCountdownDuration;
        OnGateCountdownReset?.Invoke();
        OnGateCountdownTick?.Invoke(gateCountdownDuration);
    }

    /// <summary>
    /// 게이트 통과 확정. CheerName/TeamCheerWord 2차 변경분만 세션에 재확정하고 T.Stage1로
    /// 전환한다(§3.4.1 — 스폰/색배정/시드/세션시각/DisplayName/VoiceId는 Tutorial에서 이미
    /// 확정되어 그대로 재사용되므로 여기서 다시 계산하지 않는다).
    /// </summary>
    void CompleteGate()
    {
        if (_gateCompleted) return;
        _gateCompleted = true;

        // 오설정이면 진행하지 않는다 — 그대로 LoadNextScene()을 부르면 M.Stage1로 되돌아간다(Start 참고).
        if (!IsInSceneSequence())
        {
            Debug.LogError("[InterludeNetworkManager] 게이트 완료 — 이 씬이 SceneFlowManager.sceneSequence에 " +
                           "없어 다음 씬을 특정할 수 없습니다(그대로 진행하면 M.Stage1로 되돌아감). 씬 전환 중단.", this);
            return;
        }

        OnGateCountdownComplete?.Invoke();
        Debug.Log("[InterludeNetworkManager] 게이트 통과 — T.Stage1 진입 처리 시작");

        // CheerName 2차 확정 — Tutorial CompleteGate와 완전히 동일한 헬퍼 재사용(§3.4 코드 변경 #3).
        // 안 바꾼 플레이어는 기존 세션값(=실시간 NV, §3.4 코드 변경 #2로 이미 씨딩됨)이 그대로
        // 다시 확정되고, 바꾼 플레이어만 새 값으로 갈아치워진다.
        var sessionNames = PlayerCheerNameSync.BuildSessionCheerNames();
        GameSession.Instance?.SetSessionCheerNames(sessionNames);
        BroadcastSessionCheerNamesClientRpc(
            new FixedString32Bytes(sessionNames[0]), new FixedString32Bytes(sessionNames[1]),
            new FixedString32Bytes(sessionNames[2]), new FixedString32Bytes(sessionNames[3]));

        // TeamCheerWord 2차 확정 — CheerService NV는 이미 이 씬 OnNetworkSpawn에서 세션값으로
        // seed돼 있으므로(CheerService 기존 패턴), 여기서 그 값을 다시 GameSession에 되돌려
        // Host가 안 바꿨으면 그대로, 바꿨으면 새 값이 반영된다. Tutorial CompleteGate와 동일 패턴.
        string teamWord = CheerService.Instance != null
            ? CheerService.Instance.TeamCheerWord
            : GameSession.DefaultTeamCheerWord;
        GameSession.Instance?.SetSessionTeamCheerWord(teamWord);
        BroadcastSessionTeamCheerWordClientRpc(new FixedString32Bytes(teamWord));

        if (SceneFlowManager.Instance == null)
        {
            Debug.LogError("[InterludeNetworkManager] 게이트 완료 — SceneFlowManager.Instance null, 씬 전환 중단");
            return;
        }
        SceneFlowManager.Instance.LoadNextScene();
    }

    /// <summary>세션 확정 CheerName을 모든 클라이언트의 GameSession에 배포(TutorialNetworkManager와 동일 패턴).</summary>
    [ClientRpc]
    void BroadcastSessionCheerNamesClientRpc(FixedString32Bytes n0, FixedString32Bytes n1,
                                              FixedString32Bytes n2, FixedString32Bytes n3)
    {
        if (IsHost) return; // Host 자신은 CompleteGate()에서 이미 로컬 적용
        GameSession.Instance?.SetSessionCheerNames(
            new[] { n0.ToString(), n1.ToString(), n2.ToString(), n3.ToString() });
    }

    /// <summary>세션 확정 TeamCheerWord를 모든 클라이언트의 GameSession에 배포(TutorialNetworkManager와 동일 패턴).</summary>
    [ClientRpc]
    void BroadcastSessionTeamCheerWordClientRpc(FixedString32Bytes word)
    {
        if (IsHost) return; // Host 자신은 CompleteGate()에서 이미 로컬 적용
        GameSession.Instance?.SetSessionTeamCheerWord(word.ToString());
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 게이트 상태 출력")]
    void Debug_PrintState()
    {
        Debug.Log($"[InterludeNetworkManager] gateCompleted={_gateCompleted} isCounting={_isCounting} countdown={_countdown} " +
                  $"occupants={(TutorialGatherZone.Instance != null ? TutorialGatherZone.Instance.OccupantCount.ToString() : "N/A")} " +
                  $"connected={PlayerSpawnCoordinator.EntryCount}");
    }
#endif
}
