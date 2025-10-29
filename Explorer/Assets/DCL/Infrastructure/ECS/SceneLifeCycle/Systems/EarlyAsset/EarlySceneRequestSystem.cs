using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Ipfs;
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

        private bool sceneRequestInitialized;

        public EarlySceneRequestSystem(World world, StartParcel startParcel, IRealmData realmData) : base(world)
        {
            this.startParcel = startParcel;
            this.realmData = realmData;
        }

        protected override void Update(float t)
        {
            if (!realmData.Configured)
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
                new GetSceneDefinitionList(entityDefinitionList, pointersList, new CommonLoadingArguments(realmData.Ipfs.AssetBundleRegistry)),
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
                if (Result.Succeeded && Result.Asset.Value.Count > 0)
                {
                    if (Result.Asset.Value[0].SupportInitialSceneState())
                    {
                        //Do nothing. We just needed loaded in memory, we dont care the result.
                        //Whoever needs it, will grab it later
                        //Test URL
                        World.Create(EarlyAssetBundleFlag.CreateAssetBundleRequest(Result.Asset.Value[0]));
                    }
                }

                //Nothing to do with it after creation of the early asset bundle request
                World.Destroy(entity);
            }
        }
    }
}
