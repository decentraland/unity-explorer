using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using ECS.Abstract;
using UnityEngine;

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

            stateScene = new ElementBinding<string>(string.Empty);
            remoteParticipantsScene = new ElementBinding<string>(string.Empty);

            debugBuilder.AddWidget("Rooms")!
                        .SetVisibilityBinding(new DebugWidgetVisibilityBinding(true))!
                        .AddCustomMarker("State", stateScene)!
                        .AddCustomMarker("Remote Participants", remoteParticipantsScene);
        }

        protected override void Update(float t)
        {
            var text = $"{HealthInfo(archipelagoIslandRoom, "Island Room")}";
            if (text != previous) Debug.Log(text);
            previous = text;

            stateScene.SetAndUpdate(gateKeeperSceneRoom.CurrentState().ToString());

            remoteParticipantsScene.SetAndUpdate(
                (gateKeeperSceneRoom.CurrentState() is IConnectiveRoom.State.Running
                    ? gateKeeperSceneRoom.Room().Participants.RemoteParticipantSids().Count
                    : 0
                ).ToString()
            );
        }

        private static string HealthInfo(IConnectiveRoom connectiveRoom, string name) =>
            $"{name}: {connectiveRoom.CurrentState()}; participantsCount {(connectiveRoom.CurrentState() is IConnectiveRoom.State.Running ? connectiveRoom.Room().Participants.RemoteParticipantSids().Count : 0)}";
    }
}
