using DCL.SceneRunner.Scene;
using ECS.StreamableLoading.AssetBundles;
using System.Collections.Generic;

namespace ECS.StreamableLoading.InitialSceneState
{
    public struct InitialSceneStateInfo : IInitialSceneState
    {
        private AssetBundleData? assetBundleData;
        public HashSet<string> ISSAssets { get; private set; }
        public static InitialSceneStateInfo CreateISS(AssetBundleData assetBundleData, HashSet<string> ISSAssets) =>
            new ()
            {
                assetBundleData = assetBundleData,
                ISSAssets = ISSAssets
            };

        public static InitialSceneStateInfo CreateEmpty() =>
            new ()
            {
                ISSAssets = new HashSet<string>()
            };

        public void Dispose()
        {
            assetBundleData?.Dereference();
        }
    }
}
