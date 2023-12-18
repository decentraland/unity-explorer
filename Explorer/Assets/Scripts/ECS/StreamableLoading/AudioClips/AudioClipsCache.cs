using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using Utility;

namespace ECS.StreamableLoading.AudioClips
{
    public class AudioClipsCache : IStreamableCache<AudioClip, GetAudioClipIntention>
    {
        internal readonly Dictionary<GetAudioClipIntention, (AudioClip clip, int referenceCount)> cache;

        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<AudioClip>?>> OngoingRequests { get; }
        public IDictionary<string, StreamableLoadingResult<AudioClip>> IrrecoverableFailures { get; }

        public AudioClipsCache()
        {
            cache = new Dictionary<GetAudioClipIntention, (AudioClip clip, int referenceCount)>(this);
            IrrecoverableFailures = new Dictionary<string, StreamableLoadingResult<AudioClip>>();
            OngoingRequests = new Dictionary<string, UniTaskCompletionSource<StreamableLoadingResult<AudioClip>?>>();
        }

        public void Dispose()
        {
            foreach ((AudioClip clip, int referenceCount) clip in cache.Values)
                UnityObjectUtils.SafeDestroy(clip.clip);

            cache.Clear();
        }

        public void Add(in GetAudioClipIntention key, AudioClip asset)
        {
            if (!cache.ContainsKey(key))
            {
                cache[key] = (asset, 1);
                ProfilingCounters.ReferencedAudioClips.Value++;
            }
            else
            {
                if (cache[key].referenceCount == 0)
                    ProfilingCounters.ReferencedAudioClips.Value++;

                cache[key] = (cache[key].clip, cache[key].referenceCount + 1);
            }

            ProfilingCounters.AudioClipsInCache.Value = cache.Count;
        }

        public bool TryGet(in GetAudioClipIntention key, out AudioClip asset)
        {
            asset = null;
            if (!cache.TryGetValue(key, out (AudioClip clip, int referenceCount) value)) return false;

            asset = value.clip;

            if (cache[key].referenceCount == 0)
                ProfilingCounters.ReferencedAudioClips.Value++;
            cache[key] = (value.clip, value.referenceCount + 1);

            return true;
        }

        public void Unload(IConcurrentBudgetProvider frameTimeBudgetProvider, int maxUnloadAmount)
        {
            // TODO: Implement cacheUnload
            ProfilingCounters.AudioClipsInCache.Value = cache.Count;
        }

        public void Dereference(in GetAudioClipIntention key, AudioClip _)
        {
            if (cache.TryGetValue(key, out (AudioClip clip, int referenceCount) value))
            {
                int newReferenceCount = value.referenceCount - 1;
                Assert.IsFalse(value.referenceCount < 0, "Reference count of AudioClip should never be negative!");

                cache[key] = (value.clip, newReferenceCount);

                if (cache[key].referenceCount == 0)
                    ProfilingCounters.ReferencedAudioClips.Value--;
            }
        }

        public bool Equals(GetAudioClipIntention x, GetAudioClipIntention y) =>
            x.Equals(y);

        public int GetHashCode(GetAudioClipIntention obj) =>
            obj.GetHashCode();
    }
}
