using Arch.Core;
using DCL.AvatarRendering.Emotes;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.Interaction.Utility;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Rooms;
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
using LiveKit.Proto;
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
        private readonly Dictionary<string, RemoteAvatarCollider> collidersByWalletId = new ();
        private readonly Transform? remoteEntitiesParent = null;
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
#if UNITY_EDITOR
            remoteEntitiesParent = new GameObject("REMOTE_ENTITIES").transform;
#endif
        }

        public void Initialize(RemoteAvatarCollider remoteAvatarCollider)
        {
            remoteAvatarColliderPool = componentPoolsRegistry.AddGameObjectPool(() => Object.Instantiate(remoteAvatarCollider));
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

                TryRemove(walletId, removeIntention.FromRoom, world);
            }
        }

        public void ForceRemoveAll(World world)
        {
            tempRemoveAll.Clear();
            tempRemoveAll.AddRange(entityParticipantTable.Wallets());
            foreach (string wallet in tempRemoveAll) Remove(wallet, RoomSource.GATEKEEPER | RoomSource.ISLAND, world);
        }

        public void TryRemove(string walletId, RoomSource roomSource, World world)
        {
            if (!entityParticipantTable.Has(walletId))
                return;

            Remove(walletId, roomSource, world);
        }

        private void Remove(string walletId, RoomSource roomSource, World world)
        {
            IReadOnlyEntityParticipantTable.Entry entry = entityParticipantTable.Get(walletId);

            if (!entityParticipantTable.Release(walletId, roomSource))
                return;

            if (collidersByWalletId.TryGetValue(walletId, out RemoteAvatarCollider remoteAvatarCollider))
            {
                remoteAvatarColliderPool.Release(remoteAvatarCollider);
                collidersGlobalCache.RemoveGlobalEntityAssociation(remoteAvatarCollider.Collider);
                collidersByWalletId.Remove(walletId);
            }

            world.AddOrGet(entry.Entity, new DeleteEntityIntention());
        }

        public Entity TryCreateOrUpdateRemoteEntity(in RemoteProfile profile, World world)
        {
            Entity entity;

            if (entityParticipantTable.TryGet(profile.WalletId, out IReadOnlyEntityParticipantTable.Entry entry))
            {
                UpdateCharacter(profile, world, entry.Entity);
                entity = entry.Entity;
            }
            else
            {
                entity = CreateCharacter(profile, world);
                entityParticipantTable.Register(profile.WalletId, entity, profile.FromRoom);
            }

            entityParticipantTable.AddRoomSource(profile.WalletId, profile.FromRoom);
            return entity;
        }

        private Entity CreateCharacter(RemoteProfile profile, World world)
        {
            var transform = transformPool.Get()!;
            transform.name = $"REMOTE_ENTITY_{profile.WalletId}";
            transform.transform.SetParent(remoteEntitiesParent);
            transform.transform.rotation = Quaternion.identity;
            transform.transform.localScale = Vector3.one;

            var remoteAvatarCollider = remoteAvatarColliderPool.Get()!;
            remoteAvatarCollider.name = $"Collider {profile.WalletId}";
            remoteAvatarCollider.transform.SetParent(transform);
            remoteAvatarCollider.transform.rotation = Quaternion.identity;
            remoteAvatarCollider.transform.localScale = Vector3.one;
            collidersByWalletId.TryAdd(profile.WalletId, remoteAvatarCollider);

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

        private void UpdateCharacter(in RemoteProfile remoteProfile, World world, Entity entity)
        {
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
