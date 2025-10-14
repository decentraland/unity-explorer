using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Ipfs;
using DCL.RealmNavigation;
using DefaultNamespace;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Utility;
using ScenePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.Ipfs.SceneDefinitions, ECS.SceneLifeCycle.SceneDefinition.GetSceneDefinitionList>;


namespace ECS.SceneLifeCycle.Systems.EarlyAsset
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(EarlyAssetBundleRequestSystem))]
    public partial class EarlySceneRequestSystem : BaseUnityLoopSystem
    {

        private StartParcel startParcel;
        private IRealmData realmData;

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
            {
                List<SceneEntityDefinition> entityDefinitionList = new List<SceneEntityDefinition>( );
                List<int2> pointersList = new List<int2>() { startParcel.Peek().ToInt2() };
                var promise = ScenePromise.Create(World,
                    new GetSceneDefinitionList(entityDefinitionList, pointersList, new CommonLoadingArguments(realmData.Ipfs.AssetBundleRegistry)),
                    PartitionComponent.TOP_PRIORITY);
                World.Create(promise, new EarlyDownloadComponentFlag());
                sceneRequestInitialized = true;
                UnityEngine.Debug.Log("JUANI THE EARLY SCENE WAS REQUESTED");
                return;
            }

            CompleteEarlySceneRequestQuery(World);
        }

        [Query]
        [All(typeof(EarlyDownloadComponentFlag))]
        private void CompleteEarlySceneRequest(Entity entity, ref ScenePromise promise, ref EarlyDownloadComponentFlag flag)
        {
            if (!string.IsNullOrEmpty(flag.AsssetBundleHash))
                return;


            if (promise.TryConsume(World, out StreamableLoadingResult<SceneDefinitions> Result))
            {
                if (Result.Succeeded)
                {
                    if (Result.Asset.Value[0].SupportInitialSceneState())
                    {
                        //Do nothing. We just needed loaded in memory, we dont care the result.
                        //Whoever needs it, will grab it later
                        UnityEngine.Debug.Log($"JUANI THE EARLY SCENE WAS RETRIEVED staticscene_{Result.Asset.Value[0].metadata.scene.DecodedBase.ToString()}");
                        //Test URL
                        flag.AsssetBundleHash = $"staticscene_{Result.Asset.Value[0].metadata.scene.DecodedBase.ToString()}";
                    }
                    else
                    {
                        //Temporal destroy
                        World.Destroy(entity);
                    }
                }
                else
                {
                    //If it failed, we need to destroy it
                    World.Destroy(entity);
                }

            }

        }
    }
}
