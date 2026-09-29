using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 마주 보는 두 벽 사이 끼임 즉사 (T.Boss P4 계단식 압박, 2026-09-26 확정).
///
/// [판정 — 둘 다 만족하면 즉사]
///  1. 내 캐릭터가 마주 보는 짝 벽(좌↔우, 앞↔뒤 — 전진 방향이 반대인 벽)에 같은 물리 스텝에 둘 다 닿아 있다.
///     좌+앞 같은 모서리는 대각선으로 빠질 수 있으므로 끼임이 아니다.
///  2. 두 벽 모두 지금 내 색이 아니다. 쉬는 벽의 Default(회색)도 "내 색 아님"이다.
///  하나라도 내 색이면 그 벽은 ColorWall이 멈추고 후퇴시키므로 즉사하지 않는다(틀린 벽 ContactDamage만).
///
/// [왜 필요한가] 돌진은 남은 바닥의 90%라 보통은 쉬는 벽에 안 닿지만, 네 벽이 계단을 따로 내려와
///  맞은편이 한 칸 앞서 있으면 돌진이 쉬는 벽을 뚫는다. 두 kinematic 벽 사이의 캐릭터는 물리가
///  밀어낼 공간이 없어 벽에 박히거나 벽 뒤로 빠졌다.
///
/// [권한] 끼임은 Owner 로컬 물리에서만 정확하다(이동 권한 = Owner) → 내 캐릭터만 판정하고
///  NetworkPlayerSetup.ReportCrushDeathServerRpc로 신고 → Host가 NetworkDamageUtil.ApplyInstantKill로 확정.
///  낙사(ReportFallDeathServerRpc)와 같은 흐름. 유예 시간 없음 — 돌진 30m/s면 한 스텝에 0.6m가 들어와
///  기다리는 동안 이미 박히거나 빠진다.
///
/// [설정] 짝을 이루는 벽마다(AdvancingWall + ColorWall이 있는 오브젝트) 이 컴포넌트 추가. 인스펙터 연결 없음 —
///  활성화된 WallCrushKill끼리 전진 방향으로 짝을 찾는다.
///
/// [고정물 끼임 — crushAgainstFixed, 2026-09-29 T.Stage1·T.Stage3]
///  통로 안에 빵 칸막이·고정 젤리·닫힌 문·장식이 있어, 벽 하나가 그쪽으로 밀면 캐릭터가 벽과 고정물 사이에 끼여
///  벽 밖으로 빠졌다. 켜면 "틀린 색 벽이 전진 중 + 미는 방향 바로 앞이 고정물에 막힘"도 즉사다.
///  표시 컴포넌트 없이 판정한다 — 캐릭터 캡슐을 미는 방향으로 FixedProbe만큼 옮겨 겹치는 콜라이더를 찾고,
///  그 콜라이더가 캡슐을 미는 방향 반대로 밀어내면(ComputePenetration) 막힌 것이다. 바닥은 위로 밀어내므로 빠진다.
///  막는 것으로 치지 않는 것: 이 벽 자신 · 플레이어 · 트리거 · 움직이는 dynamic 물체(같이 밀려남) ·
///  다른 ColorWall(두 벽 짝 판정이 따로 맡는다). 신고는 두 벽 짝과 같은 RPC, 짝 ID 자리에 -1.
///  T.Boss P4는 끔(기본값) — 두 벽 짝만 쓴다.
/// </summary>
[RequireComponent(typeof(AdvancingWall))]
public class WallCrushKill : MonoBehaviour
{
    // 전진 방향 내적이 이 값보다 작으면 마주 보는 짝. 직각(모서리)은 0이라 제외된다.
    const float OpposingDot = -0.5f;

    // 신고 후 Host 확정이 돌아오기 전 같은 끼임으로 RPC를 연타하지 않게 막는 간격(초).
    // 자동 부활(사망 +1초)보다 길면 안 된다 — 부활 직후 끼임은 다시 판정돼야 한다.
    const float ReportCooldown = 1f;

    // 고정물 끼임: 캐릭터 캡슐을 미는 방향으로 이만큼 옮겨 겹침을 본다(m). 너무 크면 스치기만 해도 죽는다.
    const float FixedProbe = 0.15f;
    // 고정물이 캡슐을 밀어내는 방향과 벽 전진 방향의 내적이 이 값보다 작으면 "정면으로 막힘". 바닥(위로 밀어냄)은 0이라 제외.
    const float FixedBlockDot = -0.5f;
    // 이 물리 스텝에 벽이 전진 방향으로 이만큼 넘게 움직였으면 전진 중(m). 후퇴·정지 중인 벽은 고정물에 끼우지 못한다.
    const float AdvanceEpsilon = 0.001f;

    static readonly List<WallCrushKill> _active = new List<WallCrushKill>();
    static readonly Collider[] _overlapBuffer = new Collider[16];
    static float _nextReportTime;

    [Tooltip("켜면 두 벽 짝 끼임에 더해 '틀린 색 벽 + 고정물(칸막이·고정 젤리·닫힌 문·장식)' 끼임도 즉사.\n" +
             "T.Stage1·T.Stage3 좌우 벽용. T.Boss P4는 끔.")]
    [SerializeField] bool crushAgainstFixed = false;

