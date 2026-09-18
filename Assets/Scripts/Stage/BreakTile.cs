using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 밟으면 무너지는 바닥 타일 — T.Stage4 함정 랜덤화 ③.
/// SSOT: Assets/Docs/TStage4TrapRandomization.md §1.4 / §4.1
///
/// [동작 — 밟기 → 경고 → 영구 붕괴]
///  1. 대기 — 평범한 바닥이다. **후보라는 표시를 하지 않는다**(머티리얼도 일반 바닥과 동일).
///     경고 연출이 이미 있으므로 미리 금 간 모습을 보여줄 필요가 없고, 후보 위치가 보이면
///     그 자체가 암기 대상이 되어 랜덤화의 목적(§0)이 사라진다.
///  2. 밟힘 — 플레이어가 윗면을 밟는 순간 경고가 시작된다.
///  3. 경고 — warnMarker가 노랑→빨강으로 물든다.
///  4. 붕괴 — 솔리드 콜라이더를 끄고 아래로 떨어진다. 위에 있던 사람은 공허로 낙하하고
///     Player.fallDeathY(프리팹 −15)를 지나며 낙사한다. **복귀는 없다.**
///
/// [①(CapacityTile)과의 역할 분담]
///  용량 타일은 maxSinkDepth까지 물러났다가 스스로 돌아온다 — 영구 구멍을 남기는 것은 ③의 몫이다.
///  ①은 팀(겹치지 말 것), ③은 각자 생존(내가 밟은 것은 내가 책임)이라는 축도 여기서 갈린다.
///
/// [권한 — 로컬 즉시 연출 + Host 확정 (2026-09-18 확정)]
///  "누가 밟았나"는 시드로도 서버 시각으로도 만들 수 없는 정보라, **낙사와 같은 모델**을 쓴다
///  (Player 로컬 Y → ReportFallDeathServerRpc → Host 확정).
///
///   · 밟은 당사자의 **Owner 머신**만 보고한다. 다른 머신은 자기가 본 충돌로 보고하지 않는다 —
///     CNT 보간 때문에 가장자리를 스치는 접촉이 머신마다 갈리는데, 붕괴가 영구적이라
///     한 번 갈리면 지형이 영영 달라진다.
///   · 보고한 머신은 **RPC 왕복을 기다리지 않고 즉시 마커를 켠다.** 내가 밟은 타일이 내 화면에서
///     RTT만큼 늦게 반응하는 것(투사체 "A안"의 그 체감)을 피하기 위한 것이고, 연출만 앞당길 뿐
///     붕괴 시각은 건드리지 않는다.
///   · **붕괴 시각은 Host가 정한 서버 시각 하나뿐이다.** 그래서 경고가 뜨는 순간은 머신마다
///     RTT만큼 달라도 바닥이 빠지는 순간은 전 머신이 동일하다(MovingCorridor의 틱 결정론과 같은 사상).
///
///  ⚠️ 서버 확정이 오기 전에는 **절대 무너지지 않는다**(_collapseAt = 미확정). 보고가 유실돼도
///     그 머신만 혼자 무너지는 일은 생기지 않는다 — 어긋나느니 안 무너지는 쪽이 안전하다.
///
/// [씬 설정]
///  1. 바닥 타일(Renderer + 솔리드 Collider, Is Trigger = false)에 붙인다.
///  2. 타일 위를 덮는 경고 마커를 자식으로 두고 SpikeLaneWarnMarker를 붙여 warnMarker에 연결한다.
///     (비우면 경고 없이 무너진다 — 튜닝·테스트용이고 실제 배치에서는 채울 것)
///  3. 머티리얼은 **일반 바닥과 동일하게** 맞출 것 (위 1번 — 후보 노출 금지).
///  4. Rigidbody는 런타임 자동 생성이라 손댈 것이 없다.
///  5. 개수는 씬에 몇 개를 붙이느냐로 정한다 — 코드에 quota가 없다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BreakTile : MonoBehaviour
{
    enum State
    {
        Idle,       // 대기 — 아직 아무도 안 밟음
        Warning,    // 경고 중 — 아직 밟을 수 있다
        Collapsing, // 낙하 중 — 콜라이더 이미 off
        Gone,       // 소멸 — 영구 구멍
    }

    [Header("경고")]
    [Tooltip("이 타일의 경고 마커. SpikeTrap·혀·M.Boss 턱과 같은 컴포넌트(노랑→빨강)로 통일한다.\n" +
             "타일 위를 덮도록 스케일을 맞춰 자식으로 배치할 것. 비우면 경고 연출이 생략된다.")]
    [SerializeField] SpikeLaneWarnMarker warnMarker;

    [Header("붕괴")]
    [Tooltip("무너진 뒤 떨어지는 속도(m/s).")]
    [SerializeField] float collapseSpeed = 9f;

    [Tooltip("이 깊이(m)까지 떨어지면 Renderer를 끈다. 낙사는 플레이어 자신의 Y로 판정되므로\n" +
             "Player.fallDeathY(−15)와 맞출 필요는 없다 — 구멍 바닥에 슬래브가 비쳐 보이지 않게 하는 값이다.")]
    [SerializeField] float fallAwayDepth = 12f;

    [Tooltip("윗면 접촉으로 인정할 여유(m). 접촉점이 타일 윗면에서 이만큼 아래까지면 '밟았다'로 본다.\n" +
             "옆면을 스치는 충돌을 밟기로 오인하지 않기 위한 값.")]
    [SerializeField] float topContactTolerance = 0.25f;

    [Header("이벤트")]
    [Tooltip("경고가 시작될 때 1회. SFX 등 연출용.")]
    public UnityEvent OnWarnStarted;

    [Tooltip("바닥이 빠지는 순간 1회. 전 머신에서 같은 서버 시각에 발동한다.")]
    public UnityEvent OnCollapsed;

    public bool IsArmed   => _state == State.Warning;
    public bool HasFallen => _state == State.Collapsing || _state == State.Gone;

    /// <summary>아직 아무도 밟지 않았는가. Host가 중복 보고를 거르는 데 쓴다.</summary>
    public bool IsIdle => _state == State.Idle;

    State  _state = State.Idle;
    bool   _reported;                    // 이 머신이 이미 Host에 보고했는가(중복 ServerRpc 방지)
    double _collapseAt = double.MaxValue; // Host가 확정한 붕괴 서버 시각. 미확정이면 MaxValue
    float  _depth;

    BreakTileDirector _director;
    int               _index = -1;

    Vector3   _restLocalPos;
    Collider  _solid;
    Renderer  _renderer;
    Rigidbody _rb;

    void Awake()
    {
        _restLocalPos = transform.localPosition;

        _solid = GetComponent<Collider>();
        if (_solid.isTrigger)
        {
            Debug.LogWarning(
                $"[BreakTile] {name}: 솔리드 콜라이더가 Is Trigger로 켜져 있다. " +
                "플레이어가 밟고 설 바닥이어야 하므로 꺼야 한다.", this);
        }

        _renderer = GetComponent<Renderer>();

        // CapacityTile과 동일한 이동 방식 — Kinematic Rigidbody + MovePosition.
        // 움직이는 Static Collider는 매 프레임 물리 씬의 정적 AABB 트리를 다시 만든다.
        _rb = GetComponent<Rigidbody>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
        _rb.isKinematic   = true;
        _rb.useGravity    = false;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    /// <summary>BreakTileDirector가 판을 활성화한 직후 호출 — 보고 경로와 전 머신 공통 인덱스를 심는다.</summary>
    public void Bind(BreakTileDirector director, int index)
    {
        _director = director;
        _index    = index;
    }

    // ── 밟힘 감지 (로컬) ────────────────────────────────────────

    void OnCollisionEnter(Collision collision) => TryStep(collision);

    // Enter를 놓친 경우(타일 위에서 스폰·텔레포트 착지 등)의 안전망 — PressurePad의 Stay와 같은 역할.
    void OnCollisionStay(Collision collision) => TryStep(collision);

    void TryStep(Collision collision)
    {
        if (_state != State.Idle || _reported) return;
        if (_director == null || _index < 0) return;

        Player p = collision.collider.GetComponent<Player>();
        if (p == null || p.IsDead) return;

        // 밟은 당사자의 머신만 보고한다 — 클래스 주석의 권한 항목 참고.
        NetworkObject netObj = p.GetComponent<NetworkObject>();
        if (netObj != null && !netObj.IsOwner) return;

        if (!IsTopContact(collision)) return;

        _reported = true;
        _director.ReportStep(_index);

        // 보고한 머신만 RPC 왕복을 기다리지 않고 미리 켠다. 붕괴 시각은 여전히 Host 확정값이다.
        StartWarnVisual();
    }

    /// <summary>
    /// 옆면을 스치는 충돌을 밟기로 오인하지 않기 위한 판정. 접촉점이 타일 윗면 근처면 밟은 것이다.
    /// 노멀 방향은 콜라이더 순서에 따라 부호가 뒤집혀 신뢰할 수 없으므로 접촉점 높이로 본다.
    /// </summary>
    bool IsTopContact(Collision collision)
    {
        float topY = _solid.bounds.max.y;
        for (int i = 0; i < collision.contactCount; i++)
            if (collision.GetContact(i).point.y >= topY - topContactTolerance) return true;
        return false;
    }

    void StartWarnVisual()
    {
        if (_state != State.Idle) return;
        _state = State.Warning;

        warnMarker?.PlayWarning(_director != null ? _director.WarnSeconds : 1f);
        OnWarnStarted?.Invoke();
    }

    // ── Host 확정 ───────────────────────────────────────────────

    /// <summary>
    /// Host가 확정한 붕괴 서버 시각을 받는다. 전 머신이 이 값 하나로 같은 순간에 무너진다.
    /// 아직 경고를 시작하지 않은 머신(=내가 밟지 않은 쪽)은 여기서 마커도 같이 켠다.
    /// </summary>
    public void ArmFromServer(double collapseServerTime)
    {
        if (_state == State.Collapsing || _state == State.Gone) return;

        StartWarnVisual();      // 이미 Warning이면 아무 일 없음
        _collapseAt = collapseServerTime;
    }

    // ── 진행 ────────────────────────────────────────────────────

    void FixedUpdate()
    {
        switch (_state)
        {
            case State.Warning:
                // 서버 확정 전에는 _collapseAt이 MaxValue라 절대 무너지지 않는다(클래스 주석 ⚠️).
                if (ServerNow() >= _collapseAt) Collapse();
                break;

            case State.Collapsing:
                _depth += collapseSpeed * Time.fixedDeltaTime;
                if (_depth >= fallAwayDepth)
                {
                    _depth = fallAwayDepth;
                    _state = State.Gone;
                    if (_renderer != null) _renderer.enabled = false;
                }
                ApplyDepth();
                break;
        }
    }

    void Collapse()
    {
        _state = State.Collapsing;
        if (_solid != null) _solid.enabled = false;

        // 마커는 바닥과 함께 내려가지 않는다 — 구멍 위에 빨간 판이 떠 있으면 아직 밟을 수 있는
        // 것처럼 보인다. SpikeLane.Trigger()가 ResetWarning을 부르는 것과 같다.
        warnMarker?.ResetWarning();
        OnCollapsed?.Invoke();
    }

    void ApplyDepth()
    {
        Vector3 restWorld = transform.parent != null
            ? transform.parent.TransformPoint(_restLocalPos)
            : _restLocalPos;

        _rb.MovePosition(restWorld + Vector3.down * _depth);
    }

    static double ServerNow()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsListening ? nm.ServerTime.Time : Time.timeAsDouble;
    }

    /// <summary>
    /// 휴지 상태로 되돌린다. T4는 스테이지 실패 시 씬 전체가 재로드되므로(StageNetworkState 실패 문)
    /// 보통 쓸 일이 없고, 제자리 리셋이 필요한 테스트·툴용이다.
    /// </summary>
    public void ResetTile()
    {
        if (_rb == null) return; // Awake 전(Edit 모드 등) 호출 방지

        _state      = State.Idle;
        _reported   = false;
        _collapseAt = double.MaxValue;
        _depth      = 0f;
        warnMarker?.ResetWarning();
        if (_solid != null)    _solid.enabled = true;
        if (_renderer != null) _renderer.enabled = true;
        ApplyDepth();
    }

    void OnValidate()
    {
        collapseSpeed       = Mathf.Max(0.01f, collapseSpeed);
        fallAwayDepth       = Mathf.Max(0.1f, fallAwayDepth);
        topContactTolerance = Mathf.Max(0.01f, topContactTolerance);
    }
}
