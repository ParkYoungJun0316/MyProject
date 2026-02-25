using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 스테이지 목표 추상 기반 클래스.
/// StageManager에 등록하여 사용.
/// 구체 클래스: SurviveTimeObjective, KillAllEnemiesObjective,
///              HoldZoneObjective, HoldColorTilesObjective
/// </summary>
public abstract class StageObjective : MonoBehaviour
{
    [Header("목표 공통")]
    [Tooltip("Inspector에서 표시될 목표 이름")]
    public string objectiveName = "Objective";

    [Header("이벤트")]
    [Tooltip("목표 완료 시")]
    public UnityEvent OnCompleted;
    [Tooltip("목표 실패 시 (선택 — 실패 개념 없는 목표는 무시)")]
    public UnityEvent OnFailed;

    public bool IsCompleted { get; protected set; }
    public bool IsFailed    { get; protected set; }

    /// <summary>StageManager가 스테이지 시작 시 호출</summary>
    public abstract void Begin();

    /// <summary>StageManager의 Update에서 호출 (매 프레임)</summary>
    public abstract void Tick();

    /// <summary>목표 완료 처리. 구체 클래스에서 호출</summary>
    protected void Complete()
    {
        if (IsCompleted || IsFailed) return;
        IsCompleted = true;
        OnCompleted?.Invoke();
    }

    /// <summary>목표 실패 처리. 구체 클래스에서 호출</summary>
    protected void Fail()
    {
        if (IsCompleted || IsFailed) return;
        IsFailed = true;
        OnFailed?.Invoke();
    }
}
