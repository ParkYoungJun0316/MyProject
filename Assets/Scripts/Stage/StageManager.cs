using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 스테이지 매니저.
/// objectives[] 의 목표가 전부 완료되면 OnStageClear 발동.
/// 하나라도 실패하면 OnStageFailed 발동.
///
/// [축 SSOT: NetworkDesign.md §11A]
/// Clear/Fail 확정(Resolve)은 Host 레인에서만 판정한다 (Update() 하단 IsServer 가드).
/// Fail은 별도 소프트 리셋을 두지 않고 전원 즉사 → §11 사망 문(전원 씬 리로드)으로 재진입시킨다.
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

        // Tick()은 전 머신에서 로컬 실행 (진행률 표시 등 Consumer 용도).
        // Complete()/Fail() 자체는 각 Objective가 스스로 Host 가드하는 것과 별개로,
        // 클리어/실패 "확정"은 아래에서 Host 레인 하나로만 판정한다 (§11A.0 Progress/Resolve).
        for (int i = 0; i < objectives.Length; i++)
            if (objectives[i] != null) objectives[i].Tick();

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.IsServer) return;

        _completedCount = 0;
        for (int i = 0; i < objectives.Length; i++)
        {
            if (objectives[i] == null) continue;

            if (objectives[i].IsFailed)
            {
                _isFailed = true;
                OnStageFailed?.Invoke();
                KillAllPlayersOnFail();
                return;
            }

            if (objectives[i].IsCompleted)
                _completedCount++;
        }

        // objectives가 0개면 즉시 클리어되지 않도록 가드
        // 보스 씬에서 StageManager가 함정 전용으로만 쓰일 때 적용
        if (objectives.Length > 0 && _completedCount >= objectives.Length)
        {
            _isCleared = true;
            DeactivateAllTraps();
            DestroyAllProjectiles();
            OnStageClear?.Invoke();
        }
    }

    /// <summary>
    /// 스테이지 실패 → §11 사망 문으로 병합 (NetworkDesign.md §11A.3).
    /// 별도 리셋 경로를 만들지 않고 전원 즉사시켜 기존 사망 리로드(전원 씬 리로드)로 재진입시킨다.
    /// Update()가 이미 Host 레인으로 가드한 뒤 호출하므로 여기서 다시 가드하지 않음.
    /// </summary>
    void KillAllPlayersOnFail()
    {
        Player[] players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (Player p in players)
        {
            if (p == null || p.IsDead) continue;
            NetworkDamageUtil.ApplyInstantKill(p);
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
        var  nm         = NetworkManager.Singleton;
        bool isNetworked = nm != null && nm.IsListening;

        // 온라인: Host만 Despawn (NGO가 전원에 자동 전파)
        if (isNetworked && !nm.IsServer) return;

        TrapProjectile[] projectiles = FindObjectsByType<TrapProjectile>(FindObjectsSortMode.None);
        foreach (TrapProjectile p in projectiles)
        {
            if (p == null) continue;
            var netObj = p.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned) netObj.Despawn(true);
        }
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
        KillAllPlayersOnFail();
    }
}
