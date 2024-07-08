using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.NFTShapes;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using Utility.Multithreading;
using Utility.PriorityQueue;

namespace ECS.StreamableLoading.NFTShapes
{
    public class NftShapeCache : StreamableCacheBase<Texture2D, GetNFTShapeIntention>, IStreamableCache<Texture2D, GetNFTShapeIntention>
    {
        internal readonly Dictionary<GetNFTShapeIntention, Texture2D> cache;
        private readonly SimplePriorityQueue<GetNFTShapeIntention, long> unloadQueue = new ();

        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<Texture2D>?>> OngoingRequests { get; }
        public IDictionary<string, StreamableLoadingResult<Texture2D>> IrrecoverableFailures { get; }

        public NftShapeCache()
        {
            cache = new Dictionary<GetNFTShapeIntention, Texture2D>(this);
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

        public void Add(in GetNFTShapeIntention key, Texture2D asset)
        {
            Add(cache, unloadQueue, ProfilingCounters.TexturesInCache, in key, asset);
        }

        public bool TryGet(in GetNFTShapeIntention key, out Texture2D texture)
        {
            if (!cache.TryGetValue(key, out texture)) return false;

            unloadQueue.TryUpdatePriority(key, MultithreadingUtility.FrameCount);
            return true;
        }

        public void Unload(IPerformanceBudget frameTimeBudget, int maxUnloadAmount)
        {
            for (var i = 0; frameTimeBudget.TrySpendBudget()
                            && i < maxUnloadAmount && unloadQueue.Count > 0
                            && unloadQueue.TryDequeue(out GetNFTShapeIntention key); i++)
            {
                UnityObjectUtils.SafeDestroy(cache[key]!);
                ProfilingCounters.TexturesAmount.Value--;

                cache.Remove(key);
            }

            ProfilingCounters.TexturesInCache.Value = cache.Count;
        }

        public bool Equals(GetNFTShapeIntention x, GetNFTShapeIntention y) =>
            x.Equals(y);

        public int GetHashCode(GetNFTShapeIntention obj) =>
            obj.GetHashCode();
    }
}
