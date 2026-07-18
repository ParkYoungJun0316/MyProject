using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 스테이지 목표 추상 기반 클래스.
/// StageManager에 등록하여 사용.
/// 구체 클래스: SurviveTimeObjective, ReachZoneObjective
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

    /// <summary>StageManager.Update()에서 매 프레임 호출 (전 머신 로컬 실행 — 진행률 표시 등)</summary>
    public abstract void Tick();

    /// <summary>
    /// 목표 완료 처리. 구체 클래스에서 호출.
    /// [축 SSOT: NetworkDesign.md §11A.2] 이 호출은 Host 레인에서만 일어나야 한다 —
    /// 구체 클래스가 Client 트리거로 직접 호출하면 §11A 위반.
    /// </summary>
    protected void Complete()
    {
        if (IsCompleted || IsFailed) return;
        IsCompleted = true;
        OnCompleted?.Invoke();
    }

    /// <summary>
    /// 목표 실패 처리. 구체 클래스에서 호출.
    /// [축 SSOT: NetworkDesign.md §11A.2] Complete()와 동일 — Host 레인에서만.
    /// </summary>
    protected void Fail()
    {
        if (IsCompleted || IsFailed) return;
        IsFailed = true;
        OnFailed?.Invoke();
    }
}
