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
    [UpdateAfter(typeof(ResolveVisualSceneStateSystem))]
    public partial class UpdateVisualSceneStateSystem : BaseUnityLoopSystem
    {
        private readonly IRealmData realmData;
        private readonly IScenesCache scenesCache;

        public UpdateVisualSceneStateSystem(World world, IRealmData realmData, IScenesCache scenesCache) : base(world)
        {
            this.realmData = realmData;
            this.scenesCache = scenesCache;
        }

        protected override void Update(float t)
        {
            SwapLODToScenePromiseQuery(World);
            SwapSceneFacadeToLODQuery(World);
            SwapScenePromiseToLODQuery(World);
        }
        
        [Query]
        [None(typeof(SceneLODInfo))]
        private void SwapSceneFacadeToLOD(Entity entity, ref VisualSceneState visualSceneState,
            ISceneFacade sceneFacade, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (!visualSceneState.IsDirty) return;

            if (visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_LOD)
            {
                //Create LODInfo
                var sceneLODInfo = new SceneLODInfo { IsDirty = true };

                //Dispose scene
                sceneFacade.DisposeSceneFacade(scenesCache, sceneDefinitionComponent.Parcels);

                visualSceneState.IsDirty = false;

                World.Add(entity, sceneLODInfo);
                World.Remove<ISceneFacade, AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entity);
            }
        }

        [Query]
        [None(typeof(AssetPromise<ISceneFacade, GetSceneFacadeIntention>))]
        private void SwapLODToScenePromise(Entity entity, ref VisualSceneState visualSceneState,
            ref SceneLODInfo sceneLODInfo, ref SceneDefinitionComponent sceneDefinitionComponent, ref PartitionComponent partition)
        {
            if (!visualSceneState.IsDirty) return;

            if (visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_SCENE)
            {
                sceneLODInfo.Dispose(World);
                visualSceneState.IsDirty = false;

                //Show Scene
                World.Add(entity, AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(World,
                    new GetSceneFacadeIntention(realmData.Ipfs, sceneDefinitionComponent),
                    partition));
                World.Remove<SceneLODInfo>(entity);
            }
        }

        [Query]
        [None(typeof(SceneLODInfo))]
        private void SwapScenePromiseToLOD(Entity entity, ref VisualSceneState visualSceneState,
            AssetPromise<ISceneFacade, GetSceneFacadeIntention> promise)
        {
            if (!visualSceneState.IsDirty) return;

            if (visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_LOD)
            {
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
        }
    }
}