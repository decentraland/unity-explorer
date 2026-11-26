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
        public static async UniTask CheckAssetBundleManifestFallbackAsync(World world, EntityDefinitionBase entityDefinition, IPartitionComponent partition, CancellationToken ct, bool useManualManifest = false, bool skipException = false, bool retryCachedFailure = false)
        {
            await CheckAssetBundleManifestFallbackInternalAsync(world, entityDefinition, partition, ct, useManualManifest, skipException, retryCachedFailure);

            if (entityDefinition.id is { } entityId)
                entityDefinition.AssetBundleManifestVersionOrFailed.InjectContent(entityId, entityDefinition.content);
        }

        public static async UniTask CheckAssetBundleManifestFallbackAsync(World world, TrimmedEntityDefinitionBase entityDefinition, IPartitionComponent partition, CancellationToken ct, bool useManualManifest = false)
        {
            await CheckAssetBundleManifestFallbackInternalAsync(world, entityDefinition, partition, ct, useManualManifest);
        }

        private static async UniTask CheckAssetBundleManifestFallbackInternalAsync(World world, TrimmedEntityDefinitionBase entityDefinition, IPartitionComponent partition, CancellationToken ct, bool useManualManifest = false, bool skipException = false, bool retryCachedFailure = false)
        {
            if (useManualManifest)
            {
                entityDefinition.assetBundleManifestVersion = AssetBundleManifestVersion.CreateManualManifest();
                return;
            }

            //Fallback needed for when the asset-bundle-registry does not have the asset bundle manifest
            //Also used for the PX escape
            if (entityDefinition.assetBundleManifestVersion == null || entityDefinition.assetBundleManifestVersion.IsEmpty()
                || (retryCachedFailure && entityDefinition.assetBundleManifestVersion.assetBundleManifestRequestFailed))
            {
                //Needed to use the UnityEngine.Time.realtimeSinceStartup on the intention creation
                await UniTask.SwitchToMainThread();

                if (!skipException)
                {
                    Sentry.Unity.SentrySdk.AddBreadcrumb($"AB manifest version missing for entity: {entityDefinition.id}");
                    ReportHub.LogException(new Exception("AssetBundleManifestFallbackHelper: AB Manifest Fallback requested"), ReportCategory.ASSET_BUNDLES);
                }

                if (entityDefinition.id is not { Length: > 0 } entityId)
                {
                    entityDefinition.assetBundleManifestVersion = AssetBundleManifestVersion.FAILED;
                    return;
                }

                var promise = AssetBundleManifestPromise.Create(world,
                    GetAssetBundleManifestIntention.Create(entityId, new CommonLoadingArguments(entityId) { RetryCachedFailures = retryCachedFailure }),
                    partition);

                try { promise = await promise.ToUniTaskAsync(world, cancellationToken: ct); }
                catch (OperationCanceledException)
                {
                    promise.ForgetLoading(world);
                    throw;
                }

                StreamableLoadingResult<SceneAssetBundleManifest> assetBundleManifest = promise.Result!.Value;

                if (assetBundleManifest is { Succeeded: true, Asset: { } manifest })
                    entityDefinition.assetBundleManifestVersion = AssetBundleManifestVersion.CreateFromFallback(manifest.GetVersion(), manifest.GetBuildDate());
                else
                {
                    assetBundleManifest.TryLogException();
                    entityDefinition.assetBundleManifestVersion = AssetBundleManifestVersion.FAILED;
                }
            }
        }
    }
}
