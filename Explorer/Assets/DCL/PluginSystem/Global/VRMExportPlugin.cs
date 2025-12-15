using Arch.SystemGroups;
using DCL.AvatarRendering.Export;
using Utility;

namespace DCL.PluginSystem.Global
{
    public class VRMExportPlugin : IDCLGlobalPlugin
    {
        private readonly IEventBus eventBus;

        public VRMExportPlugin(IEventBus eventBus)
        {
            this.eventBus = eventBus;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            ExportAvatarSystem.InjectToWorld(ref builder, eventBus);
        }
    }
}
