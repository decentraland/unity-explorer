using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System.Threading;
using AssetBundleManifestPromise = ECS.StreamableLoading.Common.AssetPromise<SceneRunner.Scene.SceneAssetBundleManifest, ECS.StreamableLoading.AssetBundles.GetAssetBundleManifestIntention>;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     Fetches the scene asset-bundle manifest and injects its per-file deps digest map onto the
    ///     entity's <see cref="AssetBundleManifestVersion"/>. Required for v49+ ABs so the cache layers
    ///     (in-memory, disk, Unity webcache, GLTF container) can differentiate two scenes that share the
    ///     same hash but resolve different dependency closures.
    ///     <para>The manifest promise uses <c>NoCache</c>, so a completed fetch is never reused — callers
    ///     whose manifest-fallback pass already downloaded the manifest hand it in as
    ///     <c>prefetchedManifest</c> and this loader injects from it without a second download.</para>
    /// </summary>
    public static class SceneAssetBundleDigestsLoader
    {
        public static async UniTask EnsureDepsDigestsAsync(World world, EntityDefinitionBase entityDefinition, IPartitionComponent partition, CancellationToken ct, SceneAssetBundleManifest? prefetchedManifest = null)
        {
            AssetBundleManifestVersion manifestVersion = entityDefinition.AssetBundleManifestVersionOrFailed;

            // Pre-v49 manifests have no canonical assets/ layout — skip both the manifest download and the injection.
            if (!manifestVersion.SupportsDepsDigests())
                return;

            // The manifest-fallback path already downloaded the full manifest: reuse its files[] instead
            // of downloading the same JSON again.
            if (prefetchedManifest != null)
            {
                manifestVersion.InjectDepsDigests(prefetchedManifest.GetFiles());
                return;
            }

            //Needed to use the UnityEngine.Time.realtimeSinceStartup on the intention creation
            await UniTask.SwitchToMainThread();

            var promise = AssetBundleManifestPromise.Create(world,
                GetAssetBundleManifestIntention.Create(entityDefinition.id, new CommonLoadingArguments(entityDefinition.id)),
                partition);

            StreamableLoadingResult<SceneAssetBundleManifest> result = (await promise.ToUniTaskAsync(world, cancellationToken: ct)).Result.Value;

            if (result.Succeeded)
                manifestVersion.InjectDepsDigests(result.Asset.GetFiles());
            else if (result.Exception != null)
                ReportHub.LogException(result.Exception, ReportCategory.ASSET_BUNDLES);
        }
    }
}
