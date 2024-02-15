using Arch.Core;
using Arch.SystemGroups;
using DCL.Multiplayer.Connections.Credentials.Archipelago.Rooms;
using ECS.Abstract;
using ECS.Groups;

namespace DCL.Multiplayer.Connections.Systems
{
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    public partial class ConnectionRoomsSystem : BaseUnityLoopSystem
    {
        private readonly IArchipelagoIslandRoom archipelagoIslandRoom;

        public ConnectionRoomsSystem(
            World world,
            IArchipelagoIslandRoom archipelagoIslandRoom
        ) : base(world)
        {
            this.archipelagoIslandRoom = archipelagoIslandRoom;
        }

        protected override void Update(float t)
        {
            archipelagoIslandRoom.StartIfNotRunning();
        }
    }
}
