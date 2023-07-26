using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using ECS.Abstract;
using System.Threading;
using UnityEngine;

namespace ECS.Profiling.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ProfilerWindowSystem : BaseUnityLoopSystem
    {
        private readonly IProfilingProvider profilingProvider;
        private readonly IProfilerView profilerView;
        private CancellationTokenSource cts;

        public ProfilerWindowSystem(World world, IProfilingProvider profilingProvider) : base(world)
        {
            this.profilingProvider = profilingProvider;
            profilerView = Object.Instantiate(Resources.Load<ProfilerView>("ProfilerView"));
            profilerView.OnOpen += StartTimer;
            profilerView.OnClose += StopTimer;
        }

        private void StartTimer()
        {
            cts = new CancellationTokenSource();
            profilerView.SetHiccups(profilingProvider.GetHiccupCountInBuffer());
            profilerView.SetFPS((float)profilingProvider.GetAverageFrameTimeValueInNS() * 1e-9f);
            UpdateView(cts.Token).Forget();
        }

        private void StopTimer()
        {
            cts.Cancel();
        }

        protected override void Update(float t) { }

        private async UniTaskVoid UpdateView(CancellationToken ct)
        {
            while (true)
            {
                await UniTask.Delay(500, cancellationToken:ct);
                profilerView.SetHiccups(profilingProvider.GetHiccupCountInBuffer());
                profilerView.SetFPS((float)profilingProvider.GetAverageFrameTimeValueInNS() * 1e-9f);
            }
        }

        public override void Dispose()
        {
            profilerView.OnOpen -= StartTimer;
            profilerView.OnClose -= StopTimer;
            cts.Cancel();
            cts.Dispose();
            base.Dispose();
        }
    }
}
