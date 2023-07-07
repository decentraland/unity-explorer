using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.Common;
using Ipfs;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Decides if loaded scenes should launched or destroyed based on the loading radius radius
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(LoadPointersByRadiusSystem))]
    [UpdateAfter(typeof(LoadFixedPointersSystem))]
    [UpdateAfter(typeof(LoadStaticPointersSystem))]
    [UpdateAfter(typeof(CalculateParcelsInRangeSystem))]
    public partial class ResolveSceneStateByRadiusSystem : BaseUnityLoopSystem
    {
        internal ResolveSceneStateByRadiusSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            ProcessRealmQuery(World);
        }

        [Query]
        [None(typeof(StaticScenePointers))]
        private void ProcessRealm(ref RealmComponent realm, ref ParcelsInRange parcelsInRangeComponent)
        {
            HashSet<Vector2Int> parcelsInRange = parcelsInRangeComponent.Value;

            StartScenesLoadingQuery(World, parcelsInRange, realm.Ipfs);
            AddDestroyIntentionQuery(World, parcelsInRange);
        }

        [Query]
        [None(typeof(ISceneFacade), typeof(AssetPromise<ISceneFacade, GetSceneFacadeIntention>))]
        private void StartScenesLoading([Data] IReadOnlyCollection<Vector2Int> parcelsInRange, [Data] IIpfsRealm realm,
            in Entity entity, ref SceneDefinitionComponent definition)
        {
            if (definition.IsEmpty) return;

            // Create an intention if the scene is within the radius
            for (var i = 0; i < definition.Parcels.Count; i++)
            {
                if (parcelsInRange.Contains(definition.Parcels[i]))
                {
                    World.Add(entity,
                        AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(World, new GetSceneFacadeIntention(realm, definition.IpfsPath, definition.Definition)));

                    return;
                }
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void AddDestroyIntention([Data] IReadOnlyCollection<Vector2Int> parcelsInRange, in Entity entity, ref SceneDefinitionComponent definition)
        {
            if (SceneIsInRange(definition, parcelsInRange))
                return;

            World.Add<DeleteEntityIntention>(entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SceneIsInRange(in SceneDefinitionComponent definition, IReadOnlyCollection<Vector2Int> parcelsInRange)
        {
            for (var i = 0; i < definition.Parcels.Count; i++)
                if (parcelsInRange.Contains(definition.Parcels[i]))
                    return true;

            return false;
        }
    }
}
