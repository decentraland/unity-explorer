using SceneRunner.Scene;

namespace ECS.SceneLifeCycle.Components
{
    public class SceneLoadingComponent
    {
        public IpfsTypes.SceneEntityDefinition Definition;
        public SceneAssetBundleManifest AssetBundleManifest;
    }
}
