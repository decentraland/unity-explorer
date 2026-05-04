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
        public static async UniTask CheckAssetBundleManifestFallbackAsync(World world, EntityDefinitionBase entityDefinition, IPartitionComponent partition, CancellationToken ct, bool isLSD = false, bool skipException = false)
        {
            SceneAssetBundleManifest? fetchedManifest = await CheckAssetBundleManifestFallbackInternalAsync(world, entityDefinition, partition, ct, isLSD, skipException);

            entityDefinition.assetBundleManifestVersion.InjectContent(entityDefinition.id, entityDefinition.content);

            // Scenes built with v49+ ABs ship a per-file deps digest in the manifest. We need that map to differentiate
            // cache entries when two scenes share a hash but resolve different dependency closures. If the fallback already
            // fetched the manifest, reuse it; otherwise fetch it now (the promise cache dedupes concurrent requests).
            if (entityDefinition.assetBundleManifestVersion is { } version && version.HasDepsDigests())
            {
                SceneAssetBundleManifest? manifestForDigests = fetchedManifest
                    ?? await FetchSceneAssetBundleManifestAsync(world, entityDefinition.id, partition, ct);

                if (manifestForDigests != null)
                    version.InjectDepsDigests(manifestForDigests.GetDepsDigests());
            }
        }

        public static async UniTask CheckAssetBundleManifestFallbackAsync(World world, TrimmedEntityDefinitionBase entityDefinition, IPartitionComponent partition, CancellationToken ct, bool isLSD = false)
        {
            await CheckAssetBundleManifestFallbackInternalAsync(world, entityDefinition, partition, ct, isLSD);
        }

        private static async UniTask<SceneAssetBundleManifest?> CheckAssetBundleManifestFallbackInternalAsync(World world, TrimmedEntityDefinitionBase entityDefinition, IPartitionComponent partition, CancellationToken ct, bool isLSD = false, bool skipException = false)
        {
            if (isLSD)
            {
                entityDefinition.assetBundleManifestVersion = AssetBundleManifestVersion.CreateManualManifest();
                return null;
            }

            //Fallback needed for when the asset-bundle-registry does not have the asset bundle manifest
            //Also used for the PX escape
            if (entityDefinition.assetBundleManifestVersion == null || entityDefinition.assetBundleManifestVersion.IsEmpty())
            {
                if (!skipException)
                {
                    Sentry.Unity.SentrySdk.AddBreadcrumb($"AB manifest version missing for entity: {entityDefinition.id}");
                    ReportHub.LogException(new Exception("AssetBundleManifestFallbackHelper: AB Manifest Fallback requested"), ReportCategory.ASSET_BUNDLES);
                }

                SceneAssetBundleManifest? manifest = await FetchSceneAssetBundleManifestAsync(world, entityDefinition.id, partition, ct);

                entityDefinition.assetBundleManifestVersion = manifest != null
                    ? AssetBundleManifestVersion.CreateFromFallback(manifest.GetVersion(), manifest.GetBuildDate())
                    : AssetBundleManifestVersion.CreateFailed();

                return manifest;
            }

            return null;
        }

        private static async UniTask<SceneAssetBundleManifest?> FetchSceneAssetBundleManifestAsync(World world, string entityId, IPartitionComponent partition, CancellationToken ct)
        {
            //Needed to use the Time.realtimeSinceStartup on the intention creation
            await UniTask.SwitchToMainThread();

            var promise = AssetBundleManifestPromise.Create(world,
                GetAssetBundleManifestIntention.Create(entityId, new CommonLoadingArguments(entityId)),
                partition);

            StreamableLoadingResult<SceneAssetBundleManifest> result = (await promise.ToUniTaskAsync(world, cancellationToken: ct)).Result.Value;

            if (result.Succeeded)
                return result.Asset;

            result.TryLogException();
            return null;
        }
    }
}
