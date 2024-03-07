using Arch.Core;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Profiles.RemoteProfiles;
using DCL.Multiplayer.Profiles.Tables;
using ECS.Prioritization.Components;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Profiles.Entities
{
    public class RemoteEntities : IRemoteEntities
    {
        private readonly IEntityParticipantTable entityParticipantTable;

        public RemoteEntities(IEntityParticipantTable entityParticipantTable)
        {
            this.entityParticipantTable = entityParticipantTable;
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

            Entity entity = world.Create(
                profile.Profile,
                PartitionComponent.TOP_PRIORITY,
                new CharacterTransform(new GameObject("REMOTE_ENTITY").transform), //TODO pooling
                new CharacterAnimationComponent()
            );
            entityParticipantTable.Register(profile.WalletId, entity);
        }
    }
}
