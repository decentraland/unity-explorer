using Unity.Profiling;

namespace ECS.Profiling
{
    public class ProfilingProvider  : IProfilingProvider
    {
        private ProfilerRecorder mainThreadTimeRecorder;

        public ProfilingProvider()
        {
            mainThreadTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
        }

        public long GetCurrentFrameTimeValue() =>
            mainThreadTimeRecorder.CurrentValue;

    }
}


