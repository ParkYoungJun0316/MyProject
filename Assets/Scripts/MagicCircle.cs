using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 마법진 매니저.
/// 자식 오브젝트에 MagicCircleWaypoint를 붙여서 배치.
/// 플레이어가 waypoints 순서대로 전부 밟으면 OnCompleted 이벤트 발동.
///
/// [설정 방법]
/// 1. 빈 GameObject (MagicCircle) 생성
/// 2. 자식으로 웨이포인트 오브젝트들 배치 (MagicCircleWaypoint + Collider)
/// 3. Inspector의 waypoints 리스트에 순서대로 등록 (비우면 자식에서 자동 수집)
/// 4. OnCompleted 이벤트에 보스 공격 메서드 등 연결
/// </summary>
public class MagicCircle : MonoBehaviour
{
    [Header("웨이포인트")]
    [Tooltip("순서대로 밟아야 할 웨이포인트 목록. 비우면 자식에서 자동 수집")]
    public List<MagicCircleWaypoint> waypoints = new();

    [Header("동작 설정")]
    [Tooltip("틀린 순서로 밟으면 처음부터 초기화할지 여부")]
    public bool resetOnWrongStep = true;

    [Header("이벤트")]
    [Tooltip("순서대로 전부 밟았을 때 발동. 보스 공격 등 연결")]
    public UnityEvent OnCompleted;

    [Tooltip("초기화(리셋)됐을 때 발동")]
    public UnityEvent OnReset;

    [Header("Runtime (확인용)")]
    [SerializeField] int  _currentStep;
    [SerializeField] bool _isComplete;

    public int  CurrentStep => _currentStep;
    public bool IsComplete  => _isComplete;

    void Awake()
    {
        // Inspector에서 비어 있으면 자식 자동 수집
        if (waypoints == null || waypoints.Count == 0)
            waypoints = new List<MagicCircleWaypoint>(GetComponentsInChildren<MagicCircleWaypoint>());

        // 각 웨이포인트에 참조·순서 주입
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null) continue;
            waypoints[i].circle    = this;
            waypoints[i].stepIndex = i;
            waypoints[i].SetActivated(false);
        }
    }

    // ── 외부 호출 ────────────────────────────────────────────

    /// <summary>MagicCircleWaypoint.OnTriggerEnter에서 호출</summary>
    public void TryAdvance(int stepIndex)
    {
        if (_isComplete) return;

        if (stepIndex == _currentStep)
        {
            // 올바른 순서 → 활성화 + 진행
            waypoints[stepIndex].SetActivated(true);
            _currentStep++;

            if (_currentStep >= waypoints.Count)
            {
                _isComplete = true;
                OnCompleted?.Invoke();
            }
        }
        else if (resetOnWrongStep && stepIndex != _currentStep - 1)
        {
            // 이미 밟은 이전 단계를 다시 밟은 경우는 무시, 나머지는 리셋
            ResetCircle();
        }
    }

    /// <summary>마법진 초기화. 완성 후 재사용하거나 오답 리셋 시 호출</summary>
    public void ResetCircle()
    {
        _currentStep = 0;
        _isComplete  = false;

        for (int i = 0; i < waypoints.Count; i++)
            if (waypoints[i] != null)
                waypoints[i].SetActivated(false);

        OnReset?.Invoke();
    }

    // ── 에디터 지원 ──────────────────────────────────────────

    [ContextMenu("테스트: 마법진 완성")]
    void Debug_Complete()
    {
        for (int i = 0; i < waypoints.Count; i++)
            if (waypoints[i] != null) waypoints[i].SetActivated(true);
        _currentStep = waypoints.Count;
        _isComplete  = true;
        OnCompleted?.Invoke();
    }

    [ContextMenu("테스트: 마법진 초기화")]
    void Debug_Reset() => ResetCircle();

    void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Count == 0) return;

        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null) continue;

            // 진행 상태에 따라 선 색상 변경
            Gizmos.color = i < _currentStep ? Color.cyan : new Color(0.3f, 0.3f, 1f, 0.6f);
            Gizmos.DrawSphere(waypoints[i].transform.position, 0.15f);

            // 다음 웨이포인트로 선 연결
            if (i < waypoints.Count - 1 && waypoints[i + 1] != null)
                Gizmos.DrawLine(waypoints[i].transform.position,
                                waypoints[i + 1].transform.position);
        }

        // 마지막 → 첫 번째 연결선 (순환 경로 시각화)
        if (waypoints.Count > 1
            && waypoints[0] != null
            && waypoints[waypoints.Count - 1] != null)
        {
            Gizmos.color = new Color(0.3f, 0.3f, 1f, 0.2f);
            Gizmos.DrawLine(waypoints[waypoints.Count - 1].transform.position,
                            waypoints[0].transform.position);
        }
    }
}
