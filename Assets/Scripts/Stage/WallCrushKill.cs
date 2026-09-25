using System.Collections.Generic;
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
/// </summary>
[RequireComponent(typeof(AdvancingWall))]
public class WallCrushKill : MonoBehaviour
{
    // 전진 방향 내적이 이 값보다 작으면 마주 보는 짝. 직각(모서리)은 0이라 제외된다.
    const float OpposingDot = -0.5f;

    // 신고 후 Host 확정이 돌아오기 전 같은 끼임으로 RPC를 연타하지 않게 막는 간격(초).
    // 자동 부활(사망 +1초)보다 짧으면 안 된다 — 부활 직후 끼임은 다시 판정돼야 한다.
    const float ReportCooldown = 1f;

    static readonly List<WallCrushKill> _active = new List<WallCrushKill>();
    static float _nextReportTime;

    AdvancingWall _wall;
    ColorWall     _color;

    // 내 캐릭터가 마지막으로 닿은 물리 스텝(Time.fixedTime). 같은 스텝 값이면 "동시에 닿음".
    float  _lastTouchStep = -1f;
    Player _lastTouchPlayer;

    void Awake()
    {
        _wall  = GetComponent<AdvancingWall>();
        _color = GetComponent<ColorWall>();
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

            Report(p);
            return;
        }
    }

    bool IsMyColor(Player p) => _color != null && _color.IsColorMatch(p);

    void Report(Player p)
    {
        var netSetup = p.GetComponent<NetworkPlayerSetup>();
        if (netSetup == null) return;

        _nextReportTime = Time.time + ReportCooldown;
        netSetup.ReportCrushDeathServerRpc();
    }
}
