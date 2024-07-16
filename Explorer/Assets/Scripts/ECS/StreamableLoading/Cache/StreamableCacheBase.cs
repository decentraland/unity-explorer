using System.Collections.Generic;
using Unity.Profiling;
using Utility.Multithreading;
using Utility.PriorityQueue;

namespace ECS.StreamableLoading.Cache
{
    public abstract class StreamableCacheBase<TAsset, TLoadingIntention>
    {
        protected static void Add(IDictionary<TLoadingIntention, TAsset> cache, IPriorityQueue<TLoadingIntention, long> unloadQueue, ProfilerCounterValue<int> profilerCounterValue,
            in TLoadingIntention key, TAsset asset)
        {
            if (cache.TryAdd(key, asset))
                unloadQueue.Enqueue(key, MultithreadingUtility.FrameCount);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            profilerCounterValue.Value = cache.Count;
#endif
        }
    }
}
