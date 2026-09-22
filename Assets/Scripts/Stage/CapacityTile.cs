using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 용량 타일 — T.Stage4 함정 랜덤화 ①.
/// SSOT: Assets/Docs/TStage4TrapRandomization.md §1.1 / §3.1(A안) / §4.1
///
/// [동작 — 추(錘) 방식]
///  1. 타일 위 인원이 capacity를 초과하면 sinkSpeed로 "서서히" 침강한다.
///  2. 인원이 capacity 이하로 줄면 restoreSpeed로 빠르게 복귀한다.
///     침강보다 복귀가 훨씬 빠른 것이 의도된 비대칭이다 — 실수를 되돌릴 수 있어야 한다(§1.1).
///  3. maxSinkDepth까지 내려가면 바닥이 빠진다(솔리드 콜라이더 off) → 탑승자는 아래 공허로
///     낙하 → Player.fallDeathY(프리팹 −15) 통과 시 낙사.
///  4. 바닥이 빠진 타일은 그대로 복귀한다. 영구 구멍을 남기는 것은 ①이 아니라 ③(BreakTile)의 몫이다.
///     콜라이더는 **완전히 휴지로 돌아올 때까지 계속 꺼둔다** — 올라오는 판이 떨어지던 탑승자를
///     아래에서 퍼올려 살려버리는 것을 막는 래치다.
///
/// [침강 구간은 하나뿐이다 (2026-09-21 — 경고/붕괴 2구간 폐기)]
///  구안은 dropoutDepth(2.5m, 콜라이더 살아 있음)까지가 '경고 구간', 거기서 maxSinkDepth(6m)까지
///  collapseSpeed로 더 물러나는 '붕괴 구간'이 따로 있었다. **뒤쪽 구간은 규칙에 아무 영향이 없는
///  순수 연출**이었다(그 시점엔 이미 콜라이더가 꺼져 있다). 이해 비용만 치르고 있어서 합쳤다 —
///  이제 깊이는 `maxSinkDepth` 하나이고, **거기 닿는 순간이 곧 바닥이 빠지는 순간**이다.
///  덕분에 경고 색도 정직해졌다: **빨강 = 죽는 순간**(구안의 빨강은 '이미 빠진 뒤'였다).
///
/// [권한 — 로컬 계산 (2026-09-18 확정)]
///  점유 계수·타일 높이 모두 각 머신이 로컬로 계산한다. 새 NetworkVariable·RPC 0개.
///  - 점유 여부는 사실상 '플레이어 위치의 함수'이고 위치는 CNT로 전 머신에 수렴하므로,
///    머신 간 차이는 결과가 아니라 CNT 보간 지연(~100ms)만큼의 '시작 시점' 차이뿐이다.
///  - Host 권한으로 바꿔도 Host 역시 원격 클라를 같은 지연으로 보므로 지연이 사라지지 않고,
///    Host→클라 전파 한 홉이 더 붙어 내가 밟은 타일이 내 화면에서 RTT만큼 늦게 반응한다
///    (T5 투사체 "A안"에서 이미 버린 체감).
///  - 낙사 자체는 이미 Owner 로컬 Y 판정 → ReportFallDeathServerRpc → Host 확정이므로
///    (Player.cs / NetworkPlayerSetup.cs) 탑승자의 생사는 원래부터 Owner 머신에서 결정된다.
///
///  ⚠️ 이 결정의 전제는 "느린 침강"이다. sinkSpeed를 크게 올려 순식간에 떨어지도록 튜닝하면
///     100ms 지연이 체감 가능해지고 권한 설계를 다시 봐야 한다.
///
/// [동시성 — 머신 간 어긋남을 최소화하는 장치]
///  - 점유 집계·높이 적분을 모두 FixedUpdate에서 한다. Time.fixedDeltaTime은 전 머신 동일하므로
///    프레임레이트가 달라도 같은 시간에 같은 깊이가 나온다(Update+deltaTime이면 누적 오차가 생긴다).
///  - 점유는 트리거 Enter/Exit로 즉시 반영한다(다음 FixedUpdate까지 기다리지 않음).
///    Stay는 놓친 진입을 줍는 안전망이다 — PressurePad와 같은 구성.
///  - 남는 오차 = CNT 보간 지연. 그 이상은 로컬에서 줄일 수 없다.
///
/// [감지 — 타일과 함께 내려가는 트리거 콜라이더 (2026-09-21. 정적 감지 기둥 폐기)]
///  같은 GameObject에 **Is Trigger 콜라이더를 하나 더** 둔다. 솔리드 바닥과 감지를 콜라이더
///  두 개로 나누는 것이 전부이고, 런타임 생성물도 중계 컴포넌트도 없다.
///
///  구안은 감지 기둥을 별도 오브젝트로 만들어 **휴지 위치에 고정**했다. "트리거가 타일과 함께
///  내려가면 탑승자가 트리거 밖으로 나가 점유 0 → 복귀 → 재진입 → 침강"의 진동이 생긴다고 봤기
///  때문인데, **그 전제가 틀렸다**: 트리거가 타일과 같이 내려가면 탑승자의 **상대 위치가 바뀌지
///  않으므로** 애초에 밖으로 나갈 일이 없다. 기둥이 아래로 `dropoutDepth + 1`만큼 뻗어 있던 것도
///  "안 움직이니까 내려가는 탑승자를 따라가려던" 보정이었고, 같이 내려가면 통째로 불필요해진다.
///
///  그래서 없어진 필드: `detectHeightAbove` · `detectDepthBelowDropout` · `detectShrinkXZ`.
///  전부 **콜라이더의 Size/Center로 직접 지정**한다 — 씬 뷰에서 보이고 마우스로 잡을 수 있다.
///
/// [씬 설정 — CapacityTile 프리팹 하나로 배포한다]
///  타일은 `Assets/Prefab/CapacityTile.prefab` 인스턴스다. 아래 값이 전부 프리팹에 있으므로
///  판에 몇 개를 깔든 **프리팹 한 번 수정으로 전부 반영된다.**
///  1. 솔리드 콜라이더(Is Trigger = false) — 플레이어가 밟고 설 바닥.
///  2. 감지 콜라이더(Is Trigger = true) — **XZ는 솔리드보다 좁게**(타일 이음새에서 한 사람이
///     양쪽에 동시 계수되는 것을 막는다), **Y는 넉넉하게**(타일 위에 선 사람을 놓치지 않는다).
///     7.8m 타일 기준 로컬 Size (0.9, 5, 0.9) = 월드 7.02 × 5 × 7.02.
///  3. capacity를 설정한다(기본 1).
///  4. 탑다운에서는 높이 변화가 잘 안 읽히므로 warnRenderer를 채워 색으로 알리는 것을 권장한다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CapacityTile : MonoBehaviour
{
    enum TileState
    {
        Idle,      // 휴지 — 깊이 0
        Sinking,   // 과부하 침강 — maxSinkDepth에 닿으면 바닥이 빠진다
        Restoring, // 상승
    }

    [Header("용량")]
    [Tooltip("이 타일이 버티는 최대 인원. 1 = 1타일 1명(T4 기본값).\n" +
             "인스펙터에서 자유롭게 2 이상으로 올리거나 내릴 수 있다. 0으로 두면 아무도 못 선다.")]
    public int capacity = 1;

    [Header("침강")]
    [Tooltip("과부하 중 내려가는 속도(m/s). 느릴수록 간격을 회복할 시간이 길어진다(§3.1 A안).")]
    [SerializeField] float sinkSpeed = 1f;

    [Tooltip("이 깊이(m)에 닿으면 바닥이 빠진다(콜라이더 off → 탑승자 낙하 → 낙사).\n" +
             "유예 시간 = 이 값 ÷ sinkSpeed. 기본값 2.5 ÷ 1 = 2.5초.")]
    [SerializeField] float maxSinkDepth = 2.5f;

    [Header("복귀")]
    [Tooltip("인원이 capacity 이하로 줄었을 때 올라오는 속도(m/s). 침강보다 크게 두는 것이 의도된 비대칭.\n" +
             "순간이동(스냅)이 아닌 이유: 위에 선 플레이어를 뚫고 지나가기 때문.\n" +
             "⚠️ 이 값이 크면 바닥이 빠진 구멍이 그만큼 빨리 닫힌다 — 뒷사람에게는 구멍이 보이지 않는다.")]
    [SerializeField] float restoreSpeed = 8f;

    [Header("경고 색 (선택)")]
    [Tooltip("침강 진행도(0 → maxSinkDepth)를 색으로 보여줄 Renderer. 비우면 색 연출 없음.\n" +
             "탑다운에서는 높이 변화가 거의 안 읽히므로 이 색이 사실상 주 정보 채널이다.\n" +
             "진홍에 닿는 순간이 곧 바닥이 빠지는 순간이다.")]
    [SerializeField] Renderer warnRenderer;
    [Tooltip("URP Lit이면 _BaseColor. WarnMarkerColorFx와 같은 규약.")]
    [SerializeField] string warnColorProperty = "_BaseColor";
    // 색은 WarnPalette 공용: Safe(흰색) → End(진홍).

    [Header("이벤트")]
    [Tooltip("타일 위 인원이 바뀔 때 (currentCount, capacity). UI·연출 전용 — 전 머신에서 발동.")]
    public UnityEvent<int, int> OnOccupancyChanged;

    [Tooltip("과부하가 시작돼 침강이 시작될 때 1회.")]
    public UnityEvent OnOverloadStarted;

    [Tooltip("과부하가 풀려 복귀가 시작될 때 1회.")]
    public UnityEvent OnOverloadEnded;

    [Tooltip("maxSinkDepth 도달 — 바닥이 빠지는 순간 1회.")]
    public UnityEvent OnDropout;

    public int   CurrentCount => _occupants.Count;
    public bool  IsOverloaded => _occupants.Count > Mathf.Max(0, capacity);
    public float SinkDepth    => _depth;
    public bool  HasCollapsed => _collapsedThisCycle;

    readonly HashSet<Player> _occupants = new HashSet<Player>();

    TileState _state = TileState.Idle;
    float     _depth;              // 휴지 위치 기준 아래로 내려간 거리(m). 0 = 휴지.
    bool      _collapsedThisCycle; // 이번 주기에 바닥이 빠졌는가 — 복귀가 끝날 때까지 콜라이더를 꺼두는 래치
    bool      _wasOverloaded;
    int       _lastNotifiedCount = -1;
    float     _lastAppliedDepth;
    bool      _depthApplied;

    Vector3   _restLocalPos;
    Collider  _solid;
    Rigidbody _rb;

    WarnMarkerColorFx _warnFx;

    // ── 초기화 ────────────────────────────────────────────────

    void Awake()
    {
        _restLocalPos = transform.localPosition;

        ResolveColliders();

        // DoorController와 동일한 이동 방식 — Kinematic Rigidbody + MovePosition.
        // transform.position 직접 대입은 위에 선 플레이어와의 충돌이 올바르게 풀리지 않고,
        // 움직이는 Static Collider는 매 프레임 물리 씬의 정적 AABB 트리를 다시 만든다.
        _rb = GetComponent<Rigidbody>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
        _rb.isKinematic   = true;
        _rb.useGravity    = false;
        // 침강이 느리고 탑다운에서 계속 보이므로 보간을 켜 둔다(물리 50Hz → 렌더 프레임 사이 떨림 방지).
        _rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (warnRenderer != null)
            _warnFx = new WarnMarkerColorFx(warnRenderer, warnColorProperty, WarnPalette.Safe, WarnPalette.End);

        ApplyDepth();
    }

    /// <summary>
    /// 솔리드(바닥)와 트리거(감지)를 갈라 잡는다. 같은 GameObject에 둘을 함께 두는 구성이라
    /// GetComponent&lt;Collider&gt;() 한 번으로는 어느 쪽이 잡힐지 알 수 없다.
    /// </summary>
    void ResolveColliders()
    {
        bool hasTrigger = false;

        var cols = GetComponents<Collider>();
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i].isTrigger) hasTrigger = true;
            else if (_solid == null) _solid = cols[i];
        }

        if (_solid == null)
        {
            Debug.LogError(
                $"[CapacityTile] {name}: 솔리드 콜라이더(Is Trigger = false)가 없다 — " +
                "플레이어가 밟고 설 바닥이 없다.", this);
        }

        if (!hasTrigger)
        {
            Debug.LogWarning(
                $"[CapacityTile] {name}: 감지용 트리거 콜라이더가 없다 — 점유가 잡히지 않아 " +
                "이 타일은 절대 가라앉지 않는다. Is Trigger 콜라이더를 하나 더 붙일 것.", this);
        }
    }

    void OnDisable() => _occupants.Clear();

    // ── 점유 ────────────────────────────────────────────────────
    // 이 콜백들은 같은 GameObject의 트리거 콜라이더에서만 온다(솔리드는 Collision 쪽이다).

    void OnTriggerEnter(Collider other) => TryAddOccupant(other);
    void OnTriggerStay(Collider other)  => TryAddOccupant(other);

    void OnTriggerExit(Collider other)
    {
        // GetComponent (GetComponentInParent 아님) — 아래 TryAddOccupant 주석 참고.
        Player p = other.GetComponent<Player>();
        if (p == null) return;
        _occupants.Remove(p);
    }

    void TryAddOccupant(Collider other)
    {
        // Player 콜라이더는 하나가 아니다 — 몸통 CapsuleCollider(루트)와 PlayerPunchHitbox의
        // SphereCollider(자식, 항상 켜짐)가 공존한다. GetComponentInParent로 찾으면 둘 다 같은
        // Player로 잡혀 경계에서 중복 진입/이탈이 생긴다(ColorTile.TryAddOccupant 주석의 그 버그).
        // GetComponent는 Player와 같은 GameObject의 몸통 콜라이더만 통과시킨다.
        // 부활 직후 1초는 세지 않는다 — 생존자 위치에 겹쳐 나타나는 순간 정원이 초과돼
        // 타일이 깨지고 둘 다 떨어진다(ReviveSystemDesign.md §3.1).
        Player p = other.GetComponent<Player>();
        if (p == null || !p.CountsForOccupancy) return;
        _occupants.Add(p);
    }

    void PruneOccupants()
    {
        if (_occupants.Count == 0) return;
        _occupants.RemoveWhere(p => p == null || !p.CountsForOccupancy);
    }

    // ── 상태 진행 ────────────────────────────────────────────────

    void FixedUpdate()
    {
        PruneOccupants();

        int  count      = _occupants.Count;
        int  cap        = Mathf.Max(0, capacity);
        bool overloaded = count > cap;

        if (count != _lastNotifiedCount)
        {
            _lastNotifiedCount = count;
            OnOccupancyChanged?.Invoke(count, cap);
        }

        if (overloaded != _wasOverloaded)
        {
            _wasOverloaded = overloaded;
            if (overloaded) OnOverloadStarted?.Invoke();
            else            OnOverloadEnded?.Invoke();
        }

        float dt = Time.fixedDeltaTime;

        switch (_state)
        {
            case TileState.Idle:
                if (overloaded) _state = TileState.Sinking;
                break;

            case TileState.Sinking:
                if (!overloaded)
                {
                    _state = TileState.Restoring;
                    break;
                }
                _depth += sinkSpeed * dt;
                if (_depth >= maxSinkDepth)
                {
                    _depth = maxSinkDepth;
                    EnterDropout();
                }
                break;

            case TileState.Restoring:
                // 바닥이 빠지지 않은 복귀 중에 다시 과부하가 되면 그 자리에서 다시 가라앉는다.
                // 빠진 뒤의 복귀는 되돌리지 않는다 — 콜라이더가 꺼져 있어 점유 자체가 성립하지 않는다.
                if (overloaded && !_collapsedThisCycle)
                {
                    _state = TileState.Sinking;
                    break;
                }
                _depth -= restoreSpeed * dt;
                if (_depth <= 0f)
                {
                    _depth = 0f;
                    _collapsedThisCycle = false;
                    _state = TileState.Idle;
                }
                break;
        }

        // 콜라이더는 바닥이 빠진 순간부터 휴지로 완전히 돌아올 때까지 꺼둔다.
        // 상승 도중에 켜면 아직 떨어지는 중인 탑승자를 아래에서 퍼올려 살려버린다.
        SetSolidEnabled(!_collapsedThisCycle);

        ApplyDepth();
    }

    void EnterDropout()
    {
        _state = TileState.Restoring;
        _collapsedThisCycle = true;
        OnDropout?.Invoke();
    }

    void ApplyDepth()
    {
        // 격자 한 판이 타일 100개라 대부분의 프레임은 전부 휴지 상태다.
        // 깊이가 그대로면 MovePosition도 색 갱신도 건너뛴다.
        if (_depthApplied && Mathf.Approximately(_depth, _lastAppliedDepth)) return;

        Vector3 restWorld = transform.parent != null
            ? transform.parent.TransformPoint(_restLocalPos)
            : _restLocalPos;

        _rb.MovePosition(restWorld + Vector3.down * _depth);

        // 진홍에 닿는 순간이 곧 바닥이 빠지는 순간이다(구간을 합치면서 정직해진 부분).
        if (_warnFx != null)
            _warnFx.SetProgress(maxSinkDepth > 0.0001f ? _depth / maxSinkDepth : 0f);

        _lastAppliedDepth = _depth;
        _depthApplied     = true;
    }

    void SetSolidEnabled(bool on)
    {
        if (_solid != null && _solid.enabled != on) _solid.enabled = on;
    }

    /// <summary>
    /// 휴지 상태로 즉시 되돌린다. T4는 스테이지 실패 시 씬 전체가 재로드되므로(StageNetworkState 실패 문)
    /// 보통 쓸 일이 없고, 제자리 리셋이 필요한 테스트·툴용이다.
    /// </summary>
    public void ResetTile()
    {
        if (_rb == null) return; // Awake 전(Edit 모드 등) 호출 방지

        _occupants.Clear();
        _state              = TileState.Idle;
        _depth              = 0f;
        _collapsedThisCycle = false;
        _wasOverloaded      = false;
        _lastNotifiedCount  = -1;
        _depthApplied       = false; // 강제로 한 번 적용시킨다
        SetSolidEnabled(true);
        ApplyDepth();
    }

    void OnValidate()
    {
        capacity     = Mathf.Max(0, capacity);
        sinkSpeed    = Mathf.Max(0.01f, sinkSpeed);
        maxSinkDepth = Mathf.Max(0.1f, maxSinkDepth);
        restoreSpeed = Mathf.Max(0.01f, restoreSpeed);
    }

    // ── 기즈모 ────────────────────────────────────────────────

    /// <summary>바닥이 빠지는 깊이를 씬 뷰에 그린다. 감지 범위는 콜라이더라 Unity가 알아서 그려 준다.</summary>
    void OnDrawGizmosSelected()
    {
        Collider col = null;
        var cols = GetComponents<Collider>();
        for (int i = 0; i < cols.Length; i++)
            if (!cols[i].isTrigger) { col = cols[i]; break; }
        if (col == null) return;

        Bounds b = col.bounds;
        Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.8f);
        Gizmos.DrawWireCube(
            new Vector3(b.center.x, b.max.y - maxSinkDepth, b.center.z),
            new Vector3(b.size.x, 0.02f, b.size.z));
    }
}
