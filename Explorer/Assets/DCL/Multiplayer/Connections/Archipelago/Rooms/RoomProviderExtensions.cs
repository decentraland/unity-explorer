using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Rooms.Connective;
using System.Threading;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms
{
    public static class RoomProviderExtensions
    {
        public static UniTask StartIfNeededAsync(this IRoomProvider room, CancellationToken ct) =>
            room.CurrentState() is IConnectiveRoom.State.Stopped ? room.StartAsync(ct) : UniTask.CompletedTask;

        public static string ParticipantCountInfo(this IRoomProvider room) =>
            room.CurrentState() is IConnectiveRoom.State.Running
                ? room.Room().Participants.RemoteParticipantSids().Count.ToString()
                : "Not connected";
    }
}
