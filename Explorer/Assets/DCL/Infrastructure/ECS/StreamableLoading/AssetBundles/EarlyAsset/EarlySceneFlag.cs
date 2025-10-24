using DCL.Ipfs;
using System;

namespace DefaultNamespace
{
    public struct EarlySceneFlag
    {
    }

    public struct EarlyAssetBundleFlag
    {
        public EntityDefinitionBase Scene;

        public static EarlyAssetBundleFlag CreateAssetBundleRequest(EntityDefinitionBase scene) =>
            new ()
            {
                Scene = scene,
            };
    }
}
