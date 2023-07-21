using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace ECS.Profiling
{
    public class ProfilingProvider  : IProfilingProvider
    {
        private ProfilerRecorder mainThreadTimeRecorder;
        private ProfilerRecorder fpsTimeRecorder;

        private Dictionary<HiccupKey, int> hiccupCounter;

        public ProfilingProvider()
        {
            mainThreadTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            hiccupCounter = new Dictionary<HiccupKey, int>
            {
                { HiccupKey.FiftyMS, 0 },
                { HiccupKey.FourtyMS, 0 },
                { HiccupKey.ThirtyMS, 0 },
            };

            CountHiccup().Forget();
        }

        public long GetCurrentFrameTimeValue() =>
            mainThreadTimeRecorder.CurrentValue;

        public float GetFrameRate() =>
            1 / ((float)GetRecorderFrameAverage(mainThreadTimeRecorder) * 1e-9f);

        public int GetHiccupValue(HiccupKey hiccupKey)
        {
            return hiccupCounter[hiccupKey];
        }

        private async UniTaskVoid CountHiccup()
        {
            while (true)
            {
                await UniTask.Yield();

                if (mainThreadTimeRecorder.LastValue * 1e-6f > 50)
                {
                    hiccupCounter[HiccupKey.FiftyMS]++;
                    hiccupCounter[HiccupKey.FourtyMS]++;
                    hiccupCounter[HiccupKey.ThirtyMS]++;
                }
                else if (mainThreadTimeRecorder.LastValue * 1e-6f > 40)
                {
                    hiccupCounter[HiccupKey.FourtyMS]++;
                    hiccupCounter[HiccupKey.ThirtyMS]++;
                }
                else if(mainThreadTimeRecorder.LastValue * 1e-6f > 30)
                {
                    hiccupCounter[HiccupKey.ThirtyMS]++;
                }
            }
        }

        static double GetRecorderFrameAverage(ProfilerRecorder recorder)
        {
            var samplesCount = recorder.Capacity;
            if (samplesCount == 0)
                return 0;

            double r = 0;
            unsafe
            {
                var samples = stackalloc ProfilerRecorderSample[samplesCount];
                recorder.CopyTo(samples, samplesCount);
                for (var i = 0; i < samplesCount; ++i)
                    r += samples[i].Value;
                r /= samplesCount;
            }

            return r;
        }

    }
}


