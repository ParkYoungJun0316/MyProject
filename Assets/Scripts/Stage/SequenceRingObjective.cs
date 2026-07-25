using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// SequenceRingMinigame 결과를 StageObjective로 연동.
///
/// [동작]
/// - Begin()              : 미니게임 리셋 + 시작 + 이벤트 구독
///                          StageManager.StartStage() 하나만 연결하면 됨 (다른 Objective와 동일)
/// - OnMinigameSuccess    : Complete() → StageManager.OnStageClear → PhaseManager.AdvancePhase
/// - OnMinigameFailed     : Fail()    → StageManager.OnStageFailed → 씬 리셋
/// - Tick()               : 매 프레임 시간/스텝 변화 감지 → OnProgressChanged 발동 (UI 갱신)
///
/// [클리어 조건]
/// - 제한 시간 안에 targetStepCount 완료
///
/// [실패 조건]
/// - 시간 초과 (timePenaltyOnWrong으로 시간이 0 이하가 되는 것 포함)
/// - timeLimit <= 0: UI에서 0으로 표시 (무제한 없음)
///
/// [Inspector 설정]
/// - minigame             : 연동할 SequenceRingMinigame
///                          ⚠ SequenceRingMinigame.startOnAwake 는 반드시 false 로 설정할 것
/// - objectiveName        : UI 표시 이름
/// </summary>
public class SequenceRingObjective : StageObjective
{
    [Header("링 미니게임")]
    [Tooltip("연동할 SequenceRingMinigame.\n" +
             "⚠ SequenceRingMinigame.startOnAwake 는 반드시 false 로 설정할 것.\n" +
             "StageManager.StartStage() → Begin() → StartMinigame() 자동 호출.")]
    [SerializeField] SequenceRingMinigame minigame;

    [Header("이벤트 (UI 연결용)")]
    [Tooltip("시간 또는 스텝 변화 시 호출. ObjectiveUI가 자동 구독.")]
    public UnityEvent OnProgressChanged;

    // ── 내부 상태 (전 프레임 비교용) ──────────────────────────────

    int   _lastSuccessCount   = -1;
    float _lastTimeRemaining  = -1f;

    // ── 프로퍼티 (UI가 읽어감) ────────────────────────────────────

    /// <summary>남은 스텝 수 (= targetStepCount - successCount). 미니게임 미연결 시 0.</summary>
    public int RemainingSteps =>
        minigame != null ? Mathf.Max(0, minigame.TargetStepCount - minigame.SuccessCount) : 0;

    /// <summary>총 스텝 수. 미니게임 미연결 시 0.</summary>
    public int TotalSteps =>
        minigame != null ? minigame.TargetStepCount : 0;

    /// <summary>남은 시간(초). timeLimit <= 0 이면 0 반환.</summary>
    public float TimeRemaining =>
        minigame != null ? Mathf.Max(0f, minigame.TimeRemaining) : 0f;

    /// <summary>제한 시간. 0 이하면 0 반환.</summary>
    public float TimeLimit =>
        minigame != null ? Mathf.Max(0f, minigame.TimeLimit) : 0f;

    // ── StageObjective 구현 ──────────────────────────────────────

    public override void Begin()
    {
        Unsubscribe();

        _lastSuccessCount  = -1;
        _lastTimeRemaining = -1f;

        if (minigame == null)
        {
            Debug.LogWarning($"[SequenceRingObjective] minigame이 연결되지 않았습니다. ({gameObject.name})");
            return;
        }

        minigame.OnMinigameSuccess.AddListener(HandleSuccess);
        minigame.OnMinigameFailed.AddListener(HandleFail);

        // [버그 수정 2026-07-25] StartMinigame()은 이미 IsClientOnly()면 no-op이지만
        // ResetMinigame()은 그 가드가 없어 Client에서도 그대로 _state=Idle을 썼다.
        // Client의 로컬 StartStage()(StageStartGate NV 폴링으로 트리거)가 이 Begin()을
        // 부르는 시점은 Host의 ChallengeStepBegin(0) NV 도착과 같은 프레임일 수 있어(EarlyUpdate
        // vs Update 단계 차이), 이미 HandleChallengeStepChanged가 그려놓은 Playing을 바로
        // 덮어써 버렸다(§11B ①Trigger를 OX/ColorTile처럼 Host 레인에서만 실행 — Client의
        // 직접 호출은 무시).
        if (!IsClientOnly())
        {
            minigame.ResetMinigame();
            minigame.StartMinigame();
        }

        OnProgressChanged?.Invoke();
    }

    public override void Tick()
    {
        if (IsCompleted || IsFailed || minigame == null) return;
        if (minigame.State != SequenceRingMinigame.MinigameState.Playing) return;

        int   curStep = minigame.SuccessCount;
        float curTime = minigame.TimeRemaining;

        bool changed = (curStep != _lastSuccessCount)
                    || (Mathf.Abs(curTime - _lastTimeRemaining) >= 0.09f);

        if (!changed) return;

        _lastSuccessCount  = curStep;
        _lastTimeRemaining = curTime;
        OnProgressChanged?.Invoke();
    }

    // ── 핸들러 ────────────────────────────────────────────────────

    void HandleSuccess()
    {
        OnProgressChanged?.Invoke();

        // [축 SSOT: NetworkDesign.md §11A.2] Complete() 확정은 Host 레인에서만.
        // OnMinigameSuccess는 SequenceRingMinigame.HandleChallengeClearedChanged를 통해 Host/Client 전
        // 머신에서 공통으로 발동되므로 여기서 가드 (ColorTileRoundObjective.HandleSuccess와 동일 패턴).
        if (IsClientOnly()) return;

        Complete();
    }

    void HandleFail()
    {
        OnProgressChanged?.Invoke();

        // Fail() 확정도 동일하게 Host 레인에서만. KillAllPlayers()의 NetworkDamageUtil.ApplyInstantKill은
        // 이미 내부적으로 Server 가드가 있어 전 머신에서 호출해도 안전 — 그대로 둔다.
        KillAllPlayers();
        if (IsClientOnly()) return;

        Fail();
    }

    static bool IsClientOnly()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening && !nm.IsServer;
    }

    void KillAllPlayers()
    {
        Player[] players = UnityEngine.Object.FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (Player p in players)
        {
            if (p == null || p.IsDead) continue;
            NetworkDamageUtil.ApplyInstantKill(p);
        }
    }

    // ── 구독 해제 ─────────────────────────────────────────────────

    void Unsubscribe()
    {
        if (minigame == null) return;
        minigame.OnMinigameSuccess.RemoveListener(HandleSuccess);
        minigame.OnMinigameFailed.RemoveListener(HandleFail);
    }

    void OnDestroy()
    {
        Unsubscribe();
    }
}
