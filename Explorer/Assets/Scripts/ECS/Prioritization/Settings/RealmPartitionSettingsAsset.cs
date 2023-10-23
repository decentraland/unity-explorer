using ECS.Prioritization.Components;
using UnityEngine;

namespace ECS.Prioritization
{
    [CreateAssetMenu(menuName = "Create Realm Partition Settings", fileName = "RealmPartitionSettings", order = 0)]
    public class RealmPartitionSettingsAsset : ScriptableObject, IRealmPartitionSettings
    {
        [SerializeField] private int[] fpsBuckets = { 30, 20, 10, 5, 0 };
        [SerializeField] private int behindFps = 1;
        [SerializeField] private float aggregatePositionTolerance = 0.5f;

        [field: SerializeField]
        public float AggregateAngleTolerance { get; private set; }

        [field: SerializeField]
        public int MaxLoadingDistanceInParcels { get; private set; }

        [field: SerializeField]
        public int UnloadBucket { get; private set; }

        [field: SerializeField]
        public int ScenesRequestBatchSize { get; private set; }

        [field: SerializeField]
        public int ScenesDefinitionsRequestBatchSize { get; private set; }

        public float AggregatePositionSqrTolerance { get; private set; }

        private void OnEnable()
        {
            OnValidate();
        }

        private void OnValidate()
        {
            AggregatePositionSqrTolerance = aggregatePositionTolerance * aggregatePositionTolerance;
        }

        public int GetSceneUpdateFrequency(in PartitionComponent partition)
        {
            int bucketFps = fpsBuckets[Mathf.Clamp(partition.Bucket, 0, fpsBuckets.Length - 1)];
            return partition.IsBehind ? Mathf.Min(bucketFps, behindFps) : bucketFps;
        }
    }
}
