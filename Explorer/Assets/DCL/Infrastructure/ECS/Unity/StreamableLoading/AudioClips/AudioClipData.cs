using DCL.Diagnostics;
using DCL.Profiling;
using Unity.Profiling;
using UnityEngine;
using Utility;

namespace ECS.StreamableLoading.AudioClips
{
    public class AudioClipData : StreamableRefCountData<AudioClip>
    {
        protected override ref ProfilerCounterValue<int> totalCount => ref ProfilingCounters.AudioClipsAmount;

        protected override ref ProfilerCounterValue<int> referencedCount => ref ProfilingCounters.AudioClipsReferenced;

        protected override void DestroyObject()
        {
            UnityObjectUtils.SafeDestroy(Asset);
        }

        public AudioClipData(AudioClip audioClip) : base(audioClip, ReportCategory.AUDIO) { }
    }
}
