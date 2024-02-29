using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using ECS.Abstract;

namespace DCL.Multiplayer.Connections.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ConnectionRoomsSystem))]
    public partial class DebugRoomsSystem : BaseUnityLoopSystem
    {
        private readonly IArchipelagoIslandRoom archipelagoIslandRoom;
        private readonly IGateKeeperSceneRoom gateKeeperSceneRoom;

        private readonly ElementBinding<string> stateScene;
        private readonly ElementBinding<string> remoteParticipantsScene;

        private readonly ElementBinding<string> islandsStateScene;
        private readonly ElementBinding<string> islandsRemoteParticipantsScene;

        private string? previous;

        public DebugRoomsSystem(
            World world,
            IArchipelagoIslandRoom archipelagoIslandRoom,
            IGateKeeperSceneRoom gateKeeperSceneRoom,
            IDebugContainerBuilder debugBuilder
        ) : base(world)
        {
            this.archipelagoIslandRoom = archipelagoIslandRoom;
            this.gateKeeperSceneRoom = gateKeeperSceneRoom;

            islandsStateScene = new ElementBinding<string>(string.Empty);
            islandsRemoteParticipantsScene = new ElementBinding<string>(string.Empty);

            stateScene = new ElementBinding<string>(string.Empty);
            remoteParticipantsScene = new ElementBinding<string>(string.Empty);

            debugBuilder.AddWidget("Rooms")!
                        .SetVisibilityBinding(new DebugWidgetVisibilityBinding(true))!
                        .AddCustomMarker("State", stateScene)!
                        .AddCustomMarker("Remote Participants", remoteParticipantsScene)!
                        .AddCustomMarker("State Island", islandsStateScene)!
                        .AddCustomMarker("Remote Participants Island", islandsRemoteParticipantsScene);
        }

        protected override void Update(float t)
        {
            var text = $"{HealthInfo(archipelagoIslandRoom, "Island Room")}";
            if (text != previous) ReportHub.WithReport(ReportCategory.ARCHIPELAGO_REQUEST).Log(text);
            previous = text;

            stateScene.SetAndUpdate(gateKeeperSceneRoom.CurrentState().ToString());

            remoteParticipantsScene.SetAndUpdate(
                (gateKeeperSceneRoom.CurrentState() is IConnectiveRoom.State.Running
                    ? gateKeeperSceneRoom.Room().Participants.RemoteParticipantSids().Count
                    : 0
                ).ToString()
            );

            islandsStateScene.SetAndUpdate(archipelagoIslandRoom.CurrentState().ToString());

            islandsRemoteParticipantsScene.SetAndUpdate(
                (archipelagoIslandRoom.CurrentState() is IConnectiveRoom.State.Running
                    ? archipelagoIslandRoom.Room().Participants.RemoteParticipantSids().Count
                    : 0
                ).ToString()
            );
        }

        private static string HealthInfo(IConnectiveRoom connectiveRoom, string name) =>
            $"{name}: {connectiveRoom.CurrentState()}; participantsCount {(connectiveRoom.CurrentState() is IConnectiveRoom.State.Running ? connectiveRoom.Room().Participants.RemoteParticipantSids().Count : 0)}";
    }
}
