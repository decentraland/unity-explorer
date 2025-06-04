using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.SDKComponents.Tween.Playground;
using ECS.Abstract;

namespace DCL.Time.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.UNSPECIFIED)]
    public partial class SynchronizeNtpTimeSystem : BaseUnityLoopSystem
    {
        private readonly INtpTimeService ntpTimeService;

        public SynchronizeNtpTimeSystem(World world, INtpTimeService ntpTimeService) : base(world)
        {
            this.ntpTimeService = ntpTimeService;
        }

        protected override void Update(float t)
        {
            // Update service (trigger and cooldown logic)
            // ntpTimeService.Poll()
        }
    }
}
