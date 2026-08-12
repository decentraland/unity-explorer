using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Threading;
using AssetBundleManifestPromise = ECS.StreamableLoading.Common.AssetPromise<SceneRunner.Scene.SceneAssetBundleManifest, ECS.StreamableLoading.AssetBundles.GetAssetBundleManifestIntention>;

namespace ECS.StreamableLoading.AssetBundles
{
    public static class AssetBundleManifestFallbackHelper
    {
        /// <summary>
        ///     <paramref name="useManualManifest" /> stamps the manual manifest instead of fetching a real one —
        ///     used by local scene development when its bundles come as raw GLTFs rather than from an asset-bundle server.
        /// </summary>
        public static async UniTask CheckAssetBundleManifestFallbackAsync(World world, EntityDefinitionBase entityDefinition, IPartitionComponent partition, CancellationToken ct, bool useManualManifest = false, bool skipException = false, bool isLocalSceneDevelopment = false)
        {
            await CheckAssetBundleManifestFallbackInternalAsync(world, entityDefinition, partition, ct, useManualManifest, skipException, isLocalSceneDevelopment);

            entityDefinition.assetBundleManifestVersion.InjectContent(entityDefinition.id, entityDefinition.content);
        }

        public static async UniTask CheckAssetBundleManifestFallbackAsync(World world, TrimmedEntityDefinitionBase entityDefinition, IPartitionComponent partition, CancellationToken ct, bool useManualManifest = false)
        {
            await CheckAssetBundleManifestFallbackInternalAsync(world, entityDefinition, partition, ct, useManualManifest);
        }

        private static async UniTask CheckAssetBundleManifestFallbackInternalAsync(World world, TrimmedEntityDefinitionBase entityDefinition, IPartitionComponent partition, CancellationToken ct, bool useManualManifest = false, bool skipException = false, bool isLocalSceneDevelopment = false)
        {
            if (useManualManifest)
            {
                entityDefinition.assetBundleManifestVersion = AssetBundleManifestVersion.CreateManualManifest();
                return;
            }

            //Fallback needed for when the asset-bundle-registry does not have the asset bundle manifest
            //Also used for the PX escape
            if (entityDefinition.assetBundleManifestVersion == null || entityDefinition.assetBundleManifestVersion.IsEmpty())
            {
                //Needed to use the UnityEngine.Time.realtimeSinceStartup on the intention creation
                await UniTask.SwitchToMainThread();

                if (!skipException)
                {
                    Sentry.Unity.SentrySdk.AddBreadcrumb($"AB manifest version missing for entity: {entityDefinition.id}");
                    ReportHub.LogException(new Exception("AssetBundleManifestFallbackHelper: AB Manifest Fallback requested"), ReportCategory.ASSET_BUNDLES);
                }

                var promise = AssetBundleManifestPromise.Create(world,
                    GetAssetBundleManifestIntention.Create(entityDefinition.id, new CommonLoadingArguments(entityDefinition.id)),
                    partition);

                StreamableLoadingResult<SceneAssetBundleManifest> assetBundleManifest = (await promise.ToUniTaskAsync(world, cancellationToken: ct)).Result.Value;

                if (assetBundleManifest.Succeeded)
                {
                    entityDefinition.assetBundleManifestVersion = AssetBundleManifestVersion.CreateFromFallback(assetBundleManifest.Asset.GetVersion(), assetBundleManifest.Asset.GetBuildDate());

                    // Marks the manifest so deps-digest handling stays off for LSD (fresh instance only —
                    // the FAILED sentinel below is shared and must never be mutated).
                    entityDefinition.assetBundleManifestVersion.IsLSDAsset = isLocalSceneDevelopment;
                }
                else
                {
                    assetBundleManifest.TryLogException();
                    entityDefinition.assetBundleManifestVersion = AssetBundleManifestVersion.FAILED;
                }
            }
        }
    }
}
