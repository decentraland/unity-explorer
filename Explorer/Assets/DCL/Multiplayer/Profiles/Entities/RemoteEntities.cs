using Arch.Core;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Profiles.RemoteProfiles;
using DCL.Multiplayer.Profiles.RemoveIntentions;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Optimization.Pools;
using DCL.Utilities.Extensions;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using LiveKit.Rooms;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility.PriorityQueue;

namespace DCL.Multiplayer.Profiles.Entities
{
    public class RemoteEntities : IRemoteEntities
    {
        private readonly IRoomHub roomHub;
        private readonly IEntityParticipantTable entityParticipantTable;
        private readonly IObjectPool<SimplePriorityQueue<FullMovementMessage>> queuePool;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private IComponentPool<Transform> transformPool = null!;

        public RemoteEntities(IRoomHub roomHub, IEntityParticipantTable entityParticipantTable, IComponentPoolsRegistry componentPoolsRegistry, IObjectPool<SimplePriorityQueue<FullMovementMessage>> queuePool)
        {
            this.roomHub = roomHub;
            this.entityParticipantTable = entityParticipantTable;
            this.componentPoolsRegistry = componentPoolsRegistry;
            this.queuePool = queuePool;
        }

        public void Initialize()
        {
            transformPool = componentPoolsRegistry
                           .GetReferenceTypePool<Transform>()
                           .EnsureNotNull("ReferenceTypePool of type Transform not found in the registry");
        }

        public void TryCreate(IReadOnlyCollection<RemoteProfile> list, World world)
        {
            foreach (RemoteProfile remoteProfile in list)
                TryCreateRemoteEntity(remoteProfile, world);
        }

        public void Remove(IReadOnlyCollection<RemoveIntention> list, World world)
        {
            foreach (RemoveIntention removeIntention in list)
            {
                string walletId = removeIntention.WalletId;

                if (entityParticipantTable.Has(walletId) == false)
                    continue;

                if (DoesStillExist(walletId))
                    continue;

                var entity = entityParticipantTable.Entity(walletId);

                if (world.Has<RemotePlayerMovementComponent>(entity))
                    world.Get<RemotePlayerMovementComponent>(entity).Dispose();

                world.Add(entity, new DeleteEntityIntention());
                entityParticipantTable.Release(walletId);
            }
        }

        private bool DoesStillExist(string wallet)
        {
            bool ContainsInRoom(IRoom room)
            {
                foreach (string? sid in room.Participants.RemoteParticipantSids())
                {
                    if (sid != null
                        && room.Participants.RemoteParticipant(sid) is { } participant
                        && participant.Identity == wallet
                       )
                        return true;
                }

                return false;
            }

            return ContainsInRoom(roomHub.IslandRoom()) || ContainsInRoom(roomHub.SceneRoom());
        }

        private void TryCreateRemoteEntity(in RemoteProfile profile, World world)
        {
            if (entityParticipantTable.Has(profile.WalletId))
                return;

            var transform = transformPool.Get()!;
            transform.name = $"REMOTE_ENTITY_{profile.WalletId}";
            var transformComp = new CharacterTransform(transform);

            Entity entity = world.Create(
                profile.Profile,
                PartitionComponent.TOP_PRIORITY,
                transformComp,
                new CharacterAnimationComponent(),
                new RemotePlayerMovementComponent(profile.WalletId, queuePool),
                new InterpolationComponent(),
                new ExtrapolationComponent()
            );

            entityParticipantTable.Register(profile.WalletId, entity);
        }
    }
}
