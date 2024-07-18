using Arch.Core;
using CrdtEcsBridge.Physics;
using DCL.AvatarRendering.Emotes;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
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
using DCL.Profiles;
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
        private readonly IEntityCollidersGlobalCache collidersGlobalCache;
        private readonly Dictionary<string, Collider> collidersByWalletId = new ();
        private IComponentPool<RemoteAvatarCollider> remoteAvatarColliderPool = null!;
        private IComponentPool<Transform> transformPool = null!;

        public RemoteEntities(
            IRoomHub roomHub,
            IEntityParticipantTable entityParticipantTable,
            IComponentPoolsRegistry componentPoolsRegistry,
            IObjectPool<SimplePriorityQueue<NetworkMovementMessage>> queuePool,
            IEntityCollidersGlobalCache collidersGlobalCache)
        {
            this.roomHub = roomHub;
            this.entityParticipantTable = entityParticipantTable;
            this.componentPoolsRegistry = componentPoolsRegistry;
            this.queuePool = queuePool;
            this.collidersGlobalCache = collidersGlobalCache;
        }

        public void Initialize(RemoteAvatarCollider remoteAvatarCollider)
        {
            remoteAvatarColliderPool = componentPoolsRegistry.AddGameObjectPool(() => Object.Instantiate(remoteAvatarCollider, Vector3.zero, Quaternion.identity));
            transformPool = componentPoolsRegistry
                           .GetReferenceTypePool<Transform>()
                           .EnsureNotNull("ReferenceTypePool of type Transform not found in the registry");
        }

        public void TryCreateOrUpdate(IReadOnlyCollection<RemoteProfile> list, World world)
        {
            foreach (RemoteProfile remoteProfile in list)
                TryCreateOrUpdateRemoteEntity(remoteProfile, world);
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

            if (collidersByWalletId.TryGetValue(walletId, out Collider collider))
            {
                collidersGlobalCache.RemoveGlobalEntityAssociation(collider);
                collidersByWalletId.Remove(walletId);
            }

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

        private void TryCreateOrUpdateRemoteEntity(in RemoteProfile profile, World world)
        {
            if (entityParticipantTable.Has(profile.WalletId))
            {
                UpdateCharacter(profile, world);
                return;
            }

            Entity entity = CreateCharacter(profile, world);
            entityParticipantTable.Register(profile.WalletId, entity);
        }

        private Entity CreateCharacter(RemoteProfile profile, World world)
        {
            var transform = transformPool.Get()!;
            transform.name = $"REMOTE_ENTITY_{profile.WalletId}";
            transform.transform.SetParent(null);
            transform.transform.rotation = Quaternion.identity;
            transform.transform.localScale = Vector3.one;

            var remoteAvatarCollider = remoteAvatarColliderPool.Get()!;
            remoteAvatarCollider.name = $"Collider {profile.WalletId}";
            remoteAvatarCollider.transform.SetParent(transform);
            remoteAvatarCollider.transform.rotation = Quaternion.identity;
            remoteAvatarCollider.transform.localScale = Vector3.one;
            collidersByWalletId.TryAdd(profile.WalletId, remoteAvatarCollider.Collider);

            var transformComp = new CharacterTransform(transform);

            Entity entity = world.Create(
                profile.Profile,
                remoteAvatarCollider,
                transformComp,
                new CharacterAnimationComponent(),
                new CharacterEmoteComponent(),
                new RemotePlayerMovementComponent(queuePool),
                new InterpolationComponent(),
                new ExtrapolationComponent()
            );

            collidersGlobalCache.Associate(remoteAvatarCollider.Collider, world.Reference(entity));

            ProfileUtils.CreateProfilePicturePromise(profile.Profile, world, PartitionComponent.TOP_PRIORITY);

            return entity;
        }

        private void UpdateCharacter(in RemoteProfile remoteProfile, World world)
        {
            var entity = entityParticipantTable.Entity(remoteProfile.WalletId);
            var profile = remoteProfile.Profile;

            if (world.TryGet(entity, out Profile? existingProfile))
                if (existingProfile!.Version == profile.Version)
                    return;

            world.Set(entity, profile);
            // Force to update the avatar through the profile
            profile.IsDirty = true;

            ProfileUtils.CreateProfilePicturePromise(profile, world, PartitionComponent.TOP_PRIORITY);
        }
    }
}
