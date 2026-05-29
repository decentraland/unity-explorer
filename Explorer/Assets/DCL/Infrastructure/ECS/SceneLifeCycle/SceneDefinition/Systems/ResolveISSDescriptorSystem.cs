using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.SceneRunner.Scene;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle;
using ECS.StreamableLoading.AssetBundles.InitialSceneState;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;

namespace ECS.SceneLifeCycle.SceneDefinition
{
    /// <summary>
    ///     Consumes the transient ISS descriptor promise that
    ///     <c>ResolveSceneStateByIncreasingRadiusSystem</c> attaches to a scene entity while its descriptor
    ///     is being resolved. On consume, mutates the entity's <see cref="ISSDescriptor"/> in place via
    ///     <see cref="ISSDescriptor.MarkResolved"/> and removes the promise component. The same descriptor
    ///     instance persists for the scene's lifetime, so cached class references elsewhere (e.g.
    ///     <c>OrderedDataManaged.ISSDescriptor</c>) see the resolved state without a refetch.
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
    [LogCategory(ReportCategory.SCENE_LOADING)]
    public partial class ResolveISSDescriptorSystem : BaseUnityLoopSystem
    {
        internal ResolveISSDescriptorSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            ConsumePromiseQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ConsumePromise(in Entity entity, SceneDefinitionComponent sceneDefinitionComponent, ISSDescriptor descriptor, ref AssetPromise<ISSDescriptorResolution, GetISSDescriptorIntention> promise)
        {
            if (!promise.TryConsume(World, out StreamableLoadingResult<ISSDescriptorResolution> result))
                return;

            // The loader returns a failed result for both "no descriptor JSON" and "manifest predates ISS",
            // so any failure here means this scene has no ISS and we fall back to the legacy LOD path.
            if (result is { Succeeded: true, Asset: { } resolved })
                descriptor.MarkResolved(resolved);
            else
            {
                ReportHub.Log(GetReportCategory(), $"ISSDescriptor is unavailable for scene {sceneDefinitionComponent.Definition.id} ({result.Exception?.Message ?? "unknown reason"}), fallback LOD will be used");
                descriptor.MarkResolved(ISSDescriptorResolution.NONE);
            }

            World.Remove<AssetPromise<ISSDescriptorResolution, GetISSDescriptorIntention>>(entity);
        }
    }
}
