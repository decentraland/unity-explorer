using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Rooms;
using LiveKit.Rooms;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.RoomHubs
{
    public class NullRoomHub : IRoomHub
    {
        public static readonly NullRoomHub INSTANCE = new ();

        public IRoom IslandRoom() => NullRoom.INSTANCE;

        public IGateKeeperSceneRoom SceneRoom() =>
            new IGateKeeperSceneRoom.Fake();

        public IRoom ChatRoom() =>
            NullRoom.INSTANCE;

        public UniTask StopLocalRoomsAsync() =>
            UniTask.CompletedTask;

        public IReadOnlyCollection<string> AllLocalRoomsRemoteParticipantIdentities() =>
            new List<string>();

        public UniTask<bool> StartAsync() => UniTask.FromResult(true);

        public UniTask StopAsync() =>
            UniTask.CompletedTask;

        public int ParticipantsCount => 0;
        public bool HasAnyRoomConnected => true;
    }
}
