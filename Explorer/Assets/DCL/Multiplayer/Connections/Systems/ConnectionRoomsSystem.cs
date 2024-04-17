using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.UserInAppInitializationFlow;
using ECS.Abstract;
using System.Threading;
using Utility;

namespace DCL.Multiplayer.Connections.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class ConnectionRoomsSystem : BaseUnityLoopSystem
    {
        private readonly IRealmRoomsProvider archipelagoIslandRoom;
        private readonly IGateKeeperSceneRoomProvider gateKeeperSceneRoomProvider;
        private readonly IReadOnlyRealFlowLoadingStatus loadingStatus;

        private CancellationTokenSource cancellationTokenSource = new ();

        private bool alreadyStarted;

        public ConnectionRoomsSystem(
            World world,
            IRealmRoomsProvider archipelagoIslandRoom,
            IGateKeeperSceneRoomProvider gateKeeperSceneRoomProvider,
            IReadOnlyRealFlowLoadingStatus loadingStatus) : base(world)
        {
            this.archipelagoIslandRoom = archipelagoIslandRoom;
            this.gateKeeperSceneRoomProvider = gateKeeperSceneRoomProvider;
            this.loadingStatus = loadingStatus;
        }

        public override void Dispose()
        {
            cancellationTokenSource.SafeCancelAndDispose();
            cancellationTokenSource = null;
        }

        protected override void Update(float t)
        {
            // Don't connect to the rooms until the loading process has finished
            if (loadingStatus.CurrentStage != RealFlowLoadingStatus.Stage.Completed || alreadyStarted) return;

            archipelagoIslandRoom.StartIfNeededAsync(cancellationTokenSource.Token).Forget();
            gateKeeperSceneRoomProvider.StartIfNeededAsync(cancellationTokenSource.Token).Forget();
            alreadyStarted = true;
        }
    }
}
