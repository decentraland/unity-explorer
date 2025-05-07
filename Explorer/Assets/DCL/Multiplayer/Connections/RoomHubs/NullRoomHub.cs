using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Rooms;
using LiveKit.Rooms;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.RoomHubs
{
    public class NullRoomHub : IRoomHub
    {
        public static readonly NullRoomHub INSTANCE = new ();

        public IRoom IslandRoom() => NullRoom.INSTANCE;

        public IGateKeeperSceneRoom SceneRoom() =>
            new IGateKeeperSceneRoom.Fake();

        public IReadOnlyCollection<string> AllRoomsRemoteParticipantIdentities() =>
            new List<string>();

        public UniTask<bool> StartAsync() => UniTask.FromResult(true);

        public UniTask StopAsync() =>
            UniTask.CompletedTask;

        public int ParticipantsCount => 0;
        public bool HasAnyRoomConnected => true;
    }
}
