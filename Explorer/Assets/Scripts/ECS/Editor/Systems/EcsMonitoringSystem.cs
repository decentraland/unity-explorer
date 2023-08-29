using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Diagnostics.ReportsHandling;
using ECS.Abstract;

namespace ECS.Editor.Systems
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    [LogCategory(ReportCategory.EDITOR)]
    public partial class EcsMonitoringSystem : BaseUnityLoopSystem
    {
        private World world { get; }
        private IEcsMonitor sceneMonitor { get; }

        private string originScene { get; }

        public EcsMonitoringSystem(World world, string originScene, IEcsMonitor monitor) : base(world)
        {
            this.world = world;
            this.originScene = originScene;
            this.sceneMonitor = monitor;
            this.sceneMonitor.Register(originScene, world);
        }

        protected override void Update(float t)
        {
            sceneMonitor.Tick();
        }

        public override void Dispose()
        {
            base.Dispose();
            sceneMonitor.Unregister(originScene);
        }
    }
}
