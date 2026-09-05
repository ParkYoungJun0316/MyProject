using System.Collections.Generic;

/// <summary>
/// Client → Host 1회성 요청(제출·전송·토글)의 중복 수신 필터. 이 논리의 SSOT —
/// 같은 가드를 각 스크립트에 다시 쓰지 말고 이 타입을 필드로 들 것.
///
/// [왜 필요한가 — 2026-09-01/09-05]
/// Facepunch/SteamNetworkingSockets 릴레이 트랜스포트가 같은 메시지를 중복 전달하는 상류 버그가
/// 있다(NGO #2704, SteamworksIntegrationDesign.md 트랙6). 인프로세스 재접속을 반복할수록 중복
/// 개수가 1→2→3으로 늘어난다. 근본 대응은 "웜 리커넥트는 항상 프로세스 재시작" 정책이고,
/// 이 필터는 그 정책이 뚫렸을 때를 위한 두 번째 방어선이다.
///
/// 시간 쿨다운(PlayerPunch)·Set 추가(CheerService 팀 표)·NV 대입처럼 이미 멱등인 요청에는
/// 붙이지 않는다. 대상은 "두 번 처리되면 결과가 달라지는" 비멱등 요청뿐이다.
///
/// [왜 프레임 번호가 아니라 시퀀스 번호인가]
/// 처음엔 "같은 sender의 같은 Time.frameCount 2번째 수신은 중복"으로 막았는데, 그건 중복이
/// 같은 프레임에 도착할 때만 통한다 — 틱이 갈려 도착하면 그대로 통과했다. 프레임 가드는
/// "정상 입력을 잘못 막지 않는다"만 보장했고 "모든 중복을 잡는다"는 보장이 아니었다.
/// 발신 측이 요청마다 단조 증가 번호를 붙이면 도착 시점과 무관하게 멱등이 되고, SequenceRing
/// 오답 재시도처럼 "같은 상태에 같은 요청을 다시 보내는" 정상 입력도 번호가 달라 막히지 않는다
/// (스텝 인덱스를 키로 쓰는 가드로는 이 정상 입력까지 같이 막힌다).
///
/// [사용법]
/// - 발신(Client/Owner): 요청 1건당 <see cref="NextSeq"/>를 정확히 1회 호출해 RPC 인자로 실어 보낸다.
/// - 수신(Host): RPC 본문 맨 앞에서 <see cref="IsDuplicate"/>가 true면 즉시 return.
/// - 발신·수신 인스턴스는 같은 오브젝트 수명을 공유해야 한다(같은 in-scene NetworkObject,
///   같은 플레이어 NetworkObject 등). 발신 번호만 리셋되고 수신 기록이 남으면 리셋 이후 요청이
///   전부 중복으로 버려진다 — 그래서 기록을 비우는 API를 일부러 두지 않았다.
/// </summary>
public class RpcSubmitDedup
{
    uint _localSeq;
    readonly Dictionary<ulong, uint> _lastSeqBySender = new();

    /// <summary>발신 측: 다음 요청 번호. 요청 1건당 1회만 호출할 것.</summary>
    public uint NextSeq() => ++_localSeq;

    /// <summary>수신 측(Host): 이미 처리한 번호이거나 그보다 과거면 true.</summary>
    public bool IsDuplicate(ulong senderClientId, uint seq)
    {
        if (_lastSeqBySender.TryGetValue(senderClientId, out uint last) && seq <= last)
            return true;

        _lastSeqBySender[senderClientId] = seq;
        return false;
    }
}
