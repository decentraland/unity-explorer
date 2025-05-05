using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using LiveKit.Proto;
using LiveKit.Rooms;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.RoomHubs
{
    public interface IRoomHub
    {
        IRoom IslandRoom();
        IGateKeeperSceneRoom SceneRoom();
        IRoom ChatRoom();

        UniTask<bool> StartAsync();
        UniTask StopAsync();
        UniTask StopLocalRoomsAsync();

        IReadOnlyCollection<string> AllLocalRoomsRemoteParticipantIdentities();
    }

    public static class RoomHubExtensions
    {
        public static bool HasAnyRoomConnected(this IRoomHub roomHub) =>
            roomHub.IslandRoom().Info.ConnectionState == ConnectionState.ConnConnected ||
            roomHub.SceneRoom().Room().Info.ConnectionState == ConnectionState.ConnConnected;

        public static int ParticipantsCount(this IRoomHub roomHub) =>
            roomHub.AllLocalRoomsRemoteParticipantIdentities().Count;
    }
}
