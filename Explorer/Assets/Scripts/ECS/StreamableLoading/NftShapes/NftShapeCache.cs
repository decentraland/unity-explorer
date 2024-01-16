using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using Utility.Multithreading;
using Utility.PriorityQueue;

namespace ECS.StreamableLoading.NftShapes
{
    public class NftShapeCache : IStreamableCache<Texture2D, GetNftShapeIntention>
    {
        internal readonly Dictionary<GetNftShapeIntention, Texture2D> cache;
        private readonly SimplePriorityQueue<GetNftShapeIntention, long> unloadQueue = new ();

        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<Texture2D>?>> OngoingRequests { get; }
        public IDictionary<string, StreamableLoadingResult<Texture2D>> IrrecoverableFailures { get; }

        public NftShapeCache()
        {
            cache = new Dictionary<GetNftShapeIntention, Texture2D>(this);
            IrrecoverableFailures = new Dictionary<string, StreamableLoadingResult<Texture2D>>();
            OngoingRequests = new Dictionary<string, UniTaskCompletionSource<StreamableLoadingResult<Texture2D>?>>();
        }

        public void Dispose()
        {
            foreach (Texture2D texture in cache.Values)
                UnityObjectUtils.SafeDestroy(texture);

            cache.Clear();
            unloadQueue.Clear();
        }

        public void Add(in GetNftShapeIntention key, Texture2D asset)
        {
            if (cache.TryAdd(key, asset))
                unloadQueue.Enqueue(key, MultithreadingUtility.FrameCount);

            ProfilingCounters.TexturesInCache.Value = cache.Count;
        }

        public bool TryGet(in GetNftShapeIntention key, out Texture2D texture)
        {
            if (!cache.TryGetValue(key, out texture)) return false;

            unloadQueue.TryUpdatePriority(key, MultithreadingUtility.FrameCount);
            return true;
        }

        public void Unload(IPerformanceBudget frameTimeBudget, int maxUnloadAmount)
        {
            for (var i = 0; frameTimeBudget.TrySpendBudget()
                            && i < maxUnloadAmount && unloadQueue.Count > 0
                            && unloadQueue.TryDequeue(out GetNftShapeIntention key); i++)
            {
                UnityObjectUtils.SafeDestroy(cache[key]!);
                ProfilingCounters.TexturesAmount.Value--;

                cache.Remove(key);
            }

            ProfilingCounters.TexturesInCache.Value = cache.Count;
        }

        public bool Equals(GetNftShapeIntention x, GetNftShapeIntention y) =>
            x.Equals(y);

        public int GetHashCode(GetNftShapeIntention obj) =>
            obj.GetHashCode();
    }
}
