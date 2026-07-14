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
/// </summary>
[Serializable]
public struct LobbyPlayerState : INetworkSerializable, IEquatable<LobbyPlayerState>
{
    public ulong            ClientId;
    public int              ColorIndex; // 0~3
    public bool             IsReady;
    public FixedString32Bytes CheerName; // 빈 문자열 = 색 기본값 취급

    /// <summary>빈 슬롯 기본값. ClientId = ulong.MaxValue로 식별.</summary>
    public static LobbyPlayerState Empty => new() { ClientId = ulong.MaxValue };

    public bool IsOccupied => ClientId != ulong.MaxValue;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref ColorIndex);
        serializer.SerializeValue(ref IsReady);
        serializer.SerializeValue(ref CheerName);
    }

    public bool Equals(LobbyPlayerState other) =>
        ClientId   == other.ClientId   &&
        ColorIndex == other.ColorIndex &&
        IsReady    == other.IsReady    &&
        CheerName  == other.CheerName;
}
