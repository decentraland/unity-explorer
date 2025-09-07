using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System.Threading;
using UnityEngine;
using AssetBundleManifestPromise = ECS.StreamableLoading.Common.AssetPromise<SceneRunner.Scene.SceneAssetBundleManifest, ECS.StreamableLoading.AssetBundles.GetAssetBundleManifestIntention>;

namespace ECS.StreamableLoading.AssetBundles
{
    public class AssetBundleManifestFallbackHelper
    {
        public static async UniTask CheckAssetBundleManifestFallbackAsync(World world, EntityDefinitionBase  entityDefinition, IPartitionComponent partition, CancellationToken ct)
        {
            //Fallback needed for when the asset-bundle-registry does not have the asset bundle manifest
            if (entityDefinition.assetBundleManifestVersion == null || entityDefinition.assetBundleManifestVersion.IsEmpty())
            {
                //Needed to use the Time.realtimeSinceStartup on the intention creation
                await UniTask.SwitchToMainThread();

                var promise = AssetBundleManifestPromise.Create(world,
                    GetAssetBundleManifestIntention.Create(entityDefinition.id, new CommonLoadingArguments(entityDefinition.id)),
                    partition);

                StreamableLoadingResult<SceneAssetBundleManifest> assetBundleManifest = (await promise.ToUniTaskAsync(world, cancellationToken: ct)).Result.Value;

                if (assetBundleManifest.Succeeded)
                    entityDefinition.assetBundleManifestVersion = new AssetBundleManifestVersion(assetBundleManifest.Asset.GetVersion(), assetBundleManifest.Asset.GetBuildDate());
                else
                    entityDefinition.assetBundleManifestVersion = AssetBundleManifestVersion.CreateFailed();
            }
        }
    }
}
