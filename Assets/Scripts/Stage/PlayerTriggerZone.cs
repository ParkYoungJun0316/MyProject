using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 플레이어가 트리거 영역에 진입/이탈할 때 UnityEvent를 발동하는 범용 트리거.
///
/// [사용 방법]
/// 1. 빈 GameObject에 이 스크립트 + Collider(Is Trigger = true) 추가
/// 2. OnPlayerEnter → SpinRoller.Activate() 등 원하는 메서드 연결
/// 3. fireOnce = true: 최초 1회만 발동 / false: 진입할 때마다 발동
/// </summary>
[RequireComponent(typeof(Collider))]
public class PlayerTriggerZone : MonoBehaviour
{
    [Header("이벤트")]
    [Tooltip("플레이어가 영역에 진입했을 때 발동")]
    public UnityEvent OnPlayerEnter;

    [Tooltip("플레이어가 영역을 벗어났을 때 발동")]
    public UnityEvent OnPlayerExit;

    [Header("설정")]
    [Tooltip("true: 최초 1회만 발동 후 비활성화 / false: 진입할 때마다 발동")]
    [SerializeField] bool fireOnce = true;

    [Tooltip("트리거 발동 후 이 GameObject를 비활성화 (fireOnce = true일 때만 적용)")]
    [SerializeField] bool disableAfterFire = true;

    bool _fired;

    void Awake()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (fireOnce && _fired) return;

        _fired = true;
        OnPlayerEnter?.Invoke();

        if (fireOnce && disableAfterFire)
            gameObject.SetActive(false);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (fireOnce) return;

        OnPlayerExit?.Invoke();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = _fired
            ? new Color(0.5f, 0.5f, 0.5f, 0.3f)
            : new Color(0f, 1f, 0.5f, 0.3f);
        Gizmos.DrawCube(transform.position, transform.lossyScale);

        Gizmos.color = _fired
            ? new Color(0.5f, 0.5f, 0.5f, 0.8f)
            : new Color(0f, 1f, 0.5f, 0.8f);
        Gizmos.DrawWireCube(transform.position, transform.lossyScale);
    }
}
