using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;

namespace DCL.WebRequests.Analytics
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class ShowWebRequestsAnalyticsSystem : BaseUnityLoopSystem
    {
        private readonly IWebRequestsAnalyticsContainer container;

        internal ShowWebRequestsAnalyticsSystem(World world, IWebRequestsAnalyticsContainer container) : base(world)
        {
            this.container = container;
        }

        protected override void Update(float t)
        {
            container.Update(t);
        }
    }
}
