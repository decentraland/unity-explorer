using System;

namespace DefaultNamespace
{
    public struct EarlySceneFlag
    {
        public string AsssetBundleHash;
    }

    public struct EarlyAssetBundleFlag
    {
        public string AsssetBundleHash;

        public static EarlyAssetBundleFlag CreateAssetBundleRequest(string assetBundleHash) =>
            new ()
            {
                AsssetBundleHash = assetBundleHash,
            };
    }
}
