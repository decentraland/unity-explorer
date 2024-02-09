using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.WebRequests;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Transforms.Components;
using LiveKit.Internal.FFIClients.Requests;
using LiveKit.Rooms;

namespace DCL.Multiplayer.Connections.Systems
{
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    public partial class ConnectionRoomsSystem : BaseUnityLoopSystem
    {
        private readonly IWebRequestController webRequests;
        private readonly IMutableRoomHub roomHub;
        private readonly IFFIBridge ffiBridge;

        public ConnectionRoomsSystem(World world, IWebRequestController webRequests, IFFIBridge ffiBridge, IMutableRoomHub roomHub) : base(world)
        {
            this.webRequests = webRequests;
            this.ffiBridge = ffiBridge;
            this.roomHub = roomHub;
        }

        protected override void Update(float t)
        {
            AssignRoomsQuery(World!);
        }

        [Query]
        private void AssignRooms(in TransformComponent transformComponent)
        {

        }
    }
}
