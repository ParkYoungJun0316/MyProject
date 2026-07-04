using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 인게임 네트워크 이탈 감지.
/// 누구든 이탈하면 → 전원 즉시 타이틀 복귀.
///
/// [배치]
/// M.Stage1 / T.Stage1 씬의 NetworkObject에 부착.
///
/// [인게임 Quit 버튼]
/// 버튼 OnClick → OnClickLeaveRoom()
/// </summary>
public class DisconnectManager : NetworkBehaviour
{
    // ── 초기화 ────────────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        NetworkManager.OnClientDisconnectCallback += OnClientLeft;
    }

    public override void OnNetworkDespawn()
    {
        NetworkManager.OnClientDisconnectCallback -= OnClientLeft;
    }

    // ── 이탈 감지 ─────────────────────────────────────────────────

    void OnClientLeft(ulong clientId)
    {
        bool isSelf = clientId == NetworkManager.LocalClientId
                   || !NetworkManager.IsListening;

        // 클라이언트: 내 연결이 끊김 (호스트 이탈 or 킥)
        if (isSelf && !IsHost)
        {
            Debug.Log("[DisconnectManager] 연결 끊김 — 타이틀 복귀");
            ReturnToTitle(TitleReturnReason.ClientDisconnected);
            return;
        }

        // 호스트: 다른 플레이어 이탈 → 남은 클라이언트에 알리고 전원 타이틀 복귀
        if (IsHost)
        {
            Debug.Log("[DisconnectManager] 플레이어 이탈 감지 — 전원 타이틀 복귀");
            NotifyAllReturnClientRpc();
            ReturnToTitle(TitleReturnReason.ClientDisconnected);
        }
    }

    // ── 인게임 Quit 버튼 ──────────────────────────────────────────

    /// <summary>
    /// 인게임 Quit/Leave 버튼 OnClick에 연결.
    /// Host: 클라이언트 전원에 복귀 알림 후 타이틀.
    /// Client: 직접 타이틀 복귀 (Host가 감지해서 나머지도 처리함).
    /// </summary>
    public void OnClickLeaveRoom()
    {
        if (IsHost)
            NotifyAllReturnClientRpc();

        ReturnToTitle(TitleReturnReason.HostQuitRoom);
    }

    // ── 내부 ──────────────────────────────────────────────────────

    [ClientRpc]
    void NotifyAllReturnClientRpc()
    {
        if (IsHost) return;
        ReturnToTitle(TitleReturnReason.HostQuitRoom);
    }

    void ReturnToTitle(TitleReturnReason reason)
    {
        TitleReturnFlow.Instance?.Request(new TitleReturnOptions
        {
            Reason = reason,
            Scope  = TitleReturnScope.SessionOnly,
        });
    }

    // ── 에디터 테스트 ─────────────────────────────────────────────

#if UNITY_EDITOR
    [ContextMenu("테스트: 타이틀 복귀 (Host 이탈 시뮬레이션)")]
    void Debug_SimReturn() => ReturnToTitle(TitleReturnReason.ClientDisconnected);
#endif
}
