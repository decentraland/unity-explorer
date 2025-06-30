using System;

namespace DCL.Profiling
{
    /// <summary>
    /// Doesn't require creation of predefined struct like ProfileMarker
    /// </summary>
    public struct ProfilerSampleScope : IDisposable
    {
        private ProfilerSampleScope(string label)
        {
            UnityEngine.Profiling.Profiler.BeginSample(label);
        }

        public static ProfilerSampleScope New(string label) =>
            new (label);

        public void Dispose()
        {
            UnityEngine.Profiling.Profiler.EndSample();
        }
    }
}
