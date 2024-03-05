using Arch.Core;
using DCL.Multiplayer.Profiles.RemoteProfiles;
using DCL.Multiplayer.Profiles.Tables;
using System.Collections.Generic;

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

            Entity entity = world.Create(profile.Profile);
            entityParticipantTable.Register(profile.WalletId, entity);
        }
    }
}
