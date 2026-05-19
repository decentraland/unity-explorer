using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.RealmNavigation;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles.EarlyAsset;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using Unity.Mathematics;
using Utility;
using ScenePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.Ipfs.SceneDefinitions, ECS.SceneLifeCycle.SceneDefinition.GetSceneDefinitionList>;


namespace ECS.SceneLifeCycle.Systems.EarlyAsset
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(EarlyAssetBundleRequestSystem))]
    public partial class EarlySceneRequestSystem : BaseUnityLoopSystem
    {
        private readonly StartParcel startParcel;
        private readonly IRealmData realmData;
        private readonly IDecentralandUrlsSource urlsSource;

        private bool sceneRequestInitialized;

        public EarlySceneRequestSystem(World world, StartParcel startParcel, IRealmData realmData, IDecentralandUrlsSource urlsSource) : base(world)
        {
            this.startParcel = startParcel;
            this.realmData = realmData;
            this.urlsSource = urlsSource;
        }

        protected override void Update(float t)
        {
            if (!realmData.Configured)
                return;

            //For now, this only works for Genesis
            if (!realmData.IsGenesis())
                return;

            if (!sceneRequestInitialized)
                RequestEarlyScene();

            CompleteEarlySceneRequestQuery(World);
        }

        private void RequestEarlyScene()
        {
            var entityDefinitionList = new List<SceneEntityDefinition>();
            var pointersList = new List<int2> { startParcel.Peek().ToInt2() };

            var promise = ScenePromise.Create(World,
                new GetSceneDefinitionList(entityDefinitionList, pointersList, new CommonLoadingArguments(urlsSource.Url(DecentralandUrl.EntitiesActive))),
                PartitionComponent.TOP_PRIORITY);

            World.Create(promise, new EarlySceneFlag());
            sceneRequestInitialized = true;
        }

        [Query]
        [All(typeof(EarlySceneFlag))]
        private void CompleteEarlySceneRequest(Entity entity, ref ScenePromise promise)
        {
            if (promise.TryConsume(World, out StreamableLoadingResult<SceneDefinitions> Result))
            {
                // ISS bundle prewarm previously fired here based on the eagerly-resolved descriptor.
                // Resolution is lazy now (LOD path / SDK runtime loader), so the prewarm is dropped —
                // the bundle is fetched by those paths when they actually need it.

                //Nothing to do with it after creation of the early asset bundle request
                World.Destroy(entity);
            }
        }
    }
}
