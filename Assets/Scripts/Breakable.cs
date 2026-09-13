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
///   warnColorEnabled면 지연 시작 순간 SyncBreakPendingClientRpc로 Client도 경고색 보간을 같이 시작.
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

    /// <summary>stable ID로 Breakable을 찾아 Client 측 경고색(노랑→빨강) 예고만 재생.
    /// StageNetworkState.SyncBreakPendingClientRpc에서 호출.</summary>
    public static void BreakPendingById(int id)
    {
        EnsureRegistryBuilt();
        if (_registry.TryGetValue(id, out Breakable b))
            b?.ApplyPendingWarnFromNetwork();
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

    [Header("사운드 (파괴음 — 3D)")]
    [Tooltip("이 거리(m) 이내에서는 최대 볼륨")]
    [SerializeField] private float destroyMinDistance = 5f;
    [Tooltip("이 거리(m) 밖에서는 완전 무음. 0이면 500으로 처리")]
    [SerializeField] private float destroyMaxDistance = 50f;
    [SerializeField] private AudioRolloffMode destroyRolloffMode = AudioRolloffMode.Logarithmic;

    [Header("파편 / 이펙트")]
    [Tooltip("파괴 시 생성할 파편 프리팹(TileDebrisUtil 공용 — 없으면 생략).\n" +
             "공용 러블(rubble) 조각 하나를 여러 Breakable에서 재사용 — 파괴되는 오브젝트의 현재 " +
             "Renderer.sharedMaterial을 그대로 입혀 색이 항상 일치한다.")]
    [SerializeField] private GameObject debrisPrefab = null;

    [Tooltip("파편 자동 소멸 시간(초). 0이면 자동 소멸 안 함 — 리셋(OnEnable)·비활성화 때는 어차피 " +
             "즉시 정리되므로 새지는 않지만, 리셋이 없는 오브젝트는 파편이 계속 쌓인다.")]
    [SerializeField] private float debrisLifetime = 0f;

    [Tooltip("파편에 가할 임펄스 힘 최소값.")]
    [SerializeField] private float debrisImpulseMin = 2f;

    [Tooltip("파편에 가할 임펄스 힘 최대값.")]
    [SerializeField] private float debrisImpulseMax = 5f;

    [Tooltip("파편 프리팹 스케일 배율. 공용 조각을 이 오브젝트 크기에 맞게 키우거나 줄일 때 사용(1=원본 그대로).")]
    [SerializeField] private float debrisScale = 1f;

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

    [Header("경고 색상 (breakDelay 동안 노랑→빨강)")]
    [Tooltip("true면 breakDelay 동안 이 오브젝트 자신의 머티리얼 색을 warnStartColor→warnEndColor로 " +
             "보간해 파괴를 예고한다. breakDelay가 0이면 예고할 시간이 없으므로 무시된다.\n" +
             "Host/Client 모두 SyncBreakPendingClientRpc로 같은 순간 시작하므로 두 머신에서 동일하게 보인다.")]
    [SerializeField] bool warnColorEnabled = false;

    [Tooltip("색을 입힐 셰이더 프로퍼티. URP Lit 기준 _BaseColor.")]
    [SerializeField] string warnColorProperty = "_BaseColor";

    [SerializeField] Color warnStartColor = Color.yellow;
    [SerializeField] Color warnEndColor = Color.red;

    [Header("이벤트")]
    [Tooltip("최종 파괴 직전 호출. 연출·스테이지 연동 등에 사용.")]
    public UnityEvent OnBreak;

    Renderer[] _renderers;
    Collider[] _colliders;
    bool _broken;
    bool _breakPending;
    Coroutine _breakRoutine;
    int _netIndex = -1;

    // warnColorEnabled 전용 — 렌더러당 하나씩, Awake에서 1회 구성. Client가 SyncBreakPendingClientRpc로
    // 받는 예고 전용 코루틴은 _breakRoutine(파괴 확정까지 담당)과 분리해 따로 추적한다 —
    // Host는 예고+파괴가 한 흐름(BreakSequenceRoutine)이지만 Client는 예고만 로컬로 돌고 실제
    // 파괴는 별도 RPC(SyncBreakClientRpc)로 오기 때문.
    WarnMarkerColorFx[] _warnFx;
    Coroutine _warnRoutine;

    // 스폰해 둔 파편. 리셋(OnEnable)·페이즈 종료(OnDisable)에서 즉시 치워야 "멀쩡하게 복구된
    // 오브젝트 옆에 지난 회차 파편이 뒹구는" 그림이 안 나온다 — TongueController.ClearDebris /
    // MouthBossJawSmash.ClearDebris와 같은 규약. 한 번에 하나만 살아 있다(_broken 가드).
    GameObject _debris;

    // 파괴 회차. 리셋 사이클로 같은 오브젝트가 다시 부서질 때 파편이 복사판으로 튀지 않게 시드에 섞는다.
    // Host는 ApplyFinalBreak, Client는 ApplyBreakFromNetwork로 파괴당 정확히 1회씩 DoBreakVisuals를
    // 지나므로 두 머신의 값이 어긋나지 않는다(난입·재접속이 설계상 없어 세대 차이도 안 생긴다).
    int _breakCycle;

    void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _colliders = GetComponentsInChildren<Collider>(true);

        if (warnColorEnabled)
        {
            _warnFx = new WarnMarkerColorFx[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null)
                    _warnFx[i] = new WarnMarkerColorFx(_renderers[i], warnColorProperty, warnStartColor, warnEndColor);
        }

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
        if (_warnRoutine != null)
        {
            StopCoroutine(_warnRoutine);
            _warnRoutine = null;
        }
        _breakPending = false;
        // 복구는 항상 부모 SetActive false→true 사이클을 지나므로(클래스 주석의 리셋 규약)
        // 여기 한 곳에서 치우면 되살아난 오브젝트와 파편이 겹치는 경우가 없다.
        ClearDebris();
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

        // Client도 같은 순간부터 경고색 예고를 시작하게 방송 — breakDelay는 씬에 저장된 동일
        // 직렬화 값이라 지속시간을 실어보낼 필요 없이 "지금 시작" 트리거만 보낸다.
        if (syncBreakOverNetwork && warnColorEnabled && breakDelay > 0f && _netIndex >= 0)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening && nm.IsServer)
                StageNetworkState.Instance?.SyncBreakPendingClientRpc(_netIndex);
        }

        if (_breakRoutine != null)
            StopCoroutine(_breakRoutine);
        _breakRoutine = StartCoroutine(BreakSequenceRoutine());
    }

    IEnumerator BreakSequenceRoutine()
    {
        if (breakDelay > 0f)
        {
            if (warnColorEnabled)
                yield return WarnColorRoutine(breakDelay);
            else
                yield return new WaitForSeconds(breakDelay);
        }

        _breakRoutine = null;
        _breakPending = false;
        ApplyFinalBreak();
    }

    /// <summary>
    /// Client 전용 진입점: Host의 SyncBreakPendingClientRpc 수신 시 호출. 실제 파괴 판정·확정 없이
    /// 로컬에서 동일한 breakDelay 동안 경고색 보간만 재생한다 — 파괴 확정은 이후 별도로 도착하는
    /// SyncBreakClientRpc(ApplyBreakFromNetwork)가 담당한다.
    /// </summary>
    public void ApplyPendingWarnFromNetwork()
    {
        // Client는 Phase 컨테이너가 Host보다 늦게 켜질 수 있다 — 비활성에서 StartCoroutine은 에러.
        if (_broken || !warnColorEnabled || breakDelay <= 0f || !isActiveAndEnabled) return;
        if (_warnRoutine != null) StopCoroutine(_warnRoutine);
        _warnRoutine = StartCoroutine(WarnColorRoutine(breakDelay));
    }

    /// <summary>duration에 걸쳐 warnStartColor→warnEndColor로 보간. BreakSequenceRoutine(Host, 파괴까지
    /// 이어짐)과 ApplyPendingWarnFromNetwork(Client, 예고만)이 공유 — 별도 StartCoroutine으로 감싸지
    /// 않고 직접 yield return해야 호출부의 코루틴 핸들 하나로 정지(OnDisable 등)가 가능하다.</summary>
    IEnumerator WarnColorRoutine(float duration)
    {
        ApplyWarnProgress(0f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            ApplyWarnProgress(elapsed / duration);
            yield return null;
        }

        ApplyWarnProgress(1f);
    }

    void ApplyWarnProgress(float t)
    {
        if (_warnFx == null) return;
        for (int i = 0; i < _warnFx.Length; i++)
            _warnFx[i]?.SetProgress(t);
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

                    NetworkDamageUtil.ApplyDamage(p, breakDamage);

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

        // 네트워크 지연으로 예고 코루틴이 아직 안 끝났는데 파괴 확정이 먼저 도착할 수 있다 —
        // 그대로 두면 SetVisible(false) 이후에도 계속 색을 갱신하려 든다.
        if (_warnRoutine != null)
        {
            StopCoroutine(_warnRoutine);
            _warnRoutine = null;
        }

        DoBreakVisuals();
        SetVisible(false);
        // damagePlayerOnBreak: Host 전용. Client에서는 실행하지 않음.
    }

    void DoBreakVisuals()
    {
        SFXManager.Instance?.PlayAtPoint(SFXId.Breakable_Destroy, transform.position, destroyMinDistance, destroyMaxDistance, destroyRolloffMode);
        OnBreak?.Invoke();

        // TileDebrisUtil 재사용: 이 오브젝트의 현재 Renderer 재질을 그대로 입히고 bounds 중심에 띄운다.
        // 시드가 전 머신 동일하므로 ApplyFinalBreak(Host)·ApplyBreakFromNetwork(Client) 어느 쪽으로
        // 들어와도 파편이 똑같이 튄다 — 별도 분기 없이 자동으로 동기화된다.
        if (debrisPrefab != null)
        {
            ClearDebris();
            _debris = TileDebrisUtil.BreakTile(gameObject, debrisPrefab, debrisLifetime,
                                                debrisImpulseMin, debrisImpulseMax, DebrisSeed(), debrisScale);
        }
        _breakCycle++;
    }

    /// <summary>
    /// 파편 임펄스 시드. 전 머신이 같은 값을 뽑아야 파편이 똑같이 튄다(로컬 Random 금지 원칙).
    /// _netIndex = 월드 좌표 정렬 stable ID라 Host/Client 항상 동일, _breakCycle = 파괴 회차.
    /// 혼합식은 TongueController.MixSeed / MouthBossJawSmash.MixSeed와 동일한 형태.
    /// 런타임 스폰 Breakable은 레지스트리 구성 뒤 Awake라 _netIndex가 -1로 남는다 —
    /// syncBreakOverNetwork=false로 각 머신 독립 처리하는 대상이라 동기화 문제는 없고,
    /// 같은 세션에서 여러 개가 같은 시드를 쓰는 연출 반복만 생긴다.
    /// </summary>
    int DebrisSeed()
        => NetworkSessionData.Seed ^ (_netIndex * 0x2545F491) ^ (_breakCycle * 0x27220A95);

    /// <summary>
    /// 남아 있는 파편을 즉시 삭제. debrisLifetime을 기다리지 않고 치워야 복구된 오브젝트와
    /// 겹쳐 보이지 않는다(TongueController·MouthBossJawSmash의 ClearDebris와 같은 이름·역할).
    /// </summary>
    void ClearDebris()
    {
        if (_debris == null) return;
        Destroy(_debris);
        _debris = null;
    }

    // ── 리셋 (함정과 동일: 부모 SetActive false→true 사이클로 자동 복원) ─────

    void OnEnable()
    {
        if (_breakRoutine != null)
        {
            StopCoroutine(_breakRoutine);
            _breakRoutine = null;
        }
        if (_warnRoutine != null)
        {
            StopCoroutine(_warnRoutine);
            _warnRoutine = null;
        }
        _breakPending = false;
        _broken = false;
        SetVisible(true);
        ResetWarnColor();
    }

    /// <summary>경고색 보간이 남긴 MaterialPropertyBlock 오버라이드를 지워 원래 머티리얼 색으로
    /// 되돌린다. 리셋 사이클(부모 SetActive false→true)마다 호출 — 다음 파괴 때 다시 노랑부터
    /// 시작해야지 지난 회차의 빨강이 남아있으면 안 된다.</summary>
    void ResetWarnColor()
    {
        if (!warnColorEnabled || _renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null)
                _renderers[i].SetPropertyBlock(null);
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
