using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using UnityEngine;

namespace ECS.Profiling.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ProfilingSystem : BaseUnityLoopSystem
    {
        private readonly IProfilingProvider profilingProvider;
        private readonly IProfilerView profilerView;

        private float lastTimeSinceMetricsUpdate;

        public ProfilingSystem(World world, IProfilingProvider profilingProvider) : base(world)
        {
            this.profilingProvider = profilingProvider;
            profilerView = Object.Instantiate(Resources.Load<ProfilingView>("ProfilerView"));
        }

        protected override void Update(float t)
        {
            if (profilerView.IsOpen && lastTimeSinceMetricsUpdate > 0.5f)
            {
                lastTimeSinceMetricsUpdate = 0;
                profilerView.SetHiccups(profilingProvider.GetHiccupCountInBuffer());
                profilerView.SetFPS((float)profilingProvider.GetAverageFrameTimeValueInNS() * 1e-9f);
            }

            lastTimeSinceMetricsUpdate += t;
            profilingProvider.CheckHiccup();
        }
    }
}
