using Arch.Core;
using Arch.SystemGroups;
using DCL.ECSComponents;
using ECS.ComponentsPooling;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using ECS.Unity.GLTFContainer.Asset;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Systems;
using ECS.Unity.GLTFContainer.Systems;
using System.Collections.Generic;

namespace SceneRunner.ECSWorld.Plugins
{
    public class GltfContainerPlugin : IECSWorldPlugin
    {
        private readonly GltfContainerAssetsCache assetsCache;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly GltfContainerInstantiationThrottler throttler;

        public GltfContainerPlugin(ECSWorldSingletonSharedDependencies singletonSharedDependencies)
        {
            componentPoolsRegistry = singletonSharedDependencies.ComponentPoolsRegistry;
            assetsCache = new GltfContainerAssetsCache(1000);
            throttler = new GltfContainerInstantiationThrottler(20);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder,
            in ECSWorldInstanceSharedDependencies sharedDependencies, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            // Asset loading
            PrepareGltfAssetLoadingSystem.InjectToWorld(ref builder, assetsCache);
            CreateGltfAssetFromAssetBundleSystem.InjectToWorld(ref builder, throttler);

            // GLTF Container
            LoadGltfContainerSystem.InjectToWorld(ref builder);

            //CleanUpGltfContainerSystem.InjectToWorld(ref builder, assetsCache);
            ResetGltfContainerSystem.InjectToWorld(ref builder, assetsCache);
            WriteGltfContainerLoadingStateSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, componentPoolsRegistry.GetReferenceTypePool<PBGltfContainerLoadingState>());

            ResetDirtyFlagSystem<PBGltfContainer>.InjectToWorld(ref builder);

            var releaseTransformSystem =
                CleanUpGltfContainerSystem.InjectToWorld(ref builder, assetsCache);

            finalizeWorldSystems.Add(releaseTransformSystem);
        }
    }
}
