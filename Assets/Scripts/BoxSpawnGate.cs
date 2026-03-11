using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 박스 스폰 게이트.
/// requiredTriggers[] 가 전부 활성화되면 spawners[] 를 모두 발동시킨다.
///
/// [사용 흐름]
///  1. 플레이어가 Common 박스를 각 BoxColorTrigger 구역으로 밀어 넣음
///  2. 4개 트리거가 모두 활성화되면 BoxSpawner.Spawn() 호출
///  3. cooldown 동안 재발동 방지
/// </summary>
public class BoxSpawnGate : MonoBehaviour
{
    [Header("트리거 조건")]
    [Tooltip("모두 활성화돼야 스폰이 발동되는 트리거 목록")]
    public BoxColorTrigger[] requiredTriggers;

    [Header("스포너 목록")]
    [Tooltip("트리거 충족 시 Spawn()을 호출할 BoxSpawner 목록")]
    public BoxSpawner[] spawners;

    [Header("재발동 쿨다운")]
    [Tooltip("스폰 후 재발동까지 최소 대기 시간(초). 0 = 쿨다운 없음")]
    public float cooldown = 15f;

    [Header("이벤트")]
    public UnityEvent OnGateOpen;   // 스폰 발동 시
    public UnityEvent OnGateReset;  // 트리거 하나라도 해제됐을 때

    [Header("Runtime (확인용)")]
    [SerializeField] float _nextAllowedTime;
    [SerializeField] bool  _isOpen;

    public bool IsOpen => _isOpen;

    void OnEnable()
    {
        for (int i = 0; i < requiredTriggers.Length; i++)
        {
            if (requiredTriggers[i] == null) continue;
            requiredTriggers[i].OnActivated.AddListener(CheckAndSpawn);
            requiredTriggers[i].OnDeactivated.AddListener(HandleDeactivated);
        }
    }

    void OnDisable()
    {
        for (int i = 0; i < requiredTriggers.Length; i++)
        {
            if (requiredTriggers[i] == null) continue;
            requiredTriggers[i].OnActivated.RemoveListener(CheckAndSpawn);
            requiredTriggers[i].OnDeactivated.RemoveListener(HandleDeactivated);
        }
    }

    // ── 내부 ────────────────────────────────────────────────────

    void CheckAndSpawn()
    {
        if (_isOpen) return;
        if (cooldown > 0f && Time.time < _nextAllowedTime) return;

        // 모든 트리거가 활성화됐는지 확인
        for (int i = 0; i < requiredTriggers.Length; i++)
            if (requiredTriggers[i] == null || !requiredTriggers[i].IsActive) return;

        _isOpen          = true;
        _nextAllowedTime = Time.time + cooldown;

        for (int i = 0; i < spawners.Length; i++)
            spawners[i]?.Spawn();

        OnGateOpen?.Invoke();
    }

    void HandleDeactivated()
    {
        if (!_isOpen) return;
        _isOpen = false;
        OnGateReset?.Invoke();
    }

    // ── 에디터 지원 ──────────────────────────────────────────────

    [ContextMenu("테스트: 강제 스폰")]
    void Debug_ForceSpawn()
    {
        for (int i = 0; i < spawners.Length; i++)
            spawners[i]?.Spawn();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = _isOpen
            ? new Color(0f, 1f, 0f, 0.5f)
            : new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, 1f);
    }
}
