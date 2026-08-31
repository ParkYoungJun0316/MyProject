using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 플레이어를 추적하여 함정을 조준시키는 범용 컴포넌트.
///
/// [스텔스 연동]
/// - playerVisibleLayer 에 "Player" 레이어만 등록
/// - 플레이어가 스텔스(PlayerStealth 레이어) 상태이면 타겟에서 자동 제외 → 공격 안 함
/// - 스텔스 해제(Player 레이어)로 돌아오면 자동으로 다시 타겟 포함 → 공격 재개
/// - TrapStealthSensor 없이 이 컴포넌트 단독으로 스텔스 감지까지 처리함
///
/// [범위 제한]
/// - trackZone(Collider)을 지정하면 그 안에 있는 플레이어만 타겟으로 인정 (예: 튜토리얼 은신 패드).
/// - trackZone = null(기본)이면 범위 제한 없음 — 기존 M/T 스테이지 동작 그대로.
/// - 존 밖으로 나가면 스텔스와 동일하게 타겟에서 제외됨(공격 안 함).
///
/// [네트워크 — 타겟 판정 동기화]
/// - 실제 발사(DropTrap.FireAt / ArrowTrap 발사체 Spawn)는 항상 Host 판정 기준(nm.IsServer 가드).
/// - 회전(rotateToTarget)은 예전엔 Client가 자기 로컬로 따로 계산했는데,
///   Host의 실제 발사 방향과 어긋날 수 있었다(포신은 A를 조준한 것처럼 보이는데 화살은 B에게
///   날아가는 등 — 함정 회전은 씬 오브젝트의 평범한 Transform이라 동기화가 없기 때문).
/// - 그래서 Host만 타겟을 계산해 StageNetworkState._trackerTargets(NetworkList 슬롯, Door §3.1과
///   동일 원칙)에 기록하고, Client는 로컬 재계산 없이 그 값만 반영한다. 지속 상태이므로 RPC가 아니라
///   NV — 스폰 시 자동 동기화 + GetTrackerTarget()으로 늦은 구독 캐치업까지 된다(§9 Sync 규칙).
/// - 오프라인(NetworkManager 없음/미리스닝) 씬에서는 이 분기 자체가 없어 기존처럼 완전히 로컬 계산.
///
/// [Random 모드 제약 — 중요]
/// - Random은 **DropTrap 발사 사이클(dropInterval)에서만** 유효하다.
/// - 회전·인디케이터는 매 프레임 도는 경로라 Random을 쓰면 호출마다 재추첨돼 조준이 떨리고 타겟 변경
///   동기화가 폭주한다. 그래서 이 경로는 targetMode와 무관하게 **항상 Nearest**로 동작한다.
/// - rotateToTarget = true(ArrowTrap)에 Random을 지정하면 OnValidate/Awake 경고를 띄운다.
///
/// [ArrowTrap과 사용 시]
/// - rotateToTarget = true 로 설정
/// - 오브젝트(또는 firePoint)를 보이는 플레이어 방향으로 회전 → 발사 방향 자동 조준
/// - ArrowTrap의 fireAtSeconds 스케줄은 그대로 유지됨 (타이밍은 스케줄이, 방향은 이 컴포넌트가 제어)
/// - 보이는 플레이어가 없으면 회전을 멈추고 마지막 방향 유지
///
/// [DropTrap과 사용 시]
/// - dropInterval 에 발사 주기 입력 (0이면 비활성)
/// - dropInterval 마다 보이는 플레이어 위치로 FireAt() 자동 호출
/// - 보이는 플레이어가 없으면 해당 사이클을 건너뜀 (발사 안 함)
/// - DropTrap의 fireAtSeconds 는 비워둘 것 (이 컴포넌트가 발사 타이밍 전담)
///
/// [공통]
/// - activateDelay 이후에 추적 시작
/// - controlTrapActivation = true 시, activateDelay 후 TrapBase.Activate() 도 자동 호출
///   (PhaseManager가 Activate를 이미 제어하고 있으면 false 로 유지)
/// - 부모에 StageManager가 있으면 IsStarted == true 가 될 때까지 DropLoop/Activate 대기
///   (게임 시작 전 FireAt/낙하 방지)
/// </summary>
[RequireComponent(typeof(TrapBase))]
public class TrapPlayerTracker : MonoBehaviour
{
    public enum TargetMode
    {
        Nearest,  // 가장 가까운 플레이어 1명
        Random,   // 살아있는 플레이어 중 랜덤 1명
        All,      // 살아있는 플레이어 전원 (DropTrap 전용)
    }

