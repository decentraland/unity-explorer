using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System.Threading;
using AssetBundleManifestPromise = ECS.StreamableLoading.Common.AssetPromise<SceneRunner.Scene.SceneAssetBundleManifest, ECS.StreamableLoading.AssetBundles.GetAssetBundleManifestIntention>;

namespace ECS.StreamableLoading.AssetBundles
{
    public class AssetBundleManifestFallbackHelper
    {
        public static async UniTask CheckAssetBundleManifestFallback(World world, EntityDefinitionBase  entityDefinition, IPartitionComponent partition, CancellationToken ct)
        {
            //Fallback needed for when the asset-bundle-registry does not have the asset bundle manifest
            if (entityDefinition.versions == null)
            {
                var promise = AssetBundleManifestPromise.Create(world,
                    GetAssetBundleManifestIntention.Create(entityDefinition.id, new CommonLoadingArguments(entityDefinition.id)),
                    partition);

                StreamableLoadingResult<SceneAssetBundleManifest> assetBundleManifest = (await promise.ToUniTaskAsync(world, cancellationToken: ct)).Result.Value;

                if (assetBundleManifest.Succeeded)
                    entityDefinition.versions = new AssetBundleManifestVersion(assetBundleManifest.Asset.GetVersion());
                else
                    entityDefinition.assetBundleManifestRequestFailed = true;
            }
        }
    }
}
