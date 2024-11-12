using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using Unity.Profiling;
using UnityEngine;

namespace ECS.StreamableLoading.AudioClips
{
    public class AudioClipsCache : RefCountStreamableCacheBase<AudioClipData, AudioClip, GetAudioClipIntention>
    {
        protected override ref ProfilerCounterValue<int> inCacheCount => ref ProfilingCounters.AudioClipsInCache;

        public override bool Equals(GetAudioClipIntention x, GetAudioClipIntention y) =>
            x.Equals(y);

        public override int GetHashCode(GetAudioClipIntention obj) =>
            obj.GetHashCode();
    }
}
