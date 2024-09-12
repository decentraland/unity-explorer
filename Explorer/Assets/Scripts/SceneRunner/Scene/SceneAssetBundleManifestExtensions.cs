namespace SceneRunner.Scene
{
    public static class SceneAssetBundleManifestExtensions
    {
        public static string FixCapitalization(this SceneAssetBundleManifest? sceneAssetBundleManifest, string hash)
        {
            if (sceneAssetBundleManifest == null) return hash;
            return sceneAssetBundleManifest.TryGet(hash, out string convertedFile) ? convertedFile : hash;
        }
    }
}
