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
using ECS.Unity.GLTFContainer.Asset.Systems;
using Microsoft.ClearScript.JavaScript;
using Newtonsoft.Json;
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
        private IGltfContainerAssetsCache assetsCache;

        public ResolveStaticSceneAssetBundleSystem(World world, Dictionary<string, StaticSceneAssetBundle> staticSceneAssetBundlesDictionary, IGltfContainerAssetsCache assetsCache) : base(world)
        {
            this.staticSceneAssetBundlesDictionary = staticSceneAssetBundlesDictionary;
            this.assetsCache = assetsCache;
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
        public void InitializeStaticSceneAssetBundlePromise(ref StaticSceneAssetBundle staticSceneAssetBundle)
        {
            if (staticSceneAssetBundle.Request && staticSceneAssetBundle.AssetBundlePromise == AssetBundlePromise.NULL)
            {
                staticSceneAssetBundle.AssetBundlePromise = AssetBundlePromise.Create(World,
                    GetAssetBundleIntention.CreateSingleAssetBundleHack("GP_staticscene_LZMA_StaticSceneDescriptor"),
                    PartitionComponent.TOP_PRIORITY);
                /*staticSceneAssetBundle.AssetBundlePromise = AssetBundlePromise.Create(World,
                    GetAssetBundleIntention.CreateSingleAssetBundleHack($"https://explorer-artifacts.decentraland.zone/testing/GP_staticscene_LZMA"),
                    PartitionComponent.TOP_PRIORITY);*/
            }
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
                    foreach (string assetHashes in staticSceneAssetBundle.StaticSceneDescriptor.assetHash)
                        assetsCache.Dereference(assetHashes, CreateGltfAssetFromAssetBundleSystem.CreateGltfObject(Result.Asset, (GameObject)staticSceneAssetBundle.AssetBundleData.Asset.AssetDictionary[assetHashes], "static_"));
                }
            }
        }

    }
}
