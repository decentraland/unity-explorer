using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
using ECS.StreamableLoading;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.GLTF;
using ECS.Unity.GLTFContainer.Asset.Components;
using System;

namespace ECS.Unity.GLTFContainer.Asset.Systems
{
    /// <summary>
    ///     Creates <see cref="GltfContainerAsset" /> from the <see cref="StreamableLoadingResult{T}" />.
    ///     With <c>rawGltfFallback</c> a bundle that fails to load, or loads without a GameObject, is not
    ///     a failed container: the request is re-issued as a raw GLTF load instead.
    /// </summary>
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.GLTF_CONTAINER)]
    public partial class CreateGltfAssetFromAssetBundleSystem : BaseUnityLoopSystem
    {
        private readonly IPerformanceBudget instantiationFrameTimeBudget;
        private readonly IPerformanceBudget memoryBudget;
        private readonly bool rawGltfFallback;
        private readonly AbgenConversionMetrics abgenConversionMetrics;

        internal CreateGltfAssetFromAssetBundleSystem(World world, IPerformanceBudget instantiationFrameTimeBudget, IPerformanceBudget memoryBudget, bool rawGltfFallback,
            AbgenConversionMetrics abgenConversionMetrics) : base(world)
        {
            this.instantiationFrameTimeBudget = instantiationFrameTimeBudget;
            this.memoryBudget = memoryBudget;
            this.rawGltfFallback = rawGltfFallback;
            this.abgenConversionMetrics = abgenConversionMetrics;
        }

        protected override void Update(float t)
        {
            ConvertFromAssetBundleQuery(World);
        }

        /// <summary>
        ///     Called on a separate entity with a promise creates a result with <see cref="GltfContainerAsset" />
        /// </summary>
        [Query]
        [None(typeof(StreamableLoadingResult<GltfContainerAsset>))]
        private void ConvertFromAssetBundle(in Entity entity, ref GetGltfContainerAssetIntention assetIntention, ref StreamableLoadingResult<AssetBundleData> assetBundleResult)
        {
            if (!instantiationFrameTimeBudget.TrySpendBudget() || !memoryBudget.TrySpendBudget())
                return;

            if (assetIntention.CancellationTokenSource.IsCancellationRequested)

                // Don't care anymore, the entity will be deleted in the system that created this promise
                return;

            if (!assetBundleResult.Succeeded)
            {
                if (rawGltfFallback)
                    FallBackToRawGltf(entity, assetIntention.Name, assetIntention.Hash, assetBundleResult.Exception?.Message ?? "bundle failed to load");
                else

                    // Just propagate an exception, we can't do anything
                    World.Add(entity, new StreamableLoadingResult<GltfContainerAsset>(GetReportCategory(), CreateException(assetBundleResult.Exception)));

                return;
            }

            AssetBundleData assetBundleData = assetBundleResult.Asset!;

            if (Utils.TryCreateGltfObject(assetBundleData, assetIntention.Hash, out GltfContainerAsset result))
                World.Add(entity, new StreamableLoadingResult<GltfContainerAsset>(result));
            else if (rawGltfFallback)
            {
                // The bundle is cached and reference-counted; this consumer is leaving it for the raw file.
                assetBundleData.Dereference();
                FallBackToRawGltf(entity, assetIntention.Name, assetIntention.Hash, "bundle has no GameObject");
            }
            else
                World.Add(entity, new StreamableLoadingResult<GltfContainerAsset>(GetReportData(), new ArgumentException($"Failed to load {assetIntention.Hash} from AB")));
        }

        /// <summary>
        ///     Re-issues the request as a raw GLTF load on the same promise entity: the bundle lane's
        ///     intention and result are dropped, and the pooled loading state — finished, with no transition
        ///     back — is swapped for a fresh one so deferred loading admits the GLTF lane like any new request.
        /// </summary>
        private void FallBackToRawGltf(Entity entity, string name, string hash, string reason)
        {
            if (abgenConversionMetrics.RawGltfFallbackReason == null)
                ReportHub.LogWarning(GetReportCategory(), $"{name}: asset bundle unavailable ({reason}), loading the raw GLTF instead");

            abgenConversionMetrics.OnFailed(name, $"{reason} — loaded as raw GLTF");

            World.Get<StreamableLoadingState>(entity).Dispose();
            World.Set(entity, StreamableLoadingState.Create());
            World.Remove<GetAssetBundleIntention, StreamableLoadingResult<AssetBundleData>>(entity);
            World.Add(entity, GetGLTFIntention.Create(name, hash));
        }
    }
}
