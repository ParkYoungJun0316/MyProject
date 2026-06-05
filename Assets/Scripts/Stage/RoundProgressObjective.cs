using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 원(●)—막대(━) 체인 UI를 공유하는 라운드 기반 Objective의 추상 기반 클래스.
/// GridRoundObjective, MemoryRoundObjective가 이를 상속.
/// GridRoundProgressUI는 이 타입을 참조하므로 두 Objective 모두 연결 가능.
/// </summary>
public abstract class RoundProgressObjective : StageObjective
{
    [Header("이벤트 (UI 연결용)")]
    [Tooltip("라운드 시작 / 정산 / 완료 시 호출. GridRoundProgressUI가 자동 구독.")]
    public UnityEvent OnProgressChanged;

    /// <summary>정산 완료된 라운드(구역) 수.</summary>
    public abstract int PlayedRounds      { get; }

    /// <summary>전체 라운드(구역) 수.</summary>
    public abstract int TotalRounds       { get; }

    /// <summary>현재 진행 중인 라운드 인덱스(0부터). 진행 중 아니면 -1.</summary>
    public abstract int CurrentRoundIndex { get; }
}
