using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Player별 Steam 표시 이름(DisplayName) 네트워크 동기화.
/// NetworkDesign.md §6B.7 P3 두 번째 항목 — 구 LobbyNetworkManager.SubmitDisplayNameServerRpc
/// (슬롯 귀속)가 2026-08-20 구 로비 삭제 때 함께 제거된 뒤 재구현되지 않고 있던 것을,
/// PlayerCheerNameSync와 동일하게 "슬롯"이 아니라 "이 Player NetworkObject" 귀속 패턴으로 복원.
/// Player 프리팹에 부착 (PlayerCheerNameSync·NetworkPlayerSetup과 같은 GameObject).
///
/// [역할]
/// - NetworkVariable&lt;FixedString64Bytes&gt; (Server write, Everyone read).
/// - OnNetworkSpawn 시점에 Owner가 자기 로컬 표시 이름을 1회 자동 보고한다 — CheerName과 달리
///   사용자가 입력하는 UI가 없다(§6B.7 P3, DisplayName은 항상 자동 보고 값).
/// - 세션 확정은 CheerName과 동일하게 TutorialNetworkManager.CompleteGate()에서
///   GameSession.SetSessionDisplayNames()로 처리(런타임 중 재갱신 없음, CheerName과 동일 패턴).
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class PlayerDisplayNameSync : NetworkBehaviour
{
    readonly NetworkVariable<FixedString64Bytes> _displayName = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// <summary>현재 보고된 표시 이름 (빈 문자열 = 아직 보고 전).</summary>
    public string DisplayName => _displayName.Value.ToString();

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
            ReportDisplayNameServerRpc(new FixedString64Bytes(GetLocalDisplayName()));
    }

    /// <summary>
    /// 이 클라이언트의 로컬 표시 이름. Steam 경로(§4.2 ④)에서 SteamManager가 이미 초기화돼 있으면
    /// Steam 닉네임을 쓰고, 아니면(①ParrelSync ②Dev Build 로컬 경로 — Steam을 초기화하지 않는 경로,
    /// SteamManager.cs 주석 참고) OS 계정 이름으로 폴백한다. "오프라인 모드" 분기가 아니라
    /// Steam이 아직 붙지 않은 로컬 개발 경로에서 쓸 표시값일 뿐이다.
    /// </summary>
    static string GetLocalDisplayName()
    {
        if (SteamManager.Instance != null && SteamManager.Instance.IsInitialized)
            return Steamworks.SteamClient.Name;
        return System.Environment.UserName;
    }

    /// <summary>Client(Owner) → Host. 본인 캐릭터만 자기 이름을 보고할 수 있다.</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void ReportDisplayNameServerRpc(FixedString64Bytes name, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return; // 본인 캐릭터만 보고 가능
        _displayName.Value = name;
    }

    // ── 세션 전체 이름 조회 (게이트 완료 확정 전용, PlayerCheerNameSync와 동일 패턴) ──────

    /// <summary>
    /// 현재 씬에 스폰된 모든 PlayerDisplayNameSync를 훑어 (clientId, 보고된 표시 이름) 목록을 반환.
    /// </summary>
    public static IEnumerable<(ulong ClientId, string Name)> GetAllEffectiveNames()
    {
        var all = FindObjectsByType<PlayerDisplayNameSync>(FindObjectsSortMode.None);
        foreach (var sync in all)
        {
            var netObj = sync.GetComponent<NetworkObject>();
            if (netObj == null) continue;
            yield return (netObj.OwnerClientId, sync.DisplayName);
        }
    }
}
