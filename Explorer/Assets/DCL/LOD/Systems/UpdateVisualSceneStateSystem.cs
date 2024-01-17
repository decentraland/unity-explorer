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

        public UpdateVisualSceneStateSystem(World world, IRealmData realmData) : base(world)
        {
            this.realmData = realmData;
        }

        protected override void Update(float t)
        {
            RemoveSceneLODInfoQuery(World);
            AddScenePromiseQuery(World);

            RemoveSceneFacadeQuery(World);
            RemoveScenePromiseQuery(World);
            AddSceneLODInfoQuery(World);

            //SwapLODToScenePromiseQuery(World);
            //SwapSceneFacadeToLODQuery(World);
            //SwapScenePromiseToLODQuery(World);
        }
        
        
        [Query]
        //TODO: This should have a none AssetPromise in it, but if I add it, its filtered. Ask Misha
        private void RemoveSceneLODInfo(in Entity entity, ref VisualSceneState visualSceneState,
            ref SceneLODInfo sceneLODInfo, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (!visualSceneState.IsDirty) return;

            if (visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_SCENE)
            {
                if (sceneDefinitionComponent.Definition.id.Equals("QmTAYbcAGPkmEVM8RoLtJkmWHrUb65h78JA41VmnREzA5g"))
                    Debug.Log("JUANI REMOVING LOD INFO");
                
                sceneLODInfo.Dispose(World);

                World.Remove<SceneLODInfo>(entity);
            }
        }

        [Query]
        [None(typeof(SceneLODInfo))]
        //TODO: This should have a none AssetPromise in it, but if I add it, its filtered. Ask Misha
        private void AddScenePromise(in Entity entity, ref VisualSceneState visualSceneState,
            ref SceneDefinitionComponent sceneDefinitionComponent, ref PartitionComponent partition)
        {
            if (!visualSceneState.IsDirty) return;

            if (visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_SCENE)
            {
                if (sceneDefinitionComponent.Definition.id.Equals("QmTAYbcAGPkmEVM8RoLtJkmWHrUb65h78JA41VmnREzA5g"))
                    Debug.Log("JUANI ADING SCENEPROMISE");
                
                visualSceneState.IsDirty = false;
                
                //Show Scene
                World.Add(entity, AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(World,
                    new GetSceneFacadeIntention(realmData.Ipfs, sceneDefinitionComponent),
                    partition));
            }
        }

        [Query]
        private void RemoveSceneFacade(in Entity entity, ref VisualSceneState visualSceneState,
            ISceneFacade sceneFacade, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (!visualSceneState.IsDirty) return;

            if (visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_LOD)
            {
                if (sceneDefinitionComponent.Definition.id.Equals("QmTAYbcAGPkmEVM8RoLtJkmWHrUb65h78JA41VmnREzA5g"))
                    Debug.Log("Removing scene facade");

                //Dispose scene
                sceneFacade.DisposeAsync().Forget();
                World.Remove<ISceneFacade, AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entity);
            }
        }

        [Query]
        private void RemoveScenePromise(in Entity entity, ref VisualSceneState visualSceneState,
            AssetPromise<ISceneFacade, GetSceneFacadeIntention> scenePromise,
            ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (!visualSceneState.IsDirty) return;

            if (visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_LOD)
            {
                if (sceneDefinitionComponent.Definition.id.Equals("QmTAYbcAGPkmEVM8RoLtJkmWHrUb65h78JA41VmnREzA5g"))
                    Debug.Log("Removing scene promise");

                //Dispose scene
                scenePromise.ForgetLoading(World);
                World.Remove<ISceneFacade, AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entity);
            }
        }

        [Query]
        [None(typeof(SceneFacade))]
        private void AddSceneLODInfo(in Entity entity, ref VisualSceneState visualSceneState,
            ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (!visualSceneState.IsDirty) return;

            if (visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_LOD)
            {
                if (sceneDefinitionComponent.Definition.id.Equals("QmTAYbcAGPkmEVM8RoLtJkmWHrUb65h78JA41VmnREzA5g"))
                    Debug.Log($"JUANI ANY CHANGING FROM SCENE FACADE TO LOD {sceneDefinitionComponent.Definition.id}");

                //Create LODInfo
                var sceneLODInfo = new SceneLODInfo { IsDirty = true };
                visualSceneState.IsDirty = false;
                World.Add(entity, sceneLODInfo);
            }
        }


        /*[Query]
        //[None(typeof(SceneLODInfo))]
        private void SwapSceneFacadeToLOD(in Entity entity, ref VisualSceneState visualSceneState,
            ISceneFacade sceneFacade, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (!visualSceneState.IsDirty) return;

            if (visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_LOD)
            {
                if(sceneDefinitionComponent.Definition.id.Equals("QmTAYbcAGPkmEVM8RoLtJkmWHrUb65h78JA41VmnREzA5g"))
                    Debug.Log($"JUANI ANY CHANGING FROM SCENE FACADE TO LOD {sceneDefinitionComponent.Definition.id}");

                //Create LODInfo
                var sceneLODInfo = new SceneLODInfo { IsDirty = true };

                //Dispose scene
                sceneFacade.DisposeAsync().Forget();

                visualSceneState.IsDirty = false;

                World.Add(entity, sceneLODInfo);
                World.Remove<ISceneFacade, AssetPromise<ISceneFacade, GetSceneFacadeIntention>>(entity);
            }
        }

        [Query]
        //TODO: This should have a none AssetPromise in it, but if I add it, its filtered. Ask Misha
        private void SwapLODToScenePromise(in Entity entity, ref VisualSceneState visualSceneState,
            ref SceneLODInfo sceneLODInfo, ref SceneDefinitionComponent sceneDefinitionComponent, ref PartitionComponent partition)
        {
            if (!visualSceneState.IsDirty) return;

            if (visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_SCENE)
            {
                if(sceneDefinitionComponent.Definition.id.Equals("QmTAYbcAGPkmEVM8RoLtJkmWHrUb65h78JA41VmnREzA5g"))
                    Debug.Log($"JUANI ANY CHANGING FROM LOD TO SCENE PROMISE");

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
        //[None(typeof(SceneLODInfo))]
        private void SwapScenePromiseToLOD(in Entity entity, ref VisualSceneState visualSceneState,
            AssetPromise<ISceneFacade, GetSceneFacadeIntention> promise, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (!visualSceneState.IsDirty) return;

            if (visualSceneState.CurrentVisualSceneState == VisualSceneStateEnum.SHOWING_LOD)
            {
                if(sceneDefinitionComponent.Definition.id.Equals("QmTAYbcAGPkmEVM8RoLtJkmWHrUb65h78JA41VmnREzA5g"))
                    Debug.Log($"JUANI ANY CHANGING FROM PROMISE TO LOD {sceneDefinitionComponent.Definition.id}");

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
        }*/
    }
}