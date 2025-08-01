using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(LoadPointersByIncreasingRadiusSystem))]
    [UpdateAfter(typeof(LoadFixedPointersSystem))]
    [UpdateAfter(typeof(LoadStaticPointersSystem))]
    [UpdateBefore(typeof(ResolveSceneStateByIncreasingRadiusSystem))]
    public partial class ResolveStaticSceneAssetBundleSystem : BaseUnityLoopSystem
    {

        private readonly Dictionary<string, StaticSceneAssetBundle> staticSceneAssetBundlesDictionary;

        public ResolveStaticSceneAssetBundleSystem(World world, Dictionary<string, StaticSceneAssetBundle> staticSceneAssetBundlesDictionary) : base(world)
        {
            this.staticSceneAssetBundlesDictionary = staticSceneAssetBundlesDictionary;
        }

        protected override void Update(float t)
        {
            InitializeStaticSceneAssetBundleQuery(World);
            InitializeStaticSceneAssetBundlePromiseQuery(World);
            ResolveStaticSceneAssetBundlePromiseQuery(World);
        }

        [Query]
        [None(typeof(StaticSceneAssetBundle))]
        [All(typeof(SceneDefinitionComponent))]
        public void InitializeStaticSceneAssetBundle(Entity entity, in SceneDefinitionComponent sceneDefinitionComponent)
        {
            StaticSceneAssetBundle staticScene = new StaticSceneAssetBundle();
            //TODO (JUANI): FOr now, we hardcoded it only for GP. We will later check it with manifest
            staticScene.Supported = sceneDefinitionComponent.Definition.id.Equals("bafkreifqcraqxctg4krbklm6jsbq2x5tueevhmvxx354obl4ogu5owkbqu");
            staticSceneAssetBundlesDictionary.Add(sceneDefinitionComponent.Definition.id, staticScene);
            World.Add(entity, staticScene);
        }

        [Query]
        public void InitializeStaticSceneAssetBundlePromise(ref StaticSceneAssetBundle staticSceneAssetBundle, PartitionComponent partitionComponent)
        {
            if (staticSceneAssetBundle.Request && !staticSceneAssetBundle.PromiseInitiated)
            {
                staticSceneAssetBundle.AssetBundlePromise = AssetBundlePromise.Create(World,
                    GetAssetBundleIntention.CreateSingleAssetBundleHack($"https://explorer-artifacts.decentraland.zone/testing/GP_staticscene_LZMA"),
                    partitionComponent);

                staticSceneAssetBundle.PromiseInitiated = true;
            }
        }

        [Query]
        public void ResolveStaticSceneAssetBundlePromise(ref StaticSceneAssetBundle staticSceneAssetBundle)
        {
            if (!staticSceneAssetBundle.PromiseInitiated || staticSceneAssetBundle.Consumed) return;

            if (staticSceneAssetBundle.AssetBundlePromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> Result))
            {
                staticSceneAssetBundle.AssetBundleData = Result.Asset!;
                staticSceneAssetBundle.Assets = new Dictionary<string, GameObject>();
                foreach (Object asset in staticSceneAssetBundle.AssetBundleData.assets)
                {
                    if(asset is GameObject go)
                        staticSceneAssetBundle.Assets.Add(asset.name + PlatformUtils.GetCurrentPlatform(), go);
                }
                staticSceneAssetBundle.ReadyToUse = true;
                staticSceneAssetBundle.Consumed = true;

            }
        }

    }
}
