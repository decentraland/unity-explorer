using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using ECS.Abstract;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.AssetBundles.InitialSceneState;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using System.Threading;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace ECS.SceneLifeCycle.Systems.InitialSceneState
{

    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(LoadPointersByIncreasingRadiusSystem))]
    [UpdateAfter(typeof(LoadFixedPointersSystem))]
    [UpdateAfter(typeof(LoadStaticPointersSystem))]
    [UpdateBefore(typeof(ResolveSceneStateByIncreasingRadiusSystem))]
    [UpdateBefore(typeof(ResolveSceneStateByIncreasingRadiusSystem))]
    public partial class ResolveInitialSceneStateSystem : BaseUnityLoopSystem
    {

        private readonly IGltfContainerAssetsCache assetsCache;

        public ResolveInitialSceneStateSystem(World world, IGltfContainerAssetsCache assetsCache) : base(world)
        {
            this.assetsCache = assetsCache;
        }

        protected override void Update(float t)
        {
            InitializeStaticSceneAssetBundleQuery(World);
            ResolveStaticSceneAssetBundlePromiseQuery(World);
        }

        [Query]
        [None(typeof(InitialSceneStateDescriptor))]
        [All(typeof(SceneDefinitionComponent))]
        public void InitializeStaticSceneAssetBundle(Entity entity, in SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (sceneDefinitionComponent.Definition.SupportInitialSceneState())
                World.Add(entity, InitialSceneStateDescriptor.CreateSupported(World, assetsCache, sceneDefinitionComponent.Definition));
            else
                World.Add(entity, InitialSceneStateDescriptor.CreateUnsupported(sceneDefinitionComponent.Definition.id));
        }

        [Query]
        public void ResolveStaticSceneAssetBundlePromise(ref InitialSceneStateDescriptor staticSceneAssetBundle)
        {
            // Skip if promise hasn't been created yet or is already consumed
            if (staticSceneAssetBundle.AssetBundlePromise == AssetBundlePromise.NULL || staticSceneAssetBundle.AssetBundlePromise.IsConsumed) return;

            if (staticSceneAssetBundle.AssetBundlePromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> Result))
            {
                staticSceneAssetBundle.AssetBundleData = Result;
                if (Result.Succeeded )
                {
                    //Dereferencing, because no one is using it yet, we just acquired it. The GLTFContainerAsset will start using it down here
                    //They will be referened in
                    Result.Asset.Dereference();

                    foreach (string assetHash in staticSceneAssetBundle.AssetBundleData.Asset!.InitialSceneStateMetadata!.Value.assetHash)
                        World.Create(staticSceneAssetBundle, new GetGltfContainerAssetIntention($"static_{assetHash}", assetHash, new CancellationTokenSource()), Result);
                }
            }
        }

    }
}
