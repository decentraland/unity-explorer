using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.Multiplayer.Connections.Credentials;
using DCL.Multiplayer.Connections.Credentials.Hub;
using DCL.Multiplayer.Connections.RoomHubs;
using ECS.Abstract;
using ECS.Groups;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Rooms;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Multiplayer.Connections.Systems
{
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    public partial class ConnectionRoomsSystem : BaseUnityLoopSystem
    {
        private readonly IMutableRoomHub roomHub;
        private readonly IMultiPool multiPool;
        private readonly ICredentialsHub credentialsHub;
        private Vector2Int currentParcelPosition = new (int.MaxValue, int.MaxValue);

        public ConnectionRoomsSystem(
            World world,
            IMutableRoomHub roomHub,
            IMultiPool multiPool,
            ICredentialsHub credentialsHub
        ) : base(world)
        {
            this.roomHub = roomHub;
            this.multiPool = multiPool;
            this.credentialsHub = credentialsHub;
        }

        protected override void Update(float t)
        {
            AssignRoomsQuery(World!);
        }

        [Query]
        private void AssignRooms(in CharacterTransform transformComponent)
        {
            UpdatePose(transformComponent.Position, out bool newPositioned);
            if (newPositioned) UpdateSceneRoomAsync(currentParcelPosition).Forget();
            //TODO check if I need to update the room
            if (true) UpdateIslandRoomAsync().Forget();
        }

        private void UpdatePose(in Vector3 characterPosition, out bool updated)
        {
            var newParcelPosition = ParcelMathHelper.WorldToGridPosition(characterPosition);
            updated = newParcelPosition != currentParcelPosition;
            currentParcelPosition = newParcelPosition;
        }

        private async UniTaskVoid UpdateSceneRoomAsync(Vector2Int parcelPosition)
        {
            var credentials = await credentialsHub.SceneRoomCredentials(parcelPosition, CancellationToken.None);
            var room = await NewConnectedRoom(credentials, CancellationToken.None);
            roomHub.AssignSceneRoom(room);
        }

        private async UniTaskVoid UpdateIslandRoomAsync()
        {
            var credentials = await credentialsHub.IslandRoomCredentials(CancellationToken.None);
            var room = await NewConnectedRoom(credentials, CancellationToken.None);
            roomHub.AssignIslandRoom(room);
        }

        private async UniTask<IRoom> NewConnectedRoom(ICredentials credentials, CancellationToken cancellationToken)
        {
            var room = multiPool.Get<Room>();
            bool connected = await room.Connect(credentials.Url, credentials.AuthToken, cancellationToken);

            if (connected == false)
            {
                multiPool.Release(room);
                throw new System.Exception($"Failed to connect to room {credentials.Url} with token {credentials.AuthToken}");
            }

            return room;
        }
    }
}
