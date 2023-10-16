using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using ECS;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.Common;
using Ipfs;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using Utility;
using int2Collection = System.Collections.Generic.IReadOnlyCollection<Unity.Mathematics.int2>;

// namespace changed to fix generators' bug
namespace Realm
{
    /// <summary>
    ///     Decides if loaded scenes should be launched or destroyed based on the loading radius
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
            HashSet<int2> parcelsInRange = parcelsInRangeComponent.Value;

            StartScenesLoadingQuery(World, parcelsInRange, realm.Ipfs);
            AddDestroyIntentionQuery(World, parcelsInRange);
        }

        [Query]
        [None(typeof(ISceneFacade), typeof(AssetPromise<ISceneFacade, GetSceneFacadeIntention>))]
        private void StartScenesLoading([Data] int2Collection parcelsInRange, [Data] IIpfsRealm realm,
            in Entity entity, ref SceneDefinitionComponent definition, ref PartitionComponent partitionComponent)
        {
            // Create an intention if the scene is within the radius
            if (SceneIsInRange(in definition, parcelsInRange))
                World.Add(entity,
                    AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(World,
                        new GetSceneFacadeIntention(realm, definition.IpfsPath, definition.Definition, definition.Parcels, definition.IsEmpty), partitionComponent));
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void AddDestroyIntention([Data] int2Collection parcelsInRange, in Entity entity, ref SceneDefinitionComponent definition)
        {
            if (SceneIsInRange(definition, parcelsInRange))
                return;

            World.Add(entity, DeleteEntityIntention.DeferredDeletion);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SceneIsInRange(in SceneDefinitionComponent definition, int2Collection parcelsInRange)
        {
            for (var i = 0; i < definition.Parcels.Count; i++)
                if (parcelsInRange.Contains(definition.Parcels[i].ToInt2()))
                    return true;

            return false;
        }
    }
}
