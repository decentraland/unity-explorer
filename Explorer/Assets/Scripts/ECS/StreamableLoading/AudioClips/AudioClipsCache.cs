using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace ECS.StreamableLoading.AudioClips
{
    public class AudioClipsCache : IStreamableCache<AudioClip, GetAudioClipIntention>
    {
        private static readonly Comparison<(GetAudioClipIntention intention, AudioClipData clipData)> COMPARE_BY_LAST_USED_FRAME_REVERSED =
            (pair1, pair2) => pair2.clipData.LastUsedFrame.CompareTo(pair1.clipData.LastUsedFrame);

        internal readonly Dictionary<GetAudioClipIntention, AudioClipData> cache = new ();
        private readonly List<(GetAudioClipIntention intention, AudioClipData clipData)> listedCache = new ();

        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<AudioClip>?>> OngoingRequests { get; } = new Dictionary<string, UniTaskCompletionSource<StreamableLoadingResult<AudioClip>?>>();
        public IDictionary<string, StreamableLoadingResult<AudioClip>> IrrecoverableFailures { get; } = new Dictionary<string, StreamableLoadingResult<AudioClip>>();

        public void Dispose()
        {
            foreach (AudioClipData clip in cache.Values)
                UnityObjectUtils.SafeDestroy(clip.AudioClip);

            cache.Clear();
        }

        public void Add(in GetAudioClipIntention key, AudioClip asset)
        {
            if (cache.ContainsKey(key)) return;

            cache.Add(key, new AudioClipData(asset)); // reference will be added later
            ProfilingCounters.AudioClipsInCache.Value = cache.Count;
        }

        public bool TryGet(in GetAudioClipIntention key, out AudioClip asset)
        {
            if (cache.TryGetValue(key, out AudioClipData? value))
            {
                asset = value.AudioClip;
                return true;
            }

            asset = null;
            return false;
        }

        public void Unload(IPerformanceBudget frameTimeBudgetProvider, int maxUnloadAmount)
        {
            listedCache.Sort(COMPARE_BY_LAST_USED_FRAME_REVERSED);

            for (int i = listedCache.Count - 1; frameTimeBudgetProvider.TrySpendBudget() && i >= 0 && maxUnloadAmount > 0; i--)
            {
                (GetAudioClipIntention key, AudioClipData clipData) = listedCache[i];
                if (!clipData.CanBeDisposed()) continue;

                clipData.Dispose();
                cache.Remove(key);
                listedCache.RemoveAt(i);

                maxUnloadAmount--;
            }

            ProfilingCounters.AudioClipsInCache.Value = cache.Count;
        }

        public void AddReference(in GetAudioClipIntention key, AudioClip audioClip)
        {
            if (cache.TryGetValue(key, out AudioClipData audioClipData))
                audioClipData.AddReference();
        }

        public void Dereference(in GetAudioClipIntention key, AudioClip _)
        {
            if (cache.TryGetValue(key, out AudioClipData? audioClipData))
                audioClipData.RemoveReference();
        }

        public bool Equals(GetAudioClipIntention x, GetAudioClipIntention y) =>
            x.Equals(y);

        public int GetHashCode(GetAudioClipIntention obj) =>
            obj.GetHashCode();
    }
}
