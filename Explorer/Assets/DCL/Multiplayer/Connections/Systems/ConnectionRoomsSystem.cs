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
using System;
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
        private bool assigningIslandRoom;
        private static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(10);

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
            if (assigningIslandRoom == false) UpdateIslandRoomAsync().Forget();
        }

        private void UpdatePose(in Vector3 characterPosition, out bool updated)
        {
            var newParcelPosition = ParcelMathHelper.WorldToGridPosition(characterPosition);
            updated = newParcelPosition != currentParcelPosition;
            currentParcelPosition = newParcelPosition;
        }

        private async UniTaskVoid UpdateSceneRoomAsync(Vector2Int parcelPosition)
        {
            var token = NewCancellationTokenSource();
            var credentials = await credentialsHub.SceneRoomCredentials(parcelPosition, token.Token);
            var room = await NewConnectedRoom(credentials, token.Token);
            roomHub.AssignSceneRoom(room);
        }

        private async UniTaskVoid UpdateIslandRoomAsync()
        {
            try
            {
                assigningIslandRoom = true;
                var token = NewCancellationTokenSource();
                var credentials = await credentialsHub.IslandRoomCredentials(token.Token);
                var room = await NewConnectedRoom(credentials, token.Token);
                roomHub.AssignIslandRoom(room);
            }
            catch (Exception e) { throw new Exception("Failed to assign island room", e); }
            finally { assigningIslandRoom = false; }
        }

        private async UniTask<IRoom> NewConnectedRoom(ICredentials credentials, CancellationToken cancellationToken)
        {
            var room = multiPool.Get<Room>();
            bool connected = await room.Connect(credentials.Url, credentials.AuthToken, cancellationToken);

            if (connected == false)
            {
                multiPool.Release(room);
                throw new Exception($"Failed to connect to room {credentials.Url} with token {credentials.AuthToken}");
            }

            return room;
        }

        private static CancellationTokenSource NewCancellationTokenSource() =>
            new (TIMEOUT);
    }
}
