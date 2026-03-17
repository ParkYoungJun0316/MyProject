using UnityEngine;

/// <summary>
/// 스텔스 감지 컴포넌트 — TrapBase와 함께 함정에 부착.
///
/// 플레이어가 스텔스 상태(PlayerStealth 레이어)이면 함정이 발사를 멈추고,
/// 스텔스 해제(Player 레이어)가 되면 다시 발사를 시작.
///
/// [동작 원리]
/// - PlayerStealth 스크립트가 색 매칭 시 플레이어 레이어를 Player → PlayerStealth 로 전환
/// - 이 센서는 Player 레이어로 등록된 오브젝트(= 보이는 플레이어)의 존재 여부를 감지
/// - 보이는 플레이어가 1명이라도 있으면 함정 활성, 모두 스텔스면 함정 정지
///
/// [사용법]
/// 1. ArrowTrap / DropTrap 등 TrapBase가 있는 오브젝트에 추가
/// 2. Player Visible Layer → "Player" 레이어 설정
/// 3. Detection Radius → 0이면 씬 전체, 0 초과이면 해당 반경 내만 감지
/// 4. TrapBase의 Start Active는 이 컴포넌트가 자동으로 제어하므로 무관
/// </summary>
[RequireComponent(typeof(TrapBase))]
public class TrapStealthSensor : MonoBehaviour
{
    [Header("감지 범위")]
    [Tooltip("감지 반경(m).\n0이면 씬 전체 플레이어를 감지.\n0 초과이면 해당 반경 내 플레이어만 감지.")]
    [SerializeField] private float detectionRadius = 0f;

    [Header("레이어")]
    [Tooltip("'보이는' 플레이어 레이어 마스크.\nProject Settings > Tags & Layers 에서 Player 레이어 선택.\n스텔스 시 플레이어는 PlayerStealth 레이어로 이동하므로 자동 제외됨.")]
    [SerializeField] private LayerMask playerVisibleLayer;

    [Header("비활성화 딜레이")]
    [Tooltip("플레이어가 스텔스 진입 후 함정이 멈추기까지의 지연(초).\n0이면 스텔스 즉시 정지.")]
    [SerializeField] private float deactivateDelay = 0f;

    TrapBase _trap;
    Player[] _cachedPlayers;
    int      _playerLayerId;
    bool     _anyVisible;
    float    _deactivateTimer;

    void Awake()
    {
        _trap         = GetComponent<TrapBase>();
        _playerLayerId = LayerMask.NameToLayer("Player");
    }

    void Start()
    {
        CachePlayers();
        // 초기 상태 즉시 반영 (TrapBase.startActive 설정과 무관하게 센서가 제어)
        _anyVisible = CheckAnyVisible();
        if (!_anyVisible)
            _trap.Deactivate();
    }

    void Update()
    {
        bool anyVisible = CheckAnyVisible();

        if (anyVisible)
        {
            _deactivateTimer = 0f;

            if (!_anyVisible)           // 스텔스 해제 → 활성화
            {
                _anyVisible = true;
                _trap.Activate();
            }
        }
        else
        {
            if (_anyVisible)            // 스텔스 진입 → 딜레이 후 비활성화
            {
                _deactivateTimer += Time.deltaTime;
                if (_deactivateTimer >= deactivateDelay)
                {
                    _anyVisible      = false;
                    _deactivateTimer = 0f;
                    _trap.Deactivate();
                }
            }
        }
    }

    // ── 감지 ─────────────────────────────────────────────────────────

    bool CheckAnyVisible()
    {
        if (detectionRadius > 0f)
        {
            // 범위 기반: Player 레이어 오브젝트가 반경 내에 있는지만 체크 (비용 낮음)
            return Physics.CheckSphere(transform.position, detectionRadius, playerVisibleLayer);
        }

        // 전역: 캐시된 Player 목록에서 레이어 확인
        if (_cachedPlayers == null) return false;

        foreach (Player p in _cachedPlayers)
        {
            if (p == null || p.IsDead) continue;
            // 레이어가 Player이면 '보임' (스텔스 + 피격 노출 포함 모두 Player 레이어)
            if (p.gameObject.layer == _playerLayerId) return true;
        }

        return false;
    }

    // ── 외부 API ─────────────────────────────────────────────────────

    /// <summary>
    /// 플레이어가 씬에 추가/제거(리스폰 등)된 후 외부에서 캐시를 갱신할 때 호출.
    /// </summary>
    public void RefreshPlayerCache() => CachePlayers();

    // ── 내부 ─────────────────────────────────────────────────────────

    void CachePlayers()
    {
        _cachedPlayers = FindObjectsByType<Player>(FindObjectsSortMode.None);
    }

    // ── 에디터 기즈모 ─────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (detectionRadius <= 0f) return;
        Gizmos.color = _anyVisible
            ? new Color(1f, 0.3f, 0.3f, 0.2f)
            : new Color(0.3f, 1f, 0.3f, 0.2f);
        Gizmos.DrawSphere(transform.position, detectionRadius);
        Gizmos.color = _anyVisible
            ? new Color(1f, 0.3f, 0.3f, 0.8f)
            : new Color(0.3f, 1f, 0.3f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
#endif
}
