using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Diagnostics.ReportsHandling;
using ECS.Abstract;

namespace ECS.Editor.Systems
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    [LogCategory(ReportCategory.EDITOR)]
    public partial class EditorMonitoringSystem : BaseUnityLoopSystem
    {
        private World world { get; }
        private IEditorSceneMonitor sceneMonitor { get; }

        public EditorMonitoringSystem(World world, string originScene, IEditorSceneMonitor monitor) : base(world)
        {
            this.world = world;
            this.sceneMonitor = monitor;
            this.sceneMonitor.Register(originScene, world);
        }

        protected override void Update(float t)
        {
            sceneMonitor.Tick();
        }
    }
}
