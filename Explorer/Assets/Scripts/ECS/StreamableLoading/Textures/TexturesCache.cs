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

namespace ECS.StreamableLoading.Textures
{
    public class TexturesCache : StreamableCacheBase<Texture2D, GetTextureIntention>, IStreamableCache<Texture2D, GetTextureIntention>
    {
        internal readonly Dictionary<GetTextureIntention, Texture2D> cache;
        private readonly SimplePriorityQueue<GetTextureIntention, long> unloadQueue = new ();

        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<Texture2D>?>> OngoingRequests { get; }
        public IDictionary<string, StreamableLoadingResult<Texture2D>> IrrecoverableFailures { get; }

        public TexturesCache()
        {
            cache = new Dictionary<GetTextureIntention, Texture2D>(this);
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

        public void Add(in GetTextureIntention key, Texture2D asset)
        {
            Add(cache, unloadQueue, ProfilingCounters.TexturesInCache, in key, asset);
        }

        public bool TryGet(in GetTextureIntention key, out Texture2D texture)
        {
            if (!cache.TryGetValue(key, out texture)) return false;

            unloadQueue.TryUpdatePriority(key, MultithreadingUtility.FrameCount);
            return true;
        }

        public void Unload(IPerformanceBudget frameTimeBudget, int maxUnloadAmount)
        {
            for (var i = 0; frameTimeBudget.TrySpendBudget()
                            && i < maxUnloadAmount && unloadQueue.Count > 0
                            && unloadQueue.TryDequeue(out GetTextureIntention key); i++)
            {
                UnityObjectUtils.SafeDestroy(cache[key]);
                ProfilingCounters.TexturesAmount.Value--;

                cache.Remove(key);
            }

            ProfilingCounters.TexturesInCache.Value = cache.Count;
        }

        public void Dereference(in GetTextureIntention key, Texture2D asset) { }

        public bool Equals(GetTextureIntention x, GetTextureIntention y) =>
            x.Equals(y);

        public int GetHashCode(GetTextureIntention obj) =>
            obj.GetHashCode();
    }
}