    [Header("활성화")]
    [Tooltip("추적 시작까지 대기 시간 (초). 0 = 즉시")]
    [SerializeField] float activateDelay = 0f;

    [Tooltip("true: activateDelay 후 TrapBase.Activate() 도 자동 호출\n" +
             "false: 함정 활성화는 TrapBase.startActive 또는 PhaseManager 가 제어 (권장)")]
    [SerializeField] bool controlTrapActivation = false;

    [Header("타겟 설정")]
    [Tooltip("어떤 플레이어를 노릴지.\n" +
             "Random은 DropTrap 발사 사이클(dropInterval) 전용 — 회전·인디케이터는 매 프레임 경로라 " +
             "항상 Nearest로 동작한다(Random이면 매 호출 재추첨돼 조준 떨림+동기화 폭주).")]
    [SerializeField] TargetMode targetMode = TargetMode.Nearest;

    [Tooltip("공격 대상으로 인식할 플레이어 레이어 마스크.\n" +
             "Player 레이어만 선택 → 스텔스(PlayerStealth) 상태면 자동 제외됨.\n" +
             "0(없음)이면 스텔스 무시하고 모든 살아있는 플레이어를 타겟으로 삼음.")]
    [SerializeField] LayerMask playerVisibleLayer;

    [Tooltip("추적 범위를 이 Collider(Trigger) 안으로 제한. 비워두면(null) 범위 제한 없음(기존 동작).\n" +
             "Box/Sphere/Capsule 또는 Convex MeshCollider만 정확히 판정됨(Collider.ClosestPoint 사용).\n" +
             "존 밖 플레이어는 타겟에서 제외 → DropTrap은 해당 사이클 건너뜀, ArrowTrap 회전은 마지막 방향 유지(발사 스케줄은 계속 돔).")]
    [SerializeField] Collider trackZone;

    [Header("DropTrap 전용 — 발사 주기")]
    [Tooltip("DropTrap 사용 시 플레이어 위치로 낙하를 호출하는 주기 (초). 0 = 비활성")]
    [SerializeField] float dropInterval = 0f;

    [Header("회전 추적 (ArrowTrap 전용)")]
    [Tooltip("플레이어 방향으로 오브젝트를 회전. false = 회전 없음")]
    [SerializeField] bool rotateToTarget = false;

    [Tooltip("회전 속도 (도/초). 0 = 즉시 전환")]
    [SerializeField] float rotateSpeed = 0f;

    [Tooltip("Y축만 회전 (수평 조준). true 권장")]
    [SerializeField] bool rotateYAxisOnly = true;

    TrapBase       _trap;
    DropTrap       _dropTrap;
    StageManager   _stageManager;
    Player[]       _players;
    bool           _activated;
    bool           _hasStarted;
    Quaternion     _initialRotation;
    Player         _currentTarget;

    // 회전도 인디케이터도 없는 트래커(예: 인디케이터 없는 DropTrap)는 "지금 누구 조준 중"을
    // 표시할 소비자가 아예 없다 — 타겟 추적·동기화를 건너뛴다(불필요한 NV 쓰기 방지).
    bool           _needsVisualTarget;

    // Client가 마지막으로 수신한 타겟 colorIndex. Player 캐시가 NV 도착보다 늦게 채워지는
    // 경우(스폰 순서)에 대비해 캐시 갱신 시 이 값으로 다시 해석한다. -1 = 타겟 없음.
    int            _networkTargetColorIndex = -1;

    // ── 타겟 브로드캐스트용 stable ID 레지스트리 (ArrowTrap과 동일 패턴) ──────────────
    // Awake 호출 순서는 Host/Client 간 PhaseManager SetActive 타이밍에 따라 달라질 수 있어
    // (ArrowTrap 2026-07-27 수정 사유와 동일) 씬 계층 경로로 정렬한 결정적 순서로 ID를 부여한다.
    static readonly Dictionary<int, TrapPlayerTracker> _registry = new Dictionary<int, TrapPlayerTracker>();
    static bool _registryBuilt = false;
    int _netIndex = -1;

    // NV 슬롯 개수 기준 — _registry.Count를 쓰면 트래커가 개별 파괴될 때 개수가 줄어
    // 슬롯이 재생성되며 index가 오염된다(Door InitDoorSlots와 동일한 "배정 후 개수 고정" 원칙).
    static int _registrySize = 0;

    static void EnsureRegistryBuilt()
    {
        if (_registryBuilt) return;
        _registryBuilt = true;

        TrapPlayerTracker[] all = FindObjectsByType<TrapPlayerTracker>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OrderBy(a => GetHierarchyPath(a.transform), StringComparer.Ordinal)
            .ToArray();

        for (int i = 0; i < all.Length; i++)
        {
            all[i]._netIndex = i;
            _registry[i] = all[i];
        }
        _registrySize = all.Length;
    }

