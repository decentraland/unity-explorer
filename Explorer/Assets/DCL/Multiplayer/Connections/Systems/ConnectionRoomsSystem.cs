using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.Multiplayer.Connections.Credentials;
using DCL.Multiplayer.Connections.Credentials.Archipelago.Rooms;
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
        private readonly IMultiPool multiPool;
        private readonly IArchipelagoIslandRoom archipelagoIslandRoom;
        private Vector2Int currentParcelPosition = new (int.MaxValue, int.MaxValue);
        private static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(10);

        public ConnectionRoomsSystem(
            World world,
            IArchipelagoIslandRoom archipelagoIslandRoom,
            IMultiPool multiPool
        ) : base(world)
        {
            this.archipelagoIslandRoom = archipelagoIslandRoom;
            this.multiPool = multiPool;
        }

        protected override void Update(float t)
        {
            AssignRoomsQuery(World!);
            archipelagoIslandRoom.StartIfNotRunning();
        }

        [Query]
        private void AssignRooms(in CharacterTransform transformComponent)
        {
            UpdatePose(transformComponent.Position, out bool newPositioned);
            //TODO uncomment if (newPositioned) UpdateSceneRoomAsync(currentParcelPosition).Forget();
            //TODO check if I need to update the room
            //if (assigningIslandRoom == false) UpdateIslandRoomAsync().Forget();
        }

        private void UpdatePose(in Vector3 characterPosition, out bool updated)
        {
            var newParcelPosition = ParcelMathHelper.WorldToGridPosition(characterPosition);
            updated = newParcelPosition != currentParcelPosition;
            currentParcelPosition = newParcelPosition;
        }

        private async UniTaskVoid UpdateSceneRoomAsync(Vector2Int parcelPosition)
        {
            // var token = NewCancellationTokenSource();
            // var credentials = await credentialsHub.SceneRoomCredentials(parcelPosition, token.Token);
            // var room = await NewConnectedRoom(credentials, token.Token);
            // roomHub.AssignSceneRoom(room);
        }

        private async UniTask<IRoom> NewConnectedRoom(ICredentials credentials, CancellationToken cancellationToken)
        {
            var room = multiPool.Get<Room>();
            await room.EnsuredConnect(credentials, multiPool, cancellationToken);
            return room;
        }

        private static CancellationTokenSource NewCancellationTokenSource() =>
            new (TIMEOUT);
    }
}
