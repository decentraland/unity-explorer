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
    /// <summary>
    ///     Fetches the scene asset-bundle manifest and injects its per-file deps digest map onto the
    ///     entity's <see cref="AssetBundleManifestVersion"/>. Required for v49+ ABs so the cache layers
    ///     (in-memory, disk, Unity webcache, GLTF container) can differentiate two scenes that share the
    ///     same hash but resolve different dependency closures.
    ///     <para>The fetch is deduped by <see cref="AssetBundleManifestPromise"/>'s cache.</para>
    /// </summary>
    public static class SceneAssetBundleDigestsLoader
    {
        public static async UniTask EnsureDepsDigestsAsync(World world, EntityDefinitionBase entityDefinition, IPartitionComponent partition, CancellationToken ct)
        {
            AssetBundleManifestVersion? manifestVersion = entityDefinition.assetBundleManifestVersion;

            // Pre-v49 manifests have no deps digest, and CreateFailed/CreateManualManifest entries report version < v49 as well — short-circuit those.
            if (manifestVersion == null || !manifestVersion.HasDepsDigests())
                return;

            //Needed to use the Time.realtimeSinceStartup on the intention creation
            await UniTask.SwitchToMainThread();

            var promise = AssetBundleManifestPromise.Create(world,
                GetAssetBundleManifestIntention.Create(entityDefinition.id, new CommonLoadingArguments(entityDefinition.id)),
                partition);

            StreamableLoadingResult<SceneAssetBundleManifest> result = (await promise.ToUniTaskAsync(world, cancellationToken: ct)).Result.Value;

            if (result.Succeeded)
                manifestVersion.InjectDepsDigests(result.Asset.GetDepsDigests());
            else
                result.TryLogException();
        }
    }
}
