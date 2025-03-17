using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using LiveKit.Rooms;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.RoomHubs
{
    public interface IRoomHub
    {
        IRoom IslandRoom();
        IGateKeeperSceneRoom SceneRoom();

        UniTask<bool> StartAsync();
        UniTask StopAsync();

        bool HasAnyRoomConnected { get; }
        int ParticipantsCount { get; }

        HashSet<string> AllRoomsRemoteParticipantIdentities();
    }
}
