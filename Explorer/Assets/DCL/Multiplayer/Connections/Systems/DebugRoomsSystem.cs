using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Rooms;
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
        private string? previous;

        public DebugRoomsSystem(
            World world,
            IArchipelagoIslandRoom archipelagoIslandRoom,
            IGateKeeperSceneRoom gateKeeperSceneRoom,
            IDebugContainerBuilder _
        ) : base(world)
        {
            this.archipelagoIslandRoom = archipelagoIslandRoom;
            this.gateKeeperSceneRoom = gateKeeperSceneRoom;

            // this.debugBuilder.AddWidget("Rooms")
            //     //.AddToggleField("Island Room", OnArchipelagoIslandRoomToggle, archipelagoIslandRoom.IsRunning)
            //     //.AddToggleField("Scene Room", OnGateKeeperSceneRoomToggle, gateKeeperSceneRoom.IsRunning);
            //    .AddMarker()
        }

        protected override void Update(float t)
        {
            var text = $"{HealthInfo(archipelagoIslandRoom, "Island Room")}\n{HealthInfo(gateKeeperSceneRoom, "Scene Room")}";
            if (text != previous) Debug.Log(text);
            previous = text;
        }

        private string HealthInfo(IConnectiveRoom connectiveRoom, string name) =>
            $"Health of {name}: state {connectiveRoom.CurrentState()}; participantsCount {(connectiveRoom.CurrentState() is IConnectiveRoom.State.Running ? connectiveRoom.Room().Participants.RemoteParticipantSids().Count : 0)}";
    }
}
