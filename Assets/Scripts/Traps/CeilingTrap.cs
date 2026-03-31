using UnityEngine;

/// <summary>
/// 천장 낙하 함정 — TrapStealthSensor 패턴 기반.
/// 감지 범위 내에 Player 레이어 오브젝트가 있으면 천장 WallMover를 활성화.
/// WallMover.returnAfterMove = true 로 설정 시 자동으로 원상복귀.
///
/// [설정 방법]
///  1. 천장 오브젝트에 WallMover 부착
///     - moveOffset: 아래 방향 (예: 0, -3, 0)
///     - returnAfterMove: true (자동 복귀)
///     - returnDelay: 천장이 내려온 뒤 유지 시간(초)
///  2. 이 스크립트를 천장 오브젝트(또는 별도 감지 오브젝트)에 부착
///     - ceilingWallMover: 위 WallMover 연결
///     - detectionRadius: 감지 반경 (0이면 씬 전체)
///     - playerLayer: Player 레이어 마스크
///     - cooldown: 재발동 방지 시간 (moveDuration + returnDuration 보다 크게 설정)
/// </summary>
public class CeilingTrap : MonoBehaviour
{
    [Header("천장 WallMover")]
    [Tooltip("낙하시킬 천장의 WallMover 컴포넌트")]
    [SerializeField] WallMover ceilingWallMover;

    [Header("감지")]
    [Tooltip("감지 반경(m). 0이면 씬 전체 Player를 감지")]
    [SerializeField] float detectionRadius = 3f;

    [Tooltip("Player 레이어 마스크. Project Settings > Tags & Layers 에서 Player 선택")]
    [SerializeField] LayerMask playerLayer;

    [Header("쿨다운")]
    [Tooltip("한 번 발동 후 재발동까지의 최소 대기(초).\n" +
             "WallMover의 moveDuration + returnDelay + returnDuration 합산보다 크게 설정")]
    [SerializeField] float cooldown = 5f;

    float _cooldownRemaining;

    Player[] _cachedPlayers;
    int      _playerLayerId;

    void Awake()
    {
        _playerLayerId = LayerMask.NameToLayer("Player");
    }

    void Start()
    {
        CachePlayers();
    }

    void Update()
    {
        if (_cooldownRemaining > 0f)
        {
            _cooldownRemaining -= Time.deltaTime;
            return;
        }

        if (ceilingWallMover == null) return;

        if (CheckPlayerInRange())
        {
            ceilingWallMover.Activate();
            _cooldownRemaining = cooldown;
        }
    }

    // ── 감지 ────────────────────────────────────────────────────

    bool CheckPlayerInRange()
    {
        if (detectionRadius > 0f)
            return Physics.CheckSphere(transform.position, detectionRadius, playerLayer);

        // 전역: 캐시된 플레이어 레이어 확인 (TrapStealthSensor 방식)
        if (_cachedPlayers == null) return false;
        foreach (Player p in _cachedPlayers)
        {
            if (p == null || p.IsDead) continue;
            if (p.gameObject.layer == _playerLayerId) return true;
        }
        return false;
    }

    // ── 외부 호출 ────────────────────────────────────────────────

    /// <summary>플레이어가 씬에 추가/리스폰된 후 캐시 갱신.</summary>
    public void RefreshPlayerCache() => CachePlayers();

    void CachePlayers()
    {
        _cachedPlayers = FindObjectsByType<Player>(FindObjectsSortMode.None);
    }

    // ── 에디터 ──────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (detectionRadius <= 0f) return;

        bool active = _cooldownRemaining <= 0f;
        Gizmos.color = active
            ? new Color(1f, 0.3f, 0.3f, 0.2f)
            : new Color(0.5f, 0.5f, 0.5f, 0.2f);
        Gizmos.DrawSphere(transform.position, detectionRadius);

        Gizmos.color = active
            ? new Color(1f, 0.3f, 0.3f, 0.9f)
            : new Color(0.5f, 0.5f, 0.5f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
#endif
}
