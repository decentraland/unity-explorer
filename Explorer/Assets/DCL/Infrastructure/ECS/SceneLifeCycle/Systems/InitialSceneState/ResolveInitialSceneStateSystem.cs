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
            {
                UnityEngine.Debug.Log($"JUANI TESTING INITIAL SCENE STARTED {UnityEngine.Time.frameCount}");
                World.Add(entity, InitialSceneStateDescriptor.CreateSupported(World, assetsCache, sceneDefinitionComponent.Definition.metadata.scene.DecodedBase.ToString()));
            }
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
                UnityEngine.Debug.Log($"JUANI TESTING INITIAL SCENE COMPLETED {UnityEngine.Time.frameCount}");
                staticSceneAssetBundle.AssetBundleData = Result;
                if (Result.Succeeded )
                {
                    //TODO (JUANI) : So many !
                    foreach (string assetHash in staticSceneAssetBundle.AssetBundleData.Asset!.InitialSceneStateMetadata!.Value.assetHash)
                        World.Create(staticSceneAssetBundle, new GetGltfContainerAssetIntention($"static_assset_{assetHash}", assetHash, new CancellationTokenSource()), Result);
                }
            }
        }

    }
}
