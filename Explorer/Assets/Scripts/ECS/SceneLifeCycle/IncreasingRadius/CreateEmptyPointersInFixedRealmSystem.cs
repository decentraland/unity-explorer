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
        private void CreatePointersWithIncreasingRadius(ref FixedScenePointers fixedScenePointers, ref ProcessedScenePointers processedScenePointers)
        {
            if (parcelMathJobifiedHelper.JobStarted)
            {
                parcelMathJobifiedHelper.Complete();
                fixedScenePointers.EmptyParcelsLastProcessedIndex = 0;
            }

            if (!fixedScenePointers.AllPromisesResolved) return;

            ref readonly NativeArray<ParcelMathJobifiedHelper.ParcelInfo> flatArray = ref parcelMathJobifiedHelper.LastSplit;

            var pointersCreated = 0;

            // just create empty definitions in rings
            // we save the index of the last processed parcel so we can continue from there next time as we defer it
            for (; fixedScenePointers.EmptyParcelsLastProcessedIndex < flatArray.Length && pointersCreated < realmPartitionSettings.ScenesDefinitionsRequestBatchSize; fixedScenePointers.EmptyParcelsLastProcessedIndex++)
            {
                ParcelMathJobifiedHelper.ParcelInfo parcelInfo = flatArray[fixedScenePointers.EmptyParcelsLastProcessedIndex];

                // we need to check it again as parcelInfo.AlreadyProcessed corresponds to the moment of splitting
                if (!processedScenePointers.Value.Contains(parcelInfo.Parcel))
                {
                    World.Create(SceneDefinitionComponentFactory.CreateEmpty(parcelInfo.Parcel.ToVector2Int()));
                    pointersCreated++;
                    processedScenePointers.Value.Add(parcelInfo.Parcel);
                }
            }
        }
    }
}
