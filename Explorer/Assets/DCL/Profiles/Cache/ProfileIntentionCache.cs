using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using Utility.Multithreading;
using Utility.PriorityQueue;

namespace DCL.Profiles
{
    public class ProfileIntentionCache : StreamableCacheBase<Profile, GetProfileIntention>, IStreamableCache<Profile, GetProfileIntention>
    {
        private readonly Dictionary<GetProfileIntention, Profile> cache = new ();
        private readonly SimplePriorityQueue<GetProfileIntention, long> unloadQueue = new ();

        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<Profile>?>> OngoingRequests { get; }
            = new Dictionary<string, UniTaskCompletionSource<StreamableLoadingResult<Profile>?>>();
        public IDictionary<string, StreamableLoadingResult<Profile>> IrrecoverableFailures { get; }
            = new Dictionary<string, StreamableLoadingResult<Profile>>();

        public bool Equals(GetProfileIntention x, GetProfileIntention y) =>
            x.Equals(y);

        public int GetHashCode(GetProfileIntention obj) =>
            obj.GetHashCode();

        public void Dispose()
        {
            cache.Clear();
        }

        public bool TryGet(in GetProfileIntention key, out Profile asset) =>
            cache.TryGetValue(key, out asset);

        public void Add(in GetProfileIntention key, Profile asset)
        {
            Add(cache, unloadQueue, ProfilingCounters.ProfilesInCache, in key, asset);
        }

        public void Unload(IPerformanceBudget frameTimeBudgetProvider, int maxUnloadAmount)
        {
            for (var i = 0; frameTimeBudgetProvider.TrySpendBudget()
                            && i < maxUnloadAmount
                            && unloadQueue.Count > 0
                            && unloadQueue.TryDequeue(out GetProfileIntention key); i++)
                cache.Remove(key);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ProfilingCounters.ProfilesInCache.Value = cache.Count;
#endif
        }
    }
}
