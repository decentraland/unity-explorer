using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;
/*
namespace ECS.SceneLifeCycle.Systems
{
    //TODO (JUANI): WHERE SHOULD THIS CLASS GO? PLUGINS OR ASSET BUNDLES?
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(ResolveSceneAssetBundleManifest))]
    public partial class ResolveStaticSceneStateSystem : BaseUnityLoopSystem
    {
        public ResolveStaticSceneStateSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            StartStaticSceneStateAssetBundlePromiseForScenes(World);
            ResolveStaticSceneAssetBundlePromiseQuery(World);
        }

        [Query]
        [All(typeof(AssetPromise<ISceneFacade, GetSceneFacadeIntention>), typeof(SceneAssetBundleManifest))]
        [None(typeof(StaticScene))]
        public void StartStaticSceneStateAssetBundlePromiseForScenes(Entity entity, in SceneAssetBundleManifest assetBundleManifest, ref PartitionComponent partitionComponent)
        {
            StaticScene staticScene = new StaticScene();
            if (!assetBundleManifest.SupportsSingleAssetBundle())
            {
                staticScene.IsReady = true;
            }
            else
            {
                ApplicationParametersParser.TryGetValueStatic("compression", out string compressionValue);

                //TODO (Juani) : Here we would need to do the unhacked version, and use the regular flow
                staticScene.StaticSceneAssetBundlePromise =
                    AssetBundlePromise.Create(World,
                        GetAssetBundleIntention.CreateSingleAssetBundleHack($"https://explorer-artifacts.decentraland.zone/testing/GP_staticscene_{compressionValue}"),
                        partitionComponent);
            }

            World.Add(entity, staticScene);
        }


        [Query]
        [All(typeof(StaticScene))]
        public void ResolveStaticSceneAssetBundlePromiseQuery(ref StaticScene staticScene)
        {
            if (staticScene.IsReady)
                return;

            if (staticScene.StaticSceneAssetBundlePromise.IsConsumed)
                return;

            if (staticScene.StaticSceneAssetBundlePromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> resolvedAssetBundleData))
            {
                staticScene.IsReady = true;
                staticScene.AssetData = resolvedAssetBundleData.Asset!;
            }
        }
    }
}
*/
