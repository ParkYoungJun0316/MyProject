using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 충돌 시 파괴되는 오브젝트 컴포넌트.
/// breakTriggerLayers에 해당하는 오브젝트가 닿으면 파괴 + 파편 이펙트.
/// breakDelay가 0보다 크면 지연 후 렌더/콜라이더 비활성 및 즉사 처리.
///
/// [네트워크 동기화]
/// syncBreakOverNetwork = true (기본): Host만 충돌 판정 → SyncBreakClientRpc로 Client에 동기화.
/// syncBreakOverNetwork = false      : 각 머신 독립 처리 (런타임 스폰 Boulder 등).
///
/// [권장 사용]
/// - 돌굴림 맵의 Floor/Wall 피스에 부착
/// - 돌 프리팹 → "Boulder" 레이어 설정 → breakTriggerLayers에 Boulder 지정
/// - TrapProjectile.destroyOnWall=false, destroyOnFloor=false 로 설정해야 돌이 계속 굴러감
///
/// [외부 호출]
/// Break() 를 직접 호출하면 트리거 없이 동일한 지연/즉시 파괴 시퀀스 시작 (연출용 등)
/// </summary>
[RequireComponent(typeof(Collider))]
public class Breakable : MonoBehaviour
{
    // ── 정적 레지스트리 (stable ID 기반 동기화용) ────────────────
    // ID는 씬 로드마다 0부터 순서대로 부여. 파괴·제거가 일어나도 ID는 불변.
    // Host/Client 모두 씬 로드 시 동일 순서로 Awake가 실행되므로 ID가 일치.
    static readonly Dictionary<int, Breakable> _registry = new();
    static int _nextId = 0;

    /// <summary>stable ID로 Breakable을 찾아 Client 측 파괴 연출을 적용. StageNetworkState에서 호출.</summary>
    public static void BreakById(int id)
    {
        if (_registry.TryGetValue(id, out Breakable b))
            b?.ApplyBreakFromNetwork();
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

    [Header("범위 즉사 (선택)")]
    [Tooltip("최종 파괴 시점에 반경 내 플레이어를 즉사시킬지 여부.\n" +
             "지연 시간이 있으면 지연이 끝난 뒤에만 판정.")]
    [SerializeField] private bool killPlayerOnBreak = false;

    [Tooltip("즉사 반경(m). killPlayerOnBreak=true일 때만 사용.")]
    [SerializeField] private float killRadius = 0f;

    [Tooltip("플레이어 감지 레이어. killPlayerOnBreak=true일 때 사용.")]
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

        // 씬 리로드 시 첫 Breakable이 Awake되는 시점에 레지스트리를 초기화.
        // (이전 씬의 stale 항목 방지)
        if (_registry.Count == 0) _nextId = 0;

        _netIndex = _nextId++;
        _registry[_netIndex] = this;
    }

    void OnDestroy()
    {
        // 씬 언로드·리로드 시 제거. ID 자체는 불변이므로 다른 오브젝트 ID에 영향 없음.
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

        // 즉사 판정: Host(또는 오프라인)에서만
        if (killPlayerOnBreak && killRadius > 0f)
        {
            var nm = NetworkManager.Singleton;
            bool isNetworkActive = nm != null && nm.IsListening;
            if (!isNetworkActive || nm.IsServer)
            {
                Collider[] hits = Physics.OverlapSphere(transform.position, killRadius, playerLayer);
                for (int i = 0; i < hits.Length; i++)
                {
                    Player p = hits[i].GetComponent<Player>()
                               ?? hits[i].GetComponentInParent<Player>();
                    if (p == null) continue;

                    if (isNetworkActive)
                    {
                        var netSetup = p.GetComponent<NetworkPlayerSetup>();
                        if (netSetup != null) { netSetup.ApplyDamageFromServer(9999); continue; }
                    }
                    p.KillInstantly();
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
        // killPlayerOnBreak: Host 전용. Client에서는 실행하지 않음.
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
        if (!killPlayerOnBreak || killRadius <= 0f) return;

        Gizmos.color = new Color(1f, 0.2f, 0f, 0.15f);
        Gizmos.DrawSphere(transform.position, killRadius);

        Gizmos.color = new Color(1f, 0.2f, 0f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, killRadius);
    }
#endif
}
