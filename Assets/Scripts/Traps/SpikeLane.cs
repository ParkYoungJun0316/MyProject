using UnityEngine;

/// <summary>
/// 스파이크 레인 하나.
/// SpikeLaneField 자식으로 배치. 이 레인에 속한 SpikeTrap을 일괄 발동/해제.
///
/// [설정]
/// 1. 이 GameObject 아래에 SpikeTrap 타일들 배치
/// 2. 각 SpikeTrap은 startActive = false, activateInterval = 0 으로 설정
///    (SpikeLaneField가 발동 타이밍을 제어함)
/// 3. warnMarker에 레인 전체 길이를 덮도록 스케일 맞춘 SpikeLaneWarnMarker 연결 (선택)
/// </summary>
public class SpikeLane : MonoBehaviour
{
    [Tooltip("이 레인의 경고 마커. 레인 길이에 맞춰 에디터에서 미리 배치. 없으면 경고 연출 생략")]
    [SerializeField] SpikeLaneWarnMarker warnMarker = null;

    SpikeTrap[] _traps;

    void Awake()
    {
        _traps = GetComponentsInChildren<SpikeTrap>(true);
    }

    /// <summary>경고 시작. duration초 동안 마커가 노랑→빨강으로 보간된다 (SpikeLaneField가 호출).</summary>
    public void PlayWarning(float duration) => warnMarker?.PlayWarning(duration);

    /// <summary>이 레인의 모든 스파이크 일괄 발동. 가시가 튀어오르는 순간이므로 경고 마커는 즉시 끈다.</summary>
    public void Trigger()
    {
        warnMarker?.ResetWarning();
        for (int i = 0; i < _traps.Length; i++)
            if (_traps[i] != null) _traps[i].Activate();
    }

    /// <summary>이 레인의 모든 스파이크 즉시 강제 해제. 경고 도중 중단된 경우도 포함하므로 마커도 리셋.</summary>
    public void ForceDeactivate()
    {
        warnMarker?.ResetWarning();
        for (int i = 0; i < _traps.Length; i++)
            if (_traps[i] != null) _traps[i].Deactivate();
    }
}
