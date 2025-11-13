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
        /// <param name="userId"></param>
        /// <param name="profileDTO"></param>
        /// <returns></returns>
        public Profile? ResolveProfile(string userId, ProfileJsonDto? profileDTO);

        public URLAddress PostUrl(URLDomain fromCatalyst);
    }
}
