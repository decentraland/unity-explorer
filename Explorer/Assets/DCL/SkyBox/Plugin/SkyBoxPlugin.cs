using Arch.Core;
using Arch.SystemGroups;
using DCL.DebugUtilities;
using DCL.PluginSystem.Global;
using DCL.Quality;

namespace DCL.SkyBox
{
    public class SkyBoxPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly SkyBoxSceneData sceneData;
        private readonly IRendererFeaturesCache rendererFeaturesCache;

        public SkyBoxPlugin(IDebugContainerBuilder debugContainerBuilder, SkyBoxSceneData sceneData, IRendererFeaturesCache rendererFeaturesCache)
        {
            this.debugContainerBuilder = debugContainerBuilder;
            this.sceneData = sceneData;
            this.rendererFeaturesCache = rendererFeaturesCache;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            // Update the model from the system

            TimeOfDaySystem.InjectToWorld(ref builder, debugContainerBuilder, rendererFeaturesCache, sceneData.DirectionalLight);
        }
    }
}
