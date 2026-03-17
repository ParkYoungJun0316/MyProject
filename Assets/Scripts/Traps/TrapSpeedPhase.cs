using UnityEngine;

/// <summary>
/// ArrowTrap / DropTrap 공용 — 시간 경과에 따른 속도 배율 단계.
/// afterSeconds 이후 speedMultiplier 배율이 baseSpeed에 곱해짐.
/// 여러 단계를 오름차순으로 입력하면 시간이 지날수록 자동 적용.
/// </summary>
[System.Serializable]
public class SpeedPhase
{
    [Tooltip("스케줄 시작 후 이 초가 지나면 이 배율 적용 (오름차순으로 입력)")]
    public float afterSeconds = 0f;

    [Tooltip("baseSpeed 대비 배율. 예: 1.5 = 50% 빠르게")]
    public float speedMultiplier = 1f;
}
