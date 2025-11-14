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

        public async UniTask<List<Profile>> GetAsync(IReadOnlyList<string> ids, CancellationToken ct, URLDomain? fromCatalyst = null)
        {
            ReportHub
               .WithReport(ReportCategory.PROFILE)
               .Log($"ProfileRepository: get requested for ids: {string.Join(',', ids)}, from catalyst: {fromCatalyst}");

            List<Profile>? results = await origin.GetAsync(ids, ct, fromCatalyst);

            ReportHub
               .WithReport(ReportCategory.PROFILE)
               .Log($"ProfileRepository: get finished for ids: {string.Join(',', ids)}, from catalyst: {fromCatalyst}, results count: {results.Count}");

            return results;
        }

        public async UniTask<Profile?> GetAsync(string id, int version, URLDomain? fromCatalyst, CancellationToken ct, bool getFromCacheIfPossible = true,
            IProfileRepository.BatchBehaviour batchBehaviour = IProfileRepository.BatchBehaviour.DEFAULT, IPartitionComponent? partition = null)
        {
            ReportHub
               .WithReport(ReportCategory.PROFILE)
               .Log($"ProfileRepository: get requested for id: {id}, version: {version}, from catalyst: {fromCatalyst}, {batchBehaviour}, {partition}");

            Profile? result = await origin.GetAsync(id, version, fromCatalyst, ct, getFromCacheIfPossible, batchBehaviour, partition);
            ReportHub
               .WithReport(ReportCategory.PROFILE)
               .Log($"ProfileRepository: get finished for id: {id}, version: {version}, from catalyst: {fromCatalyst}, profile: {result}{(result == null ? "null" : string.Empty)}");
            return result;
        }
    }
}
