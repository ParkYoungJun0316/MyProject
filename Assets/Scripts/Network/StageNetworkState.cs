using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 스테이지 네트워크 상태 중앙 허브. NetworkBehaviour.
/// M.Stage1 / T.Stage1 씬 내 NetworkObject GameObject에 부착.
///
/// [역할]
/// - 플레이어 사망 신고 수신 → Host가 씬 리로드 (NetworkSceneManager)
/// - Phase 진행 상태 동기화 (CurrentPhase NetworkVariable)
///
/// [배치]
/// 각 스테이지 씬에 빈 GameObject → NetworkObject + StageNetworkState 추가.
///
/// [연결]
/// - StageResetOnPlayerDeath.DoReset() → NotifyPlayerDeathServerRpc()
/// - PhaseManager.EnterPhase() → SyncPhase(index) (Host에서만 호출)
/// </summary>
public class StageNetworkState : NetworkBehaviour
{
    public static StageNetworkState Instance { get; private set; }

    // 현재 Phase 인덱스 (Host가 쓰고 전원이 읽음)
    private readonly NetworkVariable<int> _currentPhase = new(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // 카운트다운 시작 서버 시간 (Host 기록 — 카운트다운 UI 동기화)
    private readonly NetworkVariable<double> _countdownStartServerTime = new(
        -1.0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // 카운트다운 활성 여부
    private readonly NetworkVariable<bool> _isCountdownActive = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // 스테이지 시작 서버 시간 (StartStage() 직전 Host 기록 — TimerUI 동기화)
    private readonly NetworkVariable<double> _stageStartServerTime = new(
        -1.0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool _resetPending;

    // Client-side 캐시 — SyncSurvivalRemainingClientRpc 매 틱 Find 방지
    private SurviveTimeObjective _surviveObjective;

    // ── 프로퍼티 ──────────────────────────────────────────────────

    public int    CurrentPhase            => _currentPhase.Value;
    public bool   IsCountdownActive        => _isCountdownActive.Value;
    public double CountdownStartServerTime => _countdownStartServerTime.Value;
    public double StageStartServerTime     => _stageStartServerTime.Value;

    // ── 초기화 ────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        _currentPhase.OnValueChanged += OnPhaseChanged;
        _surviveObjective = FindFirstObjectByType<SurviveTimeObjective>();
    }

    public override void OnNetworkDespawn()
    {
        _currentPhase.OnValueChanged -= OnPhaseChanged;
        if (Instance == this) Instance = null;
    }

    // ── 사망 처리 ─────────────────────────────────────────────────

    /// <summary>
    /// 플레이어 사망 시 어느 클라이언트에서든 호출.
    /// Host가 1명이라도 사망 신호를 받으면 전원 씬 리로드.
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void NotifyPlayerDeathServerRpc()
    {
        if (_resetPending) return;
        _resetPending = true;

        // 사망 리로드 시 새 시드 생성 + 전체 클라이언트에 배포
        int newSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        NetworkSessionData.Seed = newSeed;
        BroadcastNewSeedClientRpc(newSeed);

        string sceneName = SceneManager.GetActiveScene().name;
        Debug.Log($"[StageNetworkState] 사망 감지 — '{sceneName}' 리로드 (새 시드: {newSeed})");
        NetworkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    [ClientRpc]
    void BroadcastNewSeedClientRpc(int seed)
    {
        NetworkSessionData.Seed = seed;
    }

    // ── StartStage 동기화 ─────────────────────────────────────────

    /// <summary>
    /// Host의 StageStartGate 카운트다운이 완료되면 호출.
    /// Client 측 StartStage는 StageStartGate.UpdateCountdownOnClient()가
    /// _stageStartServerTime NetworkVariable을 감지해 inspector 참조로 직접 호출.
    /// </summary>
    [ClientRpc]
    public void BroadcastStartStageClientRpc()
    {
        if (IsServer) return;
        Debug.Log("[StageNetworkState] Client StartStage 동기화 완료");
    }

    // ── Breakable 파괴 동기화 ─────────────────────────────────────

    /// <summary>
    /// Host: Breakable 파괴 확정 시 Client에 stable ID 전달.
    /// Client: 정적 레지스트리에서 해당 ID의 Breakable을 찾아 파괴 연출 적용.
    /// </summary>
    [ClientRpc]
    public void SyncBreakClientRpc(int breakableId)
    {
        if (IsServer) return;
        Breakable.BreakById(breakableId);
        Debug.Log($"[StageNetworkState] Breakable(id={breakableId}) 파괴 동기화");
    }

    // ── 생존 타이머 동기화 ───────────────────────────────────────

    /// <summary>
    /// Host: SurviveTimeObjective UI 틱마다 호출.
    /// Client의 SurviveTimeObjective.NotifyRemainingTime()을 통해 TimerUI를 갱신.
    /// </summary>
    [ClientRpc]
    public void SyncSurvivalRemainingClientRpc(float remaining)
    {
        if (IsServer) return;
        _surviveObjective?.NotifyRemainingTime(remaining);
    }

    // ── 시간 동기화 (NetworkTime 기반) ───────────────────────────

    /// <summary>Host: 전원 점유 → 카운트다운 시작 시각을 ServerTime으로 기록.</summary>
    public void MarkCountdownStart()
    {
        if (!IsServer) return;
        _countdownStartServerTime.Value = NetworkManager.Singleton.ServerTime.Time;
        _isCountdownActive.Value = true;
    }

    /// <summary>Host: 이탈로 카운트다운 리셋 시 호출.</summary>
    public void MarkCountdownReset()
    {
        if (!IsServer) return;
        _isCountdownActive.Value = false;
    }

    /// <summary>
    /// Host: StartStage() 직전 서버 시간 기록.
    /// TimerUI가 이 값 기준으로 Host/Client 동일한 경과 시간을 계산.
    /// </summary>
    public void MarkStageStart()
    {
        if (!IsServer) return;
        _stageStartServerTime.Value = NetworkManager.Singleton.ServerTime.Time;
        _isCountdownActive.Value    = false;
    }

    // ── Phase 동기화 ──────────────────────────────────────────────

    /// <summary>
    /// Host에서 Phase가 바뀔 때 호출. NetworkVariable로 전원에 전달.
    /// PhaseManager.EnterPhase() 호출 후 호출.
    /// </summary>
    public void SyncPhase(int phaseIndex)
    {
        if (!IsServer) return;
        _currentPhase.Value = phaseIndex;
    }

    void OnPhaseChanged(int prev, int next)
    {
        // 비오너(Client)에서도 Phase 변경을 받을 수 있도록 PhaseManager에 알림
        if (!IsServer && PhaseManager.Instance != null)
            PhaseManager.Instance.EnterPhaseOnClient(next);

        Debug.Log($"[StageNetworkState] Phase 변경: {prev} → {next}");
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 사망 신고")]
    void Debug_Death() => NotifyPlayerDeathServerRpc();

    [ContextMenu("테스트: Phase 0으로 초기화")]
    void Debug_Phase0() => SyncPhase(0);
#endif
}
