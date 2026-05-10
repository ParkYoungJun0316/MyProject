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
/// PhaseManager.RestartCurrentPhase() → DisableAllPhaseObjects → ResetAllManagedStageManagers
/// → StageManager.ResetStage() → ResetObjective() → Begin() (비활성 중 → 스폰 스킵)
/// → EnterPhase(onPhaseEnter) → StageManager.StartStage() → Begin() (활성 → 정상 스폰)
///
/// [Inspector 설정]
/// - spawner: Stage5TargetSpawner 연결 필수
/// - safeZonePrefab: 안전지대 프리팹 연결
/// - requiredCaptures: 5.1=1, 5.2=3
/// - timeLimit: 제한 시간(초)
/// </summary>
public class Stage5TargetObjective : StageObjective
{
    [Header("Stage5 설정")]
    [Tooltip("제한 시간 (초)")]
    public float timeLimit = 60f;
    [Tooltip("성공에 필요한 타겟 포획 수. stage5.1=1 / stage5.2=3")]
    public int requiredCaptures = 1;

    [Header("안전지대")]
    [Tooltip("포획 성공 위치에 생성할 안전지대 프리팹")]
    public GameObject safeZonePrefab;

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

        CleanupTargets();

        // PhaseManager 리셋 중 오브젝트가 비활성인 경우 스폰 건너뜀.
        // 이후 StageManager.StartStage() → Begin() 재호출 시 정상 스폰.
        if (!gameObject.activeInHierarchy) return;
        if (spawner == null)
        {
            Debug.LogWarning("[Stage5TargetObjective] spawner가 연결되지 않았습니다.");
            return;
        }

        Player[] players = FindObjectsByType<Player>(FindObjectsSortMode.None);

        List<Stage5TargetRunner> targets = spawner.SpawnTargets();
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
    }

    public override void Tick()
    {
        if (!_started || IsCompleted || IsFailed) return;

        _elapsed += Time.deltaTime;

        if (Time.time >= _nextUITick)
        {
            _nextUITick = Time.time + 1f;
            OnTimerChanged?.Invoke(Remaining);
        }

        // IsCompleted는 HandleCaptured()가 같은 프레임에 세팅할 수 있음 →
        // 접촉 성공이 타임아웃보다 자동으로 우선 처리됨
        if (_elapsed >= timeLimit && !IsCompleted)
            HandleTimeout();
    }

    // ── 포획 처리 ────────────────────────────────────────────────

    void HandleCaptured(Stage5TargetRunner runner)
    {
        if (IsCompleted || IsFailed) return;

        _capturedCount++;
        OnCaptureCountChanged?.Invoke(_capturedCount, requiredCaptures);

        // 포획 위치에 안전지대 즉시 생성
        if (safeZonePrefab != null)
            Instantiate(safeZonePrefab, runner.transform.position, Quaternion.identity);

        _activeTargets.Remove(runner);
        runner.OnCaptured -= HandleCaptured;
        Destroy(runner.gameObject);

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
            Destroy(t.gameObject);
        }
        _activeTargets.Clear();
    }

    void OnDisable()
    {
        // PhaseManager.DisableAllPhaseObjects() → SetActive(false) 시 호출됨
        // 동적 스폰 타겟은 Stage 계층 외부에 있으므로 여기서 명시적으로 정리
        CleanupTargets();
        _started = false;
    }
}
