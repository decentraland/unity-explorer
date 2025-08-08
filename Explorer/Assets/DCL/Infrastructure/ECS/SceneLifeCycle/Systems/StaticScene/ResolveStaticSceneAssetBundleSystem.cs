using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Asset.Systems;
using Newtonsoft.Json;
using System.Threading;
using UnityEngine;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(LoadPointersByIncreasingRadiusSystem))]
    [UpdateAfter(typeof(LoadFixedPointersSystem))]
    [UpdateAfter(typeof(LoadStaticPointersSystem))]
    [UpdateBefore(typeof(ResolveSceneStateByIncreasingRadiusSystem))]
    [UpdateBefore(typeof(ResolveSceneStateByIncreasingRadiusSystem))]
    public partial class ResolveStaticSceneAssetBundleSystem : BaseUnityLoopSystem
    {


        public ResolveStaticSceneAssetBundleSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            InitializeStaticSceneAssetBundleQuery(World);
            ResolveStaticSceneAssetBundlePromiseQuery(World);
        }

        [Query]
        [None(typeof(StaticSceneAssetBundle))]
        [All(typeof(SceneDefinitionComponent))]
        public void InitializeStaticSceneAssetBundle(Entity entity, in SceneDefinitionComponent sceneDefinitionComponent)
        {
            StaticSceneAssetBundle staticScene = new StaticSceneAssetBundle(World, sceneDefinitionComponent.Definition.id);
            World.Add(entity, staticScene);
        }

        [Query]
        public void ResolveStaticSceneAssetBundlePromise(ref StaticSceneAssetBundle staticSceneAssetBundle)
        {
            // Skip if promise hasn't been created yet or is already consumed
            if (staticSceneAssetBundle.AssetBundlePromise == AssetBundlePromise.NULL || staticSceneAssetBundle.AssetBundlePromise.IsConsumed) return;

            if (staticSceneAssetBundle.AssetBundlePromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> Result))
            {
                staticSceneAssetBundle.AssetBundleData = Result;
                if (Result.Succeeded)
                {
                    staticSceneAssetBundle.StaticSceneDescriptor =
                        (StaticSceneDescriptor)JsonConvert.DeserializeObject<StaticSceneDescriptor>(((TextAsset)staticSceneAssetBundle.AssetBundleData.Asset.AssetDictionary["StaticSceneDescriptor"]).text);

                    foreach (string assetHash in staticSceneAssetBundle.StaticSceneDescriptor.assetHash)
                        World.Create(new GetGltfContainerAssetIntention($"static_assset_{assetHash}", assetHash, new CancellationTokenSource()), Result);
                }
            }
        }

    }
}
