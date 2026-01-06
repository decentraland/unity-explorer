using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using ECS.Prioritization.Components;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Profiles
{
    public class LogProfileRepository : IProfileRepository
    {
        private readonly IProfileRepository origin;

        public LogProfileRepository(IProfileRepository origin)
        {
            this.origin = origin;
        }

        public async UniTask SetAsync(Profile profile, CancellationToken ct)
        {
            ReportHub
               .WithReport(ReportCategory.PROFILE)
               .Log($"ProfileRepository: set requested for profile: {profile}");
            await origin.SetAsync(profile, ct);
            ReportHub
               .WithReport(ReportCategory.PROFILE)
               .Log($"ProfileRepository: set finished for profile: {profile}");
        }

        public async UniTask<ProfileTier?> GetAsync(string id, int version, URLDomain? fromCatalyst, CancellationToken ct,
            bool getFromCacheIfPossible,
            IProfileRepository.FetchBehaviour fetchBehaviour, ProfileTier.Kind tier, IPartitionComponent? partition = null)
        {
            ReportHub
               .WithReport(ReportCategory.PROFILE)
               .Log($"ProfileRepository: get requested for id: {id}, version: {version}, from catalyst: {fromCatalyst}, {fetchBehaviour}, {partition}");

            ProfileTier? result = await origin.GetAsync(id, version, fromCatalyst, ct, getFromCacheIfPossible, fetchBehaviour, tier, partition);
            ReportHub
               .WithReport(ReportCategory.PROFILE)
               .Log($"ProfileRepository: get finished for id: {id}, version: {version}, from catalyst: {fromCatalyst}, profile: {result}{(result == null ? "null" : string.Empty)}");
            return result;
        }
    }
}
