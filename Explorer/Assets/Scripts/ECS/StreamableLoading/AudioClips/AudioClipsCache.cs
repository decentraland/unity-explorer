using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using Utility.Multithreading;
using Utility.PriorityQueue;

namespace ECS.StreamableLoading.AudioClips
{
    public class AudioClipsCache : IStreamableCache<AudioClip, GetAudioClipIntention>
    {
        internal readonly Dictionary<GetAudioClipIntention, AudioClip> cache;
        private readonly SimplePriorityQueue<GetAudioClipIntention, long> unloadQueue = new ();

        public IDictionary<string, UniTaskCompletionSource<StreamableLoadingResult<AudioClip>?>> OngoingRequests { get; }
        public IDictionary<string, StreamableLoadingResult<AudioClip>> IrrecoverableFailures { get; }

        public AudioClipsCache()
        {
            cache = new Dictionary<GetAudioClipIntention, AudioClip>(this);
            IrrecoverableFailures = new Dictionary<string, StreamableLoadingResult<AudioClip>>();
            OngoingRequests = new Dictionary<string, UniTaskCompletionSource<StreamableLoadingResult<AudioClip>?>>();
        }

        public void Dispose()
        {
            foreach (var clip in cache.Values)
                UnityObjectUtils.SafeDestroy(clip);

            cache.Clear();
            unloadQueue.Clear();
        }

        public void Add(in GetAudioClipIntention key, AudioClip asset)
        {
            if (cache.TryAdd(key, asset))
                unloadQueue.Enqueue(key, MultithreadingUtility.FrameCount);

            ProfilingCounters.AudioClipsInCache.Value = cache.Count;
        }

        public bool TryGet(in GetAudioClipIntention key, out AudioClip asset)
        {
            if (!cache.TryGetValue(key, out asset)) return false;

            unloadQueue.TryUpdatePriority(key, MultithreadingUtility.FrameCount);
            return true;
        }

        public void Unload(IConcurrentBudgetProvider frameTimeBudgetProvider, int maxUnloadAmount)
        {
            for (var i = 0; frameTimeBudgetProvider.TrySpendBudget()
                            && i < maxUnloadAmount && unloadQueue.Count > 0
                            && unloadQueue.TryDequeue(out GetAudioClipIntention key); i++)
            {
                if (!cache[key].UnloadAudioData()) continue; // immediate unloading of raw audio data; synchronously frees up the memory from larger part of the AudioClip's memory footprint.

                UnityObjectUtils.SafeDestroy(cache[key]); // Destroy the AudioClip object itself (metadata, ect.)
                cache.Remove(key);

                ProfilingCounters.AudioClipsAmount.Value--;
            }

            ProfilingCounters.AudioClipsInCache.Value = cache.Count;
        }

        public void Dereference(in GetAudioClipIntention key, AudioClip asset) { }

        public bool Equals(GetAudioClipIntention x, GetAudioClipIntention y) =>
            x.Equals(y);

        public int GetHashCode(GetAudioClipIntention obj) =>
            obj.GetHashCode();
    }
}
