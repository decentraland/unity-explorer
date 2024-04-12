using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Threading;

namespace DCL.Profiles
{
    public class LogProfileRepository : IProfileRepository
    {
        private readonly IProfileRepository origin;
        private readonly Action<string> log;

        public LogProfileRepository(IProfileRepository origin) : this(origin, ReportHub.WithReport(ReportCategory.PROFILE).Log) { }

        public LogProfileRepository(IProfileRepository origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

        public async UniTask SetAsync(Profile profile, CancellationToken ct)
        {
            log($"ProfileRepository: set requested for profile: {profile}");
            await origin.SetAsync(profile, ct);
            log($"ProfileRepository: set finished for profile: {profile}");
        }

        public async UniTask<Profile?> GetAsync(string id, int version, CancellationToken ct)
        {
            log($"ProfileRepository: get requested for id: {id}, version: {version}");
            var result = await origin.GetAsync(id, version, ct);
            log($"ProfileRepository: get finished for id: {id}, version: {version}, profile: {result}{(result == null ? "null" : string.Empty)}");
            return result;
        }
    }
}