    AdvancingWall _wall;
    ColorWall     _color;
    Rigidbody     _rb;

    Vector3 _prevPos;
    bool    _advancingThisStep;

    Player          _capsuleOwner;
    CapsuleCollider _capsule;

    // 내 캐릭터가 마지막으로 닿은 물리 스텝(Time.fixedTime). 같은 스텝 값이면 "동시에 닿음".
    float  _lastTouchStep = -1f;
    Player _lastTouchPlayer;

    void Awake()
    {
        _wall  = GetComponent<AdvancingWall>();
        _color = GetComponent<ColorWall>();
        _rb    = GetComponent<Rigidbody>();
        _prevPos = CurrentPosition;
    }

    Vector3 CurrentPosition => _rb != null ? _rb.position : transform.position;

    // 충돌 콜백보다 앞서 돈다 → 직전 물리 스텝의 이동량으로 전진 중인지 정한다.
    void FixedUpdate()
    {
        Vector3 pos = CurrentPosition;
        _advancingThisStep = Vector3.Dot(pos - _prevPos, _wall.WorldMoveDirection) > AdvanceEpsilon;
        _prevPos = pos;
    }

    void OnEnable()  => _active.Add(this);
    void OnDisable() => _active.Remove(this);

    void OnCollisionEnter(Collision collision) => HandleContact(collision.collider);
    void OnCollisionStay(Collision collision)  => HandleContact(collision.collider);

    void HandleContact(Collider other)
    {
        // 루트 캡슐만 인정 — 자식 PunchHitBox 무시 (ContactDamage와 동일).
        Player p = other.GetComponent<Player>();
        if (p == null || p.IsDead || !p.isOwnerControlled) return;

        _lastTouchStep   = Time.fixedTime;
        _lastTouchPlayer = p;

        if (Time.time < _nextReportTime) return;
        if (IsMyColor(p)) return;

        Vector3 myDir = _wall.WorldMoveDirection;
        foreach (WallCrushKill pair in _active)
        {
            if (pair == this) continue;
            if (pair._lastTouchPlayer != p || pair._lastTouchStep != Time.fixedTime) continue;
            if (Vector3.Dot(myDir, pair._wall.WorldMoveDirection) >= OpposingDot) continue;
            if (pair.IsMyColor(p)) continue;

            Report(p, pair);
            return;
        }

        if (crushAgainstFixed && _advancingThisStep && IsPinnedAgainstFixed(p, myDir))
            Report(p, null);
    }

    /// <summary>캐릭터가 이 벽의 전진 방향 바로 앞에서 고정물에 막혀 있는지. 이 벽이 내 색이 아닐 때만 부른다.</summary>
    bool IsPinnedAgainstFixed(Player p, Vector3 pushDir)
    {
        if (_capsuleOwner != p)
        {
            _capsuleOwner = p;
            _capsule      = p.GetComponent<CapsuleCollider>();
        }
        if (_capsule == null) return false;

        Transform t   = _capsule.transform;
        Vector3 scale = t.lossyScale;
        float radius  = _capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float half    = Mathf.Max(_capsule.height * Mathf.Abs(scale.y) * 0.5f - radius, 0f);
        Vector3 shift  = pushDir * FixedProbe;
        Vector3 center = t.TransformPoint(_capsule.center) + shift;
        Vector3 up     = t.up * half;

        int n = Physics.OverlapCapsuleNonAlloc(center - up, center + up, radius, _overlapBuffer,
                                               ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = _overlapBuffer[i];
            if (c == null || c == _capsule) continue;

            Rigidbody rb = c.attachedRigidbody;
            if (rb != null && (rb == _rb || !rb.isKinematic)) continue;
            if (c.GetComponentInParent<Player>() != null) continue;
            if (c.GetComponentInParent<ColorWall>() != null) continue;

            if (Physics.ComputePenetration(_capsule, t.position + shift, t.rotation,
                                           c, c.transform.position, c.transform.rotation,
                                           out Vector3 outDir, out _)
                && Vector3.Dot(outDir, pushDir) < FixedBlockDot)
                return true;
        }
        return false;
    }

    bool IsMyColor(Player p) => _color != null && _color.IsColorMatch(p);

    int ColorWallId => _color != null ? _color.NetId : -1;

    // 두 벽 ID와 끼인 서버 시각을 같이 보낸다 — Host가 그 시각 무렵 둘 중 하나를 이미 멈췄으면(멈춤이
    // 아직 이 머신에 도착 안 한 사이 벽이 더 밀려온 것) 무효로 한다. ColorWall.IsCrushVoidedByPause.
    // 고정물 끼임은 pair == null → 짝 ID -1(Host 무효 비교에서 항상 통과).
    void Report(Player p, WallCrushKill pair)
    {
        var netSetup = p.GetComponent<NetworkPlayerSetup>();
        if (netSetup == null) return;

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return;

        _nextReportTime = Time.time + ReportCooldown;
        netSetup.ReportCrushDeathServerRpc(ColorWallId, pair != null ? pair.ColorWallId : -1, nm.ServerTime.Time);
    }
}
