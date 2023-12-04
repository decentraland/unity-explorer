using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using Ipfs;
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
        internal ResolveStaticPointersSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
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
                    World.Add(entity,
                        AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(World,
                            new GetSceneFacadeIntention(realm, definition), partitionComponent));

                    return;
                }
            }
        }
    }
}