    static string GetHierarchyPath(Transform t)
    {
        string path = t.name + "#" + t.GetSiblingIndex().ToString("D4");
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "#" + t.GetSiblingIndex().ToString("D4") + "/" + path;
        }
        return path;
    }

    /// <summary>현재 락온 중인(보이는) 타겟 플레이어. 없으면 null.</summary>
    public Player CurrentTarget => _currentTarget;

    /// <summary>보이는(스텔스 아닌) 플레이어를 현재 타겟으로 잡고 있는지 여부.</summary>
    public bool IsTargetLocked => _currentTarget != null;

    /// <summary>타겟이 바뀔 때(획득/변경/상실) 발생. null = 타겟 없음.</summary>
    public event System.Action<Player> OnTargetChanged;

    void Awake()
    {
        _trap            = GetComponent<TrapBase>();
        _dropTrap        = GetComponent<DropTrap>();
        _stageManager    = GetComponentInParent<StageManager>();
        _initialRotation = transform.rotation;
        EnsureRegistryBuilt();

        // 회전이 없으면 "지금 누구 조준 중"을 볼 소비자가 없다 → 추적·NV 쓰기 생략.
        _needsVisualTarget = rotateToTarget;
    }

    void Start()
    {
        _hasStarted = true;
        InitTracking();
        // 네트워크·오프라인 모두 스폰 완료 시 캐시 갱신
        // (Start 시점에 플레이어가 없을 경우를 대비)
        PlayerSpawnCoordinator.OnPlayersReady += RefreshPlayerCache;
        if (PlayerSpawnCoordinator.IsReady) RefreshPlayerCache();

        // Instance는 StageNetworkState.Awake에서 대입되므로 Start 시점엔 항상 준비돼 있다
        // (BossFightObjective와 동일 전제). Host도 구독은 하지만 핸들러에서 스스로 걸러낸다.
        if (StageNetworkState.Instance != null)
        {
            StageNetworkState.Instance.OnTrackerTargetChanged += HandleNetworkTrackerTarget;
            // 늦은 구독 캐치업 — 구독 전에 이미 확정된 현재값을 1회 읽는다(IsDoorOpen 패턴).
            if (!IsTargetAuthority() && _netIndex >= 0)
                ApplyNetworkTarget(StageNetworkState.Instance.GetTrackerTarget(_netIndex));
        }
    }

    void OnDestroy()
    {
        PlayerSpawnCoordinator.OnPlayersReady -= RefreshPlayerCache;
        if (StageNetworkState.Instance != null)
            StageNetworkState.Instance.OnTrackerTargetChanged -= HandleNetworkTrackerTarget;

        _registry.Remove(_netIndex);
        // 씬의 마지막 TrapPlayerTracker가 사라지면 레지스트리를 비워 다음 씬 로드 시 재구성되게 한다.
        if (_registry.Count == 0)
        {
            _registryBuilt = false;
            _registrySize  = 0;
        }
    }

    // Stage SetActive(false → true) 사이클 시 자동 재시작
    void OnEnable()
    {
        if (!_hasStarted) return;
        InitTracking();
    }

    void OnDisable()
    {
        _activated = false;
        StopAllCoroutines();
        SetCurrentTarget(null);

        // 페이즈 리셋 시 마지막 추적 방향이 남지 않도록 초기 회전으로 복원.
        // rotateToTarget = false 이면 회전을 건드리지 않았으므로 복원도 생략.
        if (rotateToTarget)
            transform.rotation = _initialRotation;
    }

    void SetCurrentTarget(Player target)
    {
        if (_currentTarget == target) return;
        _currentTarget = target;
        OnTargetChanged?.Invoke(target);

        // Host(온라인)만 NV에 기록 — Client가 ApplyNetworkTarget()으로 이 함수를 호출할 때는
        // nm.IsServer가 false라 아래 블록이 자연히 스킵된다(되쓰기·루프 없음).
        // Despawn 이후 호출(씬 언로드·사망 리로드 중 OnDisable) 대비는 SetTrackerTarget 내부 IsSpawned 가드.
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && nm.IsServer && _netIndex >= 0)
        {
            int colorIndex = target != null ? (int)target.playerColorType : -1;
            StageNetworkState.Instance?.SetTrackerTarget(_netIndex, colorIndex);
        }
    }

    /// <summary>
    /// 온라인 Host이거나 오프라인 씬이면 로컬 계산이 권위. 순수 Client면 NV 수신값만 반영한다.
    /// </summary>
    bool IsTargetAuthority()
    {
        var nm = NetworkManager.Singleton;
        return nm == null || !nm.IsListening || nm.IsServer;
    }

    /// <summary>StageNetworkState._trackerTargets 변경 수신 — 내 슬롯이면 Client 표시에 반영.</summary>
    void HandleNetworkTrackerTarget(int index, int colorIndex)
    {
        if (index != _netIndex) return;
        if (IsTargetAuthority()) return; // Host는 자기 로컬 계산이 이미 권위
        ApplyNetworkTarget(colorIndex);
    }

    void ApplyNetworkTarget(int colorIndex)
    {
        _networkTargetColorIndex = colorIndex;

        if (colorIndex < 0)
        {
            SetCurrentTarget(null);
            return;
        }

        Player match = null;
        if (_players != null)
        {
            foreach (Player p in _players)
            {
                if (p != null && !p.IsDead && (int)p.playerColorType == colorIndex)
                {
                    match = p;
                    break;
                }
            }
        }
        SetCurrentTarget(match);
    }

    void InitTracking()
    {
        RefreshPlayerCache();
        StopAllCoroutines();
        StartCoroutine(TrackingBootstrapRoutine());
    }

    /// <summary>
    /// StageManager 자식이면 StartStage() 될 때까지 대기 후 activateDelay 적용.
    /// 독립 함정(_stageManager == null)은 즉시(또는 딜레이만) 진행.
    /// </summary>
    IEnumerator TrackingBootstrapRoutine()
    {
        if (_stageManager != null)
            while (!_stageManager.IsStarted)
                yield return null;

        if (activateDelay > 0f)
            yield return new WaitForSeconds(activateDelay);

        BeginTracking();
    }

    void BeginTracking()
    {
        _activated = true;

        if (controlTrapActivation)
            _trap.Activate();

        if (_dropTrap != null && dropInterval > 0f)
            StartCoroutine(DropLoop());
    }

    // ── DropTrap 발사 루프 ─────────────────────────────────────────────

    IEnumerator DropLoop()
    {
        while (_activated)
        {
            FireAtPlayers();
            yield return new WaitForSeconds(dropInterval);
        }
    }

    void FireAtPlayers()
    {
        if (_dropTrap == null) return;

        if (targetMode == TargetMode.All)
        {
            foreach (Player p in _players)
            {
                if (!IsValidTarget(p)) continue;
                _dropTrap.FireAt(p.transform.position);
            }
        }
        else
        {
            Player target = GetSingleTarget();
            if (target != null)
                _dropTrap.FireAt(target.transform.position);
        }
    }

    // ── ArrowTrap 방향 회전 ─────────────────────────────────────────────

    void Update()
    {
        if (!_activated)
        {
            SetCurrentTarget(null);
            return;
        }

        // 타겟 판정은 Host(또는 오프라인)만 로컬로 계산 — 이 값이 실제 발사 방향의 기준이자
        // SetCurrentTarget을 통해 온라인이면 NV로 Client에 전파된다(위 SetCurrentTarget 참고).
        // 순수 Client는 여기서 재계산하지 않는다 — 로컬로 다시 계산하면 지연·판정 차이로 회전/인디케이터가
        // Host의 실제 발사 방향과 어긋날 수 있어서다. _currentTarget은 NV 수신값만 신뢰한다.
        if (_needsVisualTarget && IsTargetAuthority())
            SetCurrentTarget(GetVisualTarget());

        if (!rotateToTarget) return;
        if (_dropTrap != null) return; // DropTrap은 회전 불필요
        if (_currentTarget == null) return;

        Vector3 dir = _currentTarget.transform.position - transform.position;
        if (rotateYAxisOnly) dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir.normalized);

        if (rotateSpeed <= 0f)
            transform.rotation = targetRot;
        else
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRot, rotateSpeed * Time.deltaTime);
    }

    // ── 타겟 선택 ──────────────────────────────────────────────────────

    /// <summary>
    /// 플레이어가 공격 가능한 상태인지 확인.
    /// playerVisibleLayer가 설정된 경우, 해당 레이어(Player)에 있어야만 타겟으로 인정.
    /// PlayerStealth 레이어이면 false → 공격 제외.
    /// playerVisibleLayer가 0이면 스텔스 무시하고 항상 true.
    /// </summary>
    bool IsVisible(Player p)
    {
        if (playerVisibleLayer.value == 0) return true;
        return (playerVisibleLayer.value & (1 << p.gameObject.layer)) != 0;
    }

    /// <summary>
    /// trackZone이 지정된 경우 그 안에 있는지 검사. trackZone == null이면 항상 true(범위 제한 없음).
    /// Collider.ClosestPoint는 convex 콜라이더에서만 정확 — Box/Sphere/Capsule 또는 Convex MeshCollider 권장.
    /// </summary>
    bool IsInZone(Player p)
    {
        if (trackZone == null) return true;
        Vector3 pos = p.transform.position;
        Vector3 closest = trackZone.ClosestPoint(pos);
        return (closest - pos).sqrMagnitude < 0.0001f;
    }

    bool IsValidTarget(Player p)
    {
        return p != null && !p.IsDead && IsVisible(p) && IsInZone(p);
    }

    /// <summary>
    /// 회전·인디케이터(지속 표시)용 타겟 — targetMode와 무관하게 항상 Nearest.
    /// 이 경로는 매 프레임 돌기 때문에 Random(호출마다 재추첨)을 쓰면 조준이 매 프레임 흔들리고
    /// 타겟 변경 NV 쓰기가 초당 수십 회로 폭주한다. Random은 DropTrap 발사 사이클 전용.
    /// </summary>
    Player GetVisualTarget() => GetNearestTarget();

    Player GetNearestTarget()
    {
        if (_players == null || _players.Length == 0) return null;

        Player nearest = null;
        float  minSqr  = float.MaxValue;
        foreach (Player p in _players)
        {
            if (!IsValidTarget(p)) continue;
            float sqr = (p.transform.position - transform.position).sqrMagnitude;
            if (sqr < minSqr)
            {
                minSqr  = sqr;
                nearest = p;
            }
        }
        return nearest;
    }

    /// <summary>DropTrap 발사 사이클용 타겟 — targetMode를 그대로 따른다(Random 포함).</summary>
    Player GetSingleTarget()
    {
        if (_players == null || _players.Length == 0) return null;

        if (targetMode == TargetMode.Random)
        {
            int validCount = 0;
            foreach (Player p in _players)
                if (IsValidTarget(p)) validCount++;

            if (validCount == 0) return null;

            int pick = UnityEngine.Random.Range(0, validCount);
            int idx  = 0;
            foreach (Player p in _players)
            {
                if (!IsValidTarget(p)) continue;
                if (idx == pick) return p;
                idx++;
            }
            return null;
        }

        // Nearest (All 모드에서 단일 타겟이 필요한 경우도 Nearest 사용)
        return GetNearestTarget();
    }

    // ── 외부 API ───────────────────────────────────────────────────────

    /// <summary>
    /// 씬에서 플레이어가 추가 / 제거된 경우 외부에서 호출하여 캐시 갱신.
    /// TrapStealthSensor.RefreshPlayerCache() 와 동일한 패턴.
    /// </summary>
    public void RefreshPlayerCache()
    {
        _players = FindObjectsByType<Player>(FindObjectsSortMode.None);

        if (IsTargetAuthority())
        {
            // Host: 씬의 트래커 수만큼 NV 슬롯 확보. 모든 트래커가 호출하지만 개수가 맞으면
            // InitTrackerTargetSlots가 no-op이라 멱등하다. OnPlayersReady 경로로 오면
            // StageNetworkState 스폰이 끝난 뒤라 IsSpawned 가드도 통과한다.
            StageNetworkState.Instance?.InitTrackerTargetSlots(_registrySize);
        }
        else
        {
            // Client: NV가 캐시보다 먼저 도착했을 수 있으므로 마지막 수신값을 새 캐시로 재해석.
            // (이게 없으면 Host의 다음 타겟 변경까지 계속 "타겟 없음"으로 보인다)
            ApplyNetworkTarget(_networkTargetColorIndex);
        }
    }

    /// <summary>외부(PhaseManager 이벤트 등)에서 추적을 즉시 중단할 때 호출.</summary>
    public void StopTracking() => _activated = false;

    /// <summary>외부에서 추적을 재개할 때 호출.</summary>
    public void ResumeTracking() => _activated = true;

#if UNITY_EDITOR
    // 인스펙터에서 잘못된 조합을 즉시 알려준다(값은 건드리지 않는다 — 씬 데이터는 사용자 소유).
    void OnValidate()
    {
        if (rotateToTarget && targetMode == TargetMode.Random)
            Debug.LogWarning($"[TrapPlayerTracker] {name}: rotateToTarget(ArrowTrap 조준)에는 Random을 쓸 수 없습니다 " +
                             "— 조준은 Nearest로 동작합니다. Random은 DropTrap의 dropInterval 발사 전용.", this);
    }
#endif
}
