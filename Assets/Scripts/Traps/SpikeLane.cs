using UnityEngine;

/// <summary>
/// 스파이크 레인 하나.
/// SpikeLaneField 자식으로 배치. 이 레인에 속한 SpikeTrap을 일괄 발동/해제.
///
/// [설정]
/// 1. 이 GameObject 아래에 SpikeTrap 타일들 배치
/// 2. 각 SpikeTrap은 startActive = false, activateInterval = 0 으로 설정
///    (SpikeLaneField가 발동 타이밍을 제어함)
/// </summary>
public class SpikeLane : MonoBehaviour
{
    SpikeTrap[] _traps;

    void Awake()
    {
        _traps = GetComponentsInChildren<SpikeTrap>(true);
    }

    /// <summary>이 레인의 모든 스파이크 일괄 발동.</summary>
    public void Trigger()
    {
        for (int i = 0; i < _traps.Length; i++)
            if (_traps[i] != null) _traps[i].Activate();
    }

    /// <summary>이 레인의 모든 스파이크 즉시 강제 해제.</summary>
    public void ForceDeactivate()
    {
        for (int i = 0; i < _traps.Length; i++)
            if (_traps[i] != null) _traps[i].Deactivate();
    }
}
