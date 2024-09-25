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

        UniTask StopIfNotAsync();
    }
}
