using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>
/// 충돌 시 파괴되는 오브젝트 컴포넌트.
/// breakTriggerLayers에 해당하는 오브젝트가 닿으면 파괴 + 파편 이펙트.
/// breakDelay가 0보다 크면 지연 후 렌더/콜라이더 비활성 및 데미지+넉백 처리.
///
/// [네트워크 동기화]
/// syncBreakOverNetwork = true (기본): Host만 충돌 판정 → SyncBreakClientRpc로 Client에 동기화.
/// syncBreakOverNetwork = false      : 각 머신 독립 처리 (런타임 스폰 Boulder 등).
///
/// [권장 사용]
/// - 돌굴림 맵의 Floor/Wall 피스에 부착
/// - 돌 프리팹 → "Boulder" 레이어 설정 → breakTriggerLayers에 Boulder 지정
/// - TrapProjectile은 Wall/Floor 파괴 판정이 없으므로(2026-07-27 제거) 돌이 항상 계속 굴러감
///
/// [외부 호출]
/// Break() 를 직접 호출하면 트리거 없이 동일한 지연/즉시 파괴 시퀀스 시작 (연출용 등)
/// </summary>
[RequireComponent(typeof(Collider))]
public class Breakable : MonoBehaviour
{
    // ── 정적 레지스트리 (stable ID 기반 동기화용) ────────────────
    // [버그 수정 2026-08 — T.Stage5 Lump Host/Client 미동기화]
    // 이전 방식(Awake() 호출 순서로 0부터 카운터 증가)은 "Host/Client 모두 씬 로드 시
    // 동일 순서로 Awake가 실행된다"는 가정에 의존했는데, 실측 결과 그 가정이 깨졌다
    // (PhaseManager.EnterPhaseOnClient()의 Phase 캐치업 배치 등으로 Host/Client의 실제
    // Awake 타이밍·묶음이 달라질 수 있음 — TStageNetworkBoard.md 관련 진단 참고).
    // 대신 Awake 순서와 무관하게 항상 같은 결과가 나오는 월드 좌표(고정 씬 데이터라
    // Host/Client 어느 프로세스에서 읽어도 값이 동일) 기준 정렬로 stable ID를 부여한다.
    // 씬(리로드 포함) 로드마다 처음 필요해지는 시점에 한 번만 전체를 모아 재구성 —
    // Scene.handle로 "같은 이름 씬이어도 리로드마다 다른 세대"를 구분한다.
    static readonly Dictionary<int, Breakable> _registry = new();
    static int _registeredSceneHandle = int.MinValue;

    /// <summary>stable ID로 Breakable을 찾아 Client 측 파괴 연출을 적용. StageNetworkState에서 호출.</summary>
    public static void BreakById(int id)
    {
        EnsureRegistryBuilt();
        if (_registry.TryGetValue(id, out Breakable b))
            b?.ApplyBreakFromNetwork();
    }

    /// <summary>
    /// 현재 씬(리로드 세대 포함)에 존재하는 모든 Breakable을 월드 좌표 기준으로 정렬해
    /// stable ID를 재부여. 이미 이번 씬 세대에서 구성됐다면 즉시 반환(중복 작업 없음).
    /// 비활성 오브젝트(Stage5.4처럼 Phase 진입 전까지 꺼져 있는 컨테이너 하위 포함)도
    /// FindObjectsInactive.Include로 함께 수집해야 Awake 시점과 무관하게 완전한 목록이 된다.
    /// </summary>
    static void EnsureRegistryBuilt()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (_registeredSceneHandle == activeScene.handle) return;

        _registry.Clear();

        Breakable[] all = FindObjectsByType<Breakable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Array.Sort(all, CompareByWorldPosition);

        for (int i = 0; i < all.Length; i++)
        {
            all[i]._netIndex = i;
            _registry[i] = all[i];
        }

