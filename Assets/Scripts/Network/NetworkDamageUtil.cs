using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 네트워크 데미지 적용 공유 유틸.
///
/// [메서드 구분]
/// ApplyDamage             — 서버 판정 전용. 적(Enemy), 씬 서버 이벤트(OXQuiz 등)에서 사용.
///                           서버에서만 실제 동작. 클라이언트 호출 시 즉시 반환.
///
/// ApplyDamageWithOwnerReport — 발사체·접촉 함정에서 사용 (Phase 2 클라 피격 신고 방식).
///                              서버: 직접 판정 (Host 플레이어 케이스).
///                              오너 클라이언트: ReportHitServerRpc로 서버에 신고.
///                              비오너 클라이언트: 무시.
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
    /// 서버: 직접 즉사 확정.
    /// 오너 클라이언트: 서버에 즉사 신고 (Owner 물리 위치 기준).
    /// 비오너 클라이언트: 무시.
    /// </summary>
    public static void ApplyInstantKill(Player p)
    {
        if (p == null) return;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            var netSetup = p.GetComponent<NetworkPlayerSetup>();
            if (netSetup == null)
            {
                if (nm.IsServer) p.KillInstantly();
                return;
            }

            if (nm.IsServer)
                netSetup.ApplyInstantKillFromServer();
            else if (netSetup.IsOwner)
                netSetup.ReportInstantKillServerRpc();
            // 비오너 클라이언트: 무시 (서버 또는 오너가 처리)
        }
        else
        {
            p.KillInstantly();
        }
    }

    /// <summary>
    /// 발사체·접촉 함정 전용.
    /// 서버: 직접 데미지 판정.
    /// 오너 클라이언트: 서버에 피격 신고 (ClientNetworkTransform 위치 불일치 보완).
    /// 비오너 클라이언트: 무시.
    /// </summary>
    public static void ApplyDamageWithOwnerReport(Player p, int amount, bool knockback = false)
    {
        if (p == null || amount <= 0) return;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            var netSetup = p.GetComponent<NetworkPlayerSetup>();
            if (netSetup == null)
            {
                if (nm.IsServer) p.TakeDamage(amount, knockback);
                return;
            }

            if (nm.IsServer)
            {
                // Host 자신의 플레이어 또는 비오너 서버 판정
                netSetup.ApplyDamageFromServer(amount, knockback);
            }
            else if (netSetup.IsOwner)
            {
                // 오너 클라이언트: 내가 맞은 걸 서버에 신고
                netSetup.ReportHitServerRpc(amount, knockback);
            }
            // 비오너 클라이언트: 무시 (오너가 신고 담당)
        }
        else
        {
            p.TakeDamage(amount, knockback);
        }
    }
}
