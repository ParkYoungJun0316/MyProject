using System.Collections;
using System.Collections.Generic;
using Dissonance;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Player별 Steam 표시 이름(DisplayName) + Dissonance VoiceId 네트워크 동기화.
/// NetworkDesign.md §6B.7 P3/P8 — 구 LobbyNetworkManager.SubmitDisplayNameServerRpc/SubmitVoiceIdServerRpc
/// (둘 다 슬롯 귀속)가 2026-08-20 구 로비 삭제 때 함께 제거된 뒤, PlayerCheerNameSync와 동일하게
/// "슬롯"이 아니라 "이 Player NetworkObject" 귀속 패턴으로 복원. 둘 다 "검증 없는 1회 자동
/// self-report" 값이라는 동일한 뼈대라서 한 컴포넌트에 필드 2개로 합침 — CheerName(입력·검증·재제출
/// 있는 별도 도메인, Cheer 서브시스템 소속)은 여기 섞지 않고 PlayerCheerNameSync에 그대로 남긴다.
/// Player 프리팹에 부착 (PlayerCheerNameSync·NetworkPlayerSetup과 같은 GameObject).
///
/// [역할]
/// - DisplayName: NetworkVariable&lt;FixedString64Bytes&gt; (Server write, Everyone read).
///   OnNetworkSpawn 시점에 Owner가 자기 로컬 표시 이름을 1회 자동 보고(사용자 입력 UI 없음, §6B.7 P3).
/// - VoiceId: 위와 동일한 자동 보고 값이지만, Dissonance가 OnNetworkSpawn 시점에 아직
///   LocalPlayerName을 확정 못 했을 수 있어(§6B.7 P8, §9.6) 코루틴으로 최대 5회(1초 간격) 재시도.
/// - 세션 확정은 둘 다 CheerName과 동일하게 TutorialNetworkManager.CompleteGate()에서
///   GameSession.SetSessionDisplayNames()/SetSessionVoiceIds()로 처리(런타임 중 재갱신 없음).
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class PlayerDisplayNameSync : NetworkBehaviour
{
    const int   VoiceIdMaxRetries      = 5;
    const float VoiceIdRetryIntervalSec = 1f;

    readonly NetworkVariable<FixedString64Bytes> _displayName = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    readonly NetworkVariable<FixedString64Bytes> _voiceId = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// <summary>현재 보고된 표시 이름 (빈 문자열 = 아직 보고 전).</summary>
    public string DisplayName => _displayName.Value.ToString();

    /// <summary>아무 플레이어의 DisplayName NV가 바뀌면 전원 로컬에서 발행. TeamStatus 즉시 반영용.</summary>
    public static event System.Action OnAnyDisplayNameChanged;

    /// <summary>현재 보고된 Dissonance VoiceId(LocalPlayerName) (빈 문자열 = 아직 보고 전/실패).</summary>
    public string VoiceId => _voiceId.Value.ToString();

    public override void OnNetworkSpawn()
    {
        _displayName.OnValueChanged += HandleDisplayNameChanged;

        if (IsOwner)
        {
            ReportDisplayNameServerRpc(new FixedString64Bytes(GetLocalDisplayName()));
            StartCoroutine(ReportVoiceIdRoutine());
        }
        else if (!string.IsNullOrEmpty(DisplayName))
        {
            OnAnyDisplayNameChanged?.Invoke();
        }
    }

    public override void OnNetworkDespawn()
    {
        _displayName.OnValueChanged -= HandleDisplayNameChanged;
    }

    void HandleDisplayNameChanged(FixedString64Bytes previous, FixedString64Bytes current)
    {
        OnAnyDisplayNameChanged?.Invoke();
    }

    /// <summary>
    /// Dissonance 초기화가 OnNetworkSpawn보다 늦을 수 있어(§9.6), LocalPlayerName이 확정될 때까지
    /// 최대 5회(1초 간격) 재시도 후 1회 보고. 이미 확정돼 있으면 즉시 1회만 보고하고 끝난다.
    /// </summary>
    IEnumerator ReportVoiceIdRoutine()
    {
        for (int attempt = 0; attempt < VoiceIdMaxRetries; attempt++)
        {
            string localVoiceId = DissonanceComms.GetSingleton()?.LocalPlayerName;
            if (!string.IsNullOrEmpty(localVoiceId))
            {
                ReportVoiceIdServerRpc(new FixedString64Bytes(localVoiceId));
                Debug.Log($"[PlayerDisplayNameSync] VoiceId 보고 성공 — attempt={attempt + 1}/{VoiceIdMaxRetries} ownerClientId={OwnerClientId}");
                yield break;
            }
            yield return new WaitForSeconds(VoiceIdRetryIntervalSec);
        }
        Debug.LogWarning($"[PlayerDisplayNameSync] VoiceId 보고 실패 — Dissonance LocalPlayerName 미확정 (재시도 소진) ownerClientId={OwnerClientId}");
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

    /// <summary>Client(Owner) → Host. 본인 캐릭터만 자기 VoiceId를 보고할 수 있다.</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void ReportVoiceIdServerRpc(FixedString64Bytes voiceId, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return; // 본인 캐릭터만 보고 가능
        _voiceId.Value = voiceId;
    }

    // ── 세션 전체 값 조회 (게이트 완료 확정 전용, PlayerCheerNameSync와 동일 패턴) ──────

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

    /// <summary>
    /// 현재 씬에 스폰된 모든 PlayerDisplayNameSync를 훑어 (clientId, 보고된 VoiceId) 목록을 반환.
    /// 아직 미보고/실패면 빈 문자열(GameSession.GetSessionVoiceId의 null 폴백과 동일하게 처리됨).
    /// </summary>
    public static IEnumerable<(ulong ClientId, string VoiceId)> GetAllEffectiveVoiceIds()
    {
        var all = FindObjectsByType<PlayerDisplayNameSync>(FindObjectsSortMode.None);
        foreach (var sync in all)
        {
            var netObj = sync.GetComponent<NetworkObject>();
            if (netObj == null) continue;
            yield return (netObj.OwnerClientId, sync.VoiceId);
        }
    }
}
