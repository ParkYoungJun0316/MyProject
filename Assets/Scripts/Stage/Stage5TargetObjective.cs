using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

/// <summary>
/// Stage5 타겟 잡기 오브젝티브.
/// StageObjective를 상속. StageManager의 objectives[]에 등록하여 사용.
///
/// [동작]
/// - Begin(): 기존 타겟 정리 + 새 타겟 스폰 + 타이머 시작
///   - PhaseManager 리셋 중(gameObject 비활성) 호출 시 스폰 건너뜀
///   - StageManager.StartStage() → Begin() 재호출 시 정상 스폰
/// - Tick(): 타이머 업데이트 + 타임아웃 판정
///   - 접촉 성공이 같은 프레임에 있으면 타임아웃보다 우선 처리됨
/// - 포획 수 >= requiredCaptures → Complete()
/// - 타임아웃 → 남은 타겟 소멸 + Fail()
///
/// [리셋 연동]
/// 리셋은 소프트 리셋 경로가 없다 — 사망/실패는 항상 §11 사망 문(전원 씬 리로드)으로 재진입한다
/// (NetworkDesign.md §11A.3). 이 씬이 재로드되면 Awake/Begin이 전부 새로 실행되므로
/// 별도 ResetObjective 류 호출은 불필요.
/// PhaseManager가 objectsToDisable/objectsToEnable로 이 오브젝트를 비활성화한 상태에서
/// EnterPhase(onPhaseEnter)로 재활성될 수 있음 — 이때 Begin()이 비활성 중이면 스폰을 건너뜀.
///
/// [Inspector 설정]
/// - spawner: Stage5TargetSpawner 연결 필수
/// - captureTable: 인원별 스폰 수 + 포획 조건 테이블 (페이즈마다 다르게 설정)
/// - timeLimit: 제한 시간(초)
/// </summary>
public class Stage5TargetObjective : StageObjective
{
    [System.Serializable]
    public struct CaptureEntry
    {
        [Tooltip("활성 플레이어 수 (1~4)")]
        public int playerCount;
        [Tooltip("스폰 마릿수 및 성공에 필요한 포획 수")]
        public int count;
    }

    [Header("Stage5 설정")]
    [Tooltip("제한 시간 (초)")]
    public float timeLimit = 60f;

    [Tooltip("인원별 Runner 스폰 수 / 포획 조건 테이블.\n" +
             "스폰 수 = 포획 조건 (전부 잡아야 성공).\n" +
             "playerCount에 해당하는 행이 없으면 마지막 행으로 fallback.\n" +
             "예) stage5.2: 1인=1, 2인=2, 3인=3, 4인=3")]
    public CaptureEntry[] captureTable = new CaptureEntry[]
    {
        new CaptureEntry { playerCount = 1, count = 1 },
        new CaptureEntry { playerCount = 2, count = 1 },
        new CaptureEntry { playerCount = 3, count = 1 },
        new CaptureEntry { playerCount = 4, count = 1 },
    };

    // captureTable이 비어 있을 때 fallback
    [HideInInspector] public int requiredCaptures = 1;

    [Header("참조")]
    [Tooltip("4코너 스폰 + 색상 셔플을 담당하는 스포너")]
    public Stage5TargetSpawner spawner;
    [Tooltip("도망 목표 후보 노드. 빈 오브젝트 Transform 배열.\n" +
             "씬 오브젝트 참조라 프리팹에 못 넣으므로 여기서 주입.\n" +
             "예: 중앙 1개 + 스폰 코너 4개 = 총 5개 등록")]
    public Transform[] nodes;

    [Header("이벤트 (UI 연결용)")]
    [Tooltip("1초 간격 남은 시간 갱신. ObjectiveUI 등에 연결")]
    public UnityEvent<float> OnTimerChanged;
    [Tooltip("포획 카운트 변경 시. (현재 포획 수, 필요 포획 수)")]
    public UnityEvent<int, int> OnCaptureCountChanged;

    float _elapsed;
    int _capturedCount;
    bool _started;
    float _nextUITick;

