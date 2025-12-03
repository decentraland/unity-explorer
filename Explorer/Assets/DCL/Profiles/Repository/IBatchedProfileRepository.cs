using CommunicationData.URLHelpers;
using System.Collections.Generic;

namespace DCL.Profiles
{
    public interface IBatchedProfileRepository : IProfileRepository
    {
        public IEnumerable<ProfilesBatchRequest> ConsumePendingBatch();

        /// <summary>
        ///     Should be called from the background thread
        /// </summary>
        public ProfileTier? ResolveProfile(string userId, ProfileTier? profile);

        public URLAddress PostUrl(URLDomain fromCatalyst, ProfileTier.Kind tier);
    }
}
