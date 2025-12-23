using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using Sentry;
using System;
using System.Threading;
using UnityEngine;
using AssetBundleManifestPromise = ECS.StreamableLoading.Common.AssetPromise<SceneRunner.Scene.SceneAssetBundleManifest, ECS.StreamableLoading.AssetBundles.GetAssetBundleManifestIntention>;

namespace ECS.StreamableLoading.AssetBundles
{
    public class AssetBundleManifestFallbackHelper
    {
        public static async UniTask CheckAssetBundleManifestFallbackAsync(World world, EntityDefinitionBase entityDefinition, IPartitionComponent partition, CancellationToken ct, bool isLSD = false)
        {
            await CheckAssetBundleManifestFallbackInternalAsync(world, entityDefinition, partition, ct, isLSD);

            entityDefinition.assetBundleManifestVersion.InjectContent(entityDefinition.id, entityDefinition.content);
        }

        private static async UniTask CheckAssetBundleManifestFallbackInternalAsync(World world, TrimmedEntityDefinitionBase entityDefinition, IPartitionComponent partition, CancellationToken ct, bool isLSD = false)
        {
            if (isLSD)
            {
                entityDefinition.assetBundleManifestVersion = AssetBundleManifestVersion.CreateManualManifest();
                return;
            }

            //Fallback needed for when the asset-bundle-registry does not have the asset bundle manifest
            //Also used for the PX escape
            if (entityDefinition.assetBundleManifestVersion == null || entityDefinition.assetBundleManifestVersion.IsEmpty())
            {
                //Needed to use the Time.realtimeSinceStartup on the intention creation
                await UniTask.SwitchToMainThread();

                SentrySdk.AddBreadcrumb($"AB manifest version missing for entity: {entityDefinition.id}");
                ReportHub.LogException(new Exception("AssetBundleManifestFallbackHelper: AB Manifest Fallback requested"), ReportCategory.ASSET_BUNDLES);

                var promise = AssetBundleManifestPromise.Create(world,
                    GetAssetBundleManifestIntention.Create(entityDefinition.id, new CommonLoadingArguments(entityDefinition.id)),
                    partition);

                StreamableLoadingResult<SceneAssetBundleManifest> assetBundleManifest = (await promise.ToUniTaskAsync(world, cancellationToken: ct)).Result.Value;

                if (assetBundleManifest.Succeeded)
                    entityDefinition.assetBundleManifestVersion = AssetBundleManifestVersion.CreateFromFallback(assetBundleManifest.Asset.GetVersion(), assetBundleManifest.Asset.GetBuildDate());
                else
                    entityDefinition.assetBundleManifestVersion = AssetBundleManifestVersion.CreateFailed();
            }
        }

        public static async UniTask CheckAssetBundleManifestFallbackAsync(World world, TrimmedEntityDefinitionBase entityDefinition, IPartitionComponent partition, CancellationToken ct, bool isLSD = false)
        {
            await CheckAssetBundleManifestFallbackInternalAsync(world, entityDefinition, partition, ct, isLSD);
        }
    }
}
