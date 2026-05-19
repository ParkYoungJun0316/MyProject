using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 스테이지 매니저.
/// objectives[] 의 목표가 전부 완료되면 OnStageClear 발동.
/// 하나라도 실패하면 OnStageFailed 발동.
///
/// [설정]
///  1. 이 오브젝트 또는 자식에 원하는 Objective 스크립트를 붙임
///  2. objectives[] 에 등록 (비우면 자식에서 자동 수집)
///  3. OnStageClear → 다음 씬 전환, 문 열기 등 연결
/// </summary>
public class StageManager : MonoBehaviour
{
    [Header("목표 목록")]
    [Tooltip("비우면 자식 오브젝트에서 자동 수집")]
    public StageObjective[] objectives;

    [Header("시작 설정")]
    [Tooltip("true: 씬 로드 즉시 자동 시작\n" +
             "false: StartStage() 호출 대기 (PlayerTriggerZone 연결 필요)")]
    public bool autoStart = false;

    [Header("이벤트")]
    public UnityEvent OnStageClear;
    public UnityEvent OnStageFailed;

    bool _isStarted;
    bool _isCleared;
    bool _isFailed;
    int  _completedCount;

    // TrapBase / FloorManager 가 Awake에서 직접 등록
    readonly List<TrapBase>   _registeredTraps = new List<TrapBase>();
    FloorManager              _registeredFloor;

    public bool IsStarted => _isStarted;
    public bool IsCleared => _isCleared;
    public bool IsFailed  => _isFailed;

    /// <summary>TrapBase.Awake()에서 자동 호출. 계층 위치 무관하게 등록됨.</summary>
    public void RegisterTrap(TrapBase trap)
    {
        if (!_registeredTraps.Contains(trap))
            _registeredTraps.Add(trap);
    }

    /// <summary>FloorManager.Awake()에서 자동 호출.</summary>
    public void RegisterFloor(FloorManager floor) => _registeredFloor = floor;

    void Awake()
    {
        if (objectives == null || objectives.Length == 0)
            objectives = GetComponentsInChildren<StageObjective>(true);
    }

    void Start()
    {
        if (autoStart) StartStage();
    }

    void Update()
    {
        if (!_isStarted || _isCleared || _isFailed) return;

        _completedCount = 0;
        for (int i = 0; i < objectives.Length; i++)
        {
            if (objectives[i] == null) continue;

            objectives[i].Tick();

            if (objectives[i].IsFailed)
            {
                _isFailed = true;
                OnStageFailed?.Invoke();
                return;
            }

            if (objectives[i].IsCompleted)
                _completedCount++;
        }

        if (_completedCount >= objectives.Length)
        {
            _isCleared = true;
            DeactivateAllTraps();
            DestroyAllProjectiles();
            OnStageClear?.Invoke();
        }
    }

    // ── 외부 호출 ─────────────────────────────────────────────────

    /// <summary>
    /// 플레이어가 트리거를 밟으면 호출. Objective 타이머/목표를 시작.
    /// PlayerTriggerZone.OnPlayerEnter에 연결.
    /// </summary>
    public void StartStage()
    {
        if (_isStarted) return;
        _isStarted = true;

        foreach (var obj in objectives)
            if (obj != null) obj.Begin();

        foreach (var trap in _registeredTraps)
            if (trap != null) trap.Activate();

        if (_registeredFloor != null) _registeredFloor.StartFloor();
    }

    /// <summary>
    /// 등록된 모든 함정을 비활성화(발사 중단).
    /// 스테이지 클리어 시 자동 호출. 외부에서도 직접 호출 가능.
    /// </summary>
    public void DeactivateAllTraps()
    {
        foreach (var trap in _registeredTraps)
            if (trap != null) trap.Deactivate();
    }

    /// <summary>
    /// 씬에 날아다니는 TrapProjectile 전부 즉시 파괴.
    /// 스테이지 클리어 시 자동 호출. 외부에서도 직접 호출 가능.
    /// </summary>
    public void DestroyAllProjectiles()
    {
        TrapProjectile[] projectiles = FindObjectsByType<TrapProjectile>(FindObjectsSortMode.None);
        foreach (TrapProjectile p in projectiles)
            if (p != null) Destroy(p.gameObject);
    }

    /// <summary>
    /// 스테이지 상태 초기화 후 모든 Objective 재시작.
    /// PhaseManager.RestartCurrentPhase() 또는 StageResetOnPlayerDeath에서 자동 호출됨.
    /// </summary>
    public void ResetStage()
    {
        _isStarted      = false;
        _isCleared      = false;
        _isFailed       = false;
        _completedCount = 0;

        DeactivateAllTraps();
        DestroyAllProjectiles();

        foreach (var obj in objectives)
            if (obj != null) obj.ResetObjective();
    }

    // ── 에디터 지원 ──────────────────────────────────────────────
    [ContextMenu("테스트: 스테이지 시작")]
    void Debug_Start() => StartStage();

    [ContextMenu("테스트: 스테이지 클리어")]
    void Debug_Clear()
    {
        _isStarted = true;
        _isCleared = true;
        DeactivateAllTraps();
        DestroyAllProjectiles();
        OnStageClear?.Invoke();
    }

    [ContextMenu("테스트: 스테이지 실패")]
    void Debug_Fail()
    {
        _isStarted = true;
        _isFailed  = true;
        OnStageFailed?.Invoke();
    }
}
