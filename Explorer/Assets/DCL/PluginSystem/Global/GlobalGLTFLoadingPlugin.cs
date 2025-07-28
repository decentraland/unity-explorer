using Arch.SystemGroups;
using DCL.WebRequests;
using ECS;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.GLTF;
using ECS.StreamableLoading.GLTF.DownloadProvider;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    /// Plugin responsible for running the LoadGLTFSystem for:
    /// * Builder API collections (AKA unreleased wearables/emotes) preview
    /// * Scene Emotes during Local Scene Development (raw GLTF animations)
    /// </summary>
    public class GlobalGLTFLoadingPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IWebRequestController webRequestController;
        private readonly IRealmData realmData;
        private readonly string builderContentURL;
        private readonly bool localSceneDevelopment;

        public GlobalGLTFLoadingPlugin(
            IWebRequestController webRequestController,
            IRealmData realmData,
            string builderContentURL,
            bool localSceneDevelopment)
        {
            this.webRequestController = webRequestController;
            this.realmData = realmData;
            this.builderContentURL = builderContentURL;
            this.localSceneDevelopment = localSceneDevelopment;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            IGltFastDownloadStrategy downloadStrategy = localSceneDevelopment ?
                new GltFastRealmDataDownloadStrategy(realmData)
                : new GltFastGlobalDownloadStrategy(builderContentURL);

            LoadGLTFSystem.InjectToWorld(
                ref builder,
                NoCache<GLTFData, GetGLTFIntention>.INSTANCE,
                webRequestController,
                true,
                true,
                false,
                downloadStrategy);
        }
    }
}
