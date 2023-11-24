using Cysharp.Threading.Tasks;
using DCL.PerformanceAndDiagnostics.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace ECS.StreamableLoading.Textures
{
    public class TexturesCache : IStreamableCache<Texture2D, GetTextureIntention>
    {
        private readonly Dictionary<GetTextureIntention, (uint LastUsedFrame, Texture2D Texture)> cache;

        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<Texture2D>?>> OngoingRequests { get; }

        public IDictionary<string, StreamableLoadingResult<Texture2D>> IrrecoverableFailures { get; }

        private bool disposed { get; set; }

        public TexturesCache()
        {
            cache = new Dictionary<GetTextureIntention, (uint LastUsedFrame, Texture2D Texture)>(this);

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
            cache.TryAdd(key, ((uint)Time.frameCount, asset));

            ProfilingCounters.TexturesInCache.Value = cache.Count;
        }

        public bool TryGet(in GetTextureIntention key, out Texture2D asset)
        {
            if (cache.TryGetValue(key, out (uint LastUsedFrame, Texture2D Texture) value))
            {
                asset = value.Texture;
                cache[key] = ((uint)Time.frameCount, asset);

                return true;
            }

            asset = null;
            return false;
        }

        public void Dereference(in GetTextureIntention key, Texture2D asset) { }

        public void Unload(IConcurrentBudgetProvider frameTimeBudgetProvider)
        {
            using (ListPool<KeyValuePair<GetTextureIntention, (uint LastUsedFrame, Texture2D texture)>>.Get(out List<KeyValuePair<GetTextureIntention, (uint LastUsedFrame, Texture2D Texture)>> sortedCache))
            {
                PrepareListSortedByLastUsage(sortedCache);

                foreach (KeyValuePair<GetTextureIntention, (uint LastUsedFrame, Texture2D Texture)> pair in sortedCache)
                {
                    if (!frameTimeBudgetProvider.TrySpendBudget()) break;

                    UnityObjectUtils.SafeDestroy(pair.Value.Texture);
                    ProfilingCounters.TexturesAmount.Value--;

                    cache.Remove(pair.Key);
                }
            }

            ProfilingCounters.TexturesInCache.Value = cache.Count;
            return;

            void PrepareListSortedByLastUsage(List<KeyValuePair<GetTextureIntention, (uint LastUsedFrame, Texture2D Texture)>> sortedCache)
            {
                foreach (KeyValuePair<GetTextureIntention, (uint LastUsedFrame, Texture2D Texture)> item in cache)
                    sortedCache.Add(item);

                sortedCache.Sort(CompareByLastUsedFrame);
            }
        }

        private static int CompareByLastUsedFrame(KeyValuePair<GetTextureIntention, (uint LastUsedFrame, Texture2D texture)> pair1, KeyValuePair<GetTextureIntention, (uint LastUsedFrame, Texture2D texture)> pair2) =>
            pair1.Value.LastUsedFrame.CompareTo(pair2.Value.LastUsedFrame);

        public bool Equals(GetTextureIntention x, GetTextureIntention y) =>
            x.Equals(y);

        public int GetHashCode(GetTextureIntention obj) =>
            obj.GetHashCode();
    }
}
