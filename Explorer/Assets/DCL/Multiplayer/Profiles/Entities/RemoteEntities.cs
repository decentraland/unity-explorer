using Arch.Core;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Profiles.RemoteProfiles;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Optimization.Pools;
using DCL.Utilities.Extensions;
using ECS.Prioritization.Components;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Profiles.Entities
{
    public class RemoteEntities : IRemoteEntities
    {
        private readonly IEntityParticipantTable entityParticipantTable;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private IComponentPool<Transform> transformPool = null!;

        public RemoteEntities(IEntityParticipantTable entityParticipantTable, IComponentPoolsRegistry componentPoolsRegistry)
        {
            this.entityParticipantTable = entityParticipantTable;
            this.componentPoolsRegistry = componentPoolsRegistry;
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
                new RemotePlayerMovementComponent(profile.WalletId),
                new InterpolationComponent(),
                new ExtrapolationComponent()
            );

            entityParticipantTable.Register(profile.WalletId, entity);
        }
    }
}
