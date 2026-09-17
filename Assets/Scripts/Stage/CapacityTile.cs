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
///  3. dropoutDepth까지 내려가면 바닥이 빠진다(솔리드 콜라이더 off) → 탑승자는 아래 공허로
///     낙하 → Player.fallDeathY(프리팹 −15) 통과 시 낙사.
///  4. 붕괴한 타일은 maxSinkDepth까지 물러났다가 스스로 복귀한다. 영구 파괴는 ①이 아니라
///     ③(랜덤 바닥 파괴)의 몫이므로 여기서 구멍을 남기지 않는다.
///
/// [권한 — 로컬 계산 (2026-09-18 확정)]
///  점유 계수·타일 높이 모두 각 머신이 로컬로 계산한다. 새 NetworkVariable·RPC 0개.
///  - 점유 여부는 '플레이어 위치만의 함수'이고 위치는 CNT로 전 머신에 수렴하므로,
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
/// [씬 설정]
///  1. 바닥 타일(Renderer + 솔리드 Collider, Is Trigger = false)에 이 컴포넌트를 붙인다.
///  2. capacity를 설정한다(기본 1).
///  3. 감지 기둥과 Kinematic Rigidbody는 런타임에 자동 생성되므로 손댈 것이 없다.
///  4. 탑다운에서는 높이 변화가 잘 안 읽히므로 warnRenderer를 채워 색으로 알리는 것을 권장한다.
///  선택 상태에서 기즈모로 감지 기둥과 dropout 깊이를 미리 볼 수 있다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CapacityTile : MonoBehaviour
{
    enum TileState
    {
        Idle,       // 휴지 — 깊이 0
        Sinking,    // 과부하 침강 — 콜라이더 유지, 아직 복구 가능
        Collapsing, // 바닥 빠짐 — 콜라이더 off, maxSinkDepth까지 후퇴(복구 불가)
        Restoring,  // 상승
    }

    [Header("용량")]
    [Tooltip("이 타일이 버티는 최대 인원. 1 = 1타일 1명(T4 기본값).\n" +
             "인스펙터에서 자유롭게 2 이상으로 올리거나 내릴 수 있다. 0으로 두면 아무도 못 선다.")]
    public int capacity = 1;

    [Header("침강 — 경고 구간 (복구 가능)")]
    [Tooltip("과부하 중 내려가는 속도(m/s). 느릴수록 간격을 회복할 시간이 길어진다(§3.1 A안).")]
    [SerializeField] float sinkSpeed = 1f;

    [Tooltip("이 깊이(m)에 닿으면 바닥이 빠진다. sinkSpeed 1 · 이 값 2.5 → 약 2.5초의 유예.")]
    [SerializeField] float dropoutDepth = 2.5f;

    [Header("침강 — 붕괴 구간 (복구 불가)")]
    [Tooltip("바닥이 빠진 뒤 타일이 물러나는 속도(m/s). 연출 전용 — 이 시점엔 콜라이더가 이미 꺼져 있다.")]
    [SerializeField] float collapseSpeed = 6f;

    [Tooltip("타일이 내려가는 최대 깊이(m). 여기 닿으면 복귀를 시작한다.")]
    [SerializeField] float maxSinkDepth = 6f;

    [Header("복귀")]
    [Tooltip("인원이 capacity 이하로 줄었을 때 올라오는 속도(m/s). 침강보다 크게 두는 것이 의도된 비대칭.\n" +
             "순간이동(스냅)이 아닌 이유: 위에 선 플레이어를 뚫고 지나가기 때문.")]
    [SerializeField] float restoreSpeed = 8f;

    [Header("감지 기둥 (런타임 자동 생성)")]
    [Tooltip("타일 윗면 기준 위로 이만큼(m)까지를 '올라서 있다'로 본다. 플레이어 키보다 크게 잡을 것.")]
    [SerializeField] float detectHeightAbove = 2.5f;

    [Tooltip("dropoutDepth보다 이만큼(m) 더 아래까지 감지한다. 침강 중인 탑승자를 계속 점유로 세기 위함 —\n" +
             "짧으면 내려가던 타일이 탑승자를 놓쳐 혼자 있는데도 계속 내려간 것처럼 보인다.")]
    [SerializeField] float detectDepthBelowDropout = 1f;

    [Tooltip("이웃 타일과 맞닿은 이음새에서 한 사람이 양쪽 타일에 동시 계수되는 것을 줄이려고 XZ를 이만큼(m) 줄인다.")]
    [SerializeField] float detectShrinkXZ = 0.1f;

    [Header("경고 색 (선택)")]
    [Tooltip("침강 진행도(0 → dropoutDepth)를 색으로 보여줄 Renderer. 비우면 색 연출 없음.\n" +
             "탑다운에서는 높이 변화가 거의 안 읽히므로 이 색이 사실상 주 정보 채널이다.")]
    [SerializeField] Renderer warnRenderer;
    [Tooltip("URP Lit이면 _BaseColor. WarnMarkerColorFx와 같은 규약.")]
    [SerializeField] string warnColorProperty = "_BaseColor";
    [SerializeField] Color warnSafeColor   = Color.white;
    [SerializeField] Color warnDangerColor = Color.red;

    [Header("이벤트")]
    [Tooltip("타일 위 인원이 바뀔 때 (currentCount, capacity). UI·연출 전용 — 전 머신에서 발동.")]
    public UnityEvent<int, int> OnOccupancyChanged;

    [Tooltip("과부하가 시작돼 침강이 시작될 때 1회.")]
    public UnityEvent OnOverloadStarted;

    [Tooltip("과부하가 풀려 복귀가 시작될 때 1회.")]
    public UnityEvent OnOverloadEnded;

    [Tooltip("dropoutDepth 도달 — 바닥이 빠지는 순간 1회.")]
    public UnityEvent OnDropout;

    public int   CurrentCount => _occupants.Count;
    public bool  IsOverloaded => _occupants.Count > Mathf.Max(0, capacity);
    public float SinkDepth    => _depth;
    public bool  HasCollapsed => _state == TileState.Collapsing || _collapsedThisCycle;

    readonly HashSet<Player> _occupants = new HashSet<Player>();

    TileState _state = TileState.Idle;
    float     _depth;                 // 휴지 위치 기준 아래로 내려간 거리(m). 0 = 휴지.
    bool      _collapsedThisCycle;    // 이번 주기에 붕괴를 거쳤는가 — 상승 중 콜라이더를 계속 꺼두기 위한 래치
    bool      _wasOverloaded;
    int       _lastNotifiedCount = -1;
    float     _lastAppliedDepth;
    bool      _depthApplied;

    Vector3   _restLocalPos;
    Collider  _solid;
    Rigidbody _rb;

    GameObject          _detectObject;
    WarnMarkerColorFx   _warnFx;

    static Transform s_detectRoot;    // 런타임 감지 기둥들을 모아두는 정리용 루트

    // ── 초기화 ────────────────────────────────────────────────

    void Awake()
    {
        _restLocalPos = transform.localPosition;

        _solid = GetComponent<Collider>();
        if (_solid.isTrigger)
        {
            Debug.LogWarning(
                $"[CapacityTile] {name}: 솔리드 콜라이더가 Is Trigger로 켜져 있다. " +
                "플레이어가 밟고 설 바닥이어야 하므로 꺼야 한다.", this);
        }

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
            _warnFx = new WarnMarkerColorFx(warnRenderer, warnColorProperty, warnSafeColor, warnDangerColor);

        BuildDetectionColumn();
        ApplyDepth();
    }

    void OnEnable()
    {
        if (_detectObject != null) _detectObject.SetActive(true);
    }

    void OnDisable()
    {
        if (_detectObject != null) _detectObject.SetActive(false);
        _occupants.Clear();
    }

    void OnDestroy()
    {
        if (_detectObject != null) Destroy(_detectObject);
    }

    /// <summary>
    /// 휴지 위치에 고정된 감지 기둥을 만든다. 타일의 자식이 아니라 별도 오브젝트인 이유는
    /// CapacityTileTriggerRelay 주석 참고(피드백 루프 차단).
    ///
    /// 부모를 두지 않는 이유: 부모에 스케일·회전이 걸려 있으면 월드 AABB로 잰 크기를 그대로
    /// BoxCollider.size에 넣을 수 없어 기둥이 타일과 어긋난다. T4 복도 바닥은 정적이라
    /// (MovingCorridor가 움직이는 것은 앞뒤 벽이다) 루트에 두어도 따라갈 대상이 없다.
    /// </summary>
    void BuildDetectionColumn()
    {
        Bounds b = _solid.bounds; // 휴지 상태의 월드 AABB — 아직 한 번도 움직이지 않았다.

        float topY   = b.max.y;
        float upperY = topY + detectHeightAbove;
        float lowerY = topY - (dropoutDepth + detectDepthBelowDropout);
        float height = Mathf.Max(0.1f, upperY - lowerY);

        if (s_detectRoot == null)
            s_detectRoot = new GameObject("CapacityTileDetectors").transform;

        _detectObject = new GameObject($"{name}__Detect");
        _detectObject.transform.SetParent(s_detectRoot, false);
        _detectObject.transform.SetPositionAndRotation(
            new Vector3(b.center.x, (upperY + lowerY) * 0.5f, b.center.z), Quaternion.identity);

        var box = _detectObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(
            Mathf.Max(0.1f, b.size.x - detectShrinkXZ * 2f),
            height,
            Mathf.Max(0.1f, b.size.z - detectShrinkXZ * 2f));

        _detectObject.AddComponent<CapacityTileTriggerRelay>().Bind(this);
    }

    // ── 점유 ────────────────────────────────────────────────────

    public void HandleTriggerEnter(Collider other) => TryAddOccupant(other);
    public void HandleTriggerStay(Collider other)  => TryAddOccupant(other);

    public void HandleTriggerExit(Collider other)
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
        Player p = other.GetComponent<Player>();
        if (p == null || p.IsDead) return;
        _occupants.Add(p);
    }

    void PruneOccupants()
    {
        if (_occupants.Count == 0) return;
        _occupants.RemoveWhere(p => p == null || p.IsDead);
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
                if (_depth >= dropoutDepth)
                {
                    _depth = dropoutDepth;
                    EnterCollapse();
                }
                break;

            case TileState.Collapsing:
                _depth += collapseSpeed * dt;
                if (_depth >= maxSinkDepth)
                {
                    _depth = maxSinkDepth;
                    _state = TileState.Restoring;
                }
                break;

            case TileState.Restoring:
                // 붕괴를 거치지 않은 복귀 중에 다시 과부하가 되면 그 자리에서 다시 가라앉는다.
                // 붕괴를 거친 복귀는 되돌리지 않는다 — 콜라이더가 꺼져 있어 점유 자체가 성립하지 않는다.
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

        // 콜라이더는 붕괴한 순간부터 휴지로 완전히 돌아올 때까지 꺼둔다.
        // 상승 도중에 켜면 아직 떨어지는 중인 탑승자를 아래에서 퍼올려 살려버린다.
        SetSolidEnabled(!_collapsedThisCycle);

        ApplyDepth();
    }

    void EnterCollapse()
    {
        _state = TileState.Collapsing;
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

        // 경고 색은 '붕괴까지 얼마나 남았는가'만 보여준다 — 붕괴 이후 후퇴 구간은 이미 끝난 판정이다.
        if (_warnFx != null)
            _warnFx.SetProgress(dropoutDepth > 0.0001f ? _depth / dropoutDepth : 0f);

        _lastAppliedDepth = _depth;
        _depthApplied     = true;
    }

    void SetSolidEnabled(bool on)
    {
        if (_solid != null && _solid.enabled != on) _solid.enabled = on;
    }

    /// <summary>
    /// 휴지 상태로 즉시 되돌린다. T4는 사망 시 씬 전체가 재로드되므로(StageResetOnPlayerDeath)
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

    // 인스펙터에서 자유롭게 튜닝하는 값들이라, 상태 머신이 멈추는 조합만 막아둔다
    // (특히 maxSinkDepth ≤ dropoutDepth면 붕괴 구간이 한 프레임 만에 끝나 바닥 빠짐이 안 보인다).
    void OnValidate()
    {
        capacity      = Mathf.Max(0, capacity);
        sinkSpeed     = Mathf.Max(0.01f, sinkSpeed);
        dropoutDepth  = Mathf.Max(0.1f, dropoutDepth);
        maxSinkDepth  = Mathf.Max(dropoutDepth + 0.1f, maxSinkDepth);
        collapseSpeed = Mathf.Max(0.01f, collapseSpeed);
        restoreSpeed  = Mathf.Max(0.01f, restoreSpeed);
    }

    // ── 기즈모 ────────────────────────────────────────────────

    // 타일을 100개 배치해야 하므로 감지 기둥과 붕괴 깊이를 씬 뷰에서 바로 확인할 수 있게 한다.
    void OnDrawGizmosSelected()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return;

        Bounds b     = col.bounds;
        float  topY  = b.max.y;
        float  upper = topY + detectHeightAbove;
        float  lower = topY - (dropoutDepth + detectDepthBelowDropout);

        Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
        Gizmos.DrawWireCube(
            new Vector3(b.center.x, (upper + lower) * 0.5f, b.center.z),
            new Vector3(
                Mathf.Max(0.1f, b.size.x - detectShrinkXZ * 2f),
                Mathf.Max(0.1f, upper - lower),
                Mathf.Max(0.1f, b.size.z - detectShrinkXZ * 2f)));

        // 바닥이 빠지는 깊이
        Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.8f);
        Gizmos.DrawWireCube(
            new Vector3(b.center.x, topY - dropoutDepth, b.center.z),
            new Vector3(b.size.x, 0.02f, b.size.z));
    }
}
