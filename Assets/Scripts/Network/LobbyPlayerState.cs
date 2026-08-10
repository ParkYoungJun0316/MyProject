using System;
using Unity.Collections;
using Unity.Netcode;

/// <summary>
/// 로비 슬롯 1개의 네트워크 동기화 상태.
/// NetworkList&lt;LobbyPlayerState&gt;의 element 타입.
///
/// ColorIndex: 0=Blue 1=Purple 2=Green 3=Yellow
/// → LobbyNetworkManager.ColorOrder 배열로 PlayerColorType 변환.
///
/// CheerName: 로비에서 확정된 호출명. 빈 문자열 = 색 기본값 취급.
/// 유효 이름: a-z 0-9 _ / 2~12자 / 소문자 저장.
/// LobbyNetworkManager.GetEffectiveCheerName() 으로 해석할 것.
///
/// DisplayName: Steam 표시 이름(SteamClient.Name) — 로컬(비-Steam) 경로에서는 "PlayerN" 폴백.
/// 각 클라이언트가 자기 자신의 값만 SubmitDisplayNameServerRpc로 보고한다(호스트는 슬롯 생성 시 직접 설정).
/// UI(LobbySlotUI)의 "ID" 텍스트에 그대로 표시됨.
///
/// VoiceId: Dissonance DissonanceComms.LocalPlayerName(세션마다 랜덤 GUID, 명시적으로 안 정하면 자동 생성).
/// 각 클라이언트가 자기 자신의 값만 SubmitVoiceIdServerRpc로 보고한다(DisplayName과 동일한 self-report
/// 패턴, 호스트도 예외 없이 동일 경로로 보고 — Dissonance 준비 시점이 슬롯 생성보다 늦어서 호스트도
/// 즉시 값을 못 넣음). OptionsTeamVoicePanel이 이 값으로 Dissonance DissonanceComms.FindPlayer(voiceId)를
/// 조회해 팀원 VoicePlayerState(수신 볼륨 등)를 찾는 키로 사용(SoundAndSettingsDesign.md §6-8 참고).
/// </summary>
[Serializable]
public struct LobbyPlayerState : INetworkSerializable, IEquatable<LobbyPlayerState>
{
    public ulong            ClientId;
    public int              ColorIndex; // 0~3
    public bool             IsReady;
    public FixedString32Bytes CheerName;    // 빈 문자열 = 색 기본값 취급
    public FixedString64Bytes DisplayName;  // Steam 표시 이름 (또는 로컬 폴백)
    public FixedString64Bytes VoiceId;      // Dissonance LocalPlayerName (빈 문자열 = 아직 미보고)

    /// <summary>빈 슬롯 기본값. ClientId = ulong.MaxValue로 식별.</summary>
    public static LobbyPlayerState Empty => new() { ClientId = ulong.MaxValue };

    public bool IsOccupied => ClientId != ulong.MaxValue;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref ColorIndex);
        serializer.SerializeValue(ref IsReady);
        serializer.SerializeValue(ref CheerName);
        serializer.SerializeValue(ref DisplayName);
        serializer.SerializeValue(ref VoiceId);
    }

    public bool Equals(LobbyPlayerState other) =>
        ClientId    == other.ClientId    &&
        ColorIndex  == other.ColorIndex  &&
        IsReady     == other.IsReady     &&
        CheerName   == other.CheerName   &&
        DisplayName == other.DisplayName &&
        VoiceId     == other.VoiceId;
}
