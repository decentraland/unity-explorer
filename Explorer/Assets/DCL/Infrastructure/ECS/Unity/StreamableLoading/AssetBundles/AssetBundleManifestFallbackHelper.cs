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
        public static async UniTask CheckAssetBundleManifestFallbackAsync(World world, EntityDefinitionBase entityDefinition, IPartitionComponent partition, CancellationToken ct, bool useManualManifest = false, bool skipException = false)
        {
            await CheckAssetBundleManifestFallbackInternalAsync(world, entityDefinition, partition, ct, useManualManifest, skipException);

            entityDefinition.assetBundleManifestVersion.InjectContent(entityDefinition.id, entityDefinition.content);
        }

        public static async UniTask CheckAssetBundleManifestFallbackAsync(World world, TrimmedEntityDefinitionBase entityDefinition, IPartitionComponent partition, CancellationToken ct, bool useManualManifest = false)
        {
            await CheckAssetBundleManifestFallbackInternalAsync(world, entityDefinition, partition, ct, useManualManifest);
        }

        private static async UniTask CheckAssetBundleManifestFallbackInternalAsync(World world, TrimmedEntityDefinitionBase entityDefinition, IPartitionComponent partition, CancellationToken ct, bool useManualManifest = false, bool skipException = false)
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
                    entityDefinition.assetBundleManifestVersion = AssetBundleManifestVersion.CreateFromFallback(assetBundleManifest.Asset.GetVersion(), assetBundleManifest.Asset.GetBuildDate());
                else
                {
                    assetBundleManifest.TryLogException();

                    // The report category is silenced in shipped players, so under the golden
                    // harness the real failure also lands in a file the driver can read.
                    if (Environment.GetEnvironmentVariable("DCL_PLAZABENCH_DETERM_CLOCK") == "1")
                        try
                        {
                            System.IO.File.AppendAllText(
                                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "golden-abfail.txt"),
                                $"{entityDefinition.id}: {assetBundleManifest.Exception}\n---\n");
                        }
                        catch (Exception) { /* diagnostics only */ }

                    entityDefinition.assetBundleManifestVersion = AssetBundleManifestVersion.FAILED;
                }
            }
        }
    }
}
