using System;
using Unity.Netcode;

/// <summary>
/// 로비 슬롯 1개의 네트워크 동기화 상태.
/// NetworkList&lt;LobbyPlayerState&gt;의 element 타입.
///
/// ColorIndex: 0=Blue 1=Purple 2=Green 3=Yellow
/// → LobbyNetworkManager.ColorOrder 배열로 PlayerColorType 변환.
/// </summary>
[Serializable]
public struct LobbyPlayerState : INetworkSerializable, IEquatable<LobbyPlayerState>
{
    public ulong ClientId;
    public int   ColorIndex; // 0~3
    public bool  IsReady;

    /// <summary>빈 슬롯 기본값. ClientId = ulong.MaxValue로 식별.</summary>
    public static LobbyPlayerState Empty => new() { ClientId = ulong.MaxValue };

    public bool IsOccupied => ClientId != ulong.MaxValue;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref ColorIndex);
        serializer.SerializeValue(ref IsReady);
    }

    public bool Equals(LobbyPlayerState other) =>
        ClientId == other.ClientId && ColorIndex == other.ColorIndex && IsReady == other.IsReady;
}
