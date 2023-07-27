using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using ECS.Abstract;
using ECS.Prioritization;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using Unity.Collections;
using Utility;

namespace ECS.SceneLifeCycle.IncreasingRadius
{
    /// <summary>
    ///     Creates empty pointers in the realm with the fixed set of scenes
    ///     <para>
    ///         The system does it when all fixed scene definitions are resolved so it knows which empty parcels are left.
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
    public partial class CreateEmptyPointersInFixedRealmSystem : BaseUnityLoopSystem
    {
        private readonly ParcelMathJobifiedHelper parcelMathJobifiedHelper;
        private readonly IRealmPartitionSettings realmPartitionSettings;

        internal CreateEmptyPointersInFixedRealmSystem(World world,
            ParcelMathJobifiedHelper parcelMathJobifiedHelper,
            IRealmPartitionSettings realmPartitionSettings) : base(world)
        {
            this.parcelMathJobifiedHelper = parcelMathJobifiedHelper;
            this.realmPartitionSettings = realmPartitionSettings;
        }

        protected override void Update(float t)
        {
            CreatePointersWithIncreasingRadiusQuery(World);
        }

        [Query]
        [None(typeof(VolatileScenePointers))]
        [All(typeof(RealmComponent))]
        private void CreatePointersWithIncreasingRadius(ref FixedScenePointers fixedScenePointers, ref ProcessesScenePointers processesScenePointers)
        {
            if (!parcelMathJobifiedHelper.JobStarted) return;

            ref readonly NativeArray<ParcelMathJobifiedHelper.ParcelInfo> flatArray = ref parcelMathJobifiedHelper.FinishParcelsRingSplit();

            if (!fixedScenePointers.AllPromisesResolved) return;

            var pointersCreated = 0;

            // just create empty definitions in rings
            for (var i = 0; i < flatArray.Length && pointersCreated < realmPartitionSettings.ScenesDefinitionsRequestBatchSize; i++)
            {
                ParcelMathJobifiedHelper.ParcelInfo parcelInfo = flatArray[i];

                if (!parcelInfo.AlreadyProcessed)
                {
                    World.Create(new SceneDefinitionComponent(parcelInfo.Parcel.ToVector2Int()));
                    pointersCreated++;
                    processesScenePointers.Value.Add(parcelInfo.Parcel);
                }
            }
        }
    }
}
