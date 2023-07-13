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
            foreach (Vector2Int parcel in definition.Parcels)
                if (staticScenePointers.Value.Contains(parcel))
                {
                    World.Add(entity,
                        AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(World, new GetSceneFacadeIntention(realm, definition.IpfsPath, definition.Definition), partitionComponent));

                    return;
                }
        }
    }
}
