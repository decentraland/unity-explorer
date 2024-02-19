using Arch.Core;
using Arch.SystemGroups;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using ECS.Abstract;
using ECS.Groups;

namespace DCL.Multiplayer.Connections.Systems
{
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    public partial class ConnectionRoomsSystem : BaseUnityLoopSystem
    {
        private readonly IArchipelagoIslandRoom archipelagoIslandRoom;
        private readonly IGateKeeperSceneRoom gateKeeperSceneRoom;

        public ConnectionRoomsSystem(
            World world,
            IArchipelagoIslandRoom archipelagoIslandRoom,
            IGateKeeperSceneRoom gateKeeperSceneRoom
        ) : base(world)
        {
            this.archipelagoIslandRoom = archipelagoIslandRoom;
            this.gateKeeperSceneRoom = gateKeeperSceneRoom;
        }

        protected override void Update(float t)
        {
            archipelagoIslandRoom.StartIfNotRunning();
            gateKeeperSceneRoom.StartIfNotRunning();
        }
    }
}
