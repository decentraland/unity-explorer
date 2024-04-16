using Arch.Core;
using DCL.Multiplayer.Profiles.Bunches;
using DCL.Multiplayer.Profiles.RemoteProfiles;
using DCL.Multiplayer.Profiles.RemoveIntentions;
using System.Collections.Generic;

namespace DCL.Multiplayer.Profiles.Entities
{
    public interface IRemoteEntities
    {
        void Initialize();

        void TryCreate(IReadOnlyCollection<RemoteProfile> list, World world);

        void Remove(IReadOnlyCollection<RemoveIntention> list, World world);

        void ForceRemoveAll(World world);
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

        public static void Remove(this IRemoteEntities remoteEntities, IRemoveIntentions removeIntentions, World world)
        {
            using OwnedBunch<RemoveIntention> bunch = removeIntentions.Bunch();
            IReadOnlyCollection<RemoveIntention> collection = bunch.Collection();
            remoteEntities.Remove(collection, world);
        }
    }
}
