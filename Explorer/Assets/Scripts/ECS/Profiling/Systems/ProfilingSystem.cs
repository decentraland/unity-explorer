using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using ECS.Abstract;
using System;
using UnityEngine;

namespace ECS.Profiling.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ProfilingSystem : BaseUnityLoopSystem
    {

        private readonly IProfilingProvider profilingProvider;
        private readonly ProfilerView profilerView;

        public ProfilingSystem(World world, IProfilingProvider profilingProvider) : base(world)
        {
            this.profilingProvider = profilingProvider;
            this.profilerView = GameObject.Instantiate(Resources.Load<ProfilerView>("ProfilerView"));
            UpdateView().Forget();
        }


        protected override void Update(float t)
        {
        }

        private async UniTaskVoid UpdateView()
        {
            while (true)
            {
                await UniTask.Delay(500);
                profilerView.averageFrameRate.text = $"Frame Rate: {profilingProvider.GetFrameRate():F1} FPS";
                profilerView.hiccupCounter.text = $"30ms: {profilingProvider.GetHiccupValue(HiccupKey.ThirtyMS)}\n"
                                                  + $"40ms: {profilingProvider.GetHiccupValue(HiccupKey.FourtyMS)}\n"
                                                  + $"50ms: {profilingProvider.GetHiccupValue(HiccupKey.FiftyMS)}";
            }
        }

    }
}