    readonly List<Stage5TargetRunner> _activeTargets = new List<Stage5TargetRunner>();

    public float Remaining => Mathf.Max(0f, timeLimit - _elapsed);
    public int CapturedCount => _capturedCount;

    // ── StageObjective 구현 ──────────────────────────────────────

    public override void Begin()
    {
        _elapsed = 0f;
        _capturedCount = 0;
        _started = false;
        _nextUITick = 0f;

        // captureTable에서 현재 인원에 맞는 값을 읽어 설정
        requiredCaptures = ResolveCaptureCount();

        CleanupTargets();

        // Client HUD 동기화 구독(중복 방지를 위해 항상 해제 후 재구독).
        if (StageNetworkState.Instance != null)
        {
            StageNetworkState.Instance.OnStage5CaptureSync -= HandleCaptureSync;
            StageNetworkState.Instance.OnStage5CaptureSync += HandleCaptureSync;
            StageNetworkState.Instance.OnStage5RemainingSync -= HandleRemainingSync;
            StageNetworkState.Instance.OnStage5RemainingSync += HandleRemainingSync;
        }

        // PhaseManager 리셋 중 오브젝트가 비활성인 경우 스폰 건너뜀.
        // 이후 StageManager.StartStage() → Begin() 재호출 시 정상 스폰.
        if (!gameObject.activeInHierarchy) return;
        if (spawner == null)
        {
            Debug.LogWarning("[Stage5TargetObjective] spawner가 연결되지 않았습니다.");
            return;
        }

        Player[] players = FindObjectsByType<Player>(FindObjectsSortMode.None);

        // Client는 spawner.SpawnTargets()가 내부에서 빈 리스트를 반환한다(Host 전권 스폰,
        // TStageNetworkBoard.md §3.2) — 포획 판정도 Host-only라 Client가 로컬로 들고
        // 있을 이유가 없다. Client HUD는 아래 SyncStage5CaptureClientRpc로만 갱신된다.
        List<Stage5TargetRunner> targets = spawner.SpawnTargets(requiredCaptures);
        foreach (Stage5TargetRunner t in targets)
        {
            if (t == null) continue;
            _activeTargets.Add(t);
            t.OnCaptured += HandleCaptured;
            t.Activate(players, nodes);
        }

        _started = true;
        OnCaptureCountChanged?.Invoke(_capturedCount, requiredCaptures);
        OnTimerChanged?.Invoke(Remaining);

        if (!IsClientOnly())
        {
            NetLog.Transition("Stage5TargetObjective", "RoundStart", $"required={requiredCaptures}");
            StageNetworkState.Instance?.SyncStage5CaptureClientRpc(_capturedCount, requiredCaptures);
        }
    }

    public override void Tick()
    {
        if (!_started || IsCompleted || IsFailed) return;

        var  nm     = NetworkManager.Singleton;
        bool isHost = nm != null && nm.IsServer;

        // Client: 판정(HandleTimeout/HandleCaptured)은 Host의 _activeTargets에서만 일어나므로
        // (Client는 항상 빈 리스트) 로컬로 _elapsed를 진행시킬 이유가 없다 — SurviveTimeObjective와
        // 동일한 "Progress는 Host 레인 하나" 원칙(NetworkDesign.md §11A). 타이머 UI는 Host가 보내는
        // SyncStage5RemainingClientRpc(HandleRemainingSync)로만 갱신된다.
        if (!isHost) return;

        _elapsed += Time.deltaTime;

        if (Time.time >= _nextUITick)
        {
            _nextUITick = Time.time + 1f;
            OnTimerChanged?.Invoke(Remaining);
            if (nm.IsListening && nm.IsServer)
                StageNetworkState.Instance?.SyncStage5RemainingClientRpc(Remaining);
        }

        // IsCompleted는 HandleCaptured()가 같은 프레임에 세팅할 수 있음 →
        // 접촉 성공이 타임아웃보다 자동으로 우선 처리됨
        if (_elapsed >= timeLimit && !IsCompleted)
            HandleTimeout();
    }

