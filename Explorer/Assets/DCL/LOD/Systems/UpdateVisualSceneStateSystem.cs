using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using Cysharp.Threading.Tasks;
using DCL.LOD.Components;
using DCL.LOD.Systems;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using NSubstitute.ReturnsExtensions;
using Realm;
using SceneRunner;
using SceneRunner.Scene;
using UnityEngine;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(ResolveSceneLODInfo))]
    public partial class UpdateVisualSceneStateSystem : BaseUnityLoopSystem
    {
        private readonly IRealmData realmData;

        public UpdateVisualSceneStateSystem(World world, IRealmData realmData) : base(world)
        {
            this.realmData = realmData;
        }

        protected override void Update(float t)
        {
            SwapLODToScenePromiseQuery(World);
            SwapSceneFacadeToLODQuery(World);
            SwapScenePromiseToLODQuery(World);
        }

        [Query]
        private void SwapLODToScenePromise(in Entity entity, ref VisualSceneState visualSceneState, 
            ref SceneLODInfo sceneLODInfo, ref SceneDefinitionComponent sceneDefinitionComponent, ref PartitionComponent partition)
        {
            if (!visualSceneState.IsDirty) return;

            if (visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_SCENE)
            {
                if (sceneDefinitionComponent.Definition.id.Equals(
                        "bafkreieifr7pyaofncd6o7vdptvqgreqxxtcn3goycmiz4cnwz7yewjldq"))
                    Debug.Log("JUANI SWAPPING LOD TO SCENE PROMISE");

                sceneLODInfo.Dispose(World);

                //Show Scene
                World.Add(entity, AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(World,
                    new GetSceneFacadeIntention(realmData.Ipfs, sceneDefinitionComponent),
                    partition));
                World.Remove<SceneLODInfo>(entity);
            }
            visualSceneState.IsDirty = false;
        }
        
        [Query]
        [None(typeof(SceneLODInfo))]
        private void SwapSceneFacadeToLOD(in Entity entity, ref VisualSceneState visualSceneState,
            ISceneFacade sceneFacade,
            ref PartitionComponent partition, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (!visualSceneState.IsDirty) return;

            if (visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_LOD)
            {
                if (sceneDefinitionComponent.Definition.id.Equals(
                        "bafkreieifr7pyaofncd6o7vdptvqgreqxxtcn3goycmiz4cnwz7yewjldq"))
                    Debug.Log("JUANI SWAPPING SCENE FACEDE TO LOD");
                //Create LODInfo
                var sceneLODInfo = new SceneLODInfo
                {
                    IsDirty = true
                };

                //Dispose scene
                sceneFacade.DisposeAsync().Forget();

                visualSceneState.IsDirty = false;

                World.Add(entity, sceneLODInfo);
                World.Remove<ISceneFacade>(entity);
            }

            visualSceneState.IsDirty = false;
        }

        [Query]
        [None(typeof(SceneLODInfo))]
        private void SwapScenePromiseToLOD(in Entity entity, ref VisualSceneState visualSceneState,
            AssetPromise<ISceneFacade, GetSceneFacadeIntention> promise, ref PartitionComponent partition,
            ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (!visualSceneState.IsDirty) return;

            if (visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_LOD)
            {
                if (sceneDefinitionComponent.Definition.id.Equals(
                        "bafkreieifr7pyaofncd6o7vdptvqgreqxxtcn3goycmiz4cnwz7yewjldq"))
                    Debug.Log("JUANI SWAPPING SCENE PROMISE TO LOD");
                //Create LODInfo
                var sceneLODInfo = new SceneLODInfo
                {
                    IsDirty = true
                };

                //Dispose Promise
                promise.ForgetLoading(World);

                visualSceneState.IsDirty = false;

                World.Add(entity, sceneLODInfo);
                World.Remove<AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entity);
            }

            visualSceneState.IsDirty = false;
        }
    }
}