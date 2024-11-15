using Arch.Core;
using Arch.SystemGroups;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using Utility;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Partitions scene entities right after their definitions are resolved so their loading is properly deferred
    ///     according to the assigned partition. Partitioning performed for non-empty scenes only
    ///     <para>
    ///         Partitioning is performed according to the closest scene parcel to the camera.
    ///         It is guaranteed that parcels array is set in a scene definition, otherwise it won't work
    ///     </para>
    /// </summary>
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(LoadFixedPointersSystem))]
    [UpdateBefore(typeof(ResolveStaticPointersSystem))]
    public sealed partial class PartitionSceneEntitiesSystem : BaseUnityLoopSystem
    {
        private readonly IComponentPool<PartitionComponent> partitionComponentPool;
        private readonly IReadOnlyCameraSamplingData readOnlyCameraSamplingData;
        private readonly IPartitionSettings partitionSettings;
        private readonly IRealmPartitionSettings realmPartitionSettings;

        private readonly QueryDescription newScenesQuery;
        private readonly QueryDescription existingScenesQuery;

        internal PartitionSceneEntitiesSystem(World world,
            IComponentPool<PartitionComponent> partitionComponentPool,
            IPartitionSettings partitionSettings,
            IReadOnlyCameraSamplingData readOnlyCameraSamplingData,
            IRealmPartitionSettings realmPartitionSettings) : base(world)
        {
            this.partitionComponentPool = partitionComponentPool;
            this.readOnlyCameraSamplingData = readOnlyCameraSamplingData;
            this.realmPartitionSettings = realmPartitionSettings;
            this.partitionSettings = partitionSettings;

            newScenesQuery = new QueryDescription()
                            .WithAll<SceneDefinitionComponent>()
                            .WithNone<PartitionComponent>();

            existingScenesQuery = new QueryDescription()
               .WithAll<SceneDefinitionComponent, PartitionComponent>();
        }

        protected override void Update(float t)
        {
            World.Add<PartitionComponent>(in newScenesQuery);

            float unloadingDistance = (Mathf.Max(1, realmPartitionSettings.UnloadingDistanceToleranceInParcels)
                                       + realmPartitionSettings.MaxLoadingDistanceInParcels)
                                      * ParcelMathHelper.PARCEL_SIZE;

            PartitionScenes partitionScenes = new ()
            {
                CameraPosition = readOnlyCameraSamplingData.Position,
                CameraForward = readOnlyCameraSamplingData.Forward,
                CameraIsDirty = readOnlyCameraSamplingData.IsDirty,
                PartitionPool = partitionComponentPool,
                UnloadingSqrDistance = unloadingDistance * unloadingDistance,

                // PartitionSettingsAsset.SqrDistanceBuckets is a List, so just cast to that so we don't
                // waste time on virtual calls when accessing its elements.
                DistanceBuckets = (List<int>)partitionSettings.SqrDistanceBuckets,
            };

            Profiler.BeginSample(nameof(PartitionScenes));

            World.InlineQuery<PartitionScenes, SceneDefinitionComponent, PartitionComponent>(
                in existingScenesQuery, ref partitionScenes);

            Profiler.EndSample();
        }

        private struct PartitionScenes : IForEach<SceneDefinitionComponent, PartitionComponent>
        {
            public Vector3 CameraPosition;
            public Vector3 CameraForward;
            public bool CameraIsDirty;
            public List<int> DistanceBuckets;
            public IComponentPool<PartitionComponent> PartitionPool;
            public float UnloadingSqrDistance;

            public void Update(ref SceneDefinitionComponent definition, ref PartitionComponent partition)
            {
                if (partition != null && !CameraIsDirty)
                    return;
                else if (partition == null)
                    partition = PartitionPool.Get();

                if (definition.IsPortableExperience)
                {
                    partition.OutOfRange = false;
                    partition.Bucket = 0;
                    partition.IsBehind = false;
                    partition.RawSqrDistance = 0f;
                    partition.IsDirty = true;
                    return;
                }

                ParcelMathHelper.SceneCircumscribedPlanes sceneBounds2d = definition.SceneGeometry.CircumscribedPlanes;
                Vector3 min = new (sceneBounds2d.MinX, 0f, sceneBounds2d.MinZ);
                Vector3 max = new (sceneBounds2d.MaxX, definition.SceneGeometry.Height, sceneBounds2d.MaxZ);

                Bounds sceneBounds = new ();
                sceneBounds.SetMinMax(min, max);

                float sqrDistance = sceneBounds.SqrDistance(CameraPosition);

                var bucketIndex = 0;

                while (bucketIndex < DistanceBuckets.Count && sqrDistance >= DistanceBuckets[bucketIndex])
                    bucketIndex++;

                min -= CameraPosition;
                max -= CameraPosition;

                bool isBehind = sqrDistance > 0f
                                && Vector3.Dot(CameraForward, new Vector3(min.x, min.y, min.z)) < 0f
                                && Vector3.Dot(CameraForward, new Vector3(min.x, min.y, max.z)) < 0f
                                && Vector3.Dot(CameraForward, new Vector3(min.x, max.y, min.z)) < 0f
                                && Vector3.Dot(CameraForward, new Vector3(min.x, max.y, max.z)) < 0f
                                && Vector3.Dot(CameraForward, new Vector3(max.x, min.y, min.z)) < 0f
                                && Vector3.Dot(CameraForward, new Vector3(max.x, min.y, max.z)) < 0f
                                && Vector3.Dot(CameraForward, new Vector3(max.x, max.y, min.z)) < 0f
                                && Vector3.Dot(CameraForward, new Vector3(max.x, max.y, max.z)) < 0f;

                bool isDirty = partition.IsBehind != isBehind || partition.Bucket != bucketIndex;

                partition.Bucket = (byte)bucketIndex;
                partition.IsBehind = isBehind;
                partition.IsDirty = isDirty;
                partition.OutOfRange = sqrDistance > UnloadingSqrDistance;
                partition.RawSqrDistance = sqrDistance;
            }
        }
    }
}
