using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using LiveKit.Rooms;
using System;

namespace DCL.Multiplayer.Connections.RoomHubs
{
    public class RoomHub : IRoomHub
    {
        private readonly IArchipelagoIslandRoom archipelagoIslandRoom;
        private readonly IGateKeeperSceneRoom gateKeeperSceneRoom;

        public RoomHub(IArchipelagoIslandRoom archipelagoIslandRoom, IGateKeeperSceneRoom gateKeeperSceneRoom)
        {
            this.archipelagoIslandRoom = archipelagoIslandRoom;
            this.gateKeeperSceneRoom = gateKeeperSceneRoom;
        }

        public IRoom IslandRoom() =>
            archipelagoIslandRoom.Room();

        public IRoom SceneRoom() =>
            gateKeeperSceneRoom.Room();
    }
}
