using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.UserInAppInitializationFlow;
using ECS.Abstract;

namespace DCL.Multiplayer.Connections.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class ConnectionRoomsSystem : BaseUnityLoopSystem
    {
        private readonly IArchipelagoIslandRoom archipelagoIslandRoom;
        private readonly IGateKeeperSceneRoom gateKeeperSceneRoom;
        private readonly RealFlowLoadingStatus loadingStatus;

        public ConnectionRoomsSystem(
            World world,
            IArchipelagoIslandRoom archipelagoIslandRoom,
            IGateKeeperSceneRoom gateKeeperSceneRoom,
            RealFlowLoadingStatus loadingStatus) : base(world)
        {
            this.archipelagoIslandRoom = archipelagoIslandRoom;
            this.gateKeeperSceneRoom = gateKeeperSceneRoom;
            this.loadingStatus = loadingStatus;
        }

        protected override void Update(float t)
        {
            // Don't connect to the rooms until the loading process has finished
            if (loadingStatus.CurrentStage != RealFlowLoadingStatus.Stage.Completed) return;

            archipelagoIslandRoom.StartIfNot();
            gateKeeperSceneRoom.StartIfNot();
        }
    }
}
