using Arch.SystemGroups;
using DCL.MapRenderer;

namespace DCL.PluginSystem.Global
{
    public class MapRendererPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IMapRenderer mapRenderer;

        public MapRendererPlugin(IMapRenderer mapRenderer)
        {
            this.mapRenderer = mapRenderer;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            mapRenderer.CreateSystems(ref builder);
        }
    }
}
