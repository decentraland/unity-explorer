using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using ECS.Abstract;

namespace DCL.Multiplayer.Connections.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
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
