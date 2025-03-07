using ECS.Prioritization.Components;
using System;
using UnityEngine;
using Utility;

namespace ECS.Prioritization
{
    [CreateAssetMenu(fileName = "RealmPartitionSettings", menuName = "DCL/Prioritization/Realm Partition Settings")]
    public class RealmPartitionSettingsAsset : ScriptableObject, IRealmPartitionSettings
    {
        [SerializeField] private int[] fpsBuckets = { 30, 20, 10, 5, 0 };
        [SerializeField] private int behindFps = 1;
        [SerializeField] private float aggregatePositionTolerance = 0.5f;
        [SerializeField] private int maxLoadingDistanceInParcels;

        private void OnEnable()
        {
            OnValidate();
        }

        private void OnValidate()
        {
            AggregatePositionSqrTolerance = aggregatePositionTolerance * aggregatePositionTolerance;
        }

        [field: SerializeField] public int MinLoadingDistanceInParcels { get; private set; }

        [field: SerializeField]
        public float AggregateAngleTolerance { get; private set; }

        public Action<int>? OnMaxLoadingDistanceInParcelsChanged { get; set; }

        public int MaxLoadingDistanceInParcels
        {
            get => maxLoadingDistanceInParcels;

            set
            {
                value = Math.Clamp(value, MinLoadingDistanceInParcels,
                    Math.Min(value, ParcelMathJobifiedHelper.RADIUS_HARD_LIMIT));

                if (maxLoadingDistanceInParcels == value)
                    return;

                maxLoadingDistanceInParcels = value;
                OnMaxLoadingDistanceInParcelsChanged?.Invoke(value);
            }
        }

        [field: SerializeField] [field: Min(1)]
        public int UnloadingDistanceToleranceInParcels { get; private set; } = 1;

        [field: SerializeField]
        public int ScenesRequestBatchSize { get; private set; }

        [field: SerializeField]
        public int ScenesDefinitionsRequestBatchSize { get; private set; }

        public float AggregatePositionSqrTolerance { get; private set; }

        public int GetSceneUpdateFrequency(in PartitionComponent partition)
        {
            int bucketFps = fpsBuckets[Mathf.Clamp(partition.Bucket, 0, fpsBuckets.Length - 1)];
            return partition.IsBehind ? Mathf.Min(bucketFps, behindFps) : bucketFps;
        }
    }
}
