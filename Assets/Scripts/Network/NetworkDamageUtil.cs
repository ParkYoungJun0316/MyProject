using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 네트워크 데미지 적용 공유 유틸.
///
/// [메서드 구분]
/// ApplyDamage      — 서버 판정 전용. 함정·적·씬 이벤트 등 모든 데미지 진입점.
///                    서버에서만 실제 동작. 클라이언트 호출 시 즉시 반환.
/// ApplyInstantKill — 문 즉사 전용. 서버에서만 판정.
/// </summary>
public static class NetworkDamageUtil
{
    /// <summary>서버에서만 데미지 판정. 적/씬 이벤트 등 서버 컨텍스트 전용.</summary>
    public static void ApplyDamage(Player p, int amount, bool knockback = false)
    {
        if (p == null || amount <= 0) return;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            if (!nm.IsServer) return;

            var netSetup = p.GetComponent<NetworkPlayerSetup>();
            if (netSetup != null)
                netSetup.ApplyDamageFromServer(amount, knockback);
            else
                p.TakeDamage(amount, knockback);
        }
        else
        {
            p.TakeDamage(amount, knockback);
        }
    }

    /// <summary>
    /// 문 닫힘 즉사 전용. Jammed 애니메이션 포함.
    /// 서버에서만 판정. 클라이언트 호출 시 즉시 반환.
    /// </summary>
    public static void ApplyInstantKill(Player p)
    {
        if (p == null) return;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            if (!nm.IsServer) return;

            var netSetup = p.GetComponent<NetworkPlayerSetup>();
            if (netSetup == null)
            {
                p.KillInstantly();
                return;
            }

            netSetup.ApplyInstantKillFromServer();
        }
        else
        {
            p.KillInstantly();
        }
    }
}
