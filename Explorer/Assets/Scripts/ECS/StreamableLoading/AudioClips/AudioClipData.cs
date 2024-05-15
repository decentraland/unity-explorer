using DCL.Diagnostics;
using DCL.Profiling;
using System;
using UnityEngine;
using Utility;
using Utility.Multithreading;

namespace ECS.StreamableLoading.AudioClips
{
    public class AudioClipData : IDisposable
    {
        private int referencesCount;

        public AudioClip AudioClip { get; }
        public long LastUsedFrame { get; private set; }

        public AudioClipData(AudioClip audioClip, int referencesCount = 1)
        {
            AudioClip = audioClip;
            this.referencesCount = referencesCount;
            LastUsedFrame = MultithreadingUtility.FrameCount;

            ProfilingCounters.AudioClipsReferenced.Value++;
        }

        public void Dispose()
        {
            UnityObjectUtils.SafeDestroy(AudioClip);
            ProfilingCounters.AudioClipsAmount.Value--;
        }

        public void AddReference()
        {
            if (referencesCount == 0)
                ProfilingCounters.AudioClipsReferenced.Value++;

            referencesCount++;

            LastUsedFrame = MultithreadingUtility.FrameCount;
        }

        public void RemoveReference()
        {
            referencesCount--;

            if (referencesCount < 0)
            {
                ReportHub.LogException(new Exception("Reference count of AudioClip should never be negative!"), ReportCategory.SDK_AUDIO_SOURCES);
                //Assert.IsFalse(referencesCount < 0, "Reference count of AudioClip should never be negative!"); Leaving it commented for now :)
            }

            LastUsedFrame = MultithreadingUtility.FrameCount;

            if (referencesCount == 0)
                ProfilingCounters.AudioClipsReferenced.Value--;

        }

        public bool CanBeDisposed() =>
            referencesCount <= 0;
    }
}
