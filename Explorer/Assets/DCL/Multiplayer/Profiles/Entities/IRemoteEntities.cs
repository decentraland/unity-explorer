using Arch.Core;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Multiplayer.Profiles.RemoteProfiles;
using System.Collections.Generic;

namespace DCL.Multiplayer.Profiles.Entities
{
    public interface IRemoteEntities
    {
        void TryCreate(IReadOnlyCollection<RemoteProfile> list, World world);
    }

    public static class RemoteEntitiesExtensions
    {
        public static void TryCreate(this IRemoteEntities remoteEntities, IRemoteProfiles remoteProfiles, World world)
        {
            if (remoteProfiles.NewBunchAvailable() == false)
                return;

            using Bunch<RemoteProfile> bunch = remoteProfiles.Bunch();
            IReadOnlyCollection<RemoteProfile> collection = bunch.Collection();
            remoteEntities.TryCreate(collection, world);
        }
    }
}
