using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;

namespace ECS.Profiling.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ProfilingSystem : BaseUnityLoopSystem
    {
        private readonly IProfilingProvider profilingProvider;
        private readonly IProfilerView profilerView;

        private float lastTimeSinceMetricsUpdate;

        internal ProfilingSystem(World world, IProfilingProvider profilingProvider, ProfilingView profilingView) : base(world)
        {
            this.profilingProvider = profilingProvider;
            this.profilerView = profilingView;
        }

        protected override void Update(float t)
        {
            if (profilerView.IsOpen && lastTimeSinceMetricsUpdate > 0.5f)
            {
                lastTimeSinceMetricsUpdate = 0;

                profilerView.SetMemory(profilingProvider.TotalUsedMemoryInMB);
                profilerView.SetHiccups(profilingProvider.HiccupCountInBuffer);
                profilerView.SetFPS((float)profilingProvider.AverageFrameTimeValueInNS * 1e-9f);
            }

            lastTimeSinceMetricsUpdate += t;
            profilingProvider.CheckHiccup();
        }
    }
}
