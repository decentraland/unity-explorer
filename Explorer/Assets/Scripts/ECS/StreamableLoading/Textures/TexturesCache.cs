using Cysharp.Threading.Tasks;
using DCL.PerformanceAndDiagnostics.Optimization.PerformanceBudgeting;
using DCL.PerformanceAndDiagnostics.Optimization.Priority_Queue;
using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Utility.Multithreading;

namespace ECS.StreamableLoading.Textures
{
    public class TexturesCache : IStreamableCache<Texture2D, GetTextureIntention>
    {
        private readonly Dictionary<GetTextureIntention, Texture2D> cache;
        private readonly SimplePriorityQueue<GetTextureIntention, long> unloadQueue = new ();

        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<Texture2D>?>> OngoingRequests { get; }

        public IDictionary<string, StreamableLoadingResult<Texture2D>> IrrecoverableFailures { get; }

        private bool disposed { get; set; }

        public TexturesCache()
        {
            cache = new Dictionary<GetTextureIntention, Texture2D>(this);

            IrrecoverableFailures = DictionaryPool<string, StreamableLoadingResult<Texture2D>>.Get();
            OngoingRequests = DictionaryPool<string, UniTaskCompletionSource<StreamableLoadingResult<Texture2D>?>>.Get();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            DictionaryPool<string, StreamableLoadingResult<Texture2D>>.Release(IrrecoverableFailures as Dictionary<string, StreamableLoadingResult<Texture2D>>);
            DictionaryPool<string, UniTaskCompletionSource<StreamableLoadingResult<Texture2D>?>>.Release(OngoingRequests as Dictionary<string, UniTaskCompletionSource<StreamableLoadingResult<Texture2D>?>>);

            disposed = true;
        }

        public void Add(in GetTextureIntention key, Texture2D asset)
        {
            if (cache.TryAdd(key, asset))
                unloadQueue.Enqueue(key, MultithreadingUtility.FrameCount);

            ProfilingCounters.TexturesInCache.Value = cache.Count;
        }

        public bool TryGet(in GetTextureIntention key, out Texture2D texture)
        {
            if (!cache.TryGetValue(key, out texture)) return false;

            unloadQueue.TryUpdatePriority(key, MultithreadingUtility.FrameCount);
            return true;
        }

        public void Dereference(in GetTextureIntention key, Texture2D asset) { }

        public void Unload(IConcurrentBudgetProvider frameTimeBudgetProvider, int maxUnloadAmount)
        {
            for (var i = 0; frameTimeBudgetProvider.TrySpendBudget()
                            && i < maxUnloadAmount && unloadQueue.Count > 0
                            && unloadQueue.TryDequeue(out GetTextureIntention key); i++)
            {
                UnityObjectUtils.SafeDestroy(cache[key]);
                ProfilingCounters.TexturesAmount.Value--;

                cache.Remove(key);
            }

            ProfilingCounters.TexturesInCache.Value = cache.Count;
        }

        public bool Equals(GetTextureIntention x, GetTextureIntention y) =>
            x.Equals(y);

        public int GetHashCode(GetTextureIntention obj) =>
            obj.GetHashCode();
    }
}