        _registeredSceneHandle = activeScene.handle;
    }

    /// <summary>x→y→z 순으로 비교. 씬 파일에 저장된 고정 좌표라 Host/Client가 항상 동일한 순서를 얻는다.</summary>
    static int CompareByWorldPosition(Breakable a, Breakable b)
    {
        Vector3 pa = a.transform.position;
        Vector3 pb = b.transform.position;

        int c = pa.x.CompareTo(pb.x);
        if (c != 0) return c;

        c = pa.y.CompareTo(pb.y);
        if (c != 0) return c;

        return pa.z.CompareTo(pb.z);
    }

    [Header("파괴 조건")]
    [Tooltip("이 레이어마스크에 해당하는 오브젝트가 닿을 때만 파괴.\n0(Nothing)이면 모든 충돌에 반응.")]
    [SerializeField] private LayerMask breakTriggerLayers;

    [Header("파괴 지연")]
    [Tooltip("충돌 후 최종 파괴(숨김·파편·즉사)까지 대기 시간(초). 0이면 즉시.")]
    [SerializeField] private float breakDelay = 0f;

    [Tooltip("지연 구간 시작 시 재생. 없으면 생략.")]
    [SerializeField] private AudioClip breakDelaySound = null;

    [Tooltip("지연 사운드 볼륨 (0~1)")]
    [SerializeField] [Range(0f, 1f)] private float breakDelaySoundVolume = 1f;

    [Header("파편 / 이펙트")]
    [Tooltip("파괴 시 생성할 파편 또는 Particle 프리팹. 없으면 생략.")]
    [SerializeField] private GameObject debrisPrefab = null;

    [Tooltip("파편 자동 소멸 시간(초). 0이면 자동 소멸 안 함.")]
    [SerializeField] private float debrisLifetime = 0f;

    [Header("사운드")]
    [Tooltip("true: SFXLibrary Mouth_TeethBreak 1/2 교차 재생 (M.Stage4 이빨 등)")]
    [SerializeField] private bool useMouthTeethBreakSfx = false;

    [Tooltip("최종 파괴 시 재생할 AudioClip. useMouthTeethBreakSfx 가 켜져 있으면 무시.")]
    [SerializeField] private AudioClip breakSound = null;

    [Tooltip("파괴 사운드 볼륨 (0~1)")]
    [SerializeField] [Range(0f, 1f)] private float breakSoundVolume = 1f;

    [Header("범위 데미지 + 넉백 (선택)")]
    [Tooltip("최종 파괴 시점에 반경 내 플레이어에게 데미지+넉백을 적용할지 여부.\n" +
             "지연 시간이 있으면 지연이 끝난 뒤에만 판정.")]
    [FormerlySerializedAs("killPlayerOnBreak")]
    [SerializeField] private bool damagePlayerOnBreak = false;

    [Tooltip("데미지량. damagePlayerOnBreak=true일 때만 사용.")]
    [SerializeField] private int breakDamage = 1;

    [Tooltip("넉백 힘 최소값. damagePlayerOnBreak=true일 때만 사용 (세기만 매번 랜덤).")]
    [SerializeField] private float knockbackForceMin = 5f;

    [Tooltip("넉백 힘 최대값. damagePlayerOnBreak=true일 때만 사용.")]
    [SerializeField] private float knockbackForceMax = 10f;

    [Tooltip("판정 반경(m). damagePlayerOnBreak=true일 때만 사용.")]
    [FormerlySerializedAs("killRadius")]
    [SerializeField] private float damageRadius = 0f;

    [Tooltip("플레이어 감지 레이어. damagePlayerOnBreak=true일 때 사용.")]
    [SerializeField] private LayerMask playerLayer;

    [Header("네트워크")]
    [Tooltip("true: 멀티에서 Host만 파괴 판정 후 Client에 동기화 (씬 배치 Breakable 기본값).\n" +
             "false: 각 머신에서 독립 처리 (런타임 스폰 오브젝트에 부착된 Breakable 등).")]
    [SerializeField] bool syncBreakOverNetwork = true;

    [Header("이벤트")]
    [Tooltip("최종 파괴 직전 호출. 연출·스테이지 연동 등에 사용.")]
    public UnityEvent OnBreak;

    Renderer[] _renderers;
    Collider[] _colliders;
    bool _broken;
    bool _breakPending;
    Coroutine _breakRoutine;
    int _netIndex = -1;

    void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _colliders = GetComponentsInChildren<Collider>(true);

        // 이번 씬 세대의 레지스트리가 아직 없으면 전체를 한 번에 구성(월드 좌표 정렬).
        // 이미 다른 Breakable의 Awake()나 BreakById()가 먼저 구성해뒀다면 즉시 반환.
        EnsureRegistryBuilt();
    }

    void OnDestroy()
    {
        // 이 인스턴스가 여전히 자기 자리를 차지하고 있을 때만 제거 — 씬 리로드로 다음
        // 세대 레지스트리가 이미 구성된 뒤라면(다른 인스턴스가 같은 id를 차지) 건드리지 않는다.
        if (_registry.TryGetValue(_netIndex, out Breakable current) && current == this)
            _registry.Remove(_netIndex);
    }

    void OnDisable()
    {
        if (_breakRoutine != null)
        {
            StopCoroutine(_breakRoutine);
            _breakRoutine = null;
        }
        _breakPending = false;
    }

    // ── 물리 충돌 (non-trigger Collider) ──────────────────────────────

    void OnCollisionEnter(Collision col)
    {
        if (_broken || _breakPending) return;
        if (ShouldBreak(col.gameObject))
            StartBreakSequence();
    }

    // ── 트리거 충돌 ──────────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (_broken || _breakPending) return;
        if (ShouldBreak(other.gameObject))
            StartBreakSequence();
    }

    // ── 파괴 조건 판단 ───────────────────────────────────────────────

    bool ShouldBreak(GameObject other)
    {
        if (breakTriggerLayers.value != 0 &&
            (breakTriggerLayers.value & (1 << other.layer)) == 0)
            return false;

        return true;
    }

    // ── 파괴 처리 (외부에서 직접 호출 가능) ─────────────────────────

    /// <summary>
    /// 레이어 검사 없이 파괴 시퀀스 시작. breakDelay 적용.
    /// </summary>
    public void Break()
    {
        if (_broken || _breakPending) return;
        StartBreakSequence();
    }

    void StartBreakSequence()
    {
        if (_broken || _breakPending) return;

        // syncBreakOverNetwork: Host만 파괴 판정, Client는 SyncBreakClientRpc 수신 후 ApplyBreakFromNetwork() 실행
        // false(런타임 스폰 오브젝트): 각 머신 독립 처리 허용
        if (syncBreakOverNetwork)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening && !nm.IsServer) return;
        }

        _breakPending = true;
        if (_breakRoutine != null)
            StopCoroutine(_breakRoutine);
        _breakRoutine = StartCoroutine(BreakSequenceRoutine());
    }

    IEnumerator BreakSequenceRoutine()
    {
        if (breakDelay > 0f)
        {
            if (breakDelaySound != null)
                AudioSource.PlayClipAtPoint(breakDelaySound, transform.position, breakDelaySoundVolume);
            yield return new WaitForSeconds(breakDelay);
        }

        _breakRoutine = null;
        _breakPending = false;
        ApplyFinalBreak();
    }

    void ApplyFinalBreak()
    {
        if (_broken) return;
        _broken = true;

        // 멀티: Host가 파괴 확정 → Client에 stable ID 브로드캐스트
        if (syncBreakOverNetwork && _netIndex >= 0)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening && nm.IsServer)
                StageNetworkState.Instance?.SyncBreakClientRpc(_netIndex);
        }

        DoBreakVisuals();

        // 데미지 + 넉백 판정: Host에서만
        if (damagePlayerOnBreak && damageRadius > 0f)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || nm.IsServer)
            {
                Collider[] hits = Physics.OverlapSphere(transform.position, damageRadius, playerLayer);
                for (int i = 0; i < hits.Length; i++)
                {
                    Player p = hits[i].GetComponent<Player>()
                               ?? hits[i].GetComponentInParent<Player>();
                    if (p == null) continue;

                    NetworkDamageUtil.ApplyDamage(p, breakDamage, false);

                    Vector3 dir = p.transform.position - transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
                    dir.Normalize();
                    float force = UnityEngine.Random.Range(knockbackForceMin, knockbackForceMax);
                    NetworkDamageUtil.ApplyKnockback(p, dir, force);
                }
            }
        }

        SetVisible(false);
    }

    /// <summary>
    /// 네트워크 동기화 수신 시 Client 측 파괴 연출.
    /// 브로드캐스트·즉사 판정 없이 로컬에서만 연출 + SetVisible(false).
    /// </summary>
    public void ApplyBreakFromNetwork()
    {
        if (_broken) return;
        _broken = true;
        DoBreakVisuals();
        SetVisible(false);
        // damagePlayerOnBreak: Host 전용. Client에서는 실행하지 않음.
    }

    void DoBreakVisuals()
    {
        OnBreak?.Invoke();

        if (debrisPrefab != null)
        {
            GameObject debris = Instantiate(debrisPrefab, transform.position, transform.rotation);
            if (debrisLifetime > 0f)
                Destroy(debris, debrisLifetime);
        }

        if (useMouthTeethBreakSfx)
            SFXManager.Instance?.PlayMouthTeethBreak(transform.position);
        else if (breakSound != null)
            AudioSource.PlayClipAtPoint(breakSound, transform.position, breakSoundVolume);
    }

    // ── 리셋 (함정과 동일: 부모 SetActive false→true 사이클로 자동 복원) ─────

    void OnEnable()
    {
        if (_breakRoutine != null)
        {
            StopCoroutine(_breakRoutine);
            _breakRoutine = null;
        }
        _breakPending = false;
        _broken = false;
        SetVisible(true);
    }

    void SetVisible(bool active)
    {
        foreach (Renderer r in _renderers) if (r != null) r.enabled = active;
        foreach (Collider  c in _colliders) if (c != null) c.enabled = active;
    }

    // ── 에디터 기즈모 ─────────────────────────────────────────────────
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!damagePlayerOnBreak || damageRadius <= 0f) return;

        Gizmos.color = new Color(1f, 0.2f, 0f, 0.15f);
        Gizmos.DrawSphere(transform.position, damageRadius);

        Gizmos.color = new Color(1f, 0.2f, 0f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, damageRadius);
    }
#endif
}
