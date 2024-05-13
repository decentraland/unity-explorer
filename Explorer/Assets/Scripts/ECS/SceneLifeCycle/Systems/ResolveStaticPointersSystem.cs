using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.Ipfs;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.SceneFacade;
using ECS.StreamableLoading.Common;
using SceneRunner.Scene;
using System.Linq;
using UnityEngine;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Creates a scene facade loading promise for each static scene pointers when its definition is loaded
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(LoadStaticPointersSystem))]
    public partial class ResolveStaticPointersSystem : BaseUnityLoopSystem
    {
        private SingleInstanceEntity cameraEntity;

        internal ResolveStaticPointersSystem(World world) : base(world) { }

        public override void Initialize()
        {
            cameraEntity = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            if (!World.Has<CameraSamplingData>(cameraEntity)) return;

            ForEachRealmQuery(World);
        }

        [Query]
        private void ForEachRealm(ref RealmComponent realm, ref StaticScenePointers staticScenePointers)
        {
            StartSceneLoadingQuery(World, realm.Ipfs, in staticScenePointers);
        }

        [Query]
        [None(typeof(ISceneFacade), typeof(AssetPromise<ISceneFacade, GetSceneFacadeIntention>))]
        private void StartSceneLoading([Data] IIpfsRealm realm, [Data] in StaticScenePointers staticScenePointers,
            in Entity entity, ref SceneDefinitionComponent definition, ref PartitionComponent partitionComponent)
        {
            for (var i = 0; i < definition.Parcels.Count; i++)
            {
                Vector2Int parcel = definition.Parcels[i];

                if (staticScenePointers.Value.Contains(parcel.ToInt2()))
                {
                    CreateSceneFacadePromise.Execute(World, entity, realm, in definition, partitionComponent);
                    return;
                }
            }
        }
    }
}
