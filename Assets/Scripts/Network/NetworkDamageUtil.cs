using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 네트워크 데미지 적용 공유 유틸.
///
/// [메서드 구분]
/// ApplyDamage      — 서버 판정 전용. 함정·적·씬 이벤트 등 모든 데미지 진입점.
///                    서버에서만 실제 동작. 클라이언트 호출 시 즉시 반환.
/// ApplyHeal        — 서버 판정 전용. 팀 버프 등 모든 회복 진입점. 클라이언트 호출 시 즉시 반환.
/// ApplyInstantKill — 즉사 전용. 서버에서만 판정. 클라이언트 호출 시 즉시 반환.
/// ApplyKnockback   — 순수 넉백 전용(HP·쉴드 미변경). 서버에서만 판정. 클라이언트 호출 시 즉시 반환.
/// </summary>
public static class NetworkDamageUtil
{
    /// <summary>서버에서만 데미지 판정. 적/씬 이벤트 등 서버 컨텍스트 전용.</summary>
    public static void ApplyDamage(Player p, int amount)
    {
        if (p == null || amount <= 0) return;

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening || !nm.IsServer) return;

        var netSetup = p.GetComponent<NetworkPlayerSetup>();
        if (netSetup != null)
            netSetup.ApplyDamageFromServer(amount);
        else
            p.TakeDamage(amount);
    }

    /// <summary>서버에서만 회복 판정. 팀 버프 등 서버 컨텍스트 전용. 클라이언트 호출 시 즉시 반환.</summary>
    public static void ApplyHeal(Player p, int amount)
    {
        if (p == null || amount <= 0) return;

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening || !nm.IsServer) return;

        p.GetComponent<NetworkPlayerSetup>()?.ApplyHealFromServer(amount);
    }

    /// <summary>
    /// 즉사 판정. Fall 애니메이션 포함(NetworkPlayerSetup 있을 때, 일반 사망과 동일한 애니로 통일).
    /// 서버에서만 판정. 클라이언트 호출 시 즉시 반환.
    /// </summary>
    public static void ApplyInstantKill(Player p)
    {
        if (p == null) return;

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening || !nm.IsServer) return;

        var netSetup = p.GetComponent<NetworkPlayerSetup>();
        if (netSetup == null)
        {
            p.KillInstantly();
            return;
        }

        netSetup.ApplyInstantKillFromServer();
    }

    /// <summary>
    /// 순수 넉백 전용. HP·쉴드는 건드리지 않는다 (Punch / Breakable 등 넉백 전용 이벤트).
    /// 서버에서만 판정. 클라이언트 호출 시 즉시 반환.
    /// </summary>
    public static void ApplyKnockback(Player p, Vector3 direction, float force)
    {
        if (p == null) return;

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening || !nm.IsServer) return;

        var netSetup = p.GetComponent<NetworkPlayerSetup>();
        netSetup?.ApplyKnockbackFromServer(direction, force);
    }
}
