using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using Unity.Profiling;

namespace DCL.Profiles
{
    /// <summary>
    ///     TODO unify with <see cref="DefaultProfileCache" />
    /// </summary>
    public class ProfileIntentionCache : RefCountStreamableCacheBase<ProfileData, Profile, GetProfileIntention>
    {
        protected override ref ProfilerCounterValue<int> inCacheCount => ref ProfilingCounters.ProfilesInCache;

        public override bool Equals(GetProfileIntention x, GetProfileIntention y) =>
            x.Equals(y);

        public override int GetHashCode(GetProfileIntention obj) =>
            obj.GetHashCode();
    }
}
