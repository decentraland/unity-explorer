using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character.Components;
using DCL.Ipfs;
using DCL.LOD.Components;
using DCL.Roads.Components;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.SceneFacade;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;
using System.Linq;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.OneSceneLoading.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(ResolveSceneStateByIncreasingRadiusSystem))]
    public partial class ResolveSoloSceneStateSystem : BaseUnityLoopSystem
    {
        private readonly Entity playerEntity;

        public ResolveSoloSceneStateSystem(World world, Entity playerEntity) : base(world)
        {
            this.playerEntity = playerEntity;
        }

        protected override void Update(float t)
        {
            ProcessRealmWithSoloLoadingQuery(World, World.Get<CharacterTransform>(playerEntity).Parcel);
        }

        [Query]
        [All(typeof(SoloScenePointers))]
        [None(typeof(StaticScenePointers))]
        private void ProcessRealmWithSoloLoading([Data] Vector2Int parcel, ref RealmComponent realm)
        {
            StartLoadingSceneQuery(World, parcel, realm.Ipfs);
            StartUnloadingQuery(World, parcel);
        }

        [Query]
        [None(typeof(ISceneFacade), typeof(AssetPromise<ISceneFacade, GetSceneFacadeIntention>))]
        private void StartLoadingScene([Data] Vector2Int parcel, [Data] IIpfsRealm ipfsRealm, Entity entity,
            ref SceneDefinitionComponent sceneDefinitionComponent, in VisualSceneState visualSceneState, in PartitionComponent partitionComponent)
        {
            if (sceneDefinitionComponent.Parcels.Contains(parcel))
            {
                // REFACTOR IT!: Copy-pasted from ResolveSceneStateByIncreasingRadiusSystem.cs
                switch (visualSceneState.CurrentVisualSceneState)
                {
                    case VisualSceneStateEnum.SHOWING_LOD:
                        World.Add(entity, SceneLODInfo.Create());
                        break;
                    case VisualSceneStateEnum.ROAD:
                        World.Add(entity, RoadInfo.Create());
                        break;
                    default:
                        CreateSceneFacadePromise.Execute(World, entity, ipfsRealm, in sceneDefinitionComponent, partitionComponent);
                        break;
                }
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [Any(typeof(SceneLODInfo), typeof(ISceneFacade), typeof(AssetPromise<ISceneFacade, GetSceneFacadeIntention>), typeof(RoadInfo))]
        private void StartUnloading([Data] Vector2Int parcel, in Entity entity, ref SceneDefinitionComponent sceneDefinitionComponent)
        {
            if (!sceneDefinitionComponent.Parcels.Contains(parcel))
                World.Add(entity, DeleteEntityIntention.DeferredDeletion);
        }
    }
}