    // ── 내부 유틸 ────────────────────────────────────────────────

    /// <summary>captureTable에서 현재 활성 인원에 맞는 count를 반환. 테이블이 비어 있으면 fallback.</summary>
    int ResolveCaptureCount()
    {
        int active = GameSession.Instance != null ? GameSession.Instance.ActivePlayerCount : 4;

        if (captureTable != null && captureTable.Length > 0)
        {
            foreach (CaptureEntry entry in captureTable)
                if (entry.playerCount == active) return Mathf.Max(1, entry.count);

            // 일치하는 행 없으면 마지막 행으로 fallback
            return Mathf.Max(1, captureTable[captureTable.Length - 1].count);
        }

        return Mathf.Max(1, requiredCaptures);
    }

    // ── 포획 처리 ────────────────────────────────────────────────

    void HandleCaptured(Stage5TargetRunner runner)
    {
        if (IsCompleted || IsFailed) return;

        _capturedCount++;
        OnCaptureCountChanged?.Invoke(_capturedCount, requiredCaptures);
        NetLog.Transition("Stage5TargetObjective", "Captured", $"count={_capturedCount}/{requiredCaptures}");

        // HandleCaptured는 항상 Host에서만 호출된다 — Client의 _activeTargets는 항상 비어 있어
        // t.OnCaptured 구독 자체가 일어나지 않는다(Begin() 참고). 그래도 방어적으로 가드.
        if (!IsClientOnly())
            StageNetworkState.Instance?.SyncStage5CaptureClientRpc(_capturedCount, requiredCaptures);

        _activeTargets.Remove(runner);
        runner.OnCaptured -= HandleCaptured;
        DespawnOrDestroy(runner);

        if (_capturedCount >= requiredCaptures)
        {
            CleanupTargets();
            Complete();
        }
    }

    // ── 타임아웃 처리 ────────────────────────────────────────────

    void HandleTimeout()
    {
        // IsCompleted 재확인: HandleCaptured가 같은 프레임에 Complete()를 호출했을 수 있음
        if (IsCompleted) return;

        NetLog.Transition("Stage5TargetObjective", "Timeout", $"captured={_capturedCount}/{requiredCaptures}");
        CleanupTargets();
        Fail();
    }

    // ── 정리 ─────────────────────────────────────────────────────

    void CleanupTargets()
    {
        foreach (Stage5TargetRunner t in _activeTargets)
        {
            if (t == null) continue;
            t.OnCaptured -= HandleCaptured;
            t.Deactivate();
            DespawnOrDestroy(t);
        }
        _activeTargets.Clear();
    }

    /// <summary>Host에서만 호출됨(_activeTargets는 Host에만 채워짐) — 네트워크 오브젝트는 Despawn으로 전원 정리.</summary>
    static void DespawnOrDestroy(Stage5TargetRunner runner)
    {
        NetworkObject netObj = runner.GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
            netObj.Despawn(true);
        else
            Destroy(runner.gameObject);
    }

    void HandleCaptureSync(int captured, int required)
    {
        _capturedCount = captured;
        requiredCaptures = required;
        OnCaptureCountChanged?.Invoke(_capturedCount, requiredCaptures);
    }

    /// <summary>StageNetworkState.SyncStage5RemainingClientRpc 수신 시 호출 — Client 타이머 UI 갱신용.</summary>
    void HandleRemainingSync(float remaining)
    {
        OnTimerChanged?.Invoke(remaining);
    }

    static bool IsClientOnly()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && !nm.IsServer;
    }

    void OnDisable()
    {
        // PhaseManager.DisableAllPhaseObjects() → SetActive(false) 시 호출됨
        // 동적 스폰 타겟은 Stage 계층 외부에 있으므로 여기서 명시적으로 정리
        CleanupTargets();
        _started = false;

        if (StageNetworkState.Instance != null)
        {
            StageNetworkState.Instance.OnStage5CaptureSync -= HandleCaptureSync;
            StageNetworkState.Instance.OnStage5RemainingSync -= HandleRemainingSync;
        }
    }
}
