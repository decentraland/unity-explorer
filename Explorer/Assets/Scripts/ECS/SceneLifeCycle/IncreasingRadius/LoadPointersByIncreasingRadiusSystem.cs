using System;
using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using DCL.Ipfs;
using ECS.Prioritization;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using Unity.Mathematics;
using Utility;

namespace ECS.SceneLifeCycle.IncreasingRadius
{
    /// <summary>
    ///     Mutually exclusive to <see cref="LoadPointersByRadiusSystem" />
    /// </summary>
    [UpdateAfter(typeof(LoadFixedPointersSystem))]
    [UpdateInGroup(typeof(RealmGroup))]
    public partial class LoadPointersByIncreasingRadiusSystem : LoadScenePointerSystemBase
    {
        private readonly ParcelMathJobifiedHelper parcelMathJobifiedHelper;
        private readonly IRealmPartitionSettings realmPartitionSettings;
        private readonly IPartitionSettings partitionSettings;

        private float[]? sqrDistances;

        private bool splitIsPending;

        internal LoadPointersByIncreasingRadiusSystem(World world,
            ParcelMathJobifiedHelper parcelMathJobifiedHelper,
            IRealmPartitionSettings realmPartitionSettings, IPartitionSettings partitionSettings) : base(world)
        {
            this.parcelMathJobifiedHelper = parcelMathJobifiedHelper;
            this.realmPartitionSettings = realmPartitionSettings;
            this.partitionSettings = partitionSettings;
        }

        protected override void Update(float t)
        {
            if (realmPartitionSettings.ScenesDefinitionsRequestBatchSize != sqrDistances?.Length)
                sqrDistances = new float[realmPartitionSettings.ScenesDefinitionsRequestBatchSize];

            // VolatileScenePointers should be created from RealmController

            // job started means that there was a new split initiated (dirty state)
            if (parcelMathJobifiedHelper.JobStarted)
            {
                // no matter what finish the current split
                parcelMathJobifiedHelper.Complete();
                splitIsPending = true;
            }

            ResolveActivePromiseQuery(World);
            StartLoadingFromVolatilePointersQuery(World);
        }

        /// <summary>
        ///     We can't use this flow if realm/world provides with a fixed pointers list
        ///     as EntitiesActiveEndpoint is not supported by its content server
        /// </summary>
        [Query]
        [None(typeof(FixedScenePointers))]
        private void StartLoadingFromVolatilePointers(ref RealmComponent realm, ref VolatileScenePointers volatileScenePointers)
        {
            if (!splitIsPending) return;

            // maintain one bulk request at a time
            if (volatileScenePointers.ActivePromise != null) return;

            // Take up to <ScenesDefinitionsRequestBatchSize> closest pointers that were not processed yet
            List<int2> input = volatileScenePointers.InputReusableList;

            ref var flatArray = ref parcelMathJobifiedHelper.LastSplit;
            int i;

            splitIsPending = false;

            for (i = 0; i < flatArray.Length; i++)
            {
                ParcelMathJobifiedHelper.ParcelInfo parcelInfo = flatArray[i];

                if (parcelInfo.AlreadyProcessed)
                    continue;

                if (input.Count < realmPartitionSettings.ScenesDefinitionsRequestBatchSize)
                {
                    sqrDistances![input.Count] = parcelInfo.RingSqrDistance;
                    input.Add(parcelInfo.Parcel);
                    parcelInfo.AlreadyProcessed = true;
                    flatArray[i] = parcelInfo;
                }
                else
                {
                    splitIsPending = true;
                    break;
                }
            }

            if (input.Count == 0) return;

            Array.Clear(sqrDistances!, input.Count, sqrDistances!.Length - input.Count);

            // Use median instead of average as the latter can affect the resulting bucket unpredictably (tends to give higher values)
            Array.Sort(sqrDistances!);
            float median = sqrDistances![input.Count / 2];

            // Find the bucket
            byte bucketIndex = 0;
            for (; bucketIndex < partitionSettings.SqrDistanceBuckets.Count; bucketIndex++)
            {
                if (median < partitionSettings.SqrDistanceBuckets[bucketIndex])
                    break;
            }

            volatileScenePointers.ActivePartitionComponent.Bucket = bucketIndex;
            volatileScenePointers.ActivePromise
                = AssetPromise<SceneDefinitions, GetSceneDefinitionList>.Create(World,
                    new GetSceneDefinitionList(volatileScenePointers.RetrievedReusableList, input,
                        new CommonLoadingArguments(realm.Ipfs.EntitiesActiveEndpoint)),
                    volatileScenePointers.ActivePartitionComponent);
        }

        [Query]
        private void ResolveActivePromise(ref VolatileScenePointers volatileScenePointers, ref ProcessedScenePointers processedScenePointers)
        {
            if (!volatileScenePointers.ActivePromise.HasValue) return;

            AssetPromise<SceneDefinitions, GetSceneDefinitionList> promise = volatileScenePointers.ActivePromise.Value;

            if (!promise.TryConsume(World, out StreamableLoadingResult<SceneDefinitions> result)) return;

            // contains the list of parcels that were requested
            IReadOnlyList<int2> requestedList = promise.LoadingIntention.Pointers;

            if (result.Succeeded)
            {
                List<SceneEntityDefinition> definitions = result.Asset.Value;

                for (var i = 0; i < definitions.Count; i++)
                {
                    SceneEntityDefinition scene = definitions[i];
                    if (scene.pointers.Count == 0) continue;

                    TryCreateSceneEntity(scene, new IpfsPath(scene.id, URLDomain.EMPTY), processedScenePointers.Value);
                }

                // Empty parcels = parcels for which no scene pointers were retrieved
                for (var i = 0; i < requestedList.Count; i++)
                {
                    int2 parcel = requestedList[i];
                    if (!processedScenePointers.Value.Add(parcel)) continue;
                    World.Create(new SceneDefinitionComponent(parcel.ToVector2Int()));
                }
            }
            else
            {
                // Signal that those parcels should not be requested again
                for (var i = 0; i < requestedList.Count; i++)
                    processedScenePointers.Value.Add(requestedList[i]);
            }

            volatileScenePointers.ActivePromise = null;
            volatileScenePointers.InputReusableList.Clear();
            volatileScenePointers.RetrievedReusableList.Clear();
        }
    }
}
