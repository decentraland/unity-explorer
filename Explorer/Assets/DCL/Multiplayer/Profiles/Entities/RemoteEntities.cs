using Arch.Core;
using DCL.AvatarRendering.Emotes;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Profiles.RemoteProfiles;
using DCL.Multiplayer.Profiles.RemoveIntentions;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Optimization.Pools;
using DCL.Profiles.Helpers;
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
        private readonly IObjectPool<SimplePriorityQueue<NetworkMovementMessage>> queuePool;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly List<string> tempRemoveAll = new ();
        private IComponentPool<Transform> transformPool = null!;

        public RemoteEntities(IRoomHub roomHub, IEntityParticipantTable entityParticipantTable, IComponentPoolsRegistry componentPoolsRegistry, IObjectPool<SimplePriorityQueue<NetworkMovementMessage>> queuePool)
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

                if (DoesStillExist(walletId))
                    return;

                TryRemove(walletId, world);
            }
        }

        public void ForceRemoveAll(World world)
        {
            tempRemoveAll.Clear();
            tempRemoveAll.AddRange(entityParticipantTable.Wallets());
            foreach (string wallet in tempRemoveAll) TryRemove(wallet, world);
        }

        private void TryRemove(string walletId, World world)
        {
            if (entityParticipantTable.Has(walletId) == false)
                return;

            var entity = entityParticipantTable.Entity(walletId);

            world.AddOrGet(entity, new DeleteEntityIntention());
            entityParticipantTable.Release(walletId);
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
            transform.SetParent(null);
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            var transformComp = new CharacterTransform(transform);

            Entity entity = world.Create(
                profile.Profile,
                transformComp,
                new CharacterAnimationComponent(),
                new CharacterEmoteComponent(),
                new RemotePlayerMovementComponent(queuePool),
                new InterpolationComponent(),
                new ExtrapolationComponent()
            );

            ProfileUtils.CreateProfilePicturePromise(profile.Profile, world, PartitionComponent.TOP_PRIORITY);

            entityParticipantTable.Register(profile.WalletId, entity);
        }
    }
}
