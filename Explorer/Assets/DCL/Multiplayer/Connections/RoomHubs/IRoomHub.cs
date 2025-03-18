using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using LiveKit.Rooms;

namespace DCL.Multiplayer.Connections.RoomHubs
{
    public interface IRoomHub
    {
        IRoom IslandRoom();

        IGateKeeperSceneRoom SceneRoom();

        UniTask<bool> StartAsync();

        UniTask StopAsync();
    }

    public static class RoomHubExtensions
    {
        /// <summary>
        /// Room used for the video streaming
        /// </summary>
        public static IRoom StreamingRoom(this IRoomHub roomHub) =>
            roomHub.SceneRoom().Room();
    }
}
