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

    [Header("복구 연출 (선택 — 판정은 항상 즉시, 이건 시각 연출만)")]
    [Tooltip("복구 시 살짝 부풀었다 가라앉는 연출 시간(초). 0이면 연출 없음(기존과 동일, 즉시 복구만).\n" +
             "스케일은 원래 크기 '이상'으로만 움직인다 — Renderer와 Collider가 같은 Transform을 쓰는 " +
             "FloorTile류에서 콜라이더가 작아지면 복구 직후 그 칸에 선 플레이어가 빠져 낙사할 수 있다.")]
    [SerializeField] float restorePopDuration = 0f;

    [Tooltip("부풀어 오르는 최대 배율(0.12 = 최대 112%). 0이면 연출 없음과 동일.")]
    [SerializeField] float restorePopAmplitude = 0.12f;

    [Tooltip("여러 타일이 한꺼번에 복구될 때 조금씩 다르게 보이도록 지속시간에 주는 랜덤 폭(초, ±). 0이면 랜덤 없음.\n" +
             "머신마다 다른 값이 나오지만 콜라이더가 원래 크기보다 작아지는 순간이 없으므로 판정은 갈리지 않는다.")]
    [SerializeField] float restorePopJitter = 0f;

    [Header("이벤트")]
    [Tooltip("최종 파괴 직전 호출. 연출·스테이지 연동 등에 사용.")]
    public UnityEvent OnBreak;

    Renderer[] _renderers;
    Collider[] _colliders;
    bool _broken;
    bool _breakPending;
    Coroutine _breakRoutine;
    Coroutine _restorePopRoutine;
    Vector3 _originalLocalScale;
    bool _everEnabled;
    int _netIndex = -1;

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
        _originalLocalScale = transform.localScale;

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
        StopRestorePop();
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
        if (_breakRoutine != null)
            StopCoroutine(_breakRoutine);
        _breakRoutine = StartCoroutine(BreakSequenceRoutine());
    }

    IEnumerator BreakSequenceRoutine()
    {
        if (breakDelay > 0f)
        {
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
        StopRestorePop();

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
        StopRestorePop();
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
        _breakPending = false;
        _broken = false;
        SetVisible(true);

        // 씬 로드와 Phase 컨테이너 활성화(PhaseManager.EnterPhase의 objectsToEnable.SetActive(true))도
        // 이 OnEnable을 지난다 — 최초 활성화에서 바닥 전체가 부풀면 복구 연출이 아니라 버그로 보인다.
        // 두 번째 활성화부터가 실제 복구다(함정 리셋 사이클 / TongueController.RestoreAll 등).
        // _broken 기준으로 게이트하면 안 된다 — 혀(M.Stage4)·MouthBossJawSmash(M.Boss P4)는
        // Breakable을 지나지 않고 타일 GameObject를 직접 SetActive로 껐다 켜므로 _broken이 항상 false다.
        if (_everEnabled)
            StartRestorePop();
        _everEnabled = true;
    }

    void SetVisible(bool active)
    {
        foreach (Renderer r in _renderers) if (r != null) r.enabled = active;
        foreach (Collider  c in _colliders) if (c != null) c.enabled = active;
    }

    // ── 복구 연출 (선택 — 판정과 분리된 순수 시각 연출) ──────────────────

    /// <summary>
    /// restorePopDuration(또는 진폭)이 0이면 아무 것도 안 함 — 기존과 동일한 즉시 복구.
    /// 판정(SetVisible)은 이 호출 전에 이미 끝나 있고, 스케일도 원래 크기 이상으로만 움직이므로
    /// 콜라이더가 작아지는 순간이 없다(낙사 판정에 영향 없음).
    /// </summary>
    void StartRestorePop()
    {
        StopRestorePop();
        if (restorePopDuration <= 0f || restorePopAmplitude <= 0f) return;
        _restorePopRoutine = StartCoroutine(RestorePopRoutine());
    }

    /// <summary>
    /// 팝이 실제로 돌고 있을 때만 스케일을 원복한다 — Awake가 아직 안 돈 인스턴스
    /// (EnsureRegistryBuilt가 FindObjectsInactive.Include로 등록한 비활성 오브젝트 등)에서
    /// _originalLocalScale이 0인 채 덮어써 타일이 0 스케일로 박히는 걸 막는다.
    /// </summary>
    void StopRestorePop()
    {
        if (_restorePopRoutine == null) return;
        StopCoroutine(_restorePopRoutine);
        _restorePopRoutine = null;
        transform.localScale = _originalLocalScale;
    }

    IEnumerator RestorePopRoutine()
    {
        float duration = restorePopDuration;
        // 전역 RNG 스트림은 필요할 때만 당긴다(MouthBossJawSmash.ShuffleSeeded와 같은 원칙).
        if (restorePopJitter > 0f)
            duration += UnityEngine.Random.Range(-restorePopJitter, restorePopJitter);
        duration = Mathf.Max(0.01f, duration);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            // 1 → 1+진폭 → 1. 절대 1 미만으로 내려가지 않는다 = 콜라이더가 원래보다 작아지는 순간이 없다.
            transform.localScale = _originalLocalScale * (1f + restorePopAmplitude * Mathf.Sin(p * Mathf.PI));
            yield return null;
        }
        transform.localScale = _originalLocalScale;
        _restorePopRoutine = null;
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
