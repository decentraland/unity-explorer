using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.StreamableLoading.AssetBundles.InitialSceneState;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;

namespace ECS.SceneLifeCycle.SceneDefinition
{
    /// <summary>
    ///     Bridges <see cref="LoadISSDescriptorSystem"/> to <see cref="SceneDefinitionComponent"/>: lazily
    ///     spawns the resolver promise for each scene definition and writes the resolved descriptor onto
    ///     the component. Global-world systems (UpdateSceneLODInfoSystem, ResolveISSLODSystem) iterate
    ///     <see cref="SceneDefinitionComponent.ISSDescriptor"/> directly and don't need to know the
    ///     resolver exists. The scene-runtime path (LoadSceneSystemLogicBase) creates its own promise and
    ///     gets the same instance via the loader's ongoing-request dedup, so AttachAssetBundle on its side
    ///     mutates the same descriptor that ends up on this component.
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
    [LogCategory(ReportCategory.SCENE_LOADING)]
    public partial class ResolveISSDescriptorSystem : BaseUnityLoopSystem
    {
        internal ResolveISSDescriptorSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            SpawnPromiseQuery(World);
            ConsumePromiseQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void SpawnPromise(ref SceneDefinitionComponent sceneDefinitionComponent, ref PartitionComponent partition)
        {
            // Skip if already resolved (non-NONE descriptor) or a promise is already in flight.
            if (sceneDefinitionComponent.ISSDescriptor.CurrentState != DCL.SceneRunner.Scene.IISSDescriptor.State.None) return;
            if (sceneDefinitionComponent.ISSDescriptorPromise.Entity != Entity.Null) return;

            sceneDefinitionComponent.ISSDescriptorPromise = AssetPromise<ISSDescriptor, GetISSDescriptor>.Create(
                World, GetISSDescriptor.For(sceneDefinitionComponent.Definition), partition);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ConsumePromise(ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (sceneDefinitionComponent.ISSDescriptorPromise.Entity == Entity.Null) return;

            if (!sceneDefinitionComponent.ISSDescriptorPromise.TryConsume(World, out StreamableLoadingResult<ISSDescriptor> result))
                return;

            sceneDefinitionComponent.ISSDescriptor = result is { Succeeded: true } ? result.Asset! : ISSDescriptor.NONE;
            sceneDefinitionComponent.ISSDescriptorPromise = AssetPromise<ISSDescriptor, GetISSDescriptor>.NULL;
        }
    }
}
