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
        private readonly ILoadingStatus loadingStatus;

        private bool alreadyStarted;

        public ConnectionRoomsSystem(
            World world,
            IArchipelagoIslandRoom archipelagoIslandRoom,
            IGateKeeperSceneRoom gateKeeperSceneRoom,
            ILoadingStatus loadingStatus) : base(world)
        {
            this.archipelagoIslandRoom = archipelagoIslandRoom;
            this.gateKeeperSceneRoom = gateKeeperSceneRoom;
            this.loadingStatus = loadingStatus;
        }

        protected override void Update(float t)
        {
            // Don't connect to the rooms until the loading process has finished
            if (loadingStatus.CurrentStage.Value != LoadingStatus.LoadingStage.Completed || alreadyStarted) return;

            archipelagoIslandRoom.StartIfNotAsync();
            gateKeeperSceneRoom.StartIfNotAsync();
            alreadyStarted = true;
        }
    }
}
