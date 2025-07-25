namespace DCL.Infrastructure.ECS.StreamableLoading.AssetBundles.AssetBundleManifestHelper
{
    public interface IApplyAssetBundleManifestResult
    {
        public void ApplyAssetBundleManifestResult(string assetBundleManifestVersion, bool hasSceneIDInPath);
        public void ApplyFailedManifestResult();
    }
}
