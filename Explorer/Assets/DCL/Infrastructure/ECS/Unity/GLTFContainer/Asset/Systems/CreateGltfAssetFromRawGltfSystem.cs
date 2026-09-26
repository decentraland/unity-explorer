using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
using ECS.StreamableLoading;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.GLTF;
using ECS.Unity.GLTFContainer.Asset.Components;
using UnityEngine;

namespace ECS.Unity.GLTFContainer.Asset.Systems
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.GLTF_CONTAINER)]
    public partial class CreateGltfAssetFromRawGltfSystem : BaseUnityLoopSystem
    {
        private readonly IPerformanceBudget instantiationFrameTimeBudget;
        private readonly IPerformanceBudget memoryBudget;

        internal CreateGltfAssetFromRawGltfSystem(World world, IPerformanceBudget instantiationFrameTimeBudget, IPerformanceBudget memoryBudget) : base(world)
        {
            this.instantiationFrameTimeBudget = instantiationFrameTimeBudget;
            this.memoryBudget = memoryBudget;
        }

        protected override void Update(float t)
        {
            ConvertFromGLTFDataQuery(World);
        }

        [Query]
        [None(typeof(StreamableLoadingResult<GltfContainerAsset>))]
        private void ConvertFromGLTFData(in Entity entity,
            ref GetGltfContainerAssetIntention assetIntention,
            ref StreamableLoadingResult<GLTFData> gltfDataResult)
        {
            if (!instantiationFrameTimeBudget.TrySpendBudget() || !memoryBudget.TrySpendBudget())
                return;

            if (assetIntention.CancellationTokenSource.IsCancellationRequested)
            {
                // Release this consumer's reference. Terminal disposal is owned by GltfLoadCache.Unload —
                // by the time we get here PutAsync has stored the asset in the cache and ApplyLoadedResult
                // has bumped its reference count, so calling Dispose() here would double-dispose when the
                // cache later drains the entry.
                if (gltfDataResult.Succeeded && gltfDataResult.Asset is { } gltfData)
                    gltfData.Dereference();

                World.Destroy(entity);
                return;
            }

            if (!gltfDataResult.IsInitialized)
                return;

            if (gltfDataResult.Succeeded)
                World.Add(entity, new StreamableLoadingResult<GltfContainerAsset>(Utils.CreateGltfObject(gltfDataResult.Asset)));
            else
                World.Add(entity, new StreamableLoadingResult<GltfContainerAsset>(GetReportData(),
                    new StreamableLoadingException(LogType.Exception, gltfDataResult.Exception.Message, gltfDataResult.Exception)));
        }
    }
}
