using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 밟으면 그 자리에서 부서지는 바닥 타일 — T.Stage4 함정 랜덤화 ③.
/// SSOT: Assets/Docs/TStage4TrapRandomization.md §1.4 / §4.1
///
/// [동작 — 밟기 → 경고 → 영구 파괴]
///  1. 대기 — 평범한 바닥이다. **후보라는 표시를 하지 않는다**(머티리얼도 일반 바닥과 동일).
///     경고 연출이 이미 있으므로 미리 금 간 모습을 보여줄 필요가 없고, 후보 위치가 보이면
///     그 자체가 암기 대상이 되어 랜덤화의 목적(§0)이 사라진다.
///  2. 밟힘 — 플레이어가 윗면을 밟는 순간 경고가 시작된다.
///  3. 경고 — warnMarker가 탠저린→진홍으로 물든다.
///  4. 파괴 — **그 자리에서 부서진다.** 콜라이더·Renderer가 같은 프레임에 꺼지고 파편(RubbleShards)과
///     분출 파티클이 태어난다. 슬래브가 아래로 꺼지던 구안(2026-09-21 폐기)과 달리 구멍 밑으로
///     내려가는 판이 없다. 위에 있던 사람은 공허로 낙하하고 Player.fallDeathY(프리팹 −15)를
///     지나며 낙사한다. **복귀는 없다.**
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
///     CNT 보간 때문에 가장자리를 스치는 접촉이 머신마다 갈리는데, 파괴가 영구적이라
///     한 번 갈리면 지형이 영영 달라진다.
///   · 보고한 머신은 **RPC 왕복을 기다리지 않고 즉시 마커를 켠다.** 내가 밟은 타일이 내 화면에서
///     RTT만큼 늦게 반응하는 것(투사체 "A안"의 그 체감)을 피하기 위한 것이고, 연출만 앞당길 뿐
///     파괴 시각은 건드리지 않는다.
///   · **파괴 시각은 Host가 정한 서버 시각 하나뿐이다.** 그래서 경고가 뜨는 순간은 머신마다
///     RTT만큼 달라도 바닥이 빠지는 순간은 전 머신이 동일하다(MovingCorridor의 틱 결정론과 같은 사상).
///   · 파편 임펄스도 전 머신 공통 시드로만 뽑는다(Breakable.DebrisSeed와 같은 형태) — 파괴 연출이
///     로컬인데도 조각이 튀는 모습까지 같다.
///
///  ⚠️ 서버 확정이 오기 전에는 **절대 부서지지 않는다**(_breakAt = 미확정). 보고가 유실돼도
///     그 머신만 혼자 무너지는 일은 생기지 않는다 — 어긋나느니 안 무너지는 쪽이 안전하다.
///
/// [밟기 감지 — 콜라이더 2개 (2026-09-21. 접촉점 높이 판정 폐기)]
///  같은 GameObject에 **Is Trigger 콜라이더를 하나 더** 둔다. 솔리드 바닥은 밟고 서는 용도,
///  트리거는 "밟았다" 판정 전용이다.
///
///  구안은 `OnCollisionEnter/Stay`로 받은 접촉점의 y가 윗면에서 `topContactTolerance`(0.25m)
///  이내인지를 봤다. 옆면을 스치는 충돌을 밟기로 오인하지 않으려는 판정이었는데, **윗면에만
///  얇게 깔린 트리거**면 그 계산 자체가 필요 없다 — 모양이 곧 판정이고 씬 뷰에서 보인다.
///
///  ⚠️ 트리거를 두껍게 잡으면 **점프로 넘어가는 사람까지 발동**한다. 윗면에 얇게(0.2~0.3m) 깔 것.
///  `CapacityTile`과 달리 이쪽은 트리거가 타일과 함께 움직일 걱정이 없다 — BreakTile은
///  그 자리에서 부서질 뿐 이동하지 않는다.
///
/// [씬 설정 — BreakTile 프리팹 하나로 배포한다 (2026-09-21)]
///  타일은 `Assets/Prefab/BreakTile.prefab` 인스턴스다. 파편·파티클·소리 값이 전부 프리팹에 있으므로
///  판에 몇 개를 깔든 **프리팹 한 번 수정으로 전부 반영된다.**
///  1. 바닥 타일(Renderer + 솔리드 Collider, Is Trigger = false)에 붙인다.
///  2. **감지용 Is Trigger 콜라이더**를 하나 더 둔다. 7.8m 타일 기준 로컬 Size (0.9, 0.3, 0.9) ·
///     Center (0, 0.5, 0) = 윗면에 깔린 월드 7.02 × 0.3 × 7.02 판.
///  3. 타일 위를 덮는 경고 마커를 자식으로 두고 SpikeLaneWarnMarker를 붙여 warnMarker에 연결한다.
///     (비우면 경고 없이 부서진다 — 튜닝·테스트용이고 실제 배치에서는 채울 것)
///  4. 머티리얼은 **일반 바닥과 동일하게** 맞출 것 (위 1번 — 후보 노출 금지).
///  5. 개수는 판에 몇 개를 까느냐로 정한다 — 코드에 quota가 없다.
///  6. 인스턴스 이름은 전부 "BreakTile"이라 이름으로는 서로 구분되지 않는다. 전 머신 공통 인덱스는
///     **월드 좌표 정렬**로 배정하므로(BreakTileDirector.BindTiles) 이름과 무관하게 안전하다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BreakTile : MonoBehaviour
{
    enum State
    {
        Idle,    // 대기 — 아직 아무도 안 밟음
        Warning, // 경고 중 — 아직 밟을 수 있다
        Gone,    // 부서짐 — 영구 구멍
    }

    [Header("경고")]
    [Tooltip("이 타일의 경고 마커. SpikeTrap·혀·M.Boss 턱과 같은 컴포넌트(탠저린→진홍)로 통일한다.\n" +
             "타일 위를 덮도록 스케일을 맞춰 자식으로 배치할 것. 비우면 경고 연출이 생략된다.")]
    [SerializeField] SpikeLaneWarnMarker warnMarker;

    [Header("파괴 — 파편")]
    [Tooltip("부서질 때 생성할 파편 프리팹(TileDebrisUtil 공용 — 없으면 생략).\n" +
             "공용 러블 조각(Assets/Prefab/RubbleShards.prefab)을 쓰면 타일의 현재 재질을 그대로 입는다.")]
    [SerializeField] GameObject debrisPrefab;

    [Tooltip("파편 자동 소멸 시간(초). 0이면 자동 소멸 안 함.\n" +
             "타일은 구멍을 남기고 복구되지 않으므로 파편이 구멍 아래로 떨어져 계속 쌓인다 — 0으로 두지 말 것.")]
    [SerializeField] float debrisLifetime = 5f;

    [Tooltip("파편에 가할 임펄스 힘 최소값.")]
    [SerializeField] float debrisImpulseMin = 2f;

    [Tooltip("파편에 가할 임펄스 힘 최대값.")]
    [SerializeField] float debrisImpulseMax = 5f;

    [Tooltip("파편 프리팹 스케일 배율. 공용 조각(유닛 크기)을 타일 크기에 맞게 키울 때 쓴다(1=원본 그대로).")]
    [SerializeField] float debrisScale = 1f;

    [Header("파괴 — 파티클")]
    [Tooltip("부서지는 순간 스폰할 파티클 프리팹(Assets/Art/Particle/AcidGeyser.prefab).\n" +
             "회전은 프리팹에 저장된 값을 그대로 쓴다 — 타일 회전을 씌우면 분사 방향이 틀어진다.")]
    [SerializeField] GameObject breakParticlePrefab;

    [Tooltip("타일 중심에서 이만큼 아래(m)에 파티클을 스폰한다. 분출이 구멍을 뚫고 올라오는 그림을 만든다.")]
    [SerializeField] float particleDepth = 1f;

    [Tooltip("파티클 프리팹이 맞춰진 기준 타일 한 변(m). AcidGeyser는 3m 타일 기준이다.\n" +
             "실제 타일 한 변(Renderer bounds의 x·z 중 큰 값) ÷ 이 값만큼 파티클을 균일하게 키운다\n" +
             "(T.Boss P1 5m → ×1.67, T.Stage4 7.8m → ×2.6). 0이면 프리팹 크기 그대로.")]
    [SerializeField] float particleReferenceTileSize = 3f;

    [Tooltip("파티클 강제 소멸 시간(초). 0이면 프리팹의 Stop Action(Destroy)에 맡긴다 —\n" +
             "Looping이 켜진 프리팹을 물렸을 때만 쓰는 안전망이다.")]
    [SerializeField] float particleLifetime = 0f;

    [Header("파괴 — 소리")]
    [Tooltip("이 거리(m) 이내에서는 최대 볼륨.")]
    [SerializeField] float destroyMinDistance = 5f;

    [Tooltip("이 거리(m) 밖에서는 완전 무음. 0이면 500으로 처리.")]
    [SerializeField] float destroyMaxDistance = 50f;

    [Header("이벤트")]
    [Tooltip("경고가 시작될 때 1회. SFX 등 연출용.")]
    public UnityEvent OnWarnStarted;

    [Tooltip("바닥이 빠지는 순간 1회. 전 머신에서 같은 서버 시각에 발동한다.")]
    public UnityEvent OnCollapsed;

    State  _state = State.Idle;
    bool   _reported;                  // 이 머신이 이미 Host에 보고했는가(중복 ServerRpc 방지)
    double _breakAt = double.MaxValue;  // Host가 확정한 파괴 서버 시각. 미확정이면 MaxValue

    BreakTileDirector _director;
    int               _index = -1;

    Collider   _solid;
    Renderer   _renderer;
    GameObject _debris;
    GameObject _particle;

    void Awake()
    {
        ResolveColliders();
        _renderer = GetComponent<Renderer>();
    }

    /// <summary>
    /// 솔리드(바닥)와 트리거(밟기 감지)를 갈라 잡는다. 같은 GameObject에 둘을 함께 두는 구성이라
    /// GetComponent&lt;Collider&gt;() 한 번으로는 어느 쪽이 잡힐지 알 수 없다 —
    /// 트리거를 솔리드로 착각하면 파괴 순간에 엉뚱한 콜라이더를 끈다.
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
                $"[BreakTile] {name}: 솔리드 콜라이더(Is Trigger = false)가 없다 — " +
                "플레이어가 밟고 설 바닥이 없다.", this);
        }

        if (!hasTrigger)
        {
            Debug.LogWarning(
                $"[BreakTile] {name}: 감지용 트리거 콜라이더가 없다 — 밟아도 반응하지 않는다. " +
                "윗면에 얇게 깔리는 Is Trigger 콜라이더를 하나 더 붙일 것.", this);
        }
    }

    /// <summary>BreakTileDirector가 판을 활성화한 직후 호출 — 보고 경로와 전 머신 공통 인덱스를 심는다.</summary>
    public void Bind(BreakTileDirector director, int index)
    {
        _director = director;
        _index    = index;
    }

    // ── 밟힘 감지 (로컬) ────────────────────────────────────────
    // 이 콜백들은 같은 GameObject의 트리거 콜라이더에서만 온다(솔리드는 Collision 쪽이다).

    void OnTriggerEnter(Collider other) => TryStep(other);

    // Enter를 놓친 경우(타일 위에서 스폰·텔레포트 착지 등)의 안전망 — PressurePad의 Stay와 같은 역할.
    void OnTriggerStay(Collider other) => TryStep(other);

    void TryStep(Collider other)
    {
        if (_state != State.Idle || _reported) return;
        if (_director == null || _index < 0) return;

        // GetComponent (GetComponentInParent 아님) — 플레이어는 몸통 캡슐 말고도 자식 히트박스를
        // 들고 있어서, 부모까지 올라가 찾으면 같은 사람이 콜라이더 수만큼 걸린다
        // (CapacityTile.TryAddOccupant 주석의 그 버그).
        Player p = other.GetComponent<Player>();
        if (p == null || p.IsDead) return;

        // 밟은 당사자의 머신만 보고한다 — 클래스 주석의 권한 항목 참고.
        NetworkObject netObj = p.GetComponent<NetworkObject>();
        if (netObj != null && !netObj.IsOwner) return;

        _reported = true;
        _director.ReportStep(_index);

        // 보고한 머신만 RPC 왕복을 기다리지 않고 미리 켠다. 파괴 시각은 여전히 Host 확정값이다.
        StartWarnVisual();
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
    /// Host가 확정한 파괴 서버 시각을 받는다. 전 머신이 이 값 하나로 같은 순간에 부서진다.
    /// 아직 경고를 시작하지 않은 머신(=내가 밟지 않은 쪽)은 여기서 마커도 같이 켠다.
    /// </summary>
    public void ArmFromServer(double breakServerTime)
    {
        if (_state == State.Gone) return;

        StartWarnVisual();      // 이미 Warning이면 아무 일 없음
        _breakAt = breakServerTime;
    }

    // ── 진행 ────────────────────────────────────────────────────

    void FixedUpdate()
    {
        // 서버 확정 전에는 _breakAt이 MaxValue라 절대 부서지지 않는다(클래스 주석 ⚠️).
        if (_state == State.Warning && ServerNow() >= _breakAt) Break();
    }

    /// <summary>
    /// 그 자리에서 부서진다 — 바닥을 즉시 지우고 파편·파티클·소리를 낸다.
    /// 전 머신이 같은 서버 시각에 들어오고 파편 시드도 같으므로 결과가 갈리지 않는다.
    /// </summary>
    void Break()
    {
        _state = State.Gone;

        // 마커는 먼저 끈다 — 구멍 위에 빨간 판이 떠 있으면 아직 밟을 수 있는 것처럼 보인다
        // (SpikeLane.Trigger()가 ResetWarning을 부르는 것과 같다).
        warnMarker?.ResetWarning();

        Vector3 center   = Center();
        float   tileSize = TileSize();
        if (_solid != null) _solid.enabled = false;

        SFXManager.Instance?.PlayAtPoint(SFXId.Breakable_Destroy, center, destroyMinDistance, destroyMaxDistance);

        // 파편은 Renderer를 끄기 전에 띄운다 — TileDebrisUtil이 이 타일의 재질과 bounds를 읽어 간다.
        SpawnDebris();
        if (_renderer != null) _renderer.enabled = false;

        SpawnBreakParticle(center, tileSize);
        OnCollapsed?.Invoke();
    }

    void SpawnDebris()
    {
        if (debrisPrefab == null) return;

        ClearDebris();
        _debris = TileDebrisUtil.BreakTile(gameObject, debrisPrefab, debrisLifetime,
                                           debrisImpulseMin, debrisImpulseMax, DebrisSeed(), debrisScale);
    }

    void SpawnBreakParticle(Vector3 center, float tileSize)
    {
        if (breakParticlePrefab == null) return;

        ClearParticle();

        // 회전은 프리팹에 저장된 값 그대로 — 분사 방향이 프리팹에서 이미 맞춰져 있다.
        _particle = Instantiate(breakParticlePrefab,
                                center + Vector3.down * particleDepth,
                                breakParticlePrefab.transform.rotation);

        if (particleReferenceTileSize > 0f && tileSize > 0f)
            ScaleParticle(_particle, tileSize / particleReferenceTileSize);

        if (particleLifetime > 0f) Destroy(_particle, particleLifetime);
    }

    /// <summary>
    /// 파티클 묶음 전체를 균일하게 scale배 한다(폭·입자 크기·분출 높이 모두 비례).
    /// 루트 스케일만 바꾸면 안 된다 — AcidGeyser의 시스템들은 Scaling Mode가 Local/Shape라
    /// 부모 스케일을 무시한다. 그래서 전부 Hierarchy로 돌린 뒤 루트를 키운다.
    /// Hierarchy 모드에서도 World 시뮬레이션 공간 시스템은 속도가 스케일되지 않으므로
    /// 분출 높이가 같이 커지도록 시작 속도를 직접 곱한다(Local 공간 시스템은 transform이 알아서 늘린다).
    /// 스폰 직후 같은 프레임이라 아직 방출된 입자가 없다 — 첫 입자부터 새 값이 적용된다.
    /// </summary>
    static void ScaleParticle(GameObject root, float scale)
    {
        if (Mathf.Approximately(scale, 1f)) return;

        root.transform.localScale *= scale;

        var systems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            var main = systems[i].main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            if (main.simulationSpace != ParticleSystemSimulationSpace.Local)
                main.startSpeedMultiplier *= scale;
        }
    }

    /// <summary>타일 한 변(m) — Renderer bounds의 x·z 중 큰 값. Renderer가 없으면 0(스케일링 생략).</summary>
    float TileSize()
    {
        if (_renderer == null) return 0f;
        Vector3 size = _renderer.bounds.size;
        return Mathf.Max(size.x, size.z);
    }

    /// <summary>
    /// 파편 임펄스 시드. 전 머신이 같은 값을 뽑아야 조각이 똑같이 튄다(로컬 Random 금지 원칙).
    /// _index = 판 안에서 월드 좌표 정렬로 배정된 공통 인덱스라 Host/Client 항상 동일하고,
    /// 세션 시드를 섞어 런마다 다른 모양이 나온다. 혼합식은 Breakable.DebrisSeed와 같은 형태.
    /// </summary>
    int DebrisSeed() => NetworkSessionData.Seed ^ (_index * 0x2545F491);

    /// <summary>Renderer bounds 중심. Renderer가 없으면 transform 위치로 되돌아간다.</summary>
    Vector3 Center()
    {
        if (_renderer != null && _renderer.bounds.size.sqrMagnitude > 0.0001f)
            return _renderer.bounds.center;
        return transform.position;
    }

    void ClearDebris()
    {
        if (_debris == null) return;
        Destroy(_debris);
        _debris = null;
    }

    void ClearParticle()
    {
        if (_particle == null) return;
        Destroy(_particle);
        _particle = null;
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
        _state    = State.Idle;
        _reported = false;
        _breakAt  = double.MaxValue;

        warnMarker?.ResetWarning();
        ClearDebris();
        ClearParticle();

        if (_solid != null)    _solid.enabled = true;
        if (_renderer != null) _renderer.enabled = true;
    }

    void OnValidate()
    {
        debrisLifetime      = Mathf.Max(0f, debrisLifetime);
        debrisImpulseMin    = Mathf.Max(0f, debrisImpulseMin);
        debrisImpulseMax    = Mathf.Max(debrisImpulseMin, debrisImpulseMax);
        debrisScale         = Mathf.Max(0.01f, debrisScale);
        particleLifetime    = Mathf.Max(0f, particleLifetime);
        particleReferenceTileSize = Mathf.Max(0f, particleReferenceTileSize);
        destroyMinDistance  = Mathf.Max(0f, destroyMinDistance);
        destroyMaxDistance  = Mathf.Max(destroyMinDistance, destroyMaxDistance);
    }
}
