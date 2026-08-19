using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Player별 CheerName(응원 호출명) 네트워크 동기화 — Tutorial 사전 게이트 구간 신규 컴포넌트.
/// NetworkDesign.md §6B.7 P6, CheerAndTutorialDesign.md §3.2~3.4/§13 "Tutorial CheerName 컴포넌트".
/// Player 프리팹에 부착 (NetworkPlayerSetup·CheerKeywordEngine과 같은 GameObject).
///
/// [왜 Player에 붙나 — 색이 아니라 플레이어(슬롯)에 귀속, §3.3]
/// CheerName은 색을 바꿔도 유지되어야 하므로, PlayerSpawnCoordinator의 색 매핑과는 별도로
/// 이 Player 자신의 NetworkVariable로 갖는다 — 색 재배정과 이름이 서로 얽히지 않는다.
///
/// [역할]
/// - NetworkVariable&lt;FixedString32Bytes&gt; (Server write, Everyone read). 빈 문자열 = 색 기본값 취급(§3.1).
/// - SubmitCheerNameServerRpc — Host가 형식·예약어(CheerNameValidator) + 세션 내 중복을 검증 후 반영/거절.
/// - Tutorial엔 Ready 잠금이 없으므로 언제든 재제출 가능(§3.4) — 별도 잠금 로직 없음.
/// - 값이 바뀔 때마다(자신이든 남이든) 로컬 CheerKeywordEngine의 Vosk grammar를 즉시 재적용(§5.3).
///
/// [세션 확정]
/// 별도 "확정" 단계 없음 — TutorialGatherZone 통과 시점(TutorialNetworkManager.CompleteGate())의
/// 각자 최신값이 그대로 최종값. GameSession.SetSessionCheerNames(...)로 그 시점에 1회 옮겨진다.
///
/// [이 컴포넌트가 아직 하지 않는 것]
/// "말해보기" 테스트 UI는 별도 작업(§6B.7 P6 UI 파트/P7). 입력 UI·형식·예약어·금칙어(§3.5 #9~12,
/// CheerNameValidator.ContainsBlockedWord)·중복·그래머 재빌드는 이 컴포넌트가 전부 담당.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class PlayerCheerNameSync : NetworkBehaviour
{
    readonly NetworkVariable<FixedString32Bytes> _cheerName = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// <summary>커스텀 CheerName(소문자, 빈 문자열 = 미설정 — 색 기본값 취급, §3.1).</summary>
    public string CustomCheerName => _cheerName.Value.ToString();

    /// <summary>제출 결과 통보(성공 여부, 실패 사유 키: "format"/"reserved"/"taken"). 요청 Client에서만 유효.</summary>
    public event System.Action<bool, string> OnSubmitResult;

    public override void OnNetworkSpawn()
    {
        _cheerName.OnValueChanged += OnCheerNameChanged;
        RebuildOwnerLocalGrammar(); // 늦은 스폰 시점에 이미 값이 있을 경우(다른 플레이어가 먼저 확정) 즉시 1회 반영
    }

    public override void OnNetworkDespawn()
    {
        _cheerName.OnValueChanged -= OnCheerNameChanged;
    }

    void OnCheerNameChanged(FixedString32Bytes previous, FixedString32Bytes current) =>
        RebuildOwnerLocalGrammar();

    /// <summary>
    /// Client → Host. candidate가 빈 문자열이면 커스텀 해제(기본값 취급)로 처리.
    /// 구 LobbyNetworkManager.SetCheerNameServerRpc와 동일 규칙 — 다만 슬롯이 아니라 "이 NetworkObject"
    /// 자신의 OwnerClientId 기준으로 검증한다(Ready 잠금 없음, §3.4).
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SubmitCheerNameServerRpc(FixedString32Bytes candidate, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return; // 본인 캐릭터만 제출 가능

        string lower = candidate.ToString().Trim().ToLowerInvariant();

        if (lower.Length == 0)
        {
            _cheerName.Value = default;
            SendResult(true, "");
            return;
        }

        if (!CheerNameValidator.IsValidFormat(lower, out string reason))
        {
            SendResult(false, reason);
            return;
        }

        if (CheerNameValidator.ContainsBlockedWord(lower))
        {
            SendResult(false, "blocked");
            return;
        }

        if (IsTakenByOther(lower))
        {
            SendResult(false, "taken");
            return;
        }

        _cheerName.Value = new FixedString32Bytes(lower);
        SendResult(true, "");
    }

    /// <summary>현재 Tutorial에 접속 중인 다른 플레이어가 이미 쓰고 있는(해석 후) 이름인지 확인(§3.5 #5).</summary>
    bool IsTakenByOther(string lower)
    {
        foreach (var (clientId, name) in GetAllEffectiveNames())
        {
            if (clientId == OwnerClientId) continue;
            if (name == lower) return true;
        }
        return false;
    }

    void SendResult(bool success, string errorKey)
    {
        SubmitResultClientRpc(success, new FixedString32Bytes(errorKey), new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        });
    }

    [ClientRpc]
    void SubmitResultClientRpc(bool success, FixedString32Bytes errorKey, ClientRpcParams rpcParams = default)
    {
        OnSubmitResult?.Invoke(success, errorKey.ToString());
    }

    // ── 세션 전체 이름 조회 (Host 중복검사 / 로컬 그래머 재빌드 / 게이트 완료 확정 공용) ──

    /// <summary>
    /// 현재 씬에 스폰된 모든 PlayerCheerNameSync를 훑어 (clientId, 유효 이름) 목록을 반환.
    /// 유효 이름 = 커스텀 값이 있으면 그 값, 없으면 PlayerSpawnCoordinator 색 매핑 기준 기본값(§3.1).
    /// Host(중복검사)·모든 Client(그래머 재빌드)·게이트 완료 확정에서 공용으로 쓴다 — 매번 새로
    /// 훑으므로 Tutorial 특유의 동적 합류/이탈(§6B.2)에도 별도 캐시 무효화가 필요 없다.
    /// </summary>
    public static IEnumerable<(ulong ClientId, string Name)> GetAllEffectiveNames()
    {
        var all = FindObjectsByType<PlayerCheerNameSync>(FindObjectsSortMode.None);
        foreach (var sync in all)
        {
            var netObj = sync.GetComponent<NetworkObject>();
            if (netObj == null) continue;
            yield return (netObj.OwnerClientId, ResolveEffectiveName(netObj.OwnerClientId, sync.CustomCheerName));
        }
    }

    /// <summary>커스텀 값이 있으면 그대로, 없으면 색 기본값(§3.1) — 색 조회는 PlayerSpawnCoordinator SSOT.</summary>
    static string ResolveEffectiveName(ulong clientId, string custom)
    {
        if (!string.IsNullOrEmpty(custom)) return custom;

        if (PlayerSpawnCoordinator.TryGetColor(clientId, out var color))
        {
            int ci = PlayerColorUtil.ColorTypeToIndex(color);
            if (ci >= 0) return PlayerColorUtil.DefaultCheerNames[ci];
        }
        return "";
    }

    /// <summary>
    /// 로컬 클라이언트 자신의 Player(IsOwner)를 찾아 그 CheerKeywordEngine의 Vosk grammar를
    /// 현재 세션 전체 이름 목록으로 재적용한다(§5.3). 어느 Player의 NV가 바뀌었는지는 상관없이
    /// 항상 "내 로컬 인식기 하나"만 갱신하면 충분 — 비오너 인스턴스의 CheerKeywordEngine은
    /// NetworkPlayerSetup이 이미 비활성화해두므로(§7.3) 호출해도 무해하되 실질 동작은 없다.
    /// </summary>
    static void RebuildOwnerLocalGrammar()
    {
        var all = FindObjectsByType<PlayerCheerNameSync>(FindObjectsSortMode.None);
        PlayerCheerNameSync ownerSync = null;
        var names = new List<string>(all.Length);

        foreach (var sync in all)
        {
            var netObj = sync.GetComponent<NetworkObject>();
            if (netObj == null) continue;
            names.Add(ResolveEffectiveName(netObj.OwnerClientId, sync.CustomCheerName));
            if (netObj.IsOwner) ownerSync = sync;
        }

        if (ownerSync == null) return;

        var engine = ownerSync.GetComponent<CheerKeywordEngine>();
        if (engine == null || !engine.enabled) return;

        engine.ApplySessionGrammar(names.ToArray());
    }
}
