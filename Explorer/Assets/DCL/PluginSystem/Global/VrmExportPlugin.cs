using Arch.SystemGroups;
using DCL.AvatarRendering.Export;
using Utility;

namespace DCL.PluginSystem.Global
{
    public class VrmExportPlugin : IDCLGlobalPlugin
    {
        private readonly IEventBus eventBus;

        public VrmExportPlugin(IEventBus eventBus)
        {
            this.eventBus = eventBus;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            ExportAvatarSystem.InjectToWorld(ref builder, null, eventBus);
        }
    }
}
